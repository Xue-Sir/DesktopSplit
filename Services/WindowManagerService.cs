using System.Text;
using System.Windows.Threading;
using DesktopSplit.Interop;
using DesktopSplit.Models;

namespace DesktopSplit.Services;

/// <summary>
/// Implements the first-pass window behavior without a virtual display driver.
/// Windows are only changed when the user requests native maximize; ordinary
/// movement and resizing are left alone.
/// </summary>
public sealed class WindowManagerService : IDisposable
{
    private readonly DispatcherTimer _timer;
    private readonly Dictionary<IntPtr, ManagedWindowState> _managedWindows = [];
    private readonly Dictionary<IntPtr, NativeMethods.RECT> _lastNormalRects = [];
    private readonly uint _ownProcessId = (uint)Environment.ProcessId;
    private LayoutDefinition _layout;
    private bool _isApplying;
    private bool _isPaused;
    private bool _initialScan = true;
    private bool _disposed;

    public WindowManagerService(LayoutDefinition layout)
    {
        _layout = layout.Clone();
        _timer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromMilliseconds(180)
        };
        _timer.Tick += OnTimerTick;
    }

    public bool IsPaused => _isPaused;

    public void Start()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _timer.Start();
    }

    public void ApplyLayout(LayoutDefinition layout)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _layout = layout.Clone();

        if (_isPaused || _managedWindows.Count == 0)
        {
            return;
        }

        _isApplying = true;
        try
        {
            foreach (var (hWnd, state) in _managedWindows.ToArray())
            {
                if (!IsWindowUsable(hWnd))
                {
                    _managedWindows.Remove(hWnd);
                    continue;
                }

                state.ZoneIndex = Math.Clamp(state.ZoneIndex, 0, Math.Max(0, _layout.Zones.Count - 1));
                ApplyWindowToZone(hWnd, state);
            }
        }
        finally
        {
            _isApplying = false;
        }
    }

    public void SetPaused(bool paused)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        if (_isPaused == paused)
        {
            return;
        }

        if (paused)
        {
            _isApplying = true;
            try
            {
                foreach (var (hWnd, state) in _managedWindows.ToArray())
                {
                    RestoreWindow(hWnd, state.OriginalRect);
                }

                _managedWindows.Clear();
                _lastNormalRects.Clear();
            }
            finally
            {
                _isApplying = false;
            }
        }

        _isPaused = paused;
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _timer.Stop();
        _timer.Tick -= OnTimerTick;
        _managedWindows.Clear();
        _lastNormalRects.Clear();
    }

    private void OnTimerTick(object? sender, EventArgs e)
    {
        if (_disposed || _isPaused || _isApplying || _layout.Zones.Count == 0)
        {
            return;
        }

        _isApplying = true;
        try
        {
            NativeMethods.EnumWindows(InspectWindow, IntPtr.Zero);
            _initialScan = false;
        }
        finally
        {
            _isApplying = false;
        }
    }

    private bool InspectWindow(IntPtr hWnd, IntPtr lParam)
    {
        if (!IsWindowUsable(hWnd) || !NativeMethods.GetWindowRect(hWnd, out var currentRect))
        {
            return true;
        }

        if (NativeMethods.IsZoomed(hWnd))
        {
            if (_initialScan)
            {
                // Existing maximized windows stay untouched at startup. Their
                // normal placement is remembered for the first user maximize.
                _lastNormalRects[hWnd] = GetNormalRect(hWnd, currentRect);
                return true;
            }

            HandleNativeMaximize(hWnd, currentRect);
            return true;
        }

        if (_managedWindows.TryGetValue(hWnd, out var managedState))
        {
            if (!TryGetZoneRect(managedState.Monitor, managedState.ZoneIndex, out var expectedRect) ||
                !ApproximatelyEqual(currentRect, expectedRect))
            {
                // A user move or resize releases the custom maximize state.
                _managedWindows.Remove(hWnd);
                _lastNormalRects[hWnd] = currentRect;
            }
        }
        else
        {
            _lastNormalRects[hWnd] = currentRect;
        }

        return true;
    }

    private void HandleNativeMaximize(IntPtr hWnd, NativeMethods.RECT currentRect)
    {
        if (_managedWindows.TryGetValue(hWnd, out var managedState))
        {
            RestoreWindow(hWnd, managedState.OriginalRect);
            _managedWindows.Remove(hWnd);
            _lastNormalRects[hWnd] = managedState.OriginalRect;
            return;
        }

        var originalRect = GetNormalRect(hWnd, currentRect);
        if (_lastNormalRects.TryGetValue(hWnd, out var lastNormalRect) && lastNormalRect.Width > 0)
        {
            originalRect = lastNormalRect;
        }

        var monitor = NativeMethods.MonitorFromPoint(CenterOf(originalRect), NativeMethods.MONITOR_DEFAULTTONEAREST);
        var zoneIndex = FindZone(monitor, originalRect);
        var state = new ManagedWindowState(originalRect, zoneIndex, monitor);
        _managedWindows[hWnd] = state;
        ApplyWindowToZone(hWnd, state);
    }

    private void ApplyWindowToZone(IntPtr hWnd, ManagedWindowState state)
    {
        if (!TryGetZoneRect(state.Monitor, state.ZoneIndex, out var zoneRect))
        {
            if (!NativeMethods.GetWindowRect(hWnd, out var currentRect))
            {
                return;
            }

            state.Monitor = NativeMethods.MonitorFromPoint(CenterOf(currentRect), NativeMethods.MONITOR_DEFAULTTONEAREST);
            if (!TryGetZoneRect(state.Monitor, state.ZoneIndex, out zoneRect))
            {
                return;
            }
        }

        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetWindowPos(
            hWnd,
            IntPtr.Zero,
            zoneRect.Left,
            zoneRect.Top,
            Math.Max(1, zoneRect.Width),
            Math.Max(1, zoneRect.Height),
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_NOOWNERZORDER |
            NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_SHOWWINDOW);
    }

    private static void RestoreWindow(IntPtr hWnd, NativeMethods.RECT originalRect)
    {
        NativeMethods.ShowWindow(hWnd, NativeMethods.SW_RESTORE);
        NativeMethods.SetWindowPos(
            hWnd,
            IntPtr.Zero,
            originalRect.Left,
            originalRect.Top,
            Math.Max(1, originalRect.Width),
            Math.Max(1, originalRect.Height),
            NativeMethods.SWP_NOACTIVATE |
            NativeMethods.SWP_NOOWNERZORDER |
            NativeMethods.SWP_NOZORDER |
            NativeMethods.SWP_SHOWWINDOW);
    }

    private int FindZone(IntPtr monitor, NativeMethods.RECT rect)
    {
        if (_layout.Zones.Count == 0 || !TryGetWorkArea(monitor, out var workArea))
        {
            return 0;
        }

        var center = CenterOf(rect);
        var nearestIndex = 0;
        var nearestDistance = double.MaxValue;
        for (var index = 0; index < _layout.Zones.Count; index++)
        {
            if (!TryGetZoneRect(monitor, index, out var zoneRect))
            {
                continue;
            }

            if (center.X >= zoneRect.Left && center.X < zoneRect.Right &&
                center.Y >= zoneRect.Top && center.Y < zoneRect.Bottom)
            {
                return index;
            }

            var zoneCenter = CenterOf(zoneRect);
            var distance = Math.Pow(zoneCenter.X - center.X, 2) + Math.Pow(zoneCenter.Y - center.Y, 2);
            if (distance < nearestDistance)
            {
                nearestDistance = distance;
                nearestIndex = index;
            }
        }

        _ = workArea;
        return nearestIndex;
    }

    private bool TryGetZoneRect(IntPtr monitor, int index, out NativeMethods.RECT zoneRect)
    {
        zoneRect = default;
        if (_layout.Zones.Count == 0 || index < 0 || index >= _layout.Zones.Count || !TryGetWorkArea(monitor, out var workArea))
        {
            return false;
        }

        var zone = _layout.Zones[index];
        var left = Math.Clamp(zone.Left, 0, 1);
        var top = Math.Clamp(zone.Top, 0, 1);
        var right = Math.Clamp(zone.Left + zone.Width, 0, 1);
        var bottom = Math.Clamp(zone.Top + zone.Height, 0, 1);
        zoneRect = new NativeMethods.RECT
        {
            Left = workArea.Left + (int)Math.Round(workArea.Width * left),
            Top = workArea.Top + (int)Math.Round(workArea.Height * top),
            Right = workArea.Left + (int)Math.Round(workArea.Width * right),
            Bottom = workArea.Top + (int)Math.Round(workArea.Height * bottom)
        };
        return zoneRect.Width > 0 && zoneRect.Height > 0;
    }

    private static bool TryGetWorkArea(IntPtr monitor, out NativeMethods.RECT workArea)
    {
        workArea = default;
        if (monitor == IntPtr.Zero)
        {
            monitor = NativeMethods.MonitorFromPoint(new NativeMethods.POINT(0, 0), NativeMethods.MONITOR_DEFAULTTONEAREST);
        }

        var info = new NativeMethods.MONITORINFO
        {
            cbSize = System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.MONITORINFO>()
        };
        if (!NativeMethods.GetMonitorInfo(monitor, ref info))
        {
            return false;
        }

        workArea = info.rcWork;
        return true;
    }

    private static NativeMethods.RECT GetNormalRect(IntPtr hWnd, NativeMethods.RECT fallback)
    {
        var placement = new NativeMethods.WINDOWPLACEMENT
        {
            length = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.WINDOWPLACEMENT>()
        };
        return NativeMethods.GetWindowPlacement(hWnd, ref placement) && placement.rcNormalPosition.Width > 0
            ? placement.rcNormalPosition
            : fallback;
    }

    private bool IsWindowUsable(IntPtr hWnd)
    {
        if (!NativeMethods.IsWindowVisible(hWnd) || hWnd == IntPtr.Zero)
        {
            return false;
        }

        NativeMethods.GetWindowThreadProcessId(hWnd, out var processId);
        if (processId == _ownProcessId || NativeMethods.GetWindow(hWnd, NativeMethods.GW_OWNER) != IntPtr.Zero)
        {
            return false;
        }

        var extendedStyle = NativeMethods.GetWindowStyle(hWnd).ToInt64();
        if ((extendedStyle & NativeMethods.WS_EX_TOOLWINDOW) != 0 ||
            (extendedStyle & NativeMethods.WS_EX_NOACTIVATE) != 0 ||
            (extendedStyle & NativeMethods.WS_EX_TOPMOST) != 0)
        {
            return false;
        }

        var className = ReadWindowString(hWnd, isClassName: true);
        if (className.Equals("#32770", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Progman", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("WorkerW", StringComparison.OrdinalIgnoreCase) ||
            className.Equals("Shell_TrayWnd", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return !string.IsNullOrWhiteSpace(ReadWindowString(hWnd, isClassName: false));
    }

    private static string ReadWindowString(IntPtr hWnd, bool isClassName)
    {
        var buffer = new StringBuilder(256);
        if (isClassName)
        {
            NativeMethods.GetClassName(hWnd, buffer, buffer.Capacity);
        }
        else
        {
            NativeMethods.GetWindowText(hWnd, buffer, buffer.Capacity);
        }

        return buffer.ToString();
    }

    private static NativeMethods.POINT CenterOf(NativeMethods.RECT rect) =>
        new(rect.Left + rect.Width / 2, rect.Top + rect.Height / 2);

    private static bool ApproximatelyEqual(NativeMethods.RECT left, NativeMethods.RECT right)
    {
        const int tolerance = 4;
        return Math.Abs(left.Left - right.Left) <= tolerance &&
               Math.Abs(left.Top - right.Top) <= tolerance &&
               Math.Abs(left.Right - right.Right) <= tolerance &&
               Math.Abs(left.Bottom - right.Bottom) <= tolerance;
    }

    private sealed class ManagedWindowState
    {
        internal ManagedWindowState(NativeMethods.RECT originalRect, int zoneIndex, IntPtr monitor)
        {
            OriginalRect = originalRect;
            ZoneIndex = zoneIndex;
            Monitor = monitor;
        }

        internal NativeMethods.RECT OriginalRect { get; }
        internal int ZoneIndex { get; set; }
        internal IntPtr Monitor { get; set; }
    }
}

using System.Drawing;
using System.Reflection;
using Forms = System.Windows.Forms;

namespace DesktopSplit.Services;

public sealed class TrayService : IDisposable
{
    private readonly Forms.NotifyIcon _notifyIcon;
    private readonly Forms.ToolStripMenuItem _pauseItem;
    private readonly Icon _icon;
    private bool _disposed;

    public TrayService()
    {
        _pauseItem = new Forms.ToolStripMenuItem("暂停分区")
        {
            CheckOnClick = false
        };
        _pauseItem.Click += (_, _) => PauseRequested?.Invoke();

        var menu = new Forms.ContextMenuStrip();
        var openItem = new Forms.ToolStripMenuItem("打开设置");
        openItem.Click += (_, _) => OpenRequested?.Invoke();
        var exitItem = new Forms.ToolStripMenuItem("退出");
        exitItem.Click += (_, _) => ExitRequested?.Invoke();
        menu.Items.Add(openItem);
        menu.Items.Add(_pauseItem);
        menu.Items.Add(new Forms.ToolStripSeparator());
        menu.Items.Add(exitItem);

        _icon = LoadIcon();
        _notifyIcon = new Forms.NotifyIcon
        {
            Icon = _icon,
            Text = "DesktopSplit",
            Visible = true,
            ContextMenuStrip = menu
        };
        _notifyIcon.DoubleClick += (_, _) => OpenRequested?.Invoke();
        _notifyIcon.MouseClick += (_, args) =>
        {
            if (args.Button == Forms.MouseButtons.Left)
            {
                OpenRequested?.Invoke();
            }
        };
    }

    public event Action? OpenRequested;
    public event Action? PauseRequested;
    public event Action? ExitRequested;

    public void SetPaused(bool paused)
    {
        _pauseItem.Text = paused ? "恢复分区" : "暂停分区";
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _notifyIcon.Visible = false;
        _notifyIcon.Dispose();
        _icon.Dispose();
    }

    private static Icon LoadIcon()
    {
        const string resourceName = "DesktopSplit.Assets.DesktopSplit.ico";
        var assembly = Assembly.GetExecutingAssembly();
        using (var stream = assembly.GetManifestResourceStream(resourceName))
        {
            if (stream is not null)
            {
                using var source = new Icon(stream);
                return (Icon)source.Clone();
            }
        }

        try
        {
            var processPath = Environment.ProcessPath;
            if (!string.IsNullOrWhiteSpace(processPath))
            {
                using var extracted = Icon.ExtractAssociatedIcon(processPath);
                if (extracted is not null)
                {
                    return (Icon)extracted.Clone();
                }
            }
        }
        catch (ArgumentException)
        {
            // The dotnet host used by development runs may not expose an icon.
        }

        return (Icon)SystemIcons.Application.Clone();
    }
}

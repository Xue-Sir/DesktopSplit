using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Interop;
using MediaBrushes = System.Windows.Media.Brushes;
using MediaColor = System.Windows.Media.Color;
using MediaColorConverter = System.Windows.Media.ColorConverter;
using DesktopSplit.Interop;
using DesktopSplit.Models;
using DesktopSplit.Services;

namespace DesktopSplit;

public partial class MainWindow : Window
{
    private readonly AppSettings _settings;
    private readonly SettingsService _settingsService;
    private readonly StartupService _startupService;
    private readonly WindowManagerService _windowManager;
    private readonly List<PresetItem> _presetItems = [];
    private readonly List<System.Windows.Controls.TextBox> _pixelWidthBoxes = [];
    private LayoutDefinition _workingLayout;
    private NativeMethods.RECT _workAreaPixels;
    private bool _updatingUi;
    private bool _hasWorkAreaPixels;
    private bool _draggingDivider;
    private bool _allowClose;

    public MainWindow(
        AppSettings settings,
        SettingsService settingsService,
        StartupService startupService,
        WindowManagerService windowManager)
    {
        _settings = settings;
        _settingsService = settingsService;
        _startupService = startupService;
        _windowManager = windowManager;
        _workingLayout = settings.ActiveLayout.Clone();

        InitializeComponent();
        Loaded += OnLoaded;
    }

    public void CloseForApplication()
    {
        _allowClose = true;
        Close();
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        UpdateWorkAreaPixels();
        RefreshPresetItems();
        AutostartCheckBox.IsChecked = _settings.Autostart;
        RenderPreview();
    }

    private void OnWindowLocationChanged(object? sender, EventArgs e) => UpdateWorkAreaPixels();

    private void RefreshPresetItems()
    {
        _updatingUi = true;
        try
        {
            _presetItems.Clear();
            _presetItems.AddRange(LayoutDefinition.BuiltIns.Select(layout => new PresetItem(layout.Name, layout)));
            _presetItems.AddRange((_settings.CustomLayouts ?? []).Select(layout => new PresetItem($"自定义：{layout.Name}", layout)));
            PresetComboBox.ItemsSource = null;
            PresetComboBox.ItemsSource = _presetItems;
            PresetComboBox.SelectedItem = _presetItems.FirstOrDefault(item =>
                string.Equals(item.Layout.Name, _workingLayout.Name, StringComparison.OrdinalIgnoreCase)) ?? _presetItems.FirstOrDefault();
            UpdateRatioControls();
            UpdatePixelControls();
        }
        finally
        {
            _updatingUi = false;
        }

        RenderPreview();
    }

    private void OnPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_updatingUi || PresetComboBox.SelectedItem is not PresetItem item)
        {
            return;
        }

        _workingLayout = item.Layout.Clone();
        UpdateRatioControls();
        UpdatePixelControls();
        RenderPreview();
    }

    private void OnRatioChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_updatingUi || _workingLayout.Zones.Count != 2)
        {
            return;
        }

        _workingLayout = LayoutDefinition.TwoZone(e.NewValue / 100d, "自定义两区");
        UpdateRatioControls(updateSlider: false);
        UpdatePixelControls();
        RenderPreview();
    }

    private void OnPreviewSizeChanged(object sender, SizeChangedEventArgs e) => RenderPreview();

    private void OnPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_workingLayout.Zones.Count != 2 || PreviewCanvas.ActualWidth <= 0)
        {
            return;
        }

        var dividerX = PreviewCanvas.ActualWidth * (_workingLayout.Zones[0].Left + _workingLayout.Zones[0].Width);
        var position = e.GetPosition(PreviewCanvas);
        if (Math.Abs(position.X - dividerX) <= 14)
        {
            _draggingDivider = true;
            PreviewCanvas.CaptureMouse();
            Mouse.OverrideCursor = System.Windows.Input.Cursors.SizeWE;
            e.Handled = true;
        }
    }

    private void OnPreviewMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_draggingDivider || PreviewCanvas.ActualWidth <= 0)
        {
            return;
        }

        var position = e.GetPosition(PreviewCanvas);
        var ratio = Math.Clamp(position.X / PreviewCanvas.ActualWidth, 0.1, 0.9);
        _workingLayout = LayoutDefinition.TwoZone(ratio, "自定义两区");
        UpdateRatioControls();
        UpdatePixelControls();
        RenderPreview();
    }

    private void OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_draggingDivider)
        {
            return;
        }

        _draggingDivider = false;
        PreviewCanvas.ReleaseMouseCapture();
        Mouse.OverrideCursor = null;
        e.Handled = true;
    }

    private void OnApplyClick(object sender, RoutedEventArgs e)
    {
        _settings.ActiveLayout = _workingLayout.Clone();
        _settings.Autostart = AutostartCheckBox.IsChecked == true;
        _settingsService.Save(_settings);
        _startupService.SetEnabled(_settings.Autostart);
        _windowManager.ApplyLayout(_settings.ActiveLayout);
        Hide();
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        _workingLayout = _settings.ActiveLayout.Clone();
        AutostartCheckBox.IsChecked = _settings.Autostart;
        RefreshPresetItems();
    }

    private void OnApplyPixelClick(object sender, RoutedEventArgs e) => ApplyPixelWidths();

    private void OnPixelWidthPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Enter)
        {
            return;
        }

        ApplyPixelWidths();
        e.Handled = true;
    }

    private void OnSavePresetClick(object sender, RoutedEventArgs e)
    {
        var name = PromptForPresetName();
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        var savedLayout = _workingLayout.Clone();
        savedLayout.Name = name.Trim();
        var existing = _settings.CustomLayouts.FirstOrDefault(layout =>
            string.Equals(layout.Name, savedLayout.Name, StringComparison.OrdinalIgnoreCase));
        if (existing is not null)
        {
            existing.Zones = savedLayout.Zones;
        }
        else
        {
            _settings.CustomLayouts.Add(savedLayout);
        }

        _workingLayout = savedLayout.Clone();
        _settingsService.Save(_settings);
        RefreshPresetItems();
    }

    private string? PromptForPresetName()
    {
        var dialog = new Window
        {
            Owner = this,
            Title = "保存预设",
            Width = 360,
            Height = 160,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Background = MediaBrushes.White,
            ShowInTaskbar = false
        };

        var nameBox = new System.Windows.Controls.TextBox { Margin = new Thickness(0, 8, 0, 14), Height = 30 };
        var confirm = new System.Windows.Controls.Button { Content = "保存", Width = 76, HorizontalAlignment = System.Windows.HorizontalAlignment.Right };
        confirm.Click += (_, _) => dialog.DialogResult = true;
        var panel = new StackPanel { Margin = new Thickness(18) };
        panel.Children.Add(new TextBlock { Text = "预设名称", Foreground = MediaBrushes.DimGray });
        panel.Children.Add(nameBox);
        panel.Children.Add(confirm);
        dialog.Content = panel;
        dialog.Loaded += (_, _) =>
        {
            nameBox.Focus();
            nameBox.SelectAll();
        };

        return dialog.ShowDialog() == true ? nameBox.Text : null;
    }

    private void UpdateRatioControls(bool updateSlider = true)
    {
        if (RatioPanel is null || RatioSlider is null || RatioLabel is null || LayoutDescription is null)
        {
            return;
        }

        var twoZones = _workingLayout.Zones.Count == 2;
        RatioPanel.Visibility = twoZones ? Visibility.Visible : Visibility.Collapsed;
        if (twoZones)
        {
            var leftRatio = Math.Clamp(_workingLayout.Zones[0].Width * 100, 10, 90);
            if (updateSlider)
            {
                _updatingUi = true;
                RatioSlider.Value = leftRatio;
                _updatingUi = false;
            }

            RatioLabel.Text = $"左侧 {leftRatio:0}% / 右侧 {100 - leftRatio:0}%";
        }

        LayoutDescription.Text = twoZones
            ? "窗口的中心点落入哪个区域，就把该区域视为它的“屏幕”。普通窗口保持原位；点击最大化后填充所在区域。"
            : $"当前布局包含 {_workingLayout.Zones.Count} 个区域。各区域可以同时容纳多个窗口，区域变化会立即作用于已自定义最大化的窗口。";
    }

    private void UpdateWorkAreaPixels()
    {
        if (!IsLoaded)
        {
            return;
        }

        var handle = new WindowInteropHelper(this).Handle;
        if (handle == IntPtr.Zero || !NativeMethods.TryGetWorkAreaForWindow(handle, out var workArea))
        {
            return;
        }

        if (_hasWorkAreaPixels &&
            _workAreaPixels.Width == workArea.Width &&
            _workAreaPixels.Height == workArea.Height &&
            _workAreaPixels.Left == workArea.Left &&
            _workAreaPixels.Top == workArea.Top)
        {
            return;
        }

        _workAreaPixels = workArea;
        _hasWorkAreaPixels = true;
        UpdatePixelControls();
        RenderPreview();
    }

    private void UpdatePixelControls()
    {
        if (PixelEditorPanel is null || WorkAreaPixelLabel is null || PixelInputHint is null || ApplyPixelButton is null)
        {
            return;
        }

        PixelEditorPanel.Children.Clear();
        _pixelWidthBoxes.Clear();

        if (!_hasWorkAreaPixels || _workingLayout.Zones.Count == 0)
        {
            WorkAreaPixelLabel.Text = "当前工作区：读取中";
            PixelInputHint.Text = "读取显示器工作区后，可以直接输入各区域的像素宽度。";
            ApplyPixelButton.IsEnabled = false;
            return;
        }

        WorkAreaPixelLabel.Text = $"当前工作区：{_workAreaPixels.Width} × {_workAreaPixels.Height} px（已扣除任务栏）";
        ApplyPixelButton.IsEnabled = true;

        for (var index = 0; index < _workingLayout.Zones.Count; index++)
        {
            var zone = _workingLayout.Zones[index];
            var row = new Grid { Margin = new Thickness(0, 0, 0, 6) };
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var name = new TextBlock
            {
                Text = string.IsNullOrWhiteSpace(zone.Name) ? $"区域 {index + 1}" : zone.Name,
                Foreground = new SolidColorBrush(MediaColor.FromRgb(39, 50, 72)),
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(name, 0);
            row.Children.Add(name);

            var pixelRect = GetZonePixelRect(zone);
            var widthBox = new System.Windows.Controls.TextBox
            {
                Text = pixelRect.Width.ToString(),
                Width = 72,
                Height = 29,
                Margin = new Thickness(8, 0, 5, 0),
                Padding = new Thickness(6, 3, 6, 3),
                HorizontalContentAlignment = System.Windows.HorizontalAlignment.Right,
                VerticalContentAlignment = System.Windows.VerticalAlignment.Center,
                ToolTip = "输入该区域的宽度（像素）"
            };
            widthBox.PreviewKeyDown += OnPixelWidthPreviewKeyDown;
            _pixelWidthBoxes.Add(widthBox);
            Grid.SetColumn(widthBox, 1);
            row.Children.Add(widthBox);

            var heightLabel = new TextBlock
            {
                Text = $"× {pixelRect.Height} px",
                Foreground = new SolidColorBrush(MediaColor.FromRgb(102, 112, 133)),
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = 74
            };
            Grid.SetColumn(heightLabel, 2);
            row.Children.Add(heightLabel);

            PixelEditorPanel.Children.Add(row);
        }

        PixelInputHint.Text = $"输入各区域宽度（px），合计必须等于 {_workAreaPixels.Width}px；高度显示为当前实际高度。按 Enter 或点击按钮应用。";
    }

    private void ApplyPixelWidths()
    {
        if (!_hasWorkAreaPixels || _pixelWidthBoxes.Count != _workingLayout.Zones.Count)
        {
            return;
        }

        var widths = new int[_pixelWidthBoxes.Count];
        for (var index = 0; index < _pixelWidthBoxes.Count; index++)
        {
            if (!int.TryParse(_pixelWidthBoxes[index].Text.Trim(), out var width) || width <= 0)
            {
                System.Windows.MessageBox.Show(this, $"区域 {index + 1} 的宽度必须是大于 0 的整数像素。", "像素输入无效", MessageBoxButton.OK, MessageBoxImage.Warning);
                _pixelWidthBoxes[index].Focus();
                _pixelWidthBoxes[index].SelectAll();
                return;
            }

            widths[index] = width;
        }

        var totalWidth = widths.Sum();
        if (totalWidth != _workAreaPixels.Width)
        {
            System.Windows.MessageBox.Show(
                this,
                $"各区域宽度合计为 {totalWidth}px，但当前工作区宽度是 {_workAreaPixels.Width}px。请调整后再应用。",
                "像素输入无效",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
            return;
        }

        var updatedLayout = _workingLayout.Clone();
        var offset = 0;
        for (var index = 0; index < updatedLayout.Zones.Count; index++)
        {
            var zone = updatedLayout.Zones[index];
            zone.Left = offset / (double)_workAreaPixels.Width;
            zone.Top = 0;
            zone.Width = widths[index] / (double)_workAreaPixels.Width;
            zone.Height = 1;
            offset += widths[index];
        }

        updatedLayout.Name = $"自定义像素布局（{_workAreaPixels.Width}px）";
        _workingLayout = updatedLayout;
        UpdateRatioControls();
        UpdatePixelControls();
        RenderPreview();
    }

    private NativeMethods.RECT GetZonePixelRect(ZoneDefinition zone)
    {
        var left = Math.Clamp(zone.Left, 0, 1);
        var top = Math.Clamp(zone.Top, 0, 1);
        var right = Math.Clamp(zone.Left + zone.Width, 0, 1);
        var bottom = Math.Clamp(zone.Top + zone.Height, 0, 1);
        return new NativeMethods.RECT
        {
            Left = _workAreaPixels.Left + (int)Math.Round(_workAreaPixels.Width * left),
            Top = _workAreaPixels.Top + (int)Math.Round(_workAreaPixels.Height * top),
            Right = _workAreaPixels.Left + (int)Math.Round(_workAreaPixels.Width * right),
            Bottom = _workAreaPixels.Top + (int)Math.Round(_workAreaPixels.Height * bottom)
        };
    }

    private void RenderPreview()
    {
        if (PreviewCanvas is null || PreviewCanvas.ActualWidth <= 0 || PreviewCanvas.ActualHeight <= 0)
        {
            return;
        }

        PreviewCanvas.Children.Clear();
        var colors = new[] { "#6E8FF5", "#86C8A5", "#D9A75A", "#C38CD9", "#72B9D6" };
        for (var index = 0; index < _workingLayout.Zones.Count; index++)
        {
            var zone = _workingLayout.Zones[index];
            var border = new Border
            {
                Background = new SolidColorBrush((MediaColor)MediaColorConverter.ConvertFromString(colors[index % colors.Length])!),
                BorderBrush = new SolidColorBrush(MediaColor.FromArgb(210, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(5),
                Margin = new Thickness(2),
                Child = new TextBlock
                {
                    Text = BuildZonePreviewText(zone, index),
                    Foreground = MediaBrushes.White,
                    FontWeight = FontWeights.SemiBold,
                    FontSize = 13,
                    HorizontalAlignment = System.Windows.HorizontalAlignment.Center,
                    VerticalAlignment = System.Windows.VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center
                }
            };
            Canvas.SetLeft(border, PreviewCanvas.ActualWidth * zone.Left);
            Canvas.SetTop(border, PreviewCanvas.ActualHeight * zone.Top);
            border.Width = Math.Max(0, PreviewCanvas.ActualWidth * zone.Width);
            border.Height = Math.Max(0, PreviewCanvas.ActualHeight * zone.Height);
            PreviewCanvas.Children.Add(border);
        }
    }

    private string BuildZonePreviewText(ZoneDefinition zone, int index)
    {
        var name = string.IsNullOrWhiteSpace(zone.Name) ? $"区域 {index + 1}" : zone.Name;
        if (!_hasWorkAreaPixels)
        {
            return name;
        }

        var pixelRect = GetZonePixelRect(zone);
        return $"{name}\n{pixelRect.Width} × {pixelRect.Height} px";
    }

    private void OnWindowClosing(object? sender, CancelEventArgs e)
    {
        if (_allowClose)
        {
            return;
        }

        e.Cancel = true;
        Hide();
    }

    private sealed class PresetItem
    {
        internal PresetItem(string displayName, LayoutDefinition layout)
        {
            DisplayName = displayName;
            Layout = layout;
        }

        public string DisplayName { get; }
        internal LayoutDefinition Layout { get; }

        public override string ToString() => DisplayName;
    }
}

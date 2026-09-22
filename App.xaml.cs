using System.Windows;
using DesktopSplit.Services;

namespace DesktopSplit;

public partial class App : System.Windows.Application
{
    private Mutex? _singleInstanceMutex;
    private SettingsService? _settingsService;
    private StartupService? _startupService;
    private WindowManagerService? _windowManager;
    private TrayService? _trayService;
    private MainWindow? _mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var instanceMutex = new Mutex(initiallyOwned: true, "DesktopSplit.SingleInstance", out var createdNew);
        if (!createdNew)
        {
            instanceMutex.Dispose();
            Shutdown();
            return;
        }

        _singleInstanceMutex = instanceMutex;

        _settingsService = new SettingsService();
        var settings = _settingsService.Load();
        _startupService = new StartupService();
        _startupService.SetEnabled(settings.Autostart);

        _windowManager = new WindowManagerService(settings.ActiveLayout);
        _mainWindow = new MainWindow(settings, _settingsService, _startupService, _windowManager);
        _trayService = new TrayService();
        _trayService.OpenRequested += OpenSettings;
        _trayService.PauseRequested += TogglePaused;
        _trayService.ExitRequested += ExitApplication;
        _windowManager.Start();

        // The utility is intentionally tray-first. No settings window is opened on boot.
    }

    private void OpenSettings()
    {
        if (_mainWindow is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            _mainWindow.Show();
            _mainWindow.WindowState = WindowState.Normal;
            _mainWindow.Activate();
            _mainWindow.Focus();
        });
    }

    private void TogglePaused()
    {
        if (_windowManager is null || _trayService is null)
        {
            return;
        }

        Dispatcher.BeginInvoke(() =>
        {
            _windowManager.SetPaused(!_windowManager.IsPaused);
            _trayService.SetPaused(_windowManager.IsPaused);
        });
    }

    private void ExitApplication()
    {
        Dispatcher.BeginInvoke(() =>
        {
            _windowManager?.Dispose();
            _trayService?.Dispose();
            _mainWindow?.CloseForApplication();
            Shutdown();
        });
    }

    protected override void OnExit(ExitEventArgs e)
    {
        _windowManager?.Dispose();
        _trayService?.Dispose();
        if (_singleInstanceMutex is not null)
        {
            _singleInstanceMutex.ReleaseMutex();
            _singleInstanceMutex.Dispose();
            _singleInstanceMutex = null;
        }
        base.OnExit(e);
    }
}

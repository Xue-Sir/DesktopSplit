using System.IO;
using Microsoft.Win32;

namespace DesktopSplit.Services;

public sealed class StartupService
{
    private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
    private const string RunValueName = "DesktopSplit";

    public bool IsEnabled()
    {
        using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, writable: false);
        return key?.GetValue(RunValueName) is string value && !string.IsNullOrWhiteSpace(value);
    }

    public void SetEnabled(bool enabled)
    {
        using var key = Registry.CurrentUser.CreateSubKey(RunKeyPath);
        if (key is null)
        {
            return;
        }

        if (enabled)
        {
            key.SetValue(RunValueName, BuildCommandLine(), RegistryValueKind.String);
        }
        else
        {
            key.DeleteValue(RunValueName, throwOnMissingValue: false);
        }
    }

    private static string BuildCommandLine()
    {
        var processPath = Environment.ProcessPath ?? string.Empty;
        var entryAssembly = System.Reflection.Assembly.GetEntryAssembly()?.Location;
        var processName = Path.GetFileNameWithoutExtension(processPath);

        // During development the process is dotnet.exe; retain the DLL argument so the
        // startup entry remains useful before a published EXE exists.
        if (processName.Equals("dotnet", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(entryAssembly))
        {
            return $"{Quote(processPath)} {Quote(entryAssembly)}";
        }

        return Quote(string.IsNullOrWhiteSpace(processPath) ? entryAssembly ?? "DesktopSplit.exe" : processPath);
    }

    private static string Quote(string value) => $"\"{value.Replace("\"", "\\\"")}\"";
}

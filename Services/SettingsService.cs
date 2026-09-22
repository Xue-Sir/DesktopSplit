using System.IO;
using System.Text.Json;
using DesktopSplit.Models;

namespace DesktopSplit.Services;

public sealed class SettingsService
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public string SettingsPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        "DesktopSplit",
        "settings.json");

    public AppSettings Load()
    {
        try
        {
            if (File.Exists(SettingsPath))
            {
                var json = File.ReadAllText(SettingsPath);
                var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                if (loaded is not null)
                {
                    Normalize(loaded);
                    return loaded;
                }
            }
        }
        catch (JsonException)
        {
            // A corrupt settings file should not prevent the tray utility from starting.
        }
        catch (IOException)
        {
            // The defaults remain usable if the profile directory is temporarily unavailable.
        }

        return new AppSettings();
    }

    public void Save(AppSettings settings)
    {
        Normalize(settings);
        var directory = Path.GetDirectoryName(SettingsPath)!;
        Directory.CreateDirectory(directory);
        var temporaryPath = SettingsPath + ".tmp";
        var json = JsonSerializer.Serialize(settings, JsonOptions);
        File.WriteAllText(temporaryPath, json);
        File.Move(temporaryPath, SettingsPath, true);
    }

    private static void Normalize(AppSettings settings)
    {
        settings.CustomLayouts ??= [];
        if (settings.ActiveLayout is null || settings.ActiveLayout.Zones is null || settings.ActiveLayout.Zones.Count == 0)
        {
            settings.ActiveLayout = LayoutDefinition.MainWide();
        }

        foreach (var layout in settings.CustomLayouts)
        {
            layout.Zones ??= [];
        }
    }
}

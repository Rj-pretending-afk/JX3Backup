using System.IO;
using System.Text.Json;
using JX3ConfigSwitcher.Models;

namespace JX3ConfigSwitcher.Services;

public sealed class SettingsService
{
    private readonly PortablePaths _paths;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public SettingsService(PortablePaths paths)
    {
        _paths = paths;
    }

    public AppSettings Settings { get; private set; } = new();

    public void Load()
    {
        if (!File.Exists(_paths.SettingsPath))
        {
            Settings = new AppSettings();
            Save();
            return;
        }

        var json = File.ReadAllText(_paths.SettingsPath);
        Settings = JsonSerializer.Deserialize<AppSettings>(json) ?? new AppSettings();
    }

    public void Save()
    {
        Directory.CreateDirectory(_paths.DataDirectory);
        var json = JsonSerializer.Serialize(Settings, _jsonOptions);
        File.WriteAllText(_paths.SettingsPath, json);
    }
}

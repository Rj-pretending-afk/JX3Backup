namespace JX3ConfigSwitcher.Models;

public sealed class AppSettings
{
    public string? GamePath { get; set; }
    public string? SyncFolder { get; set; }
    public long? CurrentProfileId { get; set; }
    public string Theme { get; set; } = "Dark";
    public double WindowWidth { get; set; } = 1280;
    public double WindowHeight { get; set; } = 820;
    public string LogLevel { get; set; } = "Info";
}

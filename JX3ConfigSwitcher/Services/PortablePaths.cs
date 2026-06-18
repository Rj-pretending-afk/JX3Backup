using System;
using System.IO;

namespace JX3ConfigSwitcher.Services;

public sealed class PortablePaths
{
    public PortablePaths(string? baseDirectory = null)
    {
        BaseDirectory = Path.GetFullPath(baseDirectory ?? AppContext.BaseDirectory);
        DataDirectory = Path.Combine(BaseDirectory, "data");
        BackupDirectory = Path.Combine(DataDirectory, "backups");
        SnapshotDirectory = Path.Combine(DataDirectory, "snapshots");
        LogDirectory = Path.Combine(DataDirectory, "logs");
        SettingsPath = Path.Combine(DataDirectory, "appsettings.json");
        DatabasePath = Path.Combine(DataDirectory, "app.db");
    }

    public string BaseDirectory { get; }
    public string DataDirectory { get; }
    public string BackupDirectory { get; }
    public string SnapshotDirectory { get; }
    public string LogDirectory { get; }
    public string SettingsPath { get; }
    public string DatabasePath { get; }

    public void EnsureCreated()
    {
        Directory.CreateDirectory(DataDirectory);
        Directory.CreateDirectory(BackupDirectory);
        Directory.CreateDirectory(SnapshotDirectory);
        Directory.CreateDirectory(LogDirectory);
        EnsureWritable();
    }

    public void EnsureWritable()
    {
        var probe = Path.Combine(DataDirectory, $".write-test-{Guid.NewGuid():N}.tmp");
        File.WriteAllText(probe, "ok");
        File.Delete(probe);
    }
}

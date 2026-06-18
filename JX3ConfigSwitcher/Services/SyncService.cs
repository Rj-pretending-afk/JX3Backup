using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JX3ConfigSwitcher.Models;

namespace JX3ConfigSwitcher.Services;

public sealed class SyncService
{
    private readonly ProfileRepository _repository;

    public SyncService(ProfileRepository repository)
    {
        _repository = repository;
    }

    public string SyncBackupToFolder(BackupVersionRecord backup, string syncFolder)
    {
        if (string.IsNullOrWhiteSpace(syncFolder))
        {
            throw new InvalidOperationException("请先设置 OneDrive/Google Drive 本地同步文件夹。");
        }

        Directory.CreateDirectory(syncFolder);
        var destination = Path.Combine(syncFolder, Path.GetFileName(backup.PackagePath));
        File.Copy(backup.PackagePath, destination, overwrite: true);
        _repository.AddLog("Info", $"同步备份到：{destination}");
        return destination;
    }

    public IReadOnlyList<string> FindPotentialConflicts(string syncFolder)
    {
        if (string.IsNullOrWhiteSpace(syncFolder) || !Directory.Exists(syncFolder))
        {
            return Array.Empty<string>();
        }

        return Directory.EnumerateFiles(syncFolder, "*.zip", SearchOption.TopDirectoryOnly)
            .GroupBy(file => ExtractSlotKey(Path.GetFileNameWithoutExtension(file)), StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() > 1)
            .Select(group => $"{group.Key}: {group.Count()} 个版本")
            .OrderBy(value => value)
            .ToList();
    }

    private static string ExtractSlotKey(string fileName)
    {
        var index = fileName.IndexOf("-slot", StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return fileName;
        }

        var end = fileName.IndexOf('-', index + 1);
        return end < 0 ? fileName : fileName[..end];
    }
}

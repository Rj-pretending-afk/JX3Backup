using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using JX3ConfigSwitcher.Models;

namespace JX3ConfigSwitcher.Services;

public sealed class BackupService
{
    private const string SkillPlacementEntryPath = "special/skill-placement.json";

    private readonly PortablePaths _paths;
    private readonly ProfileRepository _repository;
    private readonly ConfigClassifier _classifier;
    private readonly GameProcessGuard _processGuard;
    private readonly SkillPlacementService _skillPlacementService;
    private readonly JsonSerializerOptions _jsonOptions = new() { WriteIndented = true };

    public BackupService(
        PortablePaths paths,
        ProfileRepository repository,
        ConfigClassifier classifier,
        GameProcessGuard processGuard,
        SkillPlacementService skillPlacementService)
    {
        _paths = paths;
        _repository = repository;
        _classifier = classifier;
        _processGuard = processGuard;
        _skillPlacementService = skillPlacementService;
    }

    public BackupVersionRecord CreateBackup(
        ProfileRecord profile,
        int slotNumber,
        string slotName,
        SaveKind kind,
        CharacterConfig source,
        IEnumerable<ConfigModule> selectedModules,
        string? sectTag,
        string? sectColor,
        string note)
    {
        var modules = selectedModules.Distinct().ToArray();
        var slot = _repository.UpsertSlot(profile.Id, slotNumber, slotName, kind, source.Key, sectTag, sectColor);
        var packagePath = CreatePackage(profile, slot, kind, source.CharacterPath, source.Key, sectTag, sectColor, modules, note, _paths.BackupDirectory);
        var manifest = ReadManifest(packagePath);
        var record = new BackupVersionRecord(
            0,
            profile.Id,
            slotNumber,
            kind,
            packagePath,
            source.CharacterPath,
            note ?? string.Empty,
            string.Join(", ", modules.Select(GetModuleName)),
            manifest.ContainsDump,
            DateTime.Now);
        _repository.AddBackupVersion(record);
        _repository.AddLog("Info", $"创建备份：{profile.Name} / {slotNumber:00} / {slotName}");
        return record;
    }

    public BackupVersionRecord CreateAutoSnapshot(ProfileRecord profile, CharacterConfig target, string reason)
    {
        var slot = new SaveSlotRecord(
            0,
            profile.Id,
            0,
            "自动快照",
            SaveKind.AutoSnapshot,
            target.Key,
            null,
            null,
            false,
            DateTime.Now);
        var modules = Enum.GetValues(typeof(ConfigModule)).Cast<ConfigModule>().ToArray();
        var packagePath = CreatePackage(profile, slot, SaveKind.AutoSnapshot, target.CharacterPath, target.Key, null, null, modules, reason, _paths.SnapshotDirectory);
        var manifest = ReadManifest(packagePath);
        var record = new BackupVersionRecord(
            0,
            profile.Id,
            0,
            SaveKind.AutoSnapshot,
            packagePath,
            target.CharacterPath,
            reason,
            "自动快照：完整当前目标配置",
            manifest.ContainsDump,
            DateTime.Now);
        _repository.AddBackupVersion(record);
        _repository.AddLog("Info", $"危险操作前自动快照：{target.DisplayName}");
        return record;
    }

    public void RestoreBackup(string packagePath, CharacterConfig target, IEnumerable<ConfigModule> selectedModules)
    {
        if (_processGuard.IsGameRunning())
        {
            throw new InvalidOperationException("检测到剑网3正在运行。为了避免游戏写回覆盖配置，请关闭游戏后再恢复。");
        }

        var modules = selectedModules.Distinct().ToHashSet();
        using var archive = ZipFile.OpenRead(packagePath);
        var manifest = ReadManifest(archive);
        foreach (var file in manifest.Files.Where(file => modules.Contains(file.Module) && !file.RelativePath.StartsWith("special/", StringComparison.OrdinalIgnoreCase)))
        {
            var entry = archive.GetEntry("files/" + file.RelativePath.Replace('\\', '/'));
            if (entry is null)
            {
                continue;
            }

            var destination = Path.GetFullPath(Path.Combine(target.CharacterPath, file.RelativePath));
            if (!destination.StartsWith(Path.GetFullPath(target.CharacterPath), StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException($"备份包包含非法路径：{file.RelativePath}");
            }

            Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
            ExtractEntryAtomically(entry, destination);
        }

        if (modules.Contains(ConfigModule.ActionButtons))
        {
            var entry = archive.GetEntry(SkillPlacementEntryPath)
                ?? throw new InvalidDataException("备份包不包含独立技能摆放快照，无法只恢复技能/动作按钮。");
            using var reader = new StreamReader(entry.Open(), Encoding.UTF8);
            var snapshot = _skillPlacementService.Deserialize(reader.ReadToEnd());
            _skillPlacementService.RestoreToCharacter(target.CharacterPath, snapshot);
        }

        _repository.AddLog("Info", $"恢复备份到：{target.DisplayName}");
    }

    public void CopyCharacterConfig(
        ProfileRecord profile,
        CharacterConfig source,
        CharacterConfig target,
        IEnumerable<ConfigModule> selectedModules,
        string reason)
    {
        if (_processGuard.IsGameRunning())
        {
            throw new InvalidOperationException("检测到剑网3正在运行。为了避免游戏写回覆盖配置，请关闭游戏后再跨角色覆盖。");
        }

        if (string.Equals(source.CharacterPath, target.CharacterPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException("来源角色和目标角色相同，不需要跨角色覆盖。");
        }

        var modules = selectedModules.Distinct().ToArray();
        if (modules.Length == 0)
        {
            throw new InvalidOperationException("至少选择一个要覆盖的模块。");
        }

        CreateAutoSnapshot(profile, target, reason);
        var slot = new SaveSlotRecord(
            0,
            profile.Id,
            0,
            "跨角色覆盖",
            SaveKind.AutoSnapshot,
            source.Key,
            null,
            null,
            false,
            DateTime.Now);
        var packagePath = CreatePackage(
            profile,
            slot,
            SaveKind.AutoSnapshot,
            source.CharacterPath,
            source.Key,
            null,
            null,
            modules,
            reason,
            _paths.SnapshotDirectory);
        RestoreBackup(packagePath, target, modules);
        _repository.AddLog("Info", $"跨角色覆盖：{source.DisplayName} -> {target.DisplayName}");
    }

    public BackupManifest ReadManifest(string packagePath)
    {
        using var archive = ZipFile.OpenRead(packagePath);
        return ReadManifest(archive);
    }

    public string GetRiskSummary(string packagePath, IEnumerable<ConfigModule> selectedModules)
    {
        var manifest = ReadManifest(packagePath);
        var modules = selectedModules.ToHashSet();
        var selectedFiles = manifest.Files.Where(file => modules.Contains(file.Module)).ToList();
        var highRisk = selectedFiles.Where(file => file.IsHighRisk).Select(file => file.Module).Distinct().Select(GetModuleName);
        var dumpCount = selectedFiles.Count(file => file.Module == ConfigModule.FullDump);
        return $"将写入 {selectedFiles.Count} 个项目；高风险模块：{string.Join(", ", highRisk.DefaultIfEmpty("无"))}；dump 文件：{dumpCount}";
    }

    public static string GetModuleName(ConfigModule module)
    {
        return module switch
        {
            ConfigModule.UiLayout => "界面布局",
            ConfigModule.KeyBindings => "快捷键/键位",
            ConfigModule.DisplayChatAddon => "显示/聊天/插件",
            ConfigModule.Macros => "宏",
            ConfigModule.ActionButtons => "技能/动作按钮",
            ConfigModule.FullDump => "完整 dump",
            _ => module.ToString()
        };
    }

    private string CreatePackage(
        ProfileRecord profile,
        SaveSlotRecord slot,
        SaveKind kind,
        string sourcePath,
        string? sourceCharacterKey,
        string? sectTag,
        string? sectColor,
        IReadOnlyCollection<ConfigModule> modules,
        string? note,
        string outputRoot)
    {
        if (!Directory.Exists(sourcePath))
        {
            throw new DirectoryNotFoundException($"来源目录不存在：{sourcePath}");
        }

        Directory.CreateDirectory(outputRoot);
        var timestamp = DateTime.Now.ToString("yyyyMMdd-HHmmss-fff");
        var safeProfile = MakeSafeName(profile.Name);
        var fileName = $"{safeProfile}-slot{slot.SlotNumber:00}-{kind}-{timestamp}-{Guid.NewGuid():N}.zip";
        var packagePath = Path.Combine(outputRoot, fileName);
        var files = EnumerateIncludedFiles(sourcePath, modules).ToList();
        var entries = new List<BackupFileEntry>();

        using (var archive = ZipFile.Open(packagePath, ZipArchiveMode.Create))
        {
            foreach (var file in files)
            {
                var relativePath = Path.GetRelativePath(sourcePath, file);
                var module = _classifier.Classify(relativePath);
                var normalized = relativePath.Replace('\\', '/');
                archive.CreateEntryFromFile(file, "files/" + normalized, CompressionLevel.Optimal);
                entries.Add(new BackupFileEntry(
                    normalized,
                    new FileInfo(file).Length,
                    ComputeSha256(file),
                    module,
                    _classifier.IsHighRisk(module)));
            }

            if (modules.Contains(ConfigModule.ActionButtons))
            {
                try
                {
                    var snapshot = _skillPlacementService.ExtractFromCharacter(sourcePath);
                    var json = _skillPlacementService.Serialize(snapshot);
                    var bytes = Encoding.UTF8.GetBytes(json);
                    var skillEntry = archive.CreateEntry(SkillPlacementEntryPath, CompressionLevel.Optimal);
                    using (var stream = skillEntry.Open())
                    {
                        stream.Write(bytes, 0, bytes.Length);
                    }

                    entries.Add(new BackupFileEntry(
                        SkillPlacementEntryPath,
                        bytes.Length,
                        ComputeSha256(bytes),
                        ConfigModule.ActionButtons,
                        true));
                }
                catch (Exception) when (kind is SaveKind.AutoSnapshot)
                {
                    _repository.AddLog("Warn", "自动快照未能抽取独立技能摆放；已继续保存其它配置。");
                }
            }

            var manifest = new BackupManifest(
                "剑3备份器",
                typeof(BackupService).Assembly.GetName().Version?.ToString() ?? "0.1.0",
                kind,
                profile.Id,
                profile.Name,
                slot.SlotNumber,
                slot.Name,
                sourcePath,
                sourceCharacterKey,
                sectTag,
                sectColor,
                Environment.MachineName,
                DateTime.Now,
                modules.ToArray(),
                Enum.GetValues(typeof(ConfigModule)).Cast<ConfigModule>().Except(modules).ToArray(),
                entries.Any(entry => entry.Module == ConfigModule.FullDump),
                entries,
                note ?? string.Empty);

            var manifestEntry = archive.CreateEntry("manifest.json", CompressionLevel.Optimal);
            using var writer = new StreamWriter(manifestEntry.Open());
            writer.Write(JsonSerializer.Serialize(manifest, _jsonOptions));
        }

        return packagePath;
    }

    private IEnumerable<string> EnumerateIncludedFiles(string sourcePath, IReadOnlyCollection<ConfigModule> modules)
    {
        foreach (var file in Directory.EnumerateFiles(sourcePath, "*.*", SearchOption.AllDirectories))
        {
            var relativePath = Path.GetRelativePath(sourcePath, file);
            var module = _classifier.Classify(relativePath);
            if (modules.Contains(module))
            {
                yield return file;
            }
        }
    }

    private BackupManifest ReadManifest(ZipArchive archive)
    {
        var entry = archive.GetEntry("manifest.json") ?? throw new InvalidDataException("备份包缺少 manifest.json。");
        using var stream = entry.Open();
        var manifest = JsonSerializer.Deserialize<BackupManifest>(stream);
        return manifest ?? throw new InvalidDataException("manifest.json 读取失败。");
    }

    private static void ExtractEntryAtomically(ZipArchiveEntry entry, string destination)
    {
        var directory = Path.GetDirectoryName(destination) ?? throw new InvalidOperationException($"目标路径无效：{destination}");
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(destination)}.{Guid.NewGuid():N}.tmp");
        var backupPath = Path.Combine(directory, $"{Path.GetFileName(destination)}.{DateTime.Now:yyyyMMddHHmmssfff}.bak");

        try
        {
            entry.ExtractToFile(tempPath, overwrite: false);
            if (File.Exists(destination))
            {
                File.Replace(tempPath, destination, backupPath, ignoreMetadataErrors: true);
            }
            else
            {
                File.Move(tempPath, destination);
            }
        }
        finally
        {
            if (File.Exists(tempPath))
            {
                File.Delete(tempPath);
            }
        }
    }

    private static string ComputeSha256(string file)
    {
        using var sha = SHA256.Create();
        using var stream = File.OpenRead(file);
        return Convert.ToHexString(sha.ComputeHash(stream));
    }

    private static string ComputeSha256(byte[] bytes)
    {
        using var sha = SHA256.Create();
        return Convert.ToHexString(sha.ComputeHash(bytes));
    }

    private static string MakeSafeName(string value)
    {
        var invalid = Path.GetInvalidFileNameChars();
        return string.Concat(value.Select(character => invalid.Contains(character) ? '_' : character));
    }
}

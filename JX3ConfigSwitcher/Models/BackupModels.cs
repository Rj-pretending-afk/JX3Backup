using System;
using System.Collections.Generic;

namespace JX3ConfigSwitcher.Models;

public enum SaveKind
{
    Universal,
    CharacterSpecific,
    AutoSnapshot
}

public enum ConfigModule
{
    UiLayout,
    KeyBindings,
    DisplayChatAddon,
    Macros,
    ActionButtons,
    FullDump
}

public sealed record ProfileRecord(
    long Id,
    string Name,
    DateTime CreatedAt);

public sealed record SaveSlotRecord(
    long Id,
    long ProfileId,
    int SlotNumber,
    string Name,
    SaveKind Kind,
    string? CharacterKey,
    string? SectTag,
    DateTime UpdatedAt);

public sealed record BackupVersionRecord(
    long Id,
    long ProfileId,
    int SlotNumber,
    SaveKind Kind,
    string PackagePath,
    string SourcePath,
    string Note,
    string ModuleSummary,
    bool ContainsDump,
    DateTime CreatedAt);

public sealed record OperationLogRecord(
    long Id,
    string Level,
    string Message,
    DateTime CreatedAt);

public sealed record CharacterConfig(
    string Account,
    string Server,
    string CharacterName,
    string CharacterPath,
    IReadOnlyList<string> DumpFiles)
{
    public string Key => $"{Account}/{Server}/{CharacterName}";
    public string DisplayName => $"{Account} / {Server} / {CharacterName}";
}

public sealed record BackupFileEntry(
    string RelativePath,
    long Size,
    string Sha256,
    ConfigModule Module,
    bool IsHighRisk);

public sealed record BackupManifest(
    string AppName,
    string AppVersion,
    SaveKind Kind,
    long ProfileId,
    string ProfileName,
    int SlotNumber,
    string SlotName,
    string SourcePath,
    string? SourceCharacterKey,
    string? SectTag,
    string MachineName,
    DateTime CreatedAt,
    IReadOnlyList<ConfigModule> IncludedModules,
    IReadOnlyList<ConfigModule> ExcludedModules,
    bool ContainsDump,
    IReadOnlyList<BackupFileEntry> Files,
    string Note);

public sealed record ModuleChoice(
    ConfigModule Module,
    string Name,
    string Description,
    bool IsSelected,
    bool IsHighRisk);

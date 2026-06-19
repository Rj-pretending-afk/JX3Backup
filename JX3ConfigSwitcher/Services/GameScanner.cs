using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JX3ConfigSwitcher.Models;
using Microsoft.Win32;

namespace JX3ConfigSwitcher.Services;

public sealed class GameScanner
{
    private readonly IReadOnlyList<string> _candidateRoots;
    private readonly Func<IEnumerable<string>> _registryPathProvider;
    private readonly MingYiRoleInfoReader _roleInfoReader;

    public GameScanner(
        IEnumerable<string>? candidateRoots = null,
        Func<IEnumerable<string>>? registryPathProvider = null,
        MingYiRoleInfoReader? roleInfoReader = null)
    {
        _candidateRoots = candidateRoots?.ToArray() ?? BuildDefaultCandidateRoots();
        _registryPathProvider = registryPathProvider ?? BuildRegistryCandidateRoots;
        _roleInfoReader = roleInfoReader ?? new MingYiRoleInfoReader();
    }

    public IReadOnlyList<string> FindGameRoots(string? preferredPath)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            roots.Add(preferredPath);
        }

        roots.AddRange(_candidateRoots);
        roots.AddRange(_registryPathProvider().SelectMany(ExpandCandidatePath));
        var found = roots
            .Where(path => Directory.Exists(path))
            .Where(path => FindUserDataDirectory(path) is not null)
            .Select(Path.GetFullPath)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path)
            .ToList();

        return found;
    }

    public string? FindUserDataDirectory(string gameRoot)
    {
        if (string.IsNullOrWhiteSpace(gameRoot) || !Directory.Exists(gameRoot))
        {
            return null;
        }

        if (Path.GetFileName(gameRoot).Equals("userdata", StringComparison.OrdinalIgnoreCase))
        {
            return gameRoot;
        }

        var direct = Path.Combine(gameRoot, "userdata");
        if (Directory.Exists(direct))
        {
            return direct;
        }

        return FindDirectoriesNamed(gameRoot, "userdata", maxDepth: 4)
            .OrderBy(path => path.Length)
            .FirstOrDefault();
    }

    public IReadOnlyList<CharacterConfig> ScanCharacters(string gameRootOrUserData)
    {
        var userData = FindUserDataDirectory(gameRootOrUserData);
        if (userData is null)
        {
            return Array.Empty<CharacterConfig>();
        }

        return FindCharacterDirectories(userData)
            .Select(path => CreateCharacterConfig(userData, path))
            .Where(character => character is not null)
            .Select(character => character!)
            .OrderBy(character => character.Account)
            .ThenBy(character => character.Server)
            .ThenBy(character => character.CharacterName)
            .ToList();
    }

    private static IReadOnlyList<string> BuildDefaultCandidateRoots()
    {
        var roots = new List<string>();
        foreach (var drive in DriveInfo.GetDrives())
        {
            try
            {
                if (!drive.IsReady)
                {
                    continue;
                }

                roots.Add(Path.Combine(drive.RootDirectory.FullName, "JX3"));
            }
            catch
            {
                // Some removable/network drives can throw while probing. Ignore them during startup scan.
            }
        }

        return roots
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(path => path)
            .ToList();
    }

    private static IEnumerable<string> BuildRegistryCandidateRoots()
    {
        var roots = new List<string>();
        foreach (var hive in new[] { Registry.LocalMachine, Registry.CurrentUser })
        {
            foreach (var subKey in RegistryInstallKeys())
            {
                roots.AddRange(ReadRegistryPaths(hive, subKey));
            }
        }

        roots.AddRange(ReadUninstallRegistryPaths(Registry.LocalMachine));
        roots.AddRange(ReadUninstallRegistryPaths(Registry.CurrentUser));
        return roots;
    }

    private static IEnumerable<string> RegistryInstallKeys()
    {
        yield return @"SOFTWARE\Kingsoft\JX3";
        yield return @"SOFTWARE\WOW6432Node\Kingsoft\JX3";
        yield return @"SOFTWARE\SeasunGame\JX3";
        yield return @"SOFTWARE\WOW6432Node\SeasunGame\JX3";
        yield return @"SOFTWARE\Kingsoft\剑网3";
        yield return @"SOFTWARE\WOW6432Node\Kingsoft\剑网3";
    }

    private static IEnumerable<string> ReadRegistryPaths(RegistryKey hive, string subKeyName)
    {
        try
        {
            using var key = hive.OpenSubKey(subKeyName);
            if (key is null)
            {
                return Array.Empty<string>();
            }

            var names = new[] { "InstallPath", "InstallLocation", "GamePath", "ClientPath", "Path", "Directory", "DisplayIcon", "UninstallString" };
            return names
                .Select(name => key.GetValue(name) as string)
                .Where(value => !string.IsNullOrWhiteSpace(value))
                .Select(value => value!)
                .ToArray();
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> ReadUninstallRegistryPaths(RegistryKey hive)
    {
        foreach (var root in new[] { @"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall", @"SOFTWARE\WOW6432Node\Microsoft\Windows\CurrentVersion\Uninstall" })
        {
            using var key = SafeOpenSubKey(hive, root);
            if (key is null)
            {
                continue;
            }

            foreach (var subKeyName in key.GetSubKeyNames())
            {
                using var appKey = SafeOpenSubKey(key, subKeyName);
                if (appKey is not RegistryKey appRegistryKey)
                {
                    continue;
                }

                var displayName = appRegistryKey.GetValue("DisplayName") as string;
                if (string.IsNullOrWhiteSpace(displayName) || !LooksLikeJx3DisplayName(displayName))
                {
                    continue;
                }

                foreach (var valueName in new[] { "InstallLocation", "DisplayIcon", "UninstallString" })
                {
                    if (appRegistryKey.GetValue(valueName) is string value && !string.IsNullOrWhiteSpace(value))
                    {
                        yield return value;
                    }
                }
            }
        }
    }

    private static RegistryKey? SafeOpenSubKey(RegistryKey key, string name)
    {
        try
        {
            return key.OpenSubKey(name);
        }
        catch
        {
            return null;
        }
    }

    private static bool LooksLikeJx3DisplayName(string displayName)
    {
        return displayName.Contains("JX3", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("剑网3", StringComparison.OrdinalIgnoreCase)
            || displayName.Contains("剑侠情缘网络版叁", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> ExpandCandidatePath(string rawPath)
    {
        var cleaned = CleanRegistryPath(rawPath);
        if (string.IsNullOrWhiteSpace(cleaned))
        {
            yield break;
        }

        var current = File.Exists(cleaned) ? Path.GetDirectoryName(cleaned) : cleaned;
        for (var depth = 0; depth < 5 && !string.IsNullOrWhiteSpace(current); depth++)
        {
            yield return current;
            yield return Path.Combine(current, "JX3");
            current = Directory.GetParent(current)?.FullName;
        }
    }

    private static string CleanRegistryPath(string value)
    {
        var trimmed = value.Trim().Trim('"');
        if (trimmed.StartsWith("\"", StringComparison.Ordinal))
        {
            var endQuote = trimmed.IndexOf('"', 1);
            if (endQuote > 1)
            {
                return trimmed.Substring(1, endQuote - 1);
            }
        }

        var exeIndex = trimmed.IndexOf(".exe", StringComparison.OrdinalIgnoreCase);
        if (exeIndex >= 0)
        {
            return trimmed.Substring(0, exeIndex + 4).Trim('"');
        }

        return trimmed;
    }

    private static IEnumerable<string> FindCharacterDirectories(string userData)
    {
        foreach (var directory in EnumerateDirectoriesByDepth(userData, maxDepth: 5))
        {
            if (IsIgnoredUserDataDirectory(directory))
            {
                continue;
            }

            if (LooksLikeCharacterDirectory(directory))
            {
                yield return directory;
            }
        }
    }

    private CharacterConfig? CreateCharacterConfig(string userData, string characterDir)
    {
        var relativePath = Path.GetRelativePath(userData, characterDir);
        var parts = relativePath
            .Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToArray();
        if (parts.Length < 2)
        {
            return null;
        }

        var dumps = Directory.Exists(Path.Combine(characterDir, "userpreferences"))
            ? SafeFiles(Path.Combine(characterDir, "userpreferences"), "*.dump").ToList()
            : new List<string>();

        var account = parts[0];
        var characterName = parts[^1];
        var server = parts.Length >= 4
            ? $"{parts[1]} / {parts[2]}"
            : parts.Length >= 3
                ? parts[1]
                : "未知区服";

        var metadata = _roleInfoReader.TryRead(characterDir, characterName);
        return new CharacterConfig(
            account,
            server,
            characterName,
            characterDir,
            dumps,
            metadata.Sect,
            metadata.Kungfu,
            metadata.EquipmentScore);
    }

    private static bool LooksLikeCharacterDirectory(string directory)
    {
        if (Directory.Exists(Path.Combine(directory, "userpreferences"))
            && SafeFiles(Path.Combine(directory, "userpreferences"), "*.dump").Any())
        {
            return true;
        }

        var knownCharacterFiles = new[]
        {
            "custom.dat",
            "custom.dat.addon",
            "hotkey.data",
            "userpreferences.jx3dat",
            "userpreferencesasync.jx3dat"
        };

        return knownCharacterFiles.Any(file => File.Exists(Path.Combine(directory, file)));
    }

    private static bool IsIgnoredUserDataDirectory(string directory)
    {
        var name = Path.GetFileName(directory);
        return name.Equals("fight_stat", StringComparison.OrdinalIgnoreCase)
            || name.Equals("userpreferences", StringComparison.OrdinalIgnoreCase);
    }

    private static IEnumerable<string> EnumerateDirectoriesByDepth(string root, int maxDepth)
    {
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((root, 0));
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current.Depth >= maxDepth)
            {
                continue;
            }

            foreach (var directory in SafeDirectories(current.Path))
            {
                yield return directory;
                if (!IsIgnoredUserDataDirectory(directory))
                {
                    pending.Enqueue((directory, current.Depth + 1));
                }
            }
        }
    }

    private static IEnumerable<string> FindDirectoriesNamed(string root, string name, int maxDepth)
    {
        var pending = new Queue<(string Path, int Depth)>();
        pending.Enqueue((root, 0));
        while (pending.Count > 0)
        {
            var current = pending.Dequeue();
            if (current.Depth > maxDepth)
            {
                continue;
            }

            foreach (var directory in SafeDirectories(current.Path))
            {
                if (Path.GetFileName(directory).Equals(name, StringComparison.OrdinalIgnoreCase))
                {
                    yield return directory;
                }

                if (current.Depth < maxDepth)
                {
                    pending.Enqueue((directory, current.Depth + 1));
                }
            }
        }
    }

    private static IEnumerable<string> SafeDirectories(string path)
    {
        try
        {
            return Directory.EnumerateDirectories(path);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static IEnumerable<string> SafeFiles(string path, string pattern)
    {
        try
        {
            return Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }
}

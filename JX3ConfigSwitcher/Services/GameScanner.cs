using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using JX3ConfigSwitcher.Models;

namespace JX3ConfigSwitcher.Services;

public sealed class GameScanner
{
    private readonly IReadOnlyList<string> _candidateRoots;

    public GameScanner(IEnumerable<string>? candidateRoots = null)
    {
        _candidateRoots = candidateRoots?.ToArray() ?? BuildDefaultCandidateRoots();
    }

    public IReadOnlyList<string> FindGameRoots(string? preferredPath)
    {
        var roots = new List<string>();
        if (!string.IsNullOrWhiteSpace(preferredPath))
        {
            roots.Add(preferredPath);
        }

        roots.AddRange(_candidateRoots);
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

    private static CharacterConfig? CreateCharacterConfig(string userData, string characterDir)
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

        return new CharacterConfig(account, server, characterName, characterDir, dumps);
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

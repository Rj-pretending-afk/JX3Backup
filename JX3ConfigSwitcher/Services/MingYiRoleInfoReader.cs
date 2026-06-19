using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace JX3ConfigSwitcher.Services;

public sealed record RoleMetadata(string? Sect, string? Kungfu, int? EquipmentScore);

public sealed class MingYiRoleInfoReader
{
    private static readonly string[] CandidatePatterns = { "*.json", "*.lua", "*.txt", "*.ini", "*.dat" };
    private static readonly string[] CandidateDirectoryTokens = { "茗", "ming", "my", "plugin", "interface", "userdata", "角色", "role" };
    private static readonly Regex ScoreRegex = new(
        @"(?:装分|装备分|equipmentScore|equipScore|score|pveScore|pvpScore)[^\d]{0,24}(\d{4,8})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RoleMetadata TryRead(string characterDirectory, string characterName)
    {
        foreach (var file in EnumerateCandidateFiles(characterDirectory).Take(160))
        {
            var text = ReadText(file);
            if (string.IsNullOrWhiteSpace(text))
            {
                continue;
            }

            var scoped = ScopeToCharacter(text, characterName);
            var option = SectCatalog.DetectFromText(scoped);
            var score = DetectScore(scoped);
            if (option is not null || score is not null)
            {
                return new RoleMetadata(option?.Sect, option?.Kungfu, score);
            }
        }

        return new RoleMetadata(null, null, null);
    }

    private static IEnumerable<string> EnumerateCandidateFiles(string characterDirectory)
    {
        var roots = new List<string>();
        var current = new DirectoryInfo(characterDirectory);
        for (var depth = 0; depth < 6 && current.Exists; depth++)
        {
            roots.Add(current.FullName);
            current = current.Parent;
            if (current is null)
            {
                break;
            }
        }

        foreach (var root in roots.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var directory in EnumerateInterestingDirectories(root))
            {
                foreach (var pattern in CandidatePatterns)
                {
                    foreach (var file in SafeFiles(directory, pattern))
                    {
                        yield return file;
                    }
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateInterestingDirectories(string root)
    {
        yield return root;
        foreach (var directory in SafeDirectories(root))
        {
            var name = Path.GetFileName(directory);
            if (CandidateDirectoryTokens.Any(token => name.Contains(token, StringComparison.OrdinalIgnoreCase)))
            {
                yield return directory;
                foreach (var child in SafeDirectories(directory))
                {
                    yield return child;
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
            return Directory.EnumerateFiles(path, pattern, SearchOption.TopDirectoryOnly)
                .Where(file => new FileInfo(file).Length is > 0 and < 4_000_000);
        }
        catch
        {
            return Array.Empty<string>();
        }
    }

    private static string ReadText(string file)
    {
        try
        {
            var bytes = File.ReadAllBytes(file);
            if (bytes.Count(value => value == 0) > bytes.Length / 20)
            {
                return string.Empty;
            }

            return Encoding.UTF8.GetString(bytes);
        }
        catch
        {
            return string.Empty;
        }
    }

    private static string ScopeToCharacter(string text, string characterName)
    {
        var index = text.IndexOf(characterName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return text;
        }

        var start = Math.Max(0, index - 1200);
        var length = Math.Min(text.Length - start, 2400);
        return text.Substring(start, length);
    }

    private static int? DetectScore(string text)
    {
        var match = ScoreRegex.Match(text);
        if (match.Success && int.TryParse(match.Groups[1].Value, out var score))
        {
            return score;
        }

        return null;
    }
}

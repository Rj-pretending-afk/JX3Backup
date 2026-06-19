using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Data.Sqlite;

namespace JX3ConfigSwitcher.Services;

public sealed record RoleMetadata(string? Sect, string? Kungfu, int? EquipmentScore);

public sealed class MingYiRoleInfoReader
{
    private static readonly Regex TextScoreRegex = new(
        @"(?:equipmentScore|equipScore|equip_score|nEquipScore|装分|装备分)[^\d]{0,32}(\d{3,8})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex GenericScoreRegex = new(
        @"(?:score|ownerscore)[^\d]{0,32}(\d{3,8})",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    private static readonly Regex RoleNameRegex = new(
        @"(?:name|ownername|角色名|角色)[\s""'\[\]:=]+(?<value>[^""',}\]\r\n]+)",
        RegexOptions.IgnoreCase | RegexOptions.Compiled);

    public RoleMetadata TryRead(string characterDirectory, string characterName)
    {
        var metadata = new RoleMetadata(null, null, null);

        foreach (var candidate in ReadKnownMingYiSources(characterDirectory, characterName))
        {
            metadata = Merge(metadata, candidate);
            if (IsComplete(metadata))
            {
                return metadata;
            }
        }

        foreach (var candidate in ReadExplicitTextSources(characterDirectory, characterName))
        {
            metadata = Merge(metadata, candidate);
            if (IsComplete(metadata))
            {
                return metadata;
            }
        }

        return metadata;
    }

    private static IEnumerable<RoleMetadata> ReadKnownMingYiSources(string characterDirectory, string characterName)
    {
        foreach (var dataRoot in EnumerateMingYiDataRoots(characterDirectory))
        {
            foreach (var statsRoot in EnumerateRoleStatisticsRoots(dataRoot))
            {
                var roleStat = Path.Combine(statsRoot, "role_stat.jx3dat");
                if (File.Exists(roleStat))
                {
                    var metadata = TryReadRoleStatText(roleStat, characterName);
                    if (HasAny(metadata))
                    {
                        yield return metadata;
                    }
                }

                foreach (var oldDb in new[]
                         {
                             Path.Combine(statsRoot, "role_stat.v3.db"),
                             Path.Combine(statsRoot, "role_stat.v2.db")
                         })
                {
                    var metadata = TryReadOldRoleStatDatabase(oldDb, characterName);
                    if (HasAny(metadata))
                    {
                        yield return metadata;
                    }
                }

                var equipDb = Path.Combine(statsRoot, "equip_stat.v4.db");
                var equipMetadata = TryReadEquipStatDatabase(equipDb, characterName);
                if (HasAny(equipMetadata))
                {
                    yield return equipMetadata;
                }
            }
        }
    }

    private static IEnumerable<RoleMetadata> ReadExplicitTextSources(string characterDirectory, string characterName)
    {
        foreach (var file in EnumerateExplicitCandidateFiles(characterDirectory).Take(80))
        {
            var metadata = TryReadRoleStatText(file, characterName);
            if (HasAny(metadata))
            {
                yield return metadata;
            }
        }
    }

    private static RoleMetadata TryReadRoleStatText(string file, string characterName)
    {
        var text = ReadText(file);
        if (string.IsNullOrWhiteSpace(text) || text.IndexOf(characterName, StringComparison.OrdinalIgnoreCase) < 0)
        {
            return new RoleMetadata(null, null, null);
        }

        var scoped = ScopeToCharacterRecord(text, characterName);
        var kungfu = ReadIntField(scoped, "kungfu", "dwKungfuID", "dwActualKungfuID", "dwActualMountKungfuID", "dwMountKungfuID", "nKungfu");
        var force = ReadIntField(scoped, "force", "dwForceID", "ownerforce", "nForce");
        var option = SectCatalog.FindByKungfuId(kungfu);
        var sect = option?.Sect
            ?? ReadStringField(scoped, "sect", "forceName", "force_name", "门派")
            ?? SectCatalog.GetSectByForceId(force);
        var kungfuName = option?.Kungfu
            ?? ReadStringField(scoped, "kungfuName", "kungfu", "心法");

        if (!string.IsNullOrWhiteSpace(kungfuName) && int.TryParse(kungfuName, out _))
        {
            kungfuName = null;
        }

        if (string.IsNullOrWhiteSpace(sect) && !string.IsNullOrWhiteSpace(kungfuName))
        {
            var matched = SectCatalog.Find(kungfuName);
            sect = matched?.Sect;
            kungfuName = matched?.Kungfu ?? kungfuName;
        }

        return new RoleMetadata(sect, kungfuName, DetectScore(scoped, allowGeneric: true));
    }

    private static RoleMetadata TryReadOldRoleStatDatabase(string dbPath, string characterName)
    {
        if (!File.Exists(dbPath))
        {
            return new RoleMetadata(null, null, null);
        }

        return ReadSqlite(dbPath, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT force, equip_score
                FROM RoleInfo
                WHERE name = $name
                ORDER BY time DESC
                LIMIT 1
                """;
            command.Parameters.AddWithValue("$name", characterName);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return new RoleMetadata(null, null, null);
            }

            var force = ReadInt(reader, "force");
            return new RoleMetadata(
                SectCatalog.GetSectByForceId(force),
                null,
                ReadInt(reader, "equip_score"));
        });
    }

    private static RoleMetadata TryReadEquipStatDatabase(string dbPath, string characterName)
    {
        if (!File.Exists(dbPath))
        {
            return new RoleMetadata(null, null, null);
        }

        return ReadSqlite(dbPath, connection =>
        {
            using var command = connection.CreateCommand();
            command.CommandText = """
                SELECT ownerforce, ownerscore, ownersuitindex
                FROM OwnerInfo
                WHERE ownername = $name
                ORDER BY time DESC
                LIMIT 1
                """;
            command.Parameters.AddWithValue("$name", characterName);
            using var reader = command.ExecuteReader();
            if (!reader.Read())
            {
                return new RoleMetadata(null, null, null);
            }

            var force = ReadInt(reader, "ownerforce");
            var suitIndex = ReadInt(reader, "ownersuitindex");
            var score = ReadOwnerscore(ReadString(reader, "ownerscore"), suitIndex);
            return new RoleMetadata(SectCatalog.GetSectByForceId(force), null, score);
        });
    }

    private static RoleMetadata ReadSqlite(string dbPath, Func<SqliteConnection, RoleMetadata> read)
    {
        try
        {
            var builder = new SqliteConnectionStringBuilder
            {
                DataSource = dbPath,
                Mode = SqliteOpenMode.ReadOnly,
                Pooling = false
            };
            using var connection = new SqliteConnection(builder.ConnectionString);
            connection.Open();
            return read(connection);
        }
        catch
        {
            return new RoleMetadata(null, null, null);
        }
    }

    private static IEnumerable<string> EnumerateMingYiDataRoots(string characterDirectory)
    {
        var ancestors = EnumerateAncestors(characterDirectory).ToList();
        var userData = ancestors.FirstOrDefault(path =>
            Path.GetFileName(path).Equals("userdata", StringComparison.OrdinalIgnoreCase));

        foreach (var root in ancestors)
        {
            yield return Path.Combine(root, "interface", "MY#DATA");
            yield return Path.Combine(root, "MY#DATA");
        }

        if (userData is not null)
        {
            var relative = Path.GetRelativePath(userData, characterDirectory);
            var account = relative.Split(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                .FirstOrDefault(part => !string.IsNullOrWhiteSpace(part));
            if (!string.IsNullOrWhiteSpace(account))
            {
                yield return Path.Combine(userData, account, "interface", "MY#DATA");
            }
        }

        foreach (var root in ancestors.Take(5))
        {
            foreach (var match in FindDirectoriesNamed(root, "MY#DATA", maxDepth: 4))
            {
                yield return match;
            }
        }
    }

    private static IEnumerable<string> EnumerateRoleStatisticsRoots(string dataRoot)
    {
        if (!Directory.Exists(dataRoot))
        {
            yield break;
        }

        var direct = Path.Combine(dataRoot, "userdata", "role_statistics");
        if (Directory.Exists(direct))
        {
            yield return direct;
        }

        foreach (var editionDirectory in SafeDirectories(dataRoot)
                     .Where(path => Path.GetFileName(path).StartsWith("!all-users@", StringComparison.OrdinalIgnoreCase)))
        {
            var statsRoot = Path.Combine(editionDirectory, "userdata", "role_statistics");
            if (Directory.Exists(statsRoot))
            {
                yield return statsRoot;
            }
        }
    }

    private static IEnumerable<string> EnumerateExplicitCandidateFiles(string characterDirectory)
    {
        foreach (var root in EnumerateAncestors(characterDirectory).Take(6).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            foreach (var directory in SafeDirectories(root).Prepend(root))
            {
                var directoryName = Path.GetFileName(directory);
                if (!LooksLikeMingYiOrRoleDirectory(directoryName))
                {
                    continue;
                }

                foreach (var pattern in new[] { "*.json", "*.lua", "*.jx3dat", "*.txt" })
                {
                    foreach (var file in SafeFiles(directory, pattern))
                    {
                        var fileName = Path.GetFileName(file);
                        if (LooksLikeRoleStatFile(fileName))
                        {
                            yield return file;
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<string> EnumerateAncestors(string path)
    {
        var current = new DirectoryInfo(path);
        while (current.Exists)
        {
            yield return current.FullName;
            current = current.Parent;
            if (current is null)
            {
                break;
            }
        }
    }

    private static bool LooksLikeMingYiOrRoleDirectory(string name)
    {
        return name.Contains("MY", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Ming", StringComparison.OrdinalIgnoreCase)
            || name.Contains("Role", StringComparison.OrdinalIgnoreCase)
            || name.Contains("角色", StringComparison.OrdinalIgnoreCase)
            || name.Contains("茗", StringComparison.OrdinalIgnoreCase);
    }

    private static bool LooksLikeRoleStatFile(string name)
    {
        return name.Contains("role", StringComparison.OrdinalIgnoreCase)
            || name.Contains("stat", StringComparison.OrdinalIgnoreCase)
            || name.Contains("角色", StringComparison.OrdinalIgnoreCase);
    }

    private static string ReadText(string file)
    {
        try
        {
            var info = new FileInfo(file);
            if (!info.Exists || info.Length is <= 0 or > 6_000_000)
            {
                return string.Empty;
            }

            var bytes = File.ReadAllBytes(file);
            if (bytes.Length == 0 || bytes.Count(value => value == 0) > bytes.Length / 20)
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

    private static string ScopeToCharacterRecord(string text, string characterName)
    {
        var index = text.IndexOf(characterName, StringComparison.OrdinalIgnoreCase);
        if (index < 0)
        {
            return string.Empty;
        }

        var start = Math.Max(0, index - 1600);
        var length = Math.Min(text.Length - start, 3200);
        return text.Substring(start, length);
    }

    private static string? ReadStringField(string text, params string[] names)
    {
        foreach (var name in names)
        {
            var escaped = Regex.Escape(name);
            var match = Regex.Match(
                text,
                $@"(?:[""']?{escaped}[""']?\s*[:=]\s*|{escaped}[\s""'\[\]:=]+)[""'](?<value>[^""']+)[""']",
                RegexOptions.IgnoreCase);
            if (match.Success)
            {
                return match.Groups["value"].Value.Trim();
            }
        }

        return null;
    }

    private static int? ReadIntField(string text, params string[] names)
    {
        foreach (var name in names)
        {
            var escaped = Regex.Escape(name);
            var match = Regex.Match(
                text,
                $@"(?:[""']?{escaped}[""']?\s*[:=]\s*|{escaped}[\s""'\[\]:=]+)(?<value>\d+)",
                RegexOptions.IgnoreCase);
            if (match.Success && int.TryParse(match.Groups["value"].Value, out var value))
            {
                return value;
            }
        }

        return null;
    }

    private static int? DetectScore(string text, bool allowGeneric)
    {
        var match = TextScoreRegex.Match(text);
        if (!match.Success && allowGeneric && RoleNameRegex.IsMatch(text))
        {
            match = GenericScoreRegex.Match(text);
        }

        return match.Success && int.TryParse(match.Groups[1].Value, out var score)
            ? score
            : null;
    }

    private static int? ReadOwnerscore(string? ownerscore, int? suitIndex)
    {
        if (string.IsNullOrWhiteSpace(ownerscore))
        {
            return null;
        }

        if (suitIndex is not null)
        {
            var indexed = Regex.Match(
                ownerscore,
                $@"\[{suitIndex.Value}\]\s*=\s*(?<value>\d+)|{suitIndex.Value}\s*[:=]\s*(?<value>\d+)");
            if (indexed.Success && int.TryParse(indexed.Groups["value"].Value, out var score))
            {
                return score;
            }
        }

        var first = Regex.Match(ownerscore, @"\d{3,8}");
        return first.Success && int.TryParse(first.Value, out var firstScore)
            ? firstScore
            : null;
    }

    private static int? ReadInt(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        if (reader.IsDBNull(ordinal))
        {
            return null;
        }

        try
        {
            return Convert.ToInt32(reader.GetValue(ordinal));
        }
        catch
        {
            return null;
        }
    }

    private static string? ReadString(SqliteDataReader reader, string name)
    {
        var ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : Convert.ToString(reader.GetValue(ordinal));
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
                .Where(file => new FileInfo(file).Length is > 0 and < 6_000_000);
        }
        catch
        {
            return Array.Empty<string>();
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

    private static RoleMetadata Merge(RoleMetadata current, RoleMetadata incoming)
    {
        return new RoleMetadata(
            current.Sect ?? incoming.Sect,
            current.Kungfu ?? incoming.Kungfu,
            current.EquipmentScore ?? incoming.EquipmentScore);
    }

    private static bool HasAny(RoleMetadata metadata)
    {
        return metadata.Sect is not null
            || metadata.Kungfu is not null
            || metadata.EquipmentScore is not null;
    }

    private static bool IsComplete(RoleMetadata metadata)
    {
        return metadata.Sect is not null
            && metadata.Kungfu is not null
            && metadata.EquipmentScore is not null;
    }
}

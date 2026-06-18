using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace JX3ConfigSwitcher.Services;

public sealed class SkillPlacementService
{
    private const string UserPreferencesFileName = "userpreferences.jx3dat";
    private static readonly Regex ActionBarEntryPattern = new(
        @"(?<prefix>\[""(?<key>ActionBar\d+_Page\d+/\d+)""\]\s*=\s*)(?<value>\{[^{}]*\})",
        RegexOptions.Compiled);

    private readonly CndkLuaFile _cndkLuaFile;

    public SkillPlacementService(CndkLuaFile cndkLuaFile)
    {
        _cndkLuaFile = cndkLuaFile;
    }

    public SkillPlacementSnapshot ExtractFromCharacter(string characterPath)
    {
        var path = Path.Combine(characterPath, UserPreferencesFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("角色目录缺少 userpreferences.jx3dat，无法抽取技能摆放。", path);
        }

        var text = _cndkLuaFile.ReadPayloadText(path);
        var entries = ExtractEntries(text);
        if (entries.Count == 0)
        {
            throw new InvalidDataException("没有在 userpreferences.jx3dat 中找到 ActionBar 技能摆放项。");
        }

        return new SkillPlacementSnapshot(1, DateTime.Now, entries);
    }

    public void RestoreToCharacter(string characterPath, SkillPlacementSnapshot snapshot)
    {
        var path = Path.Combine(characterPath, UserPreferencesFileName);
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("目标角色目录缺少 userpreferences.jx3dat，无法只合并技能摆放。请先登录该角色生成配置。", path);
        }

        var text = _cndkLuaFile.ReadPayloadText(path);
        var merged = MergeEntries(text, snapshot.Entries);
        _cndkLuaFile.WritePayloadText(path, merged);
        _cndkLuaFile.ValidateHeader(path);
    }

    public string Serialize(SkillPlacementSnapshot snapshot)
    {
        return JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
    }

    public SkillPlacementSnapshot Deserialize(string json)
    {
        return JsonSerializer.Deserialize<SkillPlacementSnapshot>(json)
            ?? throw new InvalidDataException("技能摆放快照读取失败。");
    }

    internal static Dictionary<string, string> ExtractEntries(string luaText)
    {
        return ActionBarEntryPattern.Matches(luaText)
            .Cast<Match>()
            .GroupBy(match => match.Groups["key"].Value)
            .ToDictionary(group => group.Key, group => group.Last().Groups["value"].Value, StringComparer.Ordinal);
    }

    internal static string MergeEntries(string luaText, IReadOnlyDictionary<string, string> entries)
    {
        var remaining = new Dictionary<string, string>(entries, StringComparer.Ordinal);
        var merged = ActionBarEntryPattern.Replace(luaText, match =>
        {
            var key = match.Groups["key"].Value;
            if (!remaining.TryGetValue(key, out var value))
            {
                return match.Value;
            }

            remaining.Remove(key);
            return match.Groups["prefix"].Value + value;
        });

        if (remaining.Count == 0)
        {
            return merged;
        }

        var insertAt = merged.LastIndexOf('}');
        if (insertAt < 0)
        {
            throw new InvalidDataException("目标 userpreferences.jx3dat 不是可合并的 Lua 表。");
        }

        var additions = string.Concat(remaining.OrderBy(pair => pair.Key)
            .Select(pair => $",[\"{pair.Key}\"]={pair.Value}"));
        return merged.Insert(insertAt, additions);
    }
}

public sealed record SkillPlacementSnapshot(
    int Version,
    DateTime CreatedAt,
    IReadOnlyDictionary<string, string> Entries);

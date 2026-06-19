using System;
using System.Collections.Generic;
using System.Linq;

namespace JX3ConfigSwitcher.Services;

public sealed record SectOption(string Sect, string Kungfu, string Color)
{
    public string DisplayName => string.IsNullOrWhiteSpace(Kungfu) ? Sect : $"{Sect} / {Kungfu}";
    public string Tag => DisplayName;
    public string ContrastText => UseDarkText(Color) ? "#0F141B" : "#FFFFFF";

    private static bool UseDarkText(string color)
    {
        if (string.IsNullOrWhiteSpace(color) || color.Length < 7 || color[0] != '#')
        {
            return false;
        }

        try
        {
            var red = Convert.ToInt32(color.Substring(1, 2), 16);
            var green = Convert.ToInt32(color.Substring(3, 2), 16);
            var blue = Convert.ToInt32(color.Substring(5, 2), 16);
            var luminance = (red * 0.299) + (green * 0.587) + (blue * 0.114);
            return luminance > 150;
        }
        catch
        {
            return false;
        }
    }
}

public static class SectCatalog
{
    private static readonly IReadOnlyList<SectOption> Options = new List<SectOption>
    {
        new("未知", "手动标记", "#4CC9F0"),
        new("万花", "花间游", "#C084FC"),
        new("万花", "离经易道", "#A78BFA"),
        new("纯阳", "紫霞功", "#60A5FA"),
        new("纯阳", "太虚剑意", "#38BDF8"),
        new("少林", "易筋经", "#F59E0B"),
        new("少林", "洗髓经", "#FBBF24"),
        new("七秀", "冰心诀", "#F472B6"),
        new("七秀", "云裳心经", "#FB7185"),
        new("天策", "傲血战意", "#EF4444"),
        new("天策", "铁牢律", "#F97316"),
        new("藏剑", "问水诀", "#FACC15"),
        new("藏剑", "山居剑意", "#EAB308"),
        new("五毒", "毒经", "#22C55E"),
        new("五毒", "补天诀", "#34D399"),
        new("唐门", "惊羽诀", "#818CF8"),
        new("唐门", "天罗诡道", "#6366F1"),
        new("明教", "焚影圣诀", "#F97316"),
        new("明教", "明尊琉璃体", "#FB923C"),
        new("丐帮", "笑尘诀", "#D97706"),
        new("苍云", "分山劲", "#64748B"),
        new("苍云", "铁骨衣", "#94A3B8"),
        new("长歌", "莫问", "#14B8A6"),
        new("长歌", "相知", "#2DD4BF"),
        new("霸刀", "北傲诀", "#DC2626"),
        new("蓬莱", "凌海诀", "#0EA5E9"),
        new("凌雪阁", "隐龙诀", "#E11D48"),
        new("衍天宗", "太玄经", "#8B5CF6"),
        new("药宗", "无方", "#10B981"),
        new("药宗", "灵素", "#6EE7B7"),
        new("刀宗", "孤锋诀", "#F43F5E"),
        new("万灵", "山海心诀", "#84CC16"),
        new("段氏", "周天功", "#06B6D4")
    };

    public static IReadOnlyList<SectOption> All => Options;

    public static SectOption Default => Options[0];

    public static SectOption? Find(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return Options.FirstOrDefault(option =>
            EqualsToken(option.Tag, value)
            || EqualsToken(option.Kungfu, value)
            || EqualsToken(option.Sect, value));
    }

    public static SectOption? FindByColor(string? color)
    {
        if (string.IsNullOrWhiteSpace(color))
        {
            return null;
        }

        return Options.FirstOrDefault(option => EqualsToken(option.Color, color));
    }

    public static SectOption? DetectFromText(string text)
    {
        var kungfu = Options
            .Where(option => !string.IsNullOrWhiteSpace(option.Kungfu))
            .FirstOrDefault(option => text.Contains(option.Kungfu, StringComparison.OrdinalIgnoreCase));
        if (kungfu is not null)
        {
            return kungfu;
        }

        return Options
            .Where(option => option.Sect != "未知")
            .FirstOrDefault(option => text.Contains(option.Sect, StringComparison.OrdinalIgnoreCase));
    }

    private static bool EqualsToken(string left, string right)
    {
        return string.Equals(left.Trim(), right.Trim(), StringComparison.OrdinalIgnoreCase);
    }
}

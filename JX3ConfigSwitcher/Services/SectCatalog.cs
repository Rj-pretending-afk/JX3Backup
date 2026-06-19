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

    private static readonly IReadOnlyDictionary<int, string> ForceNames = new Dictionary<int, string>
    {
        [0] = "江湖",
        [1] = "少林",
        [2] = "万花",
        [3] = "天策",
        [4] = "纯阳",
        [5] = "七秀",
        [6] = "五毒",
        [7] = "唐门",
        [8] = "藏剑",
        [9] = "丐帮",
        [10] = "明教",
        [21] = "苍云",
        [22] = "长歌",
        [23] = "霸刀",
        [24] = "蓬莱",
        [25] = "凌雪阁",
        [211] = "衍天宗",
        [212] = "药宗",
        [213] = "刀宗",
        [214] = "万灵",
        [215] = "段氏"
    };

    private static readonly IReadOnlyDictionary<int, string> KungfuNames = new Dictionary<int, string>
    {
        [10002] = "洗髓经",
        [10003] = "易筋经",
        [10014] = "紫霞功",
        [10015] = "太虚剑意",
        [10021] = "花间游",
        [10028] = "离经易道",
        [10026] = "傲血战意",
        [10062] = "铁牢律",
        [10080] = "云裳心经",
        [10081] = "冰心诀",
        [10144] = "问水诀",
        [10145] = "山居剑意",
        [10175] = "毒经",
        [10176] = "补天诀",
        [10224] = "惊羽诀",
        [10225] = "天罗诡道",
        [10242] = "焚影圣诀",
        [10243] = "明尊琉璃体",
        [10268] = "笑尘诀",
        [10389] = "铁骨衣",
        [10390] = "分山劲",
        [10447] = "莫问",
        [10448] = "相知",
        [10464] = "北傲诀",
        [10533] = "凌海诀",
        [10585] = "隐龙诀",
        [10615] = "太玄经",
        [10626] = "灵素",
        [10627] = "无方",
        [10698] = "孤锋诀",
        [10756] = "山海心诀",
        [10786] = "周天功",
        [10821] = "幽罗引"
    };

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

    public static SectOption? FindByKungfuId(int? kungfuId)
    {
        if (kungfuId is null || !KungfuNames.TryGetValue(kungfuId.Value, out var name))
        {
            return null;
        }

        return Find(name);
    }

    public static string? GetSectByForceId(int? forceId)
    {
        return forceId is not null && ForceNames.TryGetValue(forceId.Value, out var name)
            ? name
            : null;
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

using System;
using System.Collections.Generic;
using System.IO;
using JX3ConfigSwitcher.Models;

namespace JX3ConfigSwitcher.Services;

public sealed class ConfigClassifier
{
    private static readonly string[] MacroTokens = { "macro", "宏" };
    private static readonly string[] ActionTokens = { "action", "skill", "shortcutbar", "hotbar", "quickslot", "技能", "快捷栏", "动作" };
    private static readonly string[] KeyTokens = { "key", "bind", "hotkey", "快捷键", "键位" };
    private static readonly string[] UiTokens = { "ui", "interface", "layout", "frame", "界面", "布局" };
    private static readonly string[] DisplayChatAddonTokens = { "chat", "display", "graphics", "addon", "plugin", "聊天", "显示", "插件", "画质" };

    public IReadOnlyList<ModuleChoice> GetDefaultChoices(SaveKind kind)
    {
        var safeSelected = kind is SaveKind.Universal;
        var fullSelected = kind is SaveKind.CharacterSpecific or SaveKind.AutoSnapshot;
        return new List<ModuleChoice>
        {
            new(ConfigModule.UiLayout, "界面布局", "窗口位置、界面布局和普通 UI 配置", true, false),
            new(ConfigModule.KeyBindings, "快捷键/键位", "键位相关配置；不会主动包含技能栏摆放", true, false),
            new(ConfigModule.DisplayChatAddon, "显示/聊天/插件", "画质、聊天、插件等通用设置", true, false),
            new(ConfigModule.Macros, "宏", "高风险：可能覆盖宏", fullSelected && !safeSelected, true),
            new(ConfigModule.ActionButtons, "技能/动作按钮", "高风险：可能覆盖技能摆放和动作栏", fullSelected && !safeSelected, true),
            new(ConfigModule.FullDump, "完整 dump", "高风险：userpreferences/*.dump 可能包含动作栏/宏", fullSelected && !safeSelected, true)
        };
    }

    public ConfigModule Classify(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').ToLowerInvariant();
        var fileName = Path.GetFileName(normalized);
        if ((normalized.Contains("/userpreferences/") || normalized.StartsWith("userpreferences/"))
            && fileName.EndsWith(".dump", StringComparison.OrdinalIgnoreCase))
        {
            return ConfigModule.FullDump;
        }

        if (ContainsAny(normalized, MacroTokens))
        {
            return ConfigModule.Macros;
        }

        if (ContainsAny(normalized, ActionTokens))
        {
            return ConfigModule.ActionButtons;
        }

        if (ContainsAny(normalized, KeyTokens))
        {
            return ConfigModule.KeyBindings;
        }

        if (ContainsAny(normalized, UiTokens))
        {
            return ConfigModule.UiLayout;
        }

        return ContainsAny(normalized, DisplayChatAddonTokens)
            ? ConfigModule.DisplayChatAddon
            : ConfigModule.DisplayChatAddon;
    }

    public bool IsHighRisk(ConfigModule module)
    {
        return module is ConfigModule.Macros or ConfigModule.ActionButtons or ConfigModule.FullDump;
    }

    private static bool ContainsAny(string value, IEnumerable<string> tokens)
    {
        foreach (var token in tokens)
        {
            if (value.Contains(token, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}

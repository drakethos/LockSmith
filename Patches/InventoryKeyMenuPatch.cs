using System;
using System.Text;
using System.Text.RegularExpressions;
using HarmonyLib;
using LockSmith.Access;

namespace LockSmith.Patches;

/// <summary>
/// Locksmith-key tooltip polish only: team line, description shortcut colors, clipboard hints.
/// Unified menu open + “to …” interact line are owned by DrakeModsLibs.
/// </summary>
[HarmonyPatch]
public static class InventoryKeyMenuPatch
{
    const string MenuHintColor = "#ffff00";

    static readonly Regex ClipboardDescSentence = new Regex(
        @"\s*Ctrl\+C\s+copies\s+names\s+onto\s+the\s+key\s*[;.]?\s*Ctrl\+V\s+pastes\s+onto\s+a\s+piece\.?",
        RegexOptions.IgnoreCase | RegexOptions.Compiled | RegexOptions.Singleline);

    static readonly string[] DescriptionShortcuts =
    {
        "Shift+Right-click",
        "Shift+Right Click",
        "Alt+E"
    };

    [HarmonyPatch(typeof(InventoryGrid), nameof(InventoryGrid.CreateItemTooltip))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void PolishKeyTooltip(InventoryGrid __instance, ItemDrop.ItemData? item, UITooltip tooltip)
    {
        try
        {
            if (!LockSmithConfig.EnableKeyPasses)
                return;
            if (!ChestAccessService.IsLocksmithKey(item) || tooltip == null)
                return;

            var topicField = AccessTools.Field(typeof(UITooltip), "m_topic");
            var textField = AccessTools.Field(typeof(UITooltip), "m_text");
            if (topicField == null || textField == null)
                return;

            var topic = topicField.GetValue(tooltip) as string ?? "";
            var currentText = textField.GetValue(tooltip) as string ?? "";

            var teamLine = KeyPassService.FormatMembershipTooltipLine(item);
            if (!string.IsNullOrEmpty(teamLine))
                currentText = InsertAfterDescription(item!, currentText, teamLine!);

            currentText = ClipboardDescSentence.Replace(currentText, "");
            currentText = ColorizeDescriptionShortcuts(currentText);

            const string clipboardMarker = "copy names onto key";
            if (currentText.IndexOf(clipboardMarker, StringComparison.OrdinalIgnoreCase) < 0)
            {
                var sb = new StringBuilder();
                sb.AppendLine();
                sb.AppendLine(
                    $"<color={MenuHintColor}><b>Ctrl+C</b></color> copy names onto key · <color={MenuHintColor}><b>Ctrl+V</b></color> paste onto piece");
                currentText += sb.ToString();
            }

            tooltip.Set(topic, currentText, __instance.m_tooltipAnchor);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Key tooltip polish failed: {ex.Message}");
        }
    }

    static string ColorizeDescriptionShortcuts(string tooltipText)
    {
        if (string.IsNullOrEmpty(tooltipText))
            return tooltipText;

        var result = tooltipText;
        foreach (var shortcut in DescriptionShortcuts)
            result = ColorizePlainShortcut(result, shortcut);
        return result;
    }

    static string ColorizePlainShortcut(string text, string shortcut)
    {
        if (string.IsNullOrEmpty(text) || string.IsNullOrEmpty(shortcut))
            return text;

        var colored = $"<color={MenuHintColor}><b>{shortcut}</b></color>";
        var sb = new StringBuilder(text.Length + 32);
        var start = 0;
        while (start < text.Length)
        {
            var idx = text.IndexOf(shortcut, start, StringComparison.OrdinalIgnoreCase);
            if (idx < 0)
            {
                sb.Append(text, start, text.Length - start);
                break;
            }

            if (IsAlreadyColoredShortcut(text, idx, shortcut.Length))
            {
                sb.Append(text, start, idx + shortcut.Length - start);
                start = idx + shortcut.Length;
                continue;
            }

            sb.Append(text, start, idx - start);
            sb.Append(colored);
            start = idx + shortcut.Length;
        }

        return sb.ToString();
    }

    static bool IsAlreadyColoredShortcut(string text, int index, int length)
    {
        var before = text.LastIndexOf("<color=", index, StringComparison.OrdinalIgnoreCase);
        if (before < 0)
            return false;
        var closeBefore = text.IndexOf('>', before);
        if (closeBefore < 0 || closeBefore > index)
            return false;
        var after = text.IndexOf("</color>", index + length, StringComparison.OrdinalIgnoreCase);
        return after >= 0;
    }

    static string InsertAfterDescription(ItemDrop.ItemData item, string tooltipText, string teamLine)
    {
        if (string.IsNullOrEmpty(tooltipText) || string.IsNullOrEmpty(teamLine))
            return tooltipText;

        var insert = "\n" + teamLine;
        foreach (var desc in DescriptionCandidates(item))
        {
            if (string.IsNullOrEmpty(desc))
                continue;
            var idx = tooltipText.IndexOf(desc, StringComparison.Ordinal);
            if (idx < 0)
                continue;
            return tooltipText.Insert(idx + desc.Length, insert);
        }

        return teamLine + "\n" + tooltipText;
    }

    static string[] DescriptionCandidates(ItemDrop.ItemData item)
    {
        var shared = item.m_shared?.m_description ?? "";
        var localizedShared = string.IsNullOrEmpty(shared) ? "" : Localization.instance.Localize(shared);
        var token = "$" + LockSmithLocalization.KeyDescToken;
        var localizedToken = Localization.instance.Localize(token);
        var configured = LockSmithConfig.KeyDescription?.Trim() ?? "";

        return new[]
        {
            localizedShared,
            shared,
            localizedToken,
            configured
        };
    }
}

using System.Collections.Generic;
using Jotunn.Managers;

namespace LockSmith;

/// <summary>English tokens for hover lines and center messages (Valheim-native feedback).</summary>
public static class LockSmithLocalization
{
    public const string KeyNameToken = "item_locksmith_key";
    public const string KeyDescToken = "item_locksmith_key_desc";
    public const string PiecePublicToken = "locksmith_piece_public";
    public const string PiecePrivateToken = "locksmith_piece_private";
    public const string MsgNowPublicToken = "locksmith_msg_now_public";
    public const string MsgNowPrivateToken = "locksmith_msg_now_private";
    public const string MsgDeniedToken = "locksmith_msg_denied";
    public const string MsgDisabledToken = "locksmith_msg_disabled";
    public const string MsgWrongTargetToken = "locksmith_msg_wrong_target";
    public const string MsgTempKeyGivenToken = "locksmith_msg_temp_key_given";
    public const string HoverMakePublicToken = "locksmith_hover_make_public";
    public const string HoverMakePrivateToken = "locksmith_hover_make_private";

    public static void Register()
    {
        // GetLocalization() already registers with Jotunn; do not AddLocalization again.
        var localization = LocalizationManager.Instance.GetLocalization();

        var name = string.IsNullOrWhiteSpace(LockSmithConfig.KeyName)
            ? "Locksmith Key"
            : LockSmithConfig.KeyName.Trim();
        var desc = string.IsNullOrWhiteSpace(LockSmithConfig.KeyDescription)
            ? "Equip to change chest/door interact: press E to toggle public access."
            : LockSmithConfig.KeyDescription.Trim();

        localization.AddTranslation("English", new Dictionary<string, string>
        {
            { KeyNameToken, name },
            { KeyDescToken, desc },
            { PiecePublicToken, "[Public]" },
            { PiecePrivateToken, "[Private]" },
            { MsgNowPublicToken, "Now public" },
            { MsgNowPrivateToken, "Now private" },
            { MsgDeniedToken, "Only ward members can change access" },
            { MsgDisabledToken, "Access is disabled on this server" },
            { MsgWrongTargetToken, "Hold the Locksmith key and look at a chest or door" },
            { MsgTempKeyGivenToken, "TEMP: Locksmith key added (remove before release)" },
            { HoverMakePublicToken, "Make public" },
            { HoverMakePrivateToken, "Make private" },
        });
    }

    public static string T(string token) => Localization.instance.Localize("$" + token);
}

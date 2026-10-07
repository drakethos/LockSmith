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
    public const string PieceUnmanagedToken = "locksmith_piece_unmanaged";
    public const string PiecePersonalToken = "locksmith_piece_personal";
    public const string PieceTeamToken = "locksmith_piece_team";
    public const string PieceGuestsTagToken = "locksmith_piece_guests_tag";
    public const string PieceOptInReadyToken = "locksmith_piece_optin_ready";
    public const string MsgNowPublicToken = "locksmith_msg_now_public";
    public const string MsgNowPrivateToken = "locksmith_msg_now_private";
    public const string MsgManagedToken = "locksmith_msg_managed";
    public const string MsgNowPersonalToken = "locksmith_msg_now_personal";
    public const string MsgNowTeamToken = "locksmith_msg_now_team";
    public const string MsgDeniedToken = "locksmith_msg_denied";
    public const string MsgDisabledToken = "locksmith_msg_disabled";
    public const string MsgWrongTargetToken = "locksmith_msg_wrong_target";
    public const string MsgGroupOwnerOnlyToken = "locksmith_msg_group_owner_only";
    public const string MsgGroupMemberToken = "locksmith_msg_group_member";
    public const string MsgOptInOpenedToken = "locksmith_msg_optin_opened";
    public const string MsgOptInClosedToken = "locksmith_msg_optin_closed";
    public const string MsgOptedInToken = "locksmith_msg_opted_in";
    public const string MsgOptedOutToken = "locksmith_msg_opted_out";
    public const string MsgAlreadyOptedInToken = "locksmith_msg_already_opted_in";
    public const string MsgNoTeamAccessToken = "locksmith_msg_no_team_access";
    public const string MsgClearedToken = "locksmith_msg_cleared";
    public const string MsgNeedActiveWardToken = "locksmith_msg_need_active_ward";
    public const string MsgSyncFailedToken = "locksmith_msg_sync_failed";
    public const string MsgJoinFailedToken = "locksmith_msg_join_failed";
    public const string HoverMakePublicToken = "locksmith_hover_make_public";
    public const string HoverMakePrivateToken = "locksmith_hover_make_private";
    public const string HoverDesignateToken = "locksmith_hover_designate";
    public const string HoverClearLockSmithToken = "locksmith_hover_clear";
    public const string HoverMakePersonalToken = "locksmith_hover_make_personal";
    public const string HoverMakeTeamToken = "locksmith_hover_make_team";
    public const string HoverOpenOptInToken = "locksmith_hover_open_optin";
    public const string HoverCloseOptInToken = "locksmith_hover_close_optin";
    public const string HoverJoinAccessToken = "locksmith_hover_join_access";
    public const string HoverLeaveAccessToken = "locksmith_hover_leave_access";
    public const string HoverLockMenuToken = "locksmith_hover_lock_menu";
    public const string HoverGuestsHeaderToken = "locksmith_hover_guests_header";
    public const string InventoryTabTitleToken = "locksmith_inventory_tab_title";
    public const string InventoryHintPhraseToken = "locksmith_inventory_hint_phrase";
    public const string PublicNameSuffixToken = "locksmith_public_name_suffix";
    public const string PublicPieceDescToken = "locksmith_public_piece_desc";
    public const string MsgPublicPrefabToken = "locksmith_msg_public_prefab";
    public const string PieceCategoryPublicToken = "locksmith_category_public";

    public const string DefaultKeyDescription =
        "Optional. Hold it and press <color=#ffff00><b>E</b></color> on a chest or door to open the Lock menu (Shift+E works without it). Copied guest names are saved on the key.";

    /// <summary>Pre-0.5 default, still saved in older config files. Swapped for the new default.</summary>
    const string LegacyKeyDescription =
        "Equip to designate a chest/door. After that, <color=#ffff00><b>Alt+E</b></color> toggles public/private if you have access. Key required for Team / Join setup. <color=#ffff00><b>Shift+Right-click</b></color> the key: Relabel, grab/pull names, clear, or clone.";

    public static void Register()
    {
        var localization = LocalizationManager.Instance.GetLocalization();

        var name = string.IsNullOrWhiteSpace(LockSmithConfig.KeyName)
            ? "Locksmith Key"
            : LockSmithConfig.KeyName.Trim();
        var desc = string.IsNullOrWhiteSpace(LockSmithConfig.KeyDescription)
                   || LockSmithConfig.KeyDescription.Trim() == LegacyKeyDescription
            ? DefaultKeyDescription
            : LockSmithConfig.KeyDescription.Trim();

        localization.AddTranslation("English", new Dictionary<string, string>
        {
            { KeyNameToken, name },
            { KeyDescToken, desc },
            { InventoryTabTitleToken, "Lock" },
            { InventoryHintPhraseToken, "configure lock tool" },
            { PiecePublicToken, "[Public]" },
            { PiecePrivateToken, "[Private]" },
            { PieceUnmanagedToken, "[Not LockSmith]" },
            { PiecePersonalToken, "[Personal]" },
            { PieceTeamToken, "Team" },
            { PieceGuestsTagToken, "Guests" },
            { PieceOptInReadyToken, "[Join open]" },
            { MsgNowPublicToken, "Now public" },
            { MsgNowPrivateToken, "Now private" },
            { MsgManagedToken, "LockSmith on" },
            { MsgNowPersonalToken, "Now personal" },
            { MsgNowTeamToken, "Now team (shared)" },
            { MsgDeniedToken, "Ward members only" },
            { MsgDisabledToken, "Turned off on this server" },
            { MsgWrongTargetToken, "Look at a chest or door" },
            { MsgGroupOwnerOnlyToken, "Only the owner can change this" },
            { MsgGroupMemberToken, "[Team]" },
            { MsgOptInOpenedToken, "Join open — others press E" },
            { MsgOptInClosedToken, "Join closed" },
            { MsgOptedInToken, "You have access" },
            { MsgOptedOutToken, "You left" },
            { MsgAlreadyOptedInToken, "You already have access" },
            { MsgNoTeamAccessToken, "No team access" },
            { MsgClearedToken, "LockSmith removed" },
            { MsgNeedActiveWardToken, "Needs an active ward" },
            { MsgSyncFailedToken, "Didn't go through — try again" },
            { MsgJoinFailedToken, "Couldn't join — try again" },
            { HoverMakePublicToken, "Make public" },
            { HoverMakePrivateToken, "Make private" },
            { HoverDesignateToken, "Enable LockSmith" },
            { HoverClearLockSmithToken, "Remove LockSmith" },
            { HoverMakePersonalToken, "Make personal" },
            { HoverMakeTeamToken, "Make team" },
            { HoverOpenOptInToken, "Open Join" },
            { HoverCloseOptInToken, "Close Join" },
            { HoverJoinAccessToken, "Join access" },
            { HoverLeaveAccessToken, "Leave access" },
            { HoverLockMenuToken, "Lock menu" },
            { HoverGuestsHeaderToken, "Guests:" },
            { PublicNameSuffixToken, " (public)" },
            { PublicPieceDescToken, "<color=#FFCC33>Always open to anyone. This cannot be locked.</color>" },
            { MsgPublicPrefabToken, "This piece is always public" },
            { PieceCategoryPublicToken, "Public" },
        });
    }

    public static string T(string token) => Localization.instance.Localize("$" + token);

    public static string FormatGuestCount(int count) =>
        "Guests: " + count;

    public static string FormatGuestsCount(int count) =>
        "Guests [" + count + "]";
}

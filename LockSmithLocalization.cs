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
    public const string MsgGroupNeedTeamToken = "locksmith_msg_group_need_team";
    public const string MsgOptInOpenedToken = "locksmith_msg_optin_opened";
    public const string MsgOptInClosedToken = "locksmith_msg_optin_closed";
    public const string MsgOptedInToken = "locksmith_msg_opted_in";
    public const string MsgOptedOutToken = "locksmith_msg_opted_out";
    public const string MsgLeaveConfirmToken = "locksmith_msg_leave_confirm";
    public const string MsgAlreadyOptedInToken = "locksmith_msg_already_opted_in";
    public const string MsgNoTeamAccessToken = "locksmith_msg_no_team_access";
    public const string MsgPublicNoSetupToken = "locksmith_msg_public_no_setup";
    public const string MsgClearConfirmToken = "locksmith_msg_clear_confirm";
    public const string MsgClearedToken = "locksmith_msg_cleared";
    public const string MsgNothingToClearToken = "locksmith_msg_nothing_to_clear";
    public const string MsgNeedActiveWardToken = "locksmith_msg_need_active_ward";
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
    public const string HoverGuestsHeaderToken = "locksmith_hover_guests_header";
    public const string InventoryTabTitleToken = "locksmith_inventory_tab_title";
    public const string InventoryHintPhraseToken = "locksmith_inventory_hint_phrase";
    public const string PublicNameSuffixToken = "locksmith_public_name_suffix";
    public const string MsgPublicPrefabToken = "locksmith_msg_public_prefab";
    public const string PieceCategoryPublicToken = "locksmith_category_public";

    public static void Register()
    {
        var localization = LocalizationManager.Instance.GetLocalization();

        var name = string.IsNullOrWhiteSpace(LockSmithConfig.KeyName)
            ? "Locksmith Key"
            : LockSmithConfig.KeyName.Trim();
        var desc = string.IsNullOrWhiteSpace(LockSmithConfig.KeyDescription)
            ? "Equip to designate a chest/door. After that, <color=#ffff00><b>Alt+E</b></color> toggles public/private if you have access. Key required for Team / Join setup. <color=#ffff00><b>Shift+Right-click</b></color> the key: Relabel, grab/pull names, clear, or clone."
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
            { MsgManagedToken, "LockSmith enabled on this piece" },
            { MsgNowPersonalToken, "Chest is personal (team paused — names kept)" },
            { MsgNowTeamToken, "Chest is team (shared)" },
            { MsgDeniedToken, "Only ward members can change access" },
            { MsgDisabledToken, "Access is disabled on this server" },
            { MsgWrongTargetToken, "Hold the Locksmith key and look at a chest or door" },
            { MsgGroupOwnerOnlyToken, "Only the chest creator can change team access" },
            { MsgGroupMemberToken, "[Team]" },
            { MsgGroupNeedTeamToken, "Switch the chest to Team first" },
            { MsgOptInOpenedToken, "Join is open — others press E to opt in" },
            { MsgOptInClosedToken, "Join is closed" },
            { MsgOptedInToken, "You joined access" },
            { MsgOptedOutToken, "You left access" },
            { MsgLeaveConfirmToken, "YOU SURE??? Leave access — you can't rejoin unless the owner opens Join. Press leave again within 5s to confirm." },
            { MsgAlreadyOptedInToken, "You already have access" },
            { MsgNoTeamAccessToken, "No team access" },
            { MsgPublicNoSetupToken, "Public pieces stay open — lock it private to change Join settings" },
            { MsgClearConfirmToken, "YOU SURE??? This clears guest names. {0}+E again to confirm. (Got a backup key? — someday ;))" },
            { MsgClearedToken, "LockSmith removed — piece is vanilla again" },
            { MsgNothingToClearToken, "Nothing LockSmith to clear on this piece" },
            { MsgNeedActiveWardToken, "Needs an active ward" },
            { HoverMakePublicToken, "Make public" },
            { HoverMakePrivateToken, "Make private" },
            { HoverDesignateToken, "Enable LockSmith" },
            { HoverClearLockSmithToken, "Clear LockSmith" },
            { HoverMakePersonalToken, "Make personal" },
            { HoverMakeTeamToken, "Make team" },
            { HoverOpenOptInToken, "Open join" },
            { HoverCloseOptInToken, "Close join" },
            { HoverJoinAccessToken, "Join access" },
            { HoverLeaveAccessToken, "Leave access" },
            { HoverGuestsHeaderToken, "Guests:" },
            { PublicNameSuffixToken, " (public)" },
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

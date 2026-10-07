using UnityEngine;

namespace LockSmith.Access;

/// <summary>Hover display helpers (local color, name reveal rules).</summary>
public static class AccessHoverDisplay
{
    /// <summary>
    /// Key mode: creator, ward members, or guests on that piece may see names after the count.
    /// Everyone else always gets <c>Guests - [N]</c> / <c>Team - [N]</c> only.
    /// </summary>
    public static bool CanRevealGuestNames(Vector3 piecePosition, bool isPieceCreator, ZNetView? nview = null)
    {
        if (!ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
            return false;

        if (isPieceCreator)
            return true;

        if (WardAccess.HasLocalWardAccess(piecePosition))
            return true;

        var local = Player.m_localPlayer;
        if (local != null && nview != null && nview.IsValid()
            && PieceGuestAccess.IsGuest(nview, local.GetPlayerID()))
            return true;

        return false;
    }

    public static string Colorize(string text)
    {
        if (string.IsNullOrEmpty(text))
            return text;

        var hex = LockSmithConfig.TeamLabelColorHex;
        if (string.IsNullOrEmpty(hex))
            return text;

        return "<color=" + hex + ">" + text + "</color>";
    }

    public static string TeamLabel() =>
        Colorize(LockSmithLocalization.T(LockSmithLocalization.PieceTeamToken));

    /// <summary>
    /// Plain hover line for hammer public clones (<c>PublicPieceHoverText</c>, default [Public]).
    /// </summary>
    public static string PublicPieceHoverLabel() => LockSmithConfig.PublicPieceHoverText;

    /// <summary>Count line: <c>Team - [2]</c> or <c>Guests - [2]</c>.</summary>
    public static string AccessCountLabel(bool team, int count)
    {
        if (count < 0)
            count = 0;

        return Colorize(AccessTag(team) + " - [" + count + "]");
    }

    /// <summary>
    /// Key-mode line keeps the count and appends names:
    /// <c>Guests - [2] Alice, Bob</c>.
    /// </summary>
    public static string AccessNamesLabel(bool team, int count, string names)
    {
        if (count < 0)
            count = 0;

        var line = AccessTag(team) + " - [" + count + "]";
        if (!string.IsNullOrEmpty(names))
            line += " " + names;

        return Colorize(line);
    }

    static string AccessTag(bool team) =>
        team
            ? LockSmithLocalization.T(LockSmithLocalization.PieceTeamToken)
            : LockSmithLocalization.T(LockSmithLocalization.PieceGuestsTagToken);

    /// <summary>The one action hint: <c>[E] Lock menu</c> with the key, <c>[AltPlace+E] Lock menu</c> without.</summary>
    public static string MenuHint(bool withKey)
    {
        var keys = withKey
            ? "[<color=yellow><b>$KEY_Use</b></color>]"
            : "[<color=yellow><b>$KEY_AltPlace</b></color>+<color=yellow><b>$KEY_Use</b></color>]";
        return Localization.instance.Localize(keys) + " "
               + LockSmithLocalization.T(LockSmithLocalization.HoverLockMenuToken);
    }

    /// <summary><c>[AltPlace+E] Join access</c>: joining goes through the Lock menu, never plain E.</summary>
    public static string JoinHint() =>
        Localization.instance.Localize(
            "[<color=yellow><b>$KEY_AltPlace</b></color>+<color=yellow><b>$KEY_Use</b></color>]")
        + " " + LockSmithLocalization.T(LockSmithLocalization.HoverJoinAccessToken);

    public static string GuestsCountLabel(int count) =>
        AccessCountLabel(team: false, count);

    public static string LocalizedPieceName(Container container)
    {
        if (container == null)
            return string.Empty;

        var raw = container.GetHoverName();
        if (string.IsNullOrEmpty(raw))
            raw = container.m_name ?? string.Empty;

        return Localization.instance.Localize(raw);
    }

    public static string LocalizedPieceName(Door door)
    {
        if (door == null)
            return string.Empty;

        var raw = door.GetHoverName();
        return Localization.instance.Localize(raw ?? string.Empty);
    }
}

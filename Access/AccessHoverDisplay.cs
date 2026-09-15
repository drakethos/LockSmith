using UnityEngine;

namespace LockSmith.Access;

/// <summary>Hover display helpers (local color, name reveal rules).</summary>
public static class AccessHoverDisplay
{
    /// <summary>
    /// Names with Locksmith key out for creator, ward members, or guests on that piece.
    /// Everyone else sees Guests [N] only.
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

    /// <summary>Compact access line: <c>[Team] — [2]</c> or <c>[Guests] — [2]</c>.</summary>
    public static string AccessCountLabel(bool team, int count)
    {
        if (count < 0)
            count = 0;

        var tag = team
            ? LockSmithLocalization.T(LockSmithLocalization.PieceTeamToken)
            : LockSmithLocalization.T(LockSmithLocalization.PieceGuestsTagToken);
        return Colorize(tag + " — [" + count + "]");
    }

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

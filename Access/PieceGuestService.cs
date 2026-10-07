using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Partial ward access: guests on a normal chest/door can open without being on the ward.
/// Opt-in ready = ward-style join (solo-testable via logout / second character).
/// </summary>
public static class PieceGuestService
{
    public static bool GuestsEnabled =>
        LockSmithConfig.EnablePieceGuests && LockSmithConfig.EnableOptInAccess;

    public static bool IsEligibleWardChest(Container? container) =>
        GuestsEnabled
        && container != null
        && !PublicPieceRegistration.IsPublicPiece(container)
        && PieceAccessState.IsEligibleChest(container);

    public static bool IsEligibleWardDoor(Door? door) =>
        GuestsEnabled
        && door != null
        && !PublicPieceRegistration.IsPublicPiece(door)
        && PieceAccessState.IsEligibleDoor(door);

    public static bool ShouldBypassWardForGuest(Container container)
    {
        if (!IsEligibleWardChest(container))
            return false;

        var local = Player.m_localPlayer;
        if (local == null)
            return false;

        var nview = PieceAccessState.GetNetView(container);
        return PieceGuestAccess.IsGuest(nview, local.GetPlayerID());
    }

    public static bool ShouldBypassWardForGuest(Door door)
    {
        if (!IsEligibleWardDoor(door))
            return false;

        var local = Player.m_localPlayer;
        if (local == null)
            return false;

        var nview = PieceAccessState.GetNetView(door);
        return PieceGuestAccess.IsGuest(nview, local.GetPlayerID());
    }

    /// <summary>
    /// Join is open and the local player is not on the ward and not already a guest.
    /// WardIsLove would otherwise block hover and E before they can opt in.
    /// </summary>
    public static bool IsJoinOpenForStranger(Door? door)
    {
        if (!LockSmithConfig.EnableOptInAccess || !door || !IsEligibleWardDoor(door))
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (PieceAccessState.IsPublic(nview) || !PieceGuestAccess.IsOptInReady(nview))
            return false;

        return IsLocalStranger(nview, PieceAccessState.GetPosition(door));
    }

    /// <summary>
    /// Same as <see cref="IsJoinOpenForStranger(Door)"/> for ward chests and team chests.
    /// </summary>
    public static bool IsJoinOpenForStranger(Container? container)
    {
        if (!LockSmithConfig.EnableOptInAccess || !container)
            return false;

        ZNetView? nview;
        Vector3 pos;
        if (GroupAccessState.IsPrivateFamilyChest(container))
        {
            if (!LockSmithConfig.EnableGroupChests)
                return false;

            nview = GroupAccessState.GetNetView(container);
            if (!GroupAccessState.IsTeamMode(nview) || !PieceGuestAccess.IsOptInReady(nview))
                return false;

            var local = Player.m_localPlayer;
            if (local != null && GroupAccessState.HasTeamAccess(container, local.GetPlayerID()))
                return false;

            pos = GroupAccessState.GetPosition(container);
        }
        else
        {
            if (!IsEligibleWardChest(container))
                return false;

            nview = PieceAccessState.GetNetView(container);
            if (PieceAccessState.IsPublic(nview) || !PieceGuestAccess.IsOptInReady(nview))
                return false;

            pos = PieceAccessState.GetPosition(container);
        }

        return IsLocalStranger(nview, pos);
    }

    /// <summary>Hover for a stranger while Join is open: no ward “No access” line.</summary>
    public static bool TryBuildStrangerJoinHover(Door? door, out string hoverText)
    {
        hoverText = string.Empty;
        if (!IsJoinOpenForStranger(door) || !door)
            return false;

        hoverText = BuildStrangerJoinHover(AccessHoverDisplay.LocalizedPieceName(door));
        return true;
    }

    /// <summary>Hover for a stranger while Join is open: no ward “No access” line.</summary>
    public static bool TryBuildStrangerJoinHover(Container? container, out string hoverText)
    {
        hoverText = string.Empty;
        if (!IsJoinOpenForStranger(container) || !container)
            return false;

        hoverText = BuildStrangerJoinHover(AccessHoverDisplay.LocalizedPieceName(container));
        return true;
    }

    static bool IsLocalStranger(ZNetView? nview, Vector3 pos)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var local = Player.m_localPlayer;
        if (local == null)
            return false;

        if (PieceGuestAccess.IsGuest(nview, local.GetPlayerID()))
            return false;

        // Permitted ward players already pass WIL. Only the denied player needs the bypass.
        return !WardAccess.HasLocalWardAccess(pos);
    }

    static string BuildStrangerJoinHover(string pieceName)
    {
        var sb = new StringBuilder();
        sb.Append(pieceName);
        sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken));
        sb.Append('\n').Append(AccessHoverDisplay.JoinHint());
        return sb.ToString();
    }

    public static bool CanManagePieceWithKey(Container container, long playerId)
    {
        if (GroupAccessState.IsPrivateFamilyChest(container))
            return GroupAccessState.IsCreator(container, playerId);

        if (!PieceAccessState.IsEligibleChest(container))
            return false;

        return WardAccess.HasWardAccessForPlayer(PieceAccessState.GetPosition(container), playerId);
    }

    public static bool CanManagePieceWithKey(Door door, long playerId)
    {
        if (!PieceAccessState.IsEligibleDoor(door))
            return false;

        return WardAccess.HasWardAccessForPlayer(PieceAccessState.GetPosition(door), playerId);
    }

    /// <summary>Key-mode hover for guests who cannot administer the piece.</summary>
    public static bool TryBuildGuestKeyHover(Container container, out string hoverText)
    {
        hoverText = string.Empty;

        var local = Player.m_localPlayer;
        if (local == null || !ChestAccessService.IsHoldingLocksmithKey(local))
            return false;

        var nview = GroupAccessState.IsPrivateFamilyChest(container)
            ? GroupAccessState.GetNetView(container)
            : PieceAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return false;

        var playerId = local.GetPlayerID();
        if (!PieceGuestAccess.IsGuest(nview, playerId) || CanManagePieceWithKey(container, playerId))
            return false;

        var team = GroupAccessState.IsPrivateFamilyChest(container)
            && GroupAccessState.IsTeamMode(nview);
        PieceGuestAccess.TryRefreshGuestNames(nview);
        var summary = PieceGuestAccess.FormatGuestSummary(nview, revealNames: true, team);

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);
        sb.Append('\n').Append(AccessHoverDisplay.MenuHint(withKey: true));

        hoverText = sb.ToString();
        return true;
    }

    public static bool TryBuildGuestKeyHover(Door door, out string hoverText)
    {
        hoverText = string.Empty;

        var local = Player.m_localPlayer;
        if (local == null || !ChestAccessService.IsHoldingLocksmithKey(local))
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (nview == null || !nview.IsValid())
            return false;

        var playerId = local.GetPlayerID();
        if (!PieceGuestAccess.IsGuest(nview, playerId) || CanManagePieceWithKey(door, playerId))
            return false;

        PieceGuestAccess.TryRefreshGuestNames(nview);
        var summary = PieceGuestAccess.FormatGuestSummary(nview, revealNames: true, team: false);

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(door));
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);
        sb.Append('\n').Append(AccessHoverDisplay.MenuHint(withKey: true));

        hoverText = sb.ToString();
        return true;
    }

    public static bool IsPermittedForPublicToggle(ZNetView? nview, Vector3 pos, long playerId)
    {
        if (playerId == 0L)
            return false;

        if (WardAccess.HasWardAccessForPlayer(pos, playerId))
            return true;

        return PieceGuestAccess.IsGuest(nview, playerId);
    }

    public static void RegisterRpc(Container container)
    {
        if (!GuestsEnabled || !PieceAccessState.IsEligibleChest(container))
            return;

        var nview = PieceAccessState.GetNetView(container);
        PieceGuestAccess.RegisterRpcs(nview!);
    }

    public static void RegisterRpc(Door door)
    {
        if (!GuestsEnabled || !PieceAccessState.IsEligibleDoor(door))
            return;

        var nview = PieceAccessState.GetNetView(door);
        PieceGuestAccess.RegisterRpcs(nview!);
    }

    public static bool TryHandleOptInInteract(Container container, Humanoid user, bool hold, bool alt)
    {
        if (!IsEligibleWardChest(container))
            return false;

        var nview = PieceAccessState.GetNetView(container);
        // Public = open only; no Join / Leave / setup.
        if (PieceAccessState.IsPublic(nview))
            return false;

        if (IsLocalCreator(container, user))
            return false;

        return PieceGuestAccess.TryHandleGuestSelfInteract(nview, user, hold, alt);
    }

    public static bool TryHandleOptInInteract(Door door, Humanoid user, bool hold, bool alt)
    {
        if (!IsEligibleWardDoor(door))
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (PieceAccessState.IsPublic(nview))
            return false;

        if (IsLocalCreator(door, user))
            return false;

        return PieceGuestAccess.TryHandleGuestSelfInteract(nview, user, hold, alt);
    }

    /// <summary>Non-key hover when local player is permitted on a designated ward piece.</summary>
    public static bool TryBuildGuestAccessHover(Container container, out string hoverText)
    {
        hoverText = string.Empty;

        var nview = PieceAccessState.GetNetView(container);
        var local = Player.m_localPlayer;
        if (local == null || !IsEligibleWardChest(container))
            return false;

        if (!PieceAccessState.IsManaged(nview))
            return false;

        var playerId = local.GetPlayerID();
        var pos = PieceAccessState.GetPosition(container);
        if (!IsPermittedForPublicToggle(nview, pos, playerId))
            return false;

        var isGuest = PieceGuestAccess.IsGuest(nview, playerId);
        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");
        var isPublic = PieceAccessState.IsPublic(nview);
        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));

        if (isPublic)
        {
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));
            sb.Append('\n').Append(useKey).Append(' ')
                .Append(Localization.instance.Localize("$piece_container_open"));
            AppendNoKeyMenuHint(sb, pos, PieceAccessMenu.HasNoKeyMenu(container));
            hoverText = sb.ToString();
            return true;
        }

        if (!isGuest)
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePrivateToken));

        // Simple mode (no key): no guest line; the list lives in the Lock menu.
        // Enhanced mode (key in hand): Guests - [N] plus names.
        var reveal = AccessHoverDisplay.CanRevealGuestNames(pos, isPieceCreator: false, nview);
        if (reveal)
            PieceGuestAccess.TryRefreshGuestNames(nview);
        var summary = reveal ? PieceGuestAccess.FormatGuestSummary(nview, revealNames: true, team: false) : string.Empty;
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);

        sb.Append('\n').Append(useKey).Append(' ')
            .Append(Localization.instance.Localize("$piece_container_open"));
        AppendNoKeyMenuHint(sb, pos, PieceAccessMenu.HasNoKeyMenu(container));

        hoverText = sb.ToString();
        return true;
    }

    /// <summary>
    /// No-key hover tail for permitted players: the Lock menu hint when it has a button for them,
    /// or why it can't be used right now (ward down).
    /// </summary>
    static void AppendNoKeyMenuHint(StringBuilder sb, Vector3 pos, bool hasMenu)
    {
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNeedActiveWardToken));
        else if (hasMenu)
            sb.Append('\n').Append(AccessHoverDisplay.MenuHint(withKey: false));
    }

    public static bool TryBuildGuestAccessHover(Door door, out string hoverText)
    {
        hoverText = string.Empty;

        var nview = PieceAccessState.GetNetView(door);
        var local = Player.m_localPlayer;
        if (local == null || !IsEligibleWardDoor(door))
            return false;

        if (!PieceAccessState.IsManaged(nview))
            return false;

        var playerId = local.GetPlayerID();
        var pos = PieceAccessState.GetPosition(door);
        if (!IsPermittedForPublicToggle(nview, pos, playerId))
            return false;

        var isGuest = PieceGuestAccess.IsGuest(nview, playerId);
        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");
        var isPublic = PieceAccessState.IsPublic(nview);
        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(door));

        if (isPublic)
        {
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));
            sb.Append('\n').Append(useKey).Append(' ').Append(DoorUseAction(door));
            AppendNoKeyMenuHint(sb, pos, PieceAccessMenu.HasNoKeyMenu(door));
            hoverText = sb.ToString();
            return true;
        }

        if (!isGuest)
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePrivateToken));

        // Simple mode (no key): no guest line; the list lives in the Lock menu.
        // Enhanced mode (key in hand): Guests - [N] plus names.
        var reveal = AccessHoverDisplay.CanRevealGuestNames(pos, isPieceCreator: false, nview);
        if (reveal)
            PieceGuestAccess.TryRefreshGuestNames(nview);
        var summary = reveal ? PieceGuestAccess.FormatGuestSummary(nview, revealNames: true, team: false) : string.Empty;
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);

        sb.Append('\n').Append(useKey).Append(' ').Append(DoorUseAction(door));
        AppendNoKeyMenuHint(sb, pos, PieceAccessMenu.HasNoKeyMenu(door));

        hoverText = sb.ToString();
        return true;
    }

    private static string DoorUseAction(Door door)
    {
        var open = false;
        try
        {
            var nview = PieceAccessState.GetNetView(door);
            var zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
            if (zdo != null)
                open = zdo.GetInt("state", 0) != 0;
        }
        catch (System.Exception)
        {
            // fall through to Open
        }

        return Localization.instance.Localize(open ? "$piece_door_close" : "$piece_door_open");
    }

    public static void AppendKeyHoverExtras(StringBuilder sb, Container container)
    {
        if (!IsEligibleWardChest(container))
            return;

        var nview = PieceAccessState.GetNetView(container);
        if (PieceAccessState.IsPublic(nview))
            return;

        var local = Player.m_localPlayer;
        var isCreator = local != null && IsCreator(container, local.GetPlayerID());
        var pos = PieceAccessState.GetPosition(container);
        if (AccessHoverDisplay.CanRevealGuestNames(pos, isCreator, nview))
            PieceGuestAccess.TryRefreshGuestNames(nview);
        PieceGuestAccess.AppendOptInHover(sb, nview, isCreator, pos);
    }

    public static void AppendKeyHoverExtras(StringBuilder sb, Door door)
    {
        if (!IsEligibleWardDoor(door))
            return;

        var nview = PieceAccessState.GetNetView(door);
        if (PieceAccessState.IsPublic(nview))
            return;

        var local = Player.m_localPlayer;
        var isCreator = local != null && IsCreator(door, local.GetPlayerID());
        var pos = PieceAccessState.GetPosition(door);
        if (AccessHoverDisplay.CanRevealGuestNames(pos, isCreator, nview))
            PieceGuestAccess.TryRefreshGuestNames(nview);
        PieceGuestAccess.AppendOptInHover(sb, nview, isCreator, pos);
    }

    public static string GetOptInStatusSuffix(Container container)
    {
        if (!IsEligibleWardChest(container))
            return string.Empty;

        var nview = PieceAccessState.GetNetView(container);
        if (!PieceGuestAccess.IsOptInReady(nview))
            return string.Empty;

        return "\n" + LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken);
    }

    public static string GetOptInStatusSuffix(Door door)
    {
        if (!IsEligibleWardDoor(door))
            return string.Empty;

        var nview = PieceAccessState.GetNetView(door);
        if (!PieceGuestAccess.IsOptInReady(nview))
            return string.Empty;

        return "\n" + LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken);
    }

    private static bool IsLocalCreator(Container container, Humanoid user)
    {
        var player = user as Player;
        return player != null && IsCreator(container, player.GetPlayerID());
    }

    private static bool IsLocalCreator(Door door, Humanoid user)
    {
        var player = user as Player;
        return player != null && IsCreator(door, player.GetPlayerID());
    }

    private static bool IsCreator(Container container, long playerId)
    {
        var piece = container.GetComponentInParent<Piece>();
        return piece != null && piece.GetCreator() == playerId;
    }

    private static bool IsCreator(Door door, long playerId)
    {
        var piece = door.GetComponentInParent<Piece>();
        return piece != null && piece.GetCreator() == playerId;
    }
}

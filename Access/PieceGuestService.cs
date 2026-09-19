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
    /// Guest holding key: SetupModifier+E leaves (double-confirm). Returns true if handled.
    /// </summary>
    public static bool TryHandleGuestKeyLeave(Container container, Humanoid user)
    {
        if (!LockSmithInput.IsSetupModifierHeld())
            return false;

        var player = user as Player;
        if (player == null)
            return false;

        var nview = GroupAccessState.IsPrivateFamilyChest(container)
            ? GroupAccessState.GetNetView(container)
            : PieceAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return false;

        if (!PieceGuestAccess.IsGuest(nview, player.GetPlayerID()))
            return false;

        // Ward/creator use SetupModifier for Join — don't steal that.
        if (CanManagePieceWithKey(container, player.GetPlayerID()))
            return false;

        return PieceGuestAccess.TryConfirmOptOut(nview, user, player.GetPlayerID());
    }

    public static bool TryHandleGuestKeyLeave(Door door, Humanoid user)
    {
        if (!LockSmithInput.IsSetupModifierHeld())
            return false;

        var player = user as Player;
        if (player == null)
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (nview == null || !nview.IsValid())
            return false;

        if (!PieceGuestAccess.IsGuest(nview, player.GetPlayerID()))
            return false;

        if (CanManagePieceWithKey(door, player.GetPlayerID()))
            return false;

        return PieceGuestAccess.TryConfirmOptOut(nview, user, player.GetPlayerID());
    }

    /// <summary>Guest (not ward/creator) holding key — open normally instead of key-admin UX.</summary>
    public static bool ShouldGuestKeyFallThroughOpen(Container container, Humanoid user)
    {
        var player = user as Player;
        if (player == null)
            return false;

        var playerId = player.GetPlayerID();
        if (CanManagePieceWithKey(container, playerId))
            return false;

        var nview = GroupAccessState.IsPrivateFamilyChest(container)
            ? GroupAccessState.GetNetView(container)
            : PieceAccessState.GetNetView(container);
        return PieceGuestAccess.IsGuest(nview, playerId);
    }

    public static bool ShouldGuestKeyFallThroughOpen(Door door, Humanoid user)
    {
        var player = user as Player;
        if (player == null)
            return false;

        var playerId = player.GetPlayerID();
        if (CanManagePieceWithKey(door, playerId))
            return false;

        var nview = PieceAccessState.GetNetView(door);
        return PieceGuestAccess.IsGuest(nview, playerId);
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

        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");
        var setupUse = Localization.instance.Localize(
            LockSmithInput.FormatModifierUse(LockSmithConfig.SetupModifier));

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);
        sb.Append('\n').Append(useKey).Append(' ')
            .Append(Localization.instance.Localize("$piece_container_open"));
        sb.Append('\n').Append(setupUse).Append(' ')
            .Append(LockSmithLocalization.T(LockSmithLocalization.HoverLeaveAccessToken));

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

        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");
        var setupUse = Localization.instance.Localize(
            LockSmithInput.FormatModifierUse(LockSmithConfig.SetupModifier));

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(door));
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);
        sb.Append('\n').Append(useKey).Append(' ').Append(DoorUseAction(door));
        sb.Append('\n').Append(setupUse).Append(' ')
            .Append(LockSmithLocalization.T(LockSmithLocalization.HoverLeaveAccessToken));

        hoverText = sb.ToString();
        return true;
    }

    /// <summary>
    /// No key: on a LockSmith-designated ward chest/door, permitted players Alt+E public/private.
    /// Permitted = ward access or guest. Strangers never toggle. Private-family excluded.
    /// While Join is open, Alt+E still means Leave for guests.
    /// </summary>
    public static bool TryHandleGuestPublicToggle(Container container, Humanoid user, bool hold, bool alt) =>
        TryHandlePermittedPublicToggle(container, user, hold, alt);

    public static bool TryHandlePermittedPublicToggle(Container container, Humanoid user, bool hold, bool alt)
    {
        if (hold || !alt || !LockSmithConfig.EnableGuestPublicToggle)
            return false;

        if (!IsEligibleWardChest(container) || GroupAccessState.IsPrivateFamilyChest(container))
            return false;

        var nview = PieceAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid() || !PieceAccessState.IsManaged(nview))
            return false;

        var player = user as Player;
        if (player == null)
            return false;

        var playerId = player.GetPlayerID();
        var pos = PieceAccessState.GetPosition(container);
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
            return false;

        if (!IsPermittedForPublicToggle(nview, pos, playerId))
            return false;

        // Join open → Leave takes Alt+E (existing opt-out).
        if (PieceGuestAccess.IsOptInReady(nview) && !PieceAccessState.IsPublic(nview))
            return false;

        return TogglePublicAsPermitted(nview, user, playerId, isDoor: false);
    }

    public static bool TryHandleGuestPublicToggle(Door door, Humanoid user, bool hold, bool alt) =>
        TryHandlePermittedPublicToggle(door, user, hold, alt);

    public static bool TryHandlePermittedPublicToggle(Door door, Humanoid user, bool hold, bool alt)
    {
        if (hold || !alt || !LockSmithConfig.EnableGuestPublicToggle)
            return false;

        if (!IsEligibleWardDoor(door))
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (nview == null || !nview.IsValid() || !PieceAccessState.IsManaged(nview))
            return false;

        var player = user as Player;
        if (player == null)
            return false;

        var playerId = player.GetPlayerID();
        var pos = PieceAccessState.GetPosition(door);
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
            return false;

        if (!IsPermittedForPublicToggle(nview, pos, playerId))
            return false;

        if (PieceGuestAccess.IsOptInReady(nview) && !PieceAccessState.IsPublic(nview))
            return false;

        return TogglePublicAsPermitted(nview, user, playerId, isDoor: true);
    }

    public static bool IsPermittedForPublicToggle(ZNetView? nview, Vector3 pos, long playerId)
    {
        if (playerId == 0L)
            return false;

        if (WardAccess.HasWardAccessForPlayer(pos, playerId))
            return true;

        return PieceGuestAccess.IsGuest(nview, playerId);
    }

    private static bool TogglePublicAsPermitted(ZNetView nview, Humanoid user, long playerId, bool isDoor)
    {
        var nextPublic = !PieceAccessState.IsPublic(nview);
        if (isDoor)
            DoorAccessService.RequestSetPublic(nview, nextPublic, playerId);
        else
            ChestAccessService.RequestSetPublic(nview, nextPublic, playerId);

        AccessFeedback.Show(
            user,
            nextPublic ? LockSmithLocalization.MsgNowPublicToken : LockSmithLocalization.MsgNowPrivateToken);
        return true;
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

    /// <summary>Creator + key + SetupModifier: toggle opt-in ready on ward chest.</summary>
    public static bool TryHandleKeyAlt(Container container, Humanoid user)
    {
        if (!IsEligibleWardChest(container))
            return false;

        var pos = PieceAccessState.GetPosition(container);
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgNeedActiveWardToken);
            return true;
        }

        var nview = PieceAccessState.GetNetView(container);
        if (PieceAccessState.IsPublic(nview))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgPublicNoSetupToken);
            return true;
        }

        return TryCreatorToggleOptIn(
            nview,
            pos,
            user);
    }

    public static bool TryHandleKeyAlt(Door door, Humanoid user)
    {
        if (!IsEligibleWardDoor(door))
            return false;

        var pos = PieceAccessState.GetPosition(door);
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgNeedActiveWardToken);
            return true;
        }

        var nview = PieceAccessState.GetNetView(door);
        if (PieceAccessState.IsPublic(nview))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgPublicNoSetupToken);
            return true;
        }

        return TryCreatorToggleOptIn(
            nview,
            pos,
            user);
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
        var altUse = Localization.instance.Localize(
            "[<color=yellow><b>$KEY_AltPlace</b></color>+<color=yellow><b>$KEY_Use</b></color>]");
        var isPublic = PieceAccessState.IsPublic(nview);
        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));

        if (isPublic)
        {
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));
            sb.Append('\n').Append(useKey).Append(' ')
                .Append(Localization.instance.Localize("$piece_container_open"));
            if (LockSmithConfig.EnableGuestPublicToggle
                && WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
            {
                sb.Append('\n').Append(altUse).Append(' ')
                    .Append(LockSmithLocalization.T(LockSmithLocalization.HoverMakePrivateToken));
            }
            else if (LockSmithConfig.EnableGuestPublicToggle
                     && LockSmithConfig.RequireActiveWard
                     && !WardAccess.IsInsideEnabledWard(pos))
            {
                sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNeedActiveWardToken));
            }

            hoverText = sb.ToString();
            return true;
        }

        if (!isGuest)
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePrivateToken));

        // Always show Guests - [N]; names only when holding the key.
        var reveal = AccessHoverDisplay.CanRevealGuestNames(pos, isPieceCreator: false, nview);
        if (reveal)
            PieceGuestAccess.TryRefreshGuestNames(nview);
        var summary = PieceGuestAccess.FormatGuestSummary(nview, reveal, team: false);
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);

        sb.Append('\n').Append(useKey).Append(' ')
            .Append(Localization.instance.Localize("$piece_container_open"));

        if (LockSmithConfig.EnableGuestPublicToggle
            && !PieceGuestAccess.IsOptInReady(nview)
            && WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            sb.Append('\n').Append(altUse).Append(' ')
                .Append(LockSmithLocalization.T(LockSmithLocalization.HoverMakePublicToken));
        }
        else if (isGuest && PieceGuestAccess.IsOptInReady(nview))
        {
            sb.Append('\n').Append(altUse).Append(' ')
                .Append(LockSmithLocalization.T(LockSmithLocalization.HoverLeaveAccessToken));
        }
        else if (LockSmithConfig.EnableGuestPublicToggle
                 && !PieceGuestAccess.IsOptInReady(nview)
                 && LockSmithConfig.RequireActiveWard
                 && !WardAccess.IsInsideEnabledWard(pos))
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNeedActiveWardToken));
        }

        hoverText = sb.ToString();
        return true;
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
        var altUse = Localization.instance.Localize(
            "[<color=yellow><b>$KEY_AltPlace</b></color>+<color=yellow><b>$KEY_Use</b></color>]");
        var isPublic = PieceAccessState.IsPublic(nview);
        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(door));

        if (isPublic)
        {
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));
            sb.Append('\n').Append(useKey).Append(' ').Append(DoorUseAction(door));
            if (LockSmithConfig.EnableGuestPublicToggle
                && WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
            {
                sb.Append('\n').Append(altUse).Append(' ')
                    .Append(LockSmithLocalization.T(LockSmithLocalization.HoverMakePrivateToken));
            }
            else if (LockSmithConfig.EnableGuestPublicToggle
                     && LockSmithConfig.RequireActiveWard
                     && !WardAccess.IsInsideEnabledWard(pos))
            {
                sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNeedActiveWardToken));
            }

            hoverText = sb.ToString();
            return true;
        }

        if (!isGuest)
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePrivateToken));

        // Always show Guests - [N]; names only when holding the key.
        var reveal = AccessHoverDisplay.CanRevealGuestNames(pos, isPieceCreator: false, nview);
        if (reveal)
            PieceGuestAccess.TryRefreshGuestNames(nview);
        var summary = PieceGuestAccess.FormatGuestSummary(nview, reveal, team: false);
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);

        sb.Append('\n').Append(useKey).Append(' ').Append(DoorUseAction(door));

        if (LockSmithConfig.EnableGuestPublicToggle
            && !PieceGuestAccess.IsOptInReady(nview)
            && WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            sb.Append('\n').Append(altUse).Append(' ')
                .Append(LockSmithLocalization.T(LockSmithLocalization.HoverMakePublicToken));
        }
        else if (isGuest && PieceGuestAccess.IsOptInReady(nview))
        {
            sb.Append('\n').Append(altUse).Append(' ')
                .Append(LockSmithLocalization.T(LockSmithLocalization.HoverLeaveAccessToken));
        }
        else if (LockSmithConfig.EnableGuestPublicToggle
                 && !PieceGuestAccess.IsOptInReady(nview)
                 && LockSmithConfig.RequireActiveWard
                 && !WardAccess.IsInsideEnabledWard(pos))
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNeedActiveWardToken));
        }

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

    private static bool TryCreatorToggleOptIn(ZNetView? nview, Vector3 pos, Humanoid user)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var player = user as Player;
        if (player == null)
            return false;

        // Ward members (incl. creator) may open opt-in on ward pieces.
        if (!WardAccess.HasLocalWardAccess(pos))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDeniedToken);
            return true;
        }

        if (!PieceGuestAccess.TryRequestJoinToggle(nview, player.GetPlayerID(), out var next))
        {
            AccessFeedback.ShowRaw(user, "Join is still syncing — try again.");
            return true;
        }

        AccessFeedback.Show(
            user,
            next ? LockSmithLocalization.MsgOptInOpenedToken : LockSmithLocalization.MsgOptInClosedToken);
        return true;
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

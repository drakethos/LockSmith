using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Phase 3 team/private chests: Personal↔Team + ward-style opt-in for guests.
/// </summary>
public static class GroupChestService
{
    public static bool IsEligible(Container? container) =>
        LockSmithConfig.EnableGroupChests
        && LockSmithConfig.EnableManagedAccess
        && GroupAccessState.IsPrivateFamilyChest(container);

    public static void RegisterRpc(Container container)
    {
        if (!GroupAccessState.IsPrivateFamilyChest(container))
            return;

        var nview = GroupAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return;

        nview.Unregister(GameHookTargets.RpcSetGroupMode);
        nview.Register<int, long>(GameHookTargets.RpcSetGroupMode, (long sender, int flag, long playerId) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcSetGroupMode, sender, playerId))
                ApplySetTeamMode(nview, flag == 1, playerId);
        });

        PieceClearService.RegisterRpc(nview);
        PieceGuestAccess.RegisterRpcs(nview);
        GroupAccessState.EnsureTeamModeWhenPauseDisabled(nview);
        GroupAccessState.SyncPrivacyFromZdo(container);
    }

    /// <summary>No key: join when Join is open, or leave if already a guest.</summary>
    public static bool TryHandleOptInInteract(Container container, Humanoid user, bool hold, bool alt)
    {
        if (!LockSmithConfig.EnableGroupChests)
            return false;

        if (!GroupAccessState.IsPrivateFamilyChest(container))
            return false;

        var nview = GroupAccessState.GetNetView(container);
        if (!GroupAccessState.IsTeamMode(nview))
            return false;

        var player = user as Player;
        if (player != null && GroupAccessState.IsCreator(container, player.GetPlayerID()))
            return false;

        return PieceGuestAccess.TryHandleGuestSelfInteract(nview, user, hold, alt);
    }

    /// <summary>Team guests bypass ward flash/hover No access on private-family chests.</summary>
    public static bool ShouldBypassWard(Container container)
    {
        if (!LockSmithConfig.EnableGroupChests || !GroupAccessState.IsPrivateFamilyChest(container))
            return false;

        var nview = GroupAccessState.GetNetView(container);
        if (!GroupAccessState.IsTeamMode(nview))
            return false;

        var local = Player.m_localPlayer;
        if (local == null)
            return false;

        return GroupAccessState.HasTeamAccess(container, local.GetPlayerID());
    }

    /// <summary>
    /// Non-key hover for team private chests — replaces vanilla ward No access.
    /// </summary>
    public static bool TryBuildAccessHover(Container container, out string hoverText)
    {
        hoverText = string.Empty;

        if (!LockSmithConfig.EnableGroupChests || !GroupAccessState.IsPrivateFamilyChest(container))
            return false;

        var nview = GroupAccessState.GetNetView(container);
        if (!GroupAccessState.IsTeamMode(nview))
            return false;

        var local = Player.m_localPlayer;
        if (local == null)
            return false;

        var playerId = local.GetPlayerID();
        var isCreator = GroupAccessState.IsCreator(container, playerId);
        var hasAccess = GroupAccessState.HasTeamAccess(container, playerId);
        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));

        var pos = GroupAccessState.GetPosition(container);
        var reveal = AccessHoverDisplay.CanRevealGuestNames(pos, isCreator, nview);
        if (reveal)
            PieceGuestAccess.TryRefreshGuestNames(nview);

        // Simple mode (no key): no member line; the list lives in the Lock menu.
        var summary = reveal ? PieceGuestAccess.FormatGuestSummary(nview, revealNames: true, team: true) : string.Empty;
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);

        if (PieceGuestAccess.IsOptInReady(nview))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken));

        if (hasAccess)
        {
            sb.Append('\n').Append(useKey).Append(' ')
                .Append(Localization.instance.Localize("$piece_container_open"));
            var hint = PieceAccessMenu.NoKeyHint(container);
            if (hint.Length > 0)
                sb.Append('\n').Append(hint);
        }
        else if (PieceGuestAccess.IsOptInReady(nview))
        {
            sb.Append('\n').Append(AccessHoverDisplay.JoinHint());
        }
        else
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNoTeamAccessToken));
        }

        hoverText = sb.ToString();
        return true;
    }

    public static void RequestSetTeamMode(ZNetView nview, bool team, long playerId)
    {
        PieceRpc.Request(
            nview,
            GameHookTargets.RpcSetGroupMode,
            new object[] { team ? 1 : 0, playerId },
            view => ApplySetTeamMode(view, team, playerId),
            view => GroupAccessState.IsTeamMode(view) == team,
            onFailed: PieceGuestAccess.ShowSyncFailed);
    }

    public static void ApplySetTeamMode(ZNetView nview, bool team, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        if (!LockSmithConfig.EnableGroupChests || !LockSmithConfig.EnableManagedAccess)
            return;

        var container = nview.GetComponentInChildren<Container>();
        if (!GroupAccessState.IsPrivateFamilyChest(container))
            return;

        if (!GroupAccessState.IsCreator(container!, playerId))
        {
            LockSmith.Log?.LogWarning($"Rejected group mode toggle for player {playerId} (not creator).");
            return;
        }

        if (!team && !LockSmithConfig.EnablePersonalPause)
        {
            LockSmith.Log?.LogDebug("Rejected Personal mode — EnablePersonalPause is off.");
            GroupAccessState.EnsureTeamModeWhenPauseDisabled(nview);
            return;
        }

        GroupAccessState.SetTeamMode(nview, team);
        PieceAccessState.MarkManaged(nview);
        LockSmith.Log?.LogInfo(
            $"Set locksmith_group_mode={(team ? 1 : 0)} on {container!.name} " +
            $"(readback={GroupAccessState.IsTeamMode(nview)}).");
    }

    public static bool TryResolveCheckAccess(Container container, long playerId, out bool allowed)
    {
        allowed = false;

        if (!LockSmithConfig.EnableGroupChests)
            return false;

        if (!GroupAccessState.IsPrivateFamilyChest(container))
            return false;

        var nview = GroupAccessState.GetNetView(container);
        GroupAccessState.SyncPrivacyFromZdo(container);

        if (!GroupAccessState.IsTeamMode(nview))
            return false;

        allowed = GroupAccessState.HasTeamAccess(container, playerId);
        return true;
    }

    public static bool TryBuildKeyModeHover(Container container, out string hoverText)
    {
        hoverText = string.Empty;

        if (!IsEligible(container))
            return false;

        if (!ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
            return false;

        var nview = GroupAccessState.GetNetView(container);
        var team = GroupAccessState.IsTeamMode(nview);
        if (!LockSmithConfig.EnablePersonalPause)
        {
            GroupAccessState.EnsureTeamModeWhenPauseDisabled(nview);
            team = true;
        }

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));

        var local = Player.m_localPlayer;
        var isOwner = local != null && GroupAccessState.IsCreator(container, local.GetPlayerID());
        var pos = GroupAccessState.GetPosition(container);

        if (team)
        {
            var reveal = AccessHoverDisplay.CanRevealGuestNames(pos, isOwner, nview);
            if (reveal)
                PieceGuestAccess.TryRefreshGuestNames(nview);
            sb.Append('\n').Append(PieceGuestAccess.FormatGuestSummary(nview, reveal, team: true));
        }
        else
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePersonalToken));
            // Roster stays on the ZDO while personal — show it so owners know who comes back on Make team.
            if (isOwner && PieceGuestAccess.GetGuests(nview).Count > 0)
            {
                PieceGuestAccess.TryRefreshGuestNames(nview);
                sb.Append('\n').Append(PieceGuestAccess.FormatGuestSummary(nview, revealNames: true, team: true));
            }
        }

        if (team && PieceGuestAccess.IsOptInReady(nview))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken));

        if (isOwner || (team && local != null && PieceGuestAccess.IsGuest(nview, local.GetPlayerID())))
            sb.Append('\n').Append(AccessHoverDisplay.MenuHint(withKey: true));
        else
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgGroupOwnerOnlyToken));

        hoverText = sb.ToString();
        return true;
    }

    public static string GetTeamStatusSuffix(Container container)
    {
        // Access hover replaces the whole string now.
        return string.Empty;
    }
}

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
        && LockSmithConfig.EnableKeyMode
        && GroupAccessState.IsPrivateFamilyChest(container);

    public static void RegisterRpc(Container container)
    {
        if (!GroupAccessState.IsPrivateFamilyChest(container))
            return;

        var nview = GroupAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return;

        nview.Register<int, long>(GameHookTargets.RpcSetGroupMode, (long sender, int flag, long playerId) =>
        {
            ApplySetTeamMode(nview, flag == 1, playerId);
        });

        PieceClearService.RegisterRpc(nview);
        PieceGuestAccess.RegisterRpcs(nview);
        GroupAccessState.SyncPrivacyFromZdo(container);
    }

    /// <summary>
    /// Key: E toggles Personal↔Team; Alt+E toggles opt-in ready (when Team).
    /// </summary>
    public static bool TryHandleKeyInteract(Container container, Humanoid user, bool hold, bool alt)
    {
        if (!IsEligible(container))
            return false;

        if (hold)
            return true;

        if (PieceClearService.TryHandleKeyClear(container, user))
            return true;

        var player = user as Player;
        if (player == null)
            return true;

        var playerId = player.GetPlayerID();
        if (!GroupAccessState.IsCreator(container, playerId))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgGroupOwnerOnlyToken);
            return true;
        }

        var nview = GroupAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return true;

        if (LockSmithInput.IsSetupModifierHeld())
            TryToggleOptIn(user, nview, playerId);
        else
            TryToggleTeamMode(user, nview, playerId);

        return true;
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

        var summary = PieceGuestAccess.FormatGuestSummary(nview, reveal, team: true);
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);

        if (PieceGuestAccess.IsOptInReady(nview))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken));

        if (hasAccess)
        {
            sb.Append('\n').Append(useKey).Append(' ')
                .Append(Localization.instance.Localize("$piece_container_open"));
            if (!isCreator && PieceGuestAccess.IsGuest(nview, playerId))
            {
                var altUse = Localization.instance.Localize(
                    "[<color=yellow><b>$KEY_AltPlace</b></color>+<color=yellow><b>$KEY_Use</b></color>]");
                sb.Append('\n').Append(altUse).Append(' ')
                    .Append(LockSmithLocalization.T(LockSmithLocalization.HoverLeaveAccessToken));
            }
        }
        else if (PieceGuestAccess.IsOptInReady(nview))
        {
            sb.Append('\n').Append(useKey).Append(' ')
                .Append(LockSmithLocalization.T(LockSmithLocalization.HoverJoinAccessToken));
        }
        else
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNoTeamAccessToken));
        }

        hoverText = sb.ToString();
        return true;
    }

    private static void TryToggleTeamMode(Humanoid user, ZNetView nview, long playerId)
    {
        var nextTeam = !GroupAccessState.IsTeamMode(nview);
        RequestSetTeamMode(nview, nextTeam, playerId);

        if (nview.IsOwner() && GroupAccessState.IsTeamMode(nview) != nextTeam)
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDeniedToken);
            return;
        }

        AccessFeedback.Show(
            user,
            nextTeam ? LockSmithLocalization.MsgNowTeamToken : LockSmithLocalization.MsgNowPersonalToken);
    }

    private static void TryToggleOptIn(Humanoid user, ZNetView nview, long playerId)
    {
        if (!LockSmithConfig.EnableOptInAccess)
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDisabledToken);
            return;
        }

        if (!GroupAccessState.IsTeamMode(nview))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgGroupNeedTeamToken);
            return;
        }

        var next = !PieceGuestAccess.IsOptInReady(nview);
        PieceGuestAccess.RequestSetOptIn(nview, next, playerId);
        AccessFeedback.Show(
            user,
            next ? LockSmithLocalization.MsgOptInOpenedToken : LockSmithLocalization.MsgOptInClosedToken);
    }

    public static void RequestSetTeamMode(ZNetView nview, bool team, long playerId)
    {
        if (nview.IsOwner())
            ApplySetTeamMode(nview, team, playerId);
        else
            nview.InvokeRPC(GameHookTargets.RpcSetGroupMode, team ? 1 : 0, playerId);
    }

    public static void ApplySetTeamMode(ZNetView nview, bool team, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        if (!LockSmithConfig.EnableGroupChests || !LockSmithConfig.EnableKeyMode)
            return;

        var container = nview.GetComponentInChildren<Container>();
        if (!GroupAccessState.IsPrivateFamilyChest(container))
            return;

        if (!GroupAccessState.IsCreator(container!, playerId))
        {
            LockSmith.Log?.LogWarning($"Rejected group mode toggle for player {playerId} (not creator).");
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
        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");

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
        }

        if (isOwner)
        {
            var toggle = LockSmithLocalization.T(
                team
                    ? LockSmithLocalization.HoverMakePersonalToken
                    : LockSmithLocalization.HoverMakeTeamToken);
            sb.Append('\n').Append(useKey).Append(' ').Append(toggle);

            if (team)
            {
                // Join open/close only — guest list already in summary above.
                if (LockSmithConfig.EnableOptInAccess)
                {
                    var ready = PieceGuestAccess.IsOptInReady(nview);
                    if (ready)
                        sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken));

                    var setupUse = Localization.instance.Localize(
                        LockSmithInput.FormatModifierUse(LockSmithConfig.SetupModifier));
                    var action = LockSmithLocalization.T(
                        ready
                            ? LockSmithLocalization.HoverCloseOptInToken
                            : LockSmithLocalization.HoverOpenOptInToken);
                    sb.Append('\n').Append(setupUse).Append(' ').Append(action);
                }
            }
        }
        else if (team && local != null && PieceGuestAccess.IsGuest(nview, local.GetPlayerID()))
        {
            // Access count already shown.
        }
        else
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgGroupOwnerOnlyToken));
        }

        if (isOwner)
            PieceClearService.AppendClearHover(sb, nview);

        hoverText = sb.ToString();
        return true;
    }

    public static string GetTeamStatusSuffix(Container container)
    {
        // Access hover replaces the whole string now.
        return string.Empty;
    }
}

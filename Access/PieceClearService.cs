using System.Collections.Generic;
using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Key + configurable modifier+E removes LockSmith from a piece (back to vanilla).
/// If guests exist, first press warns; second press within a few seconds confirms.
/// </summary>
public static class PieceClearService
{
    private static readonly HashSet<ZDOID> RegisteredViews = new HashSet<ZDOID>();
    private static ZDOID _pendingClearId = ZDOID.None;
    private static float _pendingClearUntil;

    private const float ConfirmWindowSeconds = 5f;

    public static bool TryHandleKeyClear(Container container, Humanoid user)
    {
        if (!LockSmithInput.IsClearModifierHeld() || container == null)
            return false;

        if (!LockSmithConfig.EnableKeyMode)
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDisabledToken);
            return true;
        }

        var nview = PieceAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return true;

        if (!HasLockSmithData(nview))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgNothingToClearToken);
            return true;
        }

        var player = user as Player;
        if (player == null)
            return true;

        var playerId = player.GetPlayerID();
        if (!CanClear(container, playerId))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDeniedToken);
            return true;
        }

        return TryConfirmOrClear(nview, user, playerId);
    }

    public static bool TryHandleKeyClear(Door door, Humanoid user)
    {
        if (!LockSmithInput.IsClearModifierHeld() || door == null)
            return false;

        if (!LockSmithConfig.EnableKeyMode)
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDisabledToken);
            return true;
        }

        var nview = PieceAccessState.GetNetView(door);
        if (nview == null || !nview.IsValid())
            return true;

        if (!HasLockSmithData(nview))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgNothingToClearToken);
            return true;
        }

        var player = user as Player;
        if (player == null)
            return true;

        var playerId = player.GetPlayerID();
        if (!CanClear(door, playerId))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDeniedToken);
            return true;
        }

        return TryConfirmOrClear(nview, user, playerId);
    }

    public static void AppendClearHover(StringBuilder sb, ZNetView? nview)
    {
        if (sb == null || nview == null || !nview.IsValid() || !HasLockSmithData(nview))
            return;

        sb.Append('\n')
            .Append(Localization.instance.Localize(LockSmithInput.FormatModifierUse(LockSmithConfig.ClearModifier)))
            .Append(' ')
            .Append(LockSmithLocalization.T(LockSmithLocalization.HoverClearLockSmithToken));
    }

    public static void RegisterRpc(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null || !RegisteredViews.Add(zdo.m_uid))
            return;

        nview.Register<long>(GameHookTargets.RpcClearLockSmith, (long sender, long playerId) =>
        {
            ApplyClear(nview, playerId);
        });
    }

    public static void RequestClear(ZNetView nview, long playerId)
    {
        if (nview.IsOwner())
            ApplyClear(nview, playerId);
        else
            nview.InvokeRPC(GameHookTargets.RpcClearLockSmith, playerId);
    }

    public static void ApplyClear(ZNetView nview, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        if (playerId == 0L)
            return;

        var container = nview.GetComponentInChildren<Container>();
        if (container != null)
        {
            if (!CanClear(container, playerId))
            {
                LockSmith.Log?.LogWarning($"Rejected LockSmith clear for player {playerId} on {nview.name}.");
                return;
            }
        }
        else
        {
            var door = nview.GetComponentInChildren<Door>();
            if (door == null || !CanClear(door, playerId))
            {
                LockSmith.Log?.LogWarning($"Rejected LockSmith clear for player {playerId} on {nview.name}.");
                return;
            }
        }

        PieceAccessState.ClearLockSmithData(nview);
        LockSmith.Log?.LogInfo($"Cleared LockSmith data on {nview.name} by {playerId}.");
    }

    public static bool HasLockSmithData(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return false;

        if (zdo.GetInt(GameHookTargets.ZdoManagedFlag.GetStableHashCode(), 0) == 1)
            return true;
        if (PieceAccessState.IsPublic(nview))
            return true;
        if (GroupAccessState.IsTeamMode(nview))
            return true;
        if (PieceGuestAccess.IsOptInReady(nview))
            return true;
        return PieceGuestAccess.GetGuests(nview).Count > 0;
    }

    private static bool TryConfirmOrClear(ZNetView nview, Humanoid user, long playerId)
    {
        var zdo = nview.GetZDO();
        if (zdo == null)
            return true;

        var guestCount = PieceGuestAccess.GetGuests(nview).Count;
        var id = zdo.m_uid;

        if (guestCount > 0)
        {
            if (_pendingClearId == id && Time.time <= _pendingClearUntil)
            {
                _pendingClearId = ZDOID.None;
                _pendingClearUntil = 0f;
                RequestClear(nview, playerId);
                AccessFeedback.Show(user, LockSmithLocalization.MsgClearedToken);
                return true;
            }

            _pendingClearId = id;
            _pendingClearUntil = Time.time + ConfirmWindowSeconds;
            var mod = LockSmithInput.ModifierLabel(LockSmithConfig.ClearModifier);
            AccessFeedback.ShowRaw(
                user,
                string.Format(
                    LockSmithLocalization.T(LockSmithLocalization.MsgClearConfirmToken),
                    mod));
            return true;
        }

        _pendingClearId = ZDOID.None;
        RequestClear(nview, playerId);
        AccessFeedback.Show(user, LockSmithLocalization.MsgClearedToken);
        return true;
    }

    private static bool CanClear(Container container, long playerId)
    {
        if (GroupAccessState.IsPrivateFamilyChest(container))
            return GroupAccessState.IsCreator(container, playerId);

        if (!PieceAccessState.IsEligibleChest(container))
            return false;

        return WardAccess.HasWardAccessForPlayer(PieceAccessState.GetPosition(container), playerId);
    }

    private static bool CanClear(Door door, long playerId)
    {
        if (!PieceAccessState.IsEligibleDoor(door))
            return false;

        return WardAccess.HasWardAccessForPlayer(PieceAccessState.GetPosition(door), playerId);
    }
}

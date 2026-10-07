using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Removes LockSmith from a piece (back to vanilla). The Lock menu asks Yes/No first.
/// </summary>
public static class PieceClearService
{
    public static void RegisterRpc(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        PieceMenuLock.RegisterRpcs(nview);

        nview.Unregister(GameHookTargets.RpcClearLockSmith);
        nview.Register<long>(GameHookTargets.RpcClearLockSmith, (long sender, long playerId) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcClearLockSmith, sender, playerId))
                ApplyClear(nview, playerId);
        });
    }

    public static void RequestClear(ZNetView nview, long playerId)
    {
        PieceRpc.Request(
            nview,
            GameHookTargets.RpcClearLockSmith,
            new object[] { playerId },
            view => ApplyClear(view, playerId),
            view => !HasLockSmithData(view),
            onFailed: PieceGuestAccess.ShowSyncFailed);
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

    private static bool CanClear(Container container, long playerId)
    {
        if (GroupAccessState.IsPrivateFamilyChest(container))
            return GroupAccessState.IsCreator(container, playerId);

        if (!PieceAccessState.IsEligibleChest(container))
            return false;

        var pos = PieceAccessState.GetPosition(container);
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
            return false;

        return WardAccess.HasWardAccessForPlayer(pos, playerId);
    }

    private static bool CanClear(Door door, long playerId)
    {
        if (!PieceAccessState.IsEligibleDoor(door))
            return false;

        var pos = PieceAccessState.GetPosition(door);
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
            return false;

        return WardAccess.HasWardAccessForPlayer(pos, playerId);
    }
}

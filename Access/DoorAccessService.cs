using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>Phase 2 door/gate public/private logic. Patches call into here.</summary>
public static class DoorAccessService
{
    public static bool ShouldBypassWardCheck(Door door) =>
        LockSmithConfig.EnableDoors
        && PieceAccessState.IsEligibleDoor(door)
        && PieceAccessState.IsPublic(PieceAccessState.GetNetView(door));

    public static bool TryToggleDoor(Door door, Humanoid user)
    {
        if (!LockSmithConfig.EnableDoors || !LockSmithConfig.EnableKeyMode)
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDisabledToken);
            return true;
        }

        if (!PieceAccessState.IsEligibleDoor(door))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgWrongTargetToken);
            return true;
        }

        var player = user as Player;
        if (player == null)
            return true;

        var playerId = player.GetPlayerID();
        var pos = PieceAccessState.GetPosition(door);

        if (!WardAccess.HasLocalWardAccess(pos))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDeniedToken);
            return true;
        }

        var nview = PieceAccessState.GetNetView(door);
        if (nview == null || !nview.IsValid())
            return true;

        var nextPublic = !PieceAccessState.IsPublic(nview);
        RequestSetPublic(nview, nextPublic, playerId);

        if (nview.IsOwner())
        {
            var applied = PieceAccessState.IsPublic(nview);
            if (applied != nextPublic)
            {
                LockSmith.Log?.LogWarning(
                    $"LockSmith public flag did not stick on {door.name} (wanted {nextPublic}, got {applied}).");
                AccessFeedback.Show(user, LockSmithLocalization.MsgDeniedToken);
                return true;
            }
        }

        AccessFeedback.Show(
            user,
            nextPublic ? LockSmithLocalization.MsgNowPublicToken : LockSmithLocalization.MsgNowPrivateToken);
        return true;
    }

    public static void RequestSetPublic(ZNetView nview, bool isPublic, long playerId)
    {
        if (nview.IsOwner())
            ApplySetPublic(nview, isPublic, playerId);
        else
            nview.InvokeRPC(GameHookTargets.RpcSetDoorPublic, isPublic ? 1 : 0, playerId);
    }

    public static void RegisterRpc(Door door)
    {
        var nview = PieceAccessState.GetNetView(door);
        if (nview == null || !nview.IsValid())
            return;

        nview.Register<int, long>(GameHookTargets.RpcSetDoorPublic, (long sender, int flag, long playerId) =>
        {
            ApplySetPublic(nview, flag == 1, playerId);
        });

        PieceAccessState.SyncGuardStoneFromZdo(door);
    }

    public static void ApplySetPublic(ZNetView nview, bool isPublic, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        var door = nview.GetComponentInChildren<Door>();
        if (!PieceAccessState.IsEligibleDoor(door))
            return;

        if (!LockSmithConfig.EnableDoors || !LockSmithConfig.EnableKeyMode)
            return;

        var pos = PieceAccessState.GetPosition(door!);

        var local = Player.m_localPlayer;
        var allowed = local != null && local.GetPlayerID() == playerId
            ? WardAccess.HasLocalWardAccess(pos)
            : WardAccess.HasWardAccessForPlayer(pos, playerId);

        if (!allowed)
        {
            LockSmith.Log?.LogWarning($"Rejected door public toggle for player {playerId} (no ward access).");
            return;
        }

        PieceAccessState.SetPublic(nview, isPublic);
        LockSmith.Log?.LogInfo(
            $"Set locksmith_public={(isPublic ? 1 : 0)}, m_checkGuardStone={door!.m_checkGuardStone} " +
            $"on {door.name} (readback={PieceAccessState.IsPublic(nview)}).");
    }

    public static bool TryBuildKeyModeHover(Door door, out string hoverText)
    {
        hoverText = string.Empty;

        if (!LockSmithConfig.EnableDoors || !LockSmithConfig.EnableKeyMode)
            return false;

        if (!ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
            return false;

        if (!PieceAccessState.IsEligibleDoor(door))
        {
            hoverText = door.GetHoverName() + "\n" +
                        LockSmithLocalization.T(LockSmithLocalization.MsgWrongTargetToken);
            return true;
        }

        var pos = PieceAccessState.GetPosition(door);
        var nview = PieceAccessState.GetNetView(door);
        var isPublic = PieceAccessState.IsPublic(nview);
        var status = LockSmithLocalization.T(
            isPublic ? LockSmithLocalization.PiecePublicToken : LockSmithLocalization.PiecePrivateToken);
        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");

        var sb = new StringBuilder();
        sb.Append(door.GetHoverName());
        sb.Append('\n').Append(status);

        if (WardAccess.HasLocalWardAccess(pos))
        {
            var action = LockSmithLocalization.T(
                isPublic
                    ? LockSmithLocalization.HoverMakePrivateToken
                    : LockSmithLocalization.HoverMakePublicToken);
            sb.Append('\n').Append(useKey).Append(' ').Append(action);
        }
        else
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgDeniedToken));
        }

        hoverText = sb.ToString();
        return true;
    }

    public static string GetPublicStatusSuffix(Door door)
    {
        if (!LockSmithConfig.EnableDoors || !PieceAccessState.IsEligibleDoor(door))
            return string.Empty;

        if (!PieceAccessState.IsPublic(PieceAccessState.GetNetView(door)))
            return string.Empty;

        return "\n" + LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken);
    }
}

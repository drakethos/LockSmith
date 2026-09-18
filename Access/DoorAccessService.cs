using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>Phase 2 door/gate public/private logic. Patches call into here.</summary>
public static class DoorAccessService
{
    public static bool ShouldBypassWardCheck(Door door)
    {
        if (!LockSmithConfig.EnableDoors || !PieceAccessState.IsEligibleDoor(door))
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (PieceAccessState.IsPublic(nview))
            return true;

        return PieceGuestService.ShouldBypassWardForGuest(door);
    }

    public static bool TryKeyInteract(Door door, Humanoid user, bool alt)
    {
        if (!LockSmithConfig.EnableDoors || !LockSmithConfig.EnableKeyMode)
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDisabledToken);
            return true;
        }

        if (PublicPieceRegistration.IsPublicPiece(door))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgPublicPrefabToken);
            return true;
        }

        if (!PieceAccessState.IsEligibleDoor(door))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgWrongTargetToken);
            return true;
        }

        var pos = PieceAccessState.GetPosition(door);
        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgNeedActiveWardToken);
            return true;
        }

        if (PieceClearService.TryHandleKeyClear(door, user))
            return true;

        if (LockSmithInput.IsSetupModifierHeld())
            return PieceGuestService.TryHandleKeyAlt(door, user);

        return TryToggleDoor(door, user);
    }

    public static bool TryToggleDoor(Door door, Humanoid user)
    {
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

        if (PieceAccessState.NeedsDesignate(nview))
        {
            RequestSetPublic(nview, PieceAccessState.IsPublic(nview), playerId);
            AccessFeedback.Show(user, LockSmithLocalization.MsgManagedToken);
            return true;
        }

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

        PieceClearService.RegisterRpc(nview);
        PieceGuestService.RegisterRpc(door);
        PieceAccessState.SyncGuardStoneFromZdo(door);
    }

    public static void ApplySetPublic(ZNetView nview, bool isPublic, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        var door = nview.GetComponentInChildren<Door>();
        if (!PieceAccessState.IsEligibleDoor(door))
            return;

        if (!LockSmithConfig.EnableDoors)
            return;

        var pos = PieceAccessState.GetPosition(door!);

        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            LockSmith.Log?.LogWarning($"Rejected door public toggle for player {playerId} (no active ward).");
            return;
        }

        var local = Player.m_localPlayer;
        var allowed = local != null && local.GetPlayerID() == playerId
            ? WardAccess.HasLocalWardAccess(pos)
            : WardAccess.HasWardAccessForPlayer(pos, playerId);

        var guestToggle = !allowed
            && LockSmithConfig.EnableGuestPublicToggle
            && LockSmithConfig.EnablePieceGuests
            && PieceAccessState.IsManaged(nview)
            && PieceGuestAccess.IsGuest(nview, playerId);

        if (guestToggle)
            allowed = true;
        else if (!LockSmithConfig.EnableKeyMode)
            return;

        if (!allowed)
        {
            LockSmith.Log?.LogWarning($"Rejected door public toggle for player {playerId} (no ward/guest access).");
            return;
        }

        PieceAccessState.SetPublic(nview, isPublic);
        if (isPublic)
            PieceGuestAccess.SetOptInReady(nview, false);

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

        if (PublicPieceRegistration.IsPublicPiece(door))
        {
            hoverText = AccessHoverDisplay.LocalizedPieceName(door) + "\n" +
                        LockSmithLocalization.T(LockSmithLocalization.MsgPublicPrefabToken);
            return true;
        }

        if (!PieceAccessState.IsEligibleDoor(door))
        {
            hoverText = AccessHoverDisplay.LocalizedPieceName(door) + "\n" +
                        LockSmithLocalization.T(LockSmithLocalization.MsgWrongTargetToken);
            return true;
        }

        var pos = PieceAccessState.GetPosition(door);
        var nview = PieceAccessState.GetNetView(door);
        var managed = !PieceAccessState.NeedsDesignate(nview);
        var isPublic = PieceAccessState.IsPublic(nview);
        var status = LockSmithLocalization.T(
            isPublic ? LockSmithLocalization.PiecePublicToken : LockSmithLocalization.PiecePrivateToken);
        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(door));
        if (managed)
            sb.Append(' ').Append(status);
        else
            sb.Append(' ').Append(LockSmithLocalization.T(LockSmithLocalization.PieceUnmanagedToken));

        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.MsgNeedActiveWardToken));
            hoverText = sb.ToString();
            return true;
        }

        if (WardAccess.HasLocalWardAccess(pos))
        {
            if (!managed)
            {
                sb.Append('\n').Append(useKey).Append(' ')
                    .Append(LockSmithLocalization.T(LockSmithLocalization.HoverDesignateToken));
            }
            else
            {
                var action = LockSmithLocalization.T(
                    isPublic
                        ? LockSmithLocalization.HoverMakePrivateToken
                        : LockSmithLocalization.HoverMakePublicToken);
                sb.Append('\n').Append(useKey).Append(' ').Append(action);
                PieceGuestService.AppendKeyHoverExtras(sb, door);
            }

            PieceClearService.AppendClearHover(sb, nview);
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

        var sb = new StringBuilder();
        if (PieceAccessState.IsPublic(PieceAccessState.GetNetView(door)))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));

        sb.Append(PieceGuestService.GetOptInStatusSuffix(door));
        return sb.ToString();
    }
}

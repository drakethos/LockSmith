using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>Phase 2 door/gate public/private logic. Patches call into here.</summary>
public static class DoorAccessService
{
    public static bool ShouldBypassWardCheck(Door door)
    {
        if (!LockSmithConfig.EnableDoors)
            return false;

        // Hammer public clones are always open — no locksmith_public ZDO.
        if (PublicPieceRegistration.IsPublicPiece(door))
            return true;

        if (!PieceAccessState.IsEligibleDoor(door))
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (PieceAccessState.IsPublic(nview))
            return true;

        return PieceGuestService.ShouldBypassWardForGuest(door);
    }

    public static void RequestSetPublic(ZNetView nview, bool isPublic, long playerId)
    {
        PieceRpc.Request(
            nview,
            GameHookTargets.RpcSetDoorPublic,
            new object[] { isPublic ? 1 : 0, playerId },
            view => ApplySetPublic(view, isPublic, playerId),
            view => PieceAccessState.IsManaged(view) && PieceAccessState.IsPublic(view) == isPublic,
            onFailed: PieceGuestAccess.ShowSyncFailed);
    }

    public static void RegisterRpc(Door door)
    {
        var nview = PieceAccessState.GetNetView(door);
        if (nview == null || !nview.IsValid())
            return;

        nview.Unregister(GameHookTargets.RpcSetDoorPublic);
        nview.Register<int, long>(GameHookTargets.RpcSetDoorPublic, (long sender, int flag, long playerId) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcSetDoorPublic, sender, playerId))
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
        else if (!LockSmithConfig.EnableManagedAccess)
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

        if (!LockSmithConfig.EnableDoors || !LockSmithConfig.EnableManagedAccess)
            return false;

        if (!ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
            return false;

        if (PublicPieceRegistration.IsPublicPiece(door))
        {
            hoverText = AccessHoverDisplay.LocalizedPieceName(door) + "\n" +
                        AccessHoverDisplay.PublicPieceHoverLabel();
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
            if (managed)
                PieceGuestService.AppendKeyHoverExtras(sb, door);
            sb.Append('\n').Append(AccessHoverDisplay.MenuHint(withKey: true));
        }
        else if (PieceAccessMenu.HasNoKeyMenu(door))
        {
            // Not on the ward but Join is open / already a guest: the menu has something for them.
            sb.Append('\n').Append(AccessHoverDisplay.MenuHint(withKey: true));
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
        if (PublicPieceRegistration.IsPublicPiece(door))
            return "\n" + AccessHoverDisplay.PublicPieceHoverLabel();

        if (!LockSmithConfig.EnableDoors || !PieceAccessState.IsEligibleDoor(door))
            return string.Empty;

        var sb = new StringBuilder();
        if (PieceAccessState.IsPublic(PieceAccessState.GetNetView(door)))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));

        sb.Append(PieceGuestService.GetOptInStatusSuffix(door));
        return sb.ToString();
    }
}

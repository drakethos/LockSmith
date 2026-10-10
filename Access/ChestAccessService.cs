using System.Text;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>Phase 1 chest public/private logic. Patches call into here.</summary>
public static class ChestAccessService
{

    public static bool ShouldBypassWardCheck(Container container)
    {
        if (!LockSmithConfig.EnableChests)
            return false;

        // Hammer public clones are always open — no locksmith_public ZDO.
        if (PublicPieceRegistration.IsPublicPiece(container))
            return true;

        if (!PieceAccessState.IsEligibleChest(container))
            return false;

        var nview = PieceAccessState.GetNetView(container);
        if (PieceAccessState.IsPublic(nview))
            return true;

        return PieceGuestService.ShouldBypassWardForGuest(container);
    }






    public static void RequestSetPublic(ZNetView nview, bool isPublic, long playerId)
    {
        PieceRpc.Request(
            nview,
            GameHookTargets.RpcSetChestPublic,
            new object[] { isPublic ? 1 : 0, playerId },
            view => ApplySetPublic(view, isPublic, playerId),
            view => PieceAccessState.IsManaged(view) && PieceAccessState.IsPublic(view) == isPublic,
            onFailed: PieceGuestAccess.ShowSyncFailed);
    }

    public static void RegisterRpc(Container container)
    {
        var nview = PieceAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return;

        nview.Unregister(GameHookTargets.RpcSetChestPublic);
        nview.Register<int, long>(GameHookTargets.RpcSetChestPublic, (long sender, int flag, long playerId) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcSetChestPublic, sender, playerId))
                ApplySetPublic(nview, flag == 1, playerId);
        });

        PieceClearService.RegisterRpc(nview);

        // Prefab default may have ward check on; mirror ZDO onto this instance.
        PieceAccessState.SyncGuardStoneFromZdo(container);
        PieceGuestService.RegisterRpc(container);
    }

    public static void ApplySetPublic(ZNetView nview, bool isPublic, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        var container = nview.GetComponentInChildren<Container>();
        if (!PieceAccessState.IsEligibleChest(container))
            return;

        if (!LockSmithConfig.EnableChests)
            return;

        var pos = PieceAccessState.GetPosition(container!);

        if (!WardAccess.AllowsToolOnPiece(pos, isPrivateFamilyChest: false))
        {
            LockSmith.Log?.LogWarning($"Rejected public toggle for player {playerId} (no active ward).");
            return;
        }

        var local = Player.m_localPlayer;
        var allowed = local != null && local.GetPlayerID() == playerId
            ? WardAccess.HasLocalWardAccess(pos)
            : WardAccess.HasWardAccessForPlayer(pos, playerId);

        // Guests (or other permitted without ward) may unlock/lock on designated pieces.
        var guestToggle = !allowed
            && GuestPerms.HasPerm(nview, playerId, GuestPerm.Toggle)
            && LockSmithConfig.EnablePieceGuests
            && PieceAccessState.IsManaged(nview)
            && PieceGuestAccess.IsGuest(nview, playerId);

        if (guestToggle)
            allowed = true;
        else if (!LockSmithConfig.EnableManagedAccess)
            return;

        if (!allowed)
        {
            LockSmith.Log?.LogWarning($"Rejected public toggle for player {playerId} (no ward/guest access).");
            return;
        }

        PieceAccessState.SetPublic(nview, isPublic);
        if (isPublic)
            PieceGuestAccess.SetOptInReady(nview, false);

        LockSmith.Log?.LogInfo(
            $"Set locksmith_public={(isPublic ? 1 : 0)}, m_checkGuardStone={container!.m_checkGuardStone} " +
            $"on {container.name} (readback={PieceAccessState.IsPublic(nview)}).");
    }


    public static string GetPublicStatusSuffix(Container container)
    {
        if (PublicPieceRegistration.IsPublicPiece(container))
            return "\n" + AccessHoverDisplay.PublicPieceHoverLabel();

        if (!LockSmithConfig.EnableChests || !PieceAccessState.IsEligibleChest(container))
            return string.Empty;

        var sb = new StringBuilder();
        if (PieceAccessState.IsPublic(PieceAccessState.GetNetView(container)))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));

        sb.Append(PieceGuestService.GetOptInStatusSuffix(container));
        return sb.ToString();
    }
}

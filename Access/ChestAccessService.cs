using System.Reflection;
using System.Text;
using DrakeModsLibs.API;
using DrakeModsLibs.Data;
using HarmonyLib;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>Phase 1 chest public/private logic. Patches call into here.</summary>
public static class ChestAccessService
{
    private static FieldInfo? _rightItemField;
    private static FieldInfo? _leftItemField;
    private static bool _fieldsResolved;

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

    public static bool IsHoldingLocksmithKey(Humanoid? user) =>
        GetEquippedLocksmithKey(user) != null;

    /// <summary>
    /// Equipped key in hand. Do not use <see cref="Humanoid.GetCurrentWeapon"/> — it ignores Tools.
    /// Reads protected <c>m_rightItem</c> / <c>m_leftItem</c> via reflection.
    /// </summary>
    public static ItemDrop.ItemData? GetEquippedLocksmithKey(Humanoid? user)
    {
        // UseKey off: a key in hand is just a prop, so every key path (interact, hover, Ctrl+C/V) stays out.
        if (user == null || !LockSmithConfig.UseKey)
            return null;

        EnsureFields();

        try
        {
            var right = _rightItemField?.GetValue(user) as ItemDrop.ItemData;
            if (IsLocksmithKey(right))
                return right;

            var left = _leftItemField?.GetValue(user) as ItemDrop.ItemData;
            if (IsLocksmithKey(left))
                return left;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogDebug($"GetEquippedLocksmithKey failed: {ex.Message}");
        }

        return null;
    }

    private static void EnsureFields()
    {
        if (_fieldsResolved)
            return;

        _fieldsResolved = true;
        _rightItemField = AccessTools.Field(typeof(Humanoid), GameHookTargets.HumanoidRightItemField);
        _leftItemField = AccessTools.Field(typeof(Humanoid), GameHookTargets.HumanoidLeftItemField);
        if (_rightItemField == null || _leftItemField == null)
            LockSmith.Log?.LogError(
                $"LockSmith could not resolve Humanoid hand fields " +
                $"({GameHookTargets.HumanoidRightItemField}/{GameHookTargets.HumanoidLeftItemField}).");
    }

    public static bool IsLocksmithKey(ItemDrop.ItemData? item)
    {
        if (item?.m_shared == null)
            return false;

        var shared = item.m_shared.m_name ?? string.Empty;
        if (shared == "$" + LockSmithLocalization.KeyNameToken
            || shared == LockSmithLocalization.KeyNameToken)
            return true;

        if (item.m_dropPrefab != null && IsKeyPrefabName(item.m_dropPrefab.name))
            return true;

        return IsKeyPrefabName(shared);
    }

    /// <summary>
    /// Prefab / migrate stamp for the Locksmith key type (not per-player).
    /// Soft <c>NoRename</c> + <c>NoCraftedByEdit</c> (admin/VIP TagBypass may still see Rename’s
    /// inventory tab via Libs <c>IsRenameInventorySuppressed</c>); hard <c>HardNoDescription</c>
    /// (nobody edits description — does not hide the Rename tab). Relabel still uses
    /// <see cref="CustomizeLibsAPI.SetCustomName"/> directly.
    /// No-ops when tags are already correct.
    /// </summary>
    public static void EnsureRenameHandOff(ItemDrop.ItemData? item)
    {
        if (!IsLocksmithKey(item) || item == null)
            return;

        try
        {
            var hasSoftRename = CustomizeLibsAPI.HasTag(item, DrakeCustomDataKeys.NoRename);
            var hasSoftCrafted = CustomizeLibsAPI.HasTag(item, DrakeCustomDataKeys.NoCraftedByEdit);
            var hasHardDesc = CustomizeLibsAPI.HasTag(item, DrakeCustomDataKeys.HardNoDescription);
            var hasSoftDesc = CustomizeLibsAPI.HasTag(item, DrakeCustomDataKeys.NoDescription);
            if (hasSoftRename && hasSoftCrafted && hasHardDesc && !hasSoftDesc)
                return;

            CustomizeLibsAPI.SetTag(item, DrakeCustomDataKeys.NoRename);
            CustomizeLibsAPI.SetTag(item, DrakeCustomDataKeys.NoCraftedByEdit);
            CustomizeLibsAPI.HardBlockDescription(item);
            if (hasSoftDesc)
                CustomizeLibsAPI.ClearTag(item, DrakeCustomDataKeys.NoDescription);
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogDebug($"EnsureRenameHandOff failed: {ex.Message}");
        }
    }

    private static bool IsKeyPrefabName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (ContentRegistration.IsKeyPrefab(name))
            return true;

        var n = name!;
        return n.IndexOf("masterkey", System.StringComparison.OrdinalIgnoreCase) >= 0
               || n.IndexOf("locksmith", System.StringComparison.OrdinalIgnoreCase) >= 0;
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

    /// <summary>
    /// Full hover replacement while key is equipped. Caller must skip vanilla GetHoverText.
    /// </summary>
    public static bool TryBuildKeyModeHover(Container container, out string hoverText)
    {
        hoverText = string.Empty;

        if (!LockSmithConfig.EnableChests || !LockSmithConfig.EnableManagedAccess)
            return false;

        if (!IsHoldingLocksmithKey(Player.m_localPlayer))
            return false;

        if (PublicPieceRegistration.IsPublicPiece(container))
        {
            hoverText = AccessHoverDisplay.LocalizedPieceName(container) + "\n" +
                        AccessHoverDisplay.PublicPieceHoverLabel();
            return true;
        }

        if (!PieceAccessState.IsEligibleChest(container))
        {
            hoverText = AccessHoverDisplay.LocalizedPieceName(container) + "\n" +
                        LockSmithLocalization.T(LockSmithLocalization.MsgWrongTargetToken);
            return true;
        }

        var pos = PieceAccessState.GetPosition(container);
        var nview = PieceAccessState.GetNetView(container);
        var managed = !PieceAccessState.NeedsDesignate(nview);
        var isPublic = PieceAccessState.IsPublic(nview);
        var status = LockSmithLocalization.T(
            isPublic ? LockSmithLocalization.PiecePublicToken : LockSmithLocalization.PiecePrivateToken);

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));
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
                PieceGuestService.AppendKeyHoverExtras(sb, container);
            sb.Append('\n').Append(AccessHoverDisplay.MenuHint(withKey: true));
        }
        else if (PieceAccessMenu.HasNoKeyMenu(container))
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

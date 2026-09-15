using System.Reflection;
using System.Text;
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
        if (!LockSmithConfig.EnableChests || !PieceAccessState.IsEligibleChest(container))
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
        if (user == null)
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

    private static bool IsKeyPrefabName(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (ContentRegistration.IsKeyPrefab(name))
            return true;

        var n = name!;
        return n.IndexOf("MasterKey", System.StringComparison.OrdinalIgnoreCase) >= 0
               || n.IndexOf("PublicKey", System.StringComparison.OrdinalIgnoreCase) >= 0
               || n.IndexOf("locksmith", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    /// <summary>
    /// Key in hand path kept for call sites that want a single bool. Prefer Interact prefix
    /// which always consumes when the key is equipped.
    /// </summary>
    public static bool TryHandleChestInteractWithKey(Container container, Humanoid user, bool hold, bool alt)
    {
        if (!IsHoldingLocksmithKey(user))
            return false;

        if (!hold)
            TryKeyInteract(container, user, alt);

        return true;
    }

    public static bool TryKeyInteract(Container container, Humanoid user, bool alt)
    {
        if (!LockSmithConfig.EnableChests || !LockSmithConfig.EnableKeyMode)
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDisabledToken);
            return true;
        }

        if (!PieceAccessState.IsEligibleChest(container))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgWrongTargetToken);
            return true;
        }

        if (PieceClearService.TryHandleKeyClear(container, user))
            return true;

        if (LockSmithInput.IsSetupModifierHeld())
            return PieceGuestService.TryHandleKeyAlt(container, user);

        return TryToggleChest(container, user);
    }

    public static bool TryToggleChest(Container container, Humanoid user)
    {
        var player = user as Player;
        if (player == null)
            return true;

        var playerId = player.GetPlayerID();
        var pos = PieceAccessState.GetPosition(container);

        if (!WardAccess.HasLocalWardAccess(pos))
        {
            AccessFeedback.Show(user, LockSmithLocalization.MsgDeniedToken);
            return true;
        }

        var nview = PieceAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return true;

        // First key use designates the piece; later uses toggle public/private.
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
                    $"LockSmith public flag did not stick on {container.name} (wanted {nextPublic}, got {applied}).");
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
            nview.InvokeRPC(GameHookTargets.RpcSetChestPublic, isPublic ? 1 : 0, playerId);
    }

    public static void RegisterRpc(Container container)
    {
        var nview = PieceAccessState.GetNetView(container);
        if (nview == null || !nview.IsValid())
            return;

        nview.Register<int, long>(GameHookTargets.RpcSetChestPublic, (long sender, int flag, long playerId) =>
        {
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
        else if (!LockSmithConfig.EnableKeyMode)
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

        if (!LockSmithConfig.EnableChests || !LockSmithConfig.EnableKeyMode)
            return false;

        if (!IsHoldingLocksmithKey(Player.m_localPlayer))
            return false;

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
        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.LocalizedPieceName(container));
        if (managed)
            sb.Append('\n').Append(status);
        else
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PieceUnmanagedToken));

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
                PieceGuestService.AppendKeyHoverExtras(sb, container);
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

    public static string GetPublicStatusSuffix(Container container)
    {
        if (!LockSmithConfig.EnableChests || !PieceAccessState.IsEligibleChest(container))
            return string.Empty;

        var sb = new StringBuilder();
        if (PieceAccessState.IsPublic(PieceAccessState.GetNetView(container)))
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PiecePublicToken));

        sb.Append(PieceGuestService.GetOptInStatusSuffix(container));
        return sb.ToString();
    }
}

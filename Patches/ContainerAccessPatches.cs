using LockSmith.Access;
using HarmonyLib;

namespace LockSmith.Patches;

/// <summary>
/// Thin Container adapters. Logic lives in <see cref="ChestAccessService"/> /
/// <see cref="GroupChestService"/>.
/// </summary>
[HarmonyPatch(typeof(Container))]
public static class ContainerAccessPatches
{
    private static bool _loggedHoverFault;
    private static bool _loggedInteractFault;
    private static bool _loggedCheckAccessFault;

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.ContainerAwake)]
    private static void AwakePostfix(Container __instance)
    {
        try
        {
            ChestAccessService.RegisterRpc(__instance);
            GroupChestService.RegisterRpc(__instance);
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.Awake LockSmith RPC register failed: {ex}");
        }
    }

    /// <summary>
    /// Phase 3: finish vanilla Group privacy stub — creator + ZDO member list.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.ContainerCheckAccess)]
    private static bool CheckAccessPrefix(Container __instance, long playerID, ref bool __result)
    {
        try
        {
            if (!GroupChestService.TryResolveCheckAccess(__instance, playerID, out var allowed))
                return true;

            __result = allowed;
            return false;
        }
        catch (System.Exception ex)
        {
            if (!_loggedCheckAccessFault)
            {
                _loggedCheckAccessFault = true;
                LockSmith.Log?.LogError($"Container.CheckAccess LockSmith prefix failed: {ex}");
            }

            return true;
        }
    }

    /// <summary>
    /// Prefix hijack: key in hand → never run vanilla Open.
    /// Private-family → team UX; normal chests → ward public/private.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.ContainerInteract)]
    private static bool InteractPrefix(
        Container __instance,
        Humanoid character,
        bool hold,
        bool alt,
        ref bool __result,
        ref bool __state)
    {
        __state = false;
        try
        {
            PieceAccessState.SyncGuardStoneFromZdo(__instance);
            GroupAccessState.SyncPrivacyFromZdo(__instance);

            if (ChestAccessService.IsHoldingLocksmithKey(character))
            {
                try
                {
                    if (!hold)
                    {
                        if (PieceGuestService.TryHandleGuestKeyLeave(__instance, character))
                        {
                            __result = true;
                            return false;
                        }

                        // Guest with key: open normally (names/leave on hover).
                        if (PieceGuestService.ShouldGuestKeyFallThroughOpen(__instance, character))
                        {
                            if (ChestAccessService.ShouldBypassWardCheck(__instance)
                                || GroupChestService.ShouldBypassWard(__instance))
                            {
                                if (__instance.m_checkGuardStone)
                                {
                                    __instance.m_checkGuardStone = false;
                                    __state = true;
                                }
                            }

                            return true;
                        }

                        if (GroupAccessState.IsPrivateFamilyChest(__instance))
                            GroupChestService.TryHandleKeyInteract(__instance, character, hold, alt);
                        else
                            ChestAccessService.TryKeyInteract(__instance, character, alt);
                    }

                    __result = true;
                    return false;
                }
                catch (System.Exception ex)
                {
                    // Never swallow Use while broken — let vanilla run so place/open aren't soft-locked.
                    LockSmith.Log?.LogError($"LockSmith chest key interact failed: {ex}");
                    return true;
                }
            }

            // Guest unlock/lock for everyone (ward chests/doors only).
            if (PieceGuestService.TryHandleGuestPublicToggle(__instance, character, hold, alt))
            {
                __result = true;
                return false;
            }

            // Join (E) / Leave (Alt+E while Join open) without key.
            if (GroupChestService.TryHandleOptInInteract(__instance, character, hold, alt)
                || PieceGuestService.TryHandleOptInInteract(__instance, character, hold, alt))
            {
                __result = true;
                return false;
            }

            if (!ChestAccessService.ShouldBypassWardCheck(__instance)
                && !GroupChestService.ShouldBypassWard(__instance))
                return true;

            if (!__instance.m_checkGuardStone)
                return true;

            __instance.m_checkGuardStone = false;
            __state = true;
        }
        catch (System.Exception ex)
        {
            if (!_loggedInteractFault)
            {
                _loggedInteractFault = true;
                LockSmith.Log?.LogError($"Container.Interact LockSmith prefix failed: {ex}");
            }
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.ContainerInteract)]
    private static void InteractPostfix(Container __instance, bool __state)
    {
        if (!__state)
            return;

        try
        {
            if (!ChestAccessService.ShouldBypassWardCheck(__instance)
                && !GroupChestService.ShouldBypassWard(__instance))
                __instance.m_checkGuardStone = true;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.Interact LockSmith postfix failed: {ex}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.ContainerTakeAll)]
    private static void TakeAllPrefix(Container __instance, ref bool __state)
    {
        __state = false;
        try
        {
            if (!ChestAccessService.ShouldBypassWardCheck(__instance))
                return;

            if (!__instance.m_checkGuardStone)
                return;

            __instance.m_checkGuardStone = false;
            __state = true;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.TakeAll LockSmith prefix failed: {ex}");
        }
    }

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.ContainerTakeAll)]
    private static void TakeAllPostfix(Container __instance, bool __state)
    {
        if (!__state)
            return;

        try
        {
            if (!ChestAccessService.ShouldBypassWardCheck(__instance)
                && !GroupChestService.ShouldBypassWard(__instance))
                __instance.m_checkGuardStone = true;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.TakeAll LockSmith postfix failed: {ex}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.ContainerGetHoverText)]
    private static bool GetHoverTextPrefix(Container __instance, ref string __result, ref bool __state)
    {
        __state = false;
        try
        {
            PieceAccessState.SyncGuardStoneFromZdo(__instance);
            GroupAccessState.SyncPrivacyFromZdo(__instance);

            if (PieceGuestService.TryBuildGuestKeyHover(__instance, out var guestKeyHover))
            {
                __result = guestKeyHover;
                __state = true;
                return false;
            }

            if (GroupChestService.TryBuildKeyModeHover(__instance, out var groupHover))
            {
                __result = groupHover;
                __state = true;
                return false;
            }

            if (ChestAccessService.TryBuildKeyModeHover(__instance, out var hover))
            {
                __result = hover;
                __state = true;
                return false;
            }

            // Replace vanilla ward "No access" for team/guest players who can open.
            if (GroupChestService.TryBuildAccessHover(__instance, out var teamHover))
            {
                __result = teamHover;
                __state = true;
                return false;
            }

            if (PieceGuestService.TryBuildGuestAccessHover(__instance, out var guestHover))
            {
                __result = guestHover;
                __state = true;
                return false;
            }
        }
        catch (System.Exception ex)
        {
            if (!_loggedHoverFault)
            {
                _loggedHoverFault = true;
                LockSmith.Log?.LogError($"Container.GetHoverText LockSmith prefix failed: {ex}");
            }
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.ContainerGetHoverText)]
    private static void GetHoverTextPostfix(Container __instance, ref string __result, bool __state)
    {
        if (__state)
            return;

        try
        {
            if (ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
                return;

            var publicSuffix = ChestAccessService.GetPublicStatusSuffix(__instance);
            if (!string.IsNullOrEmpty(publicSuffix))
                __result += publicSuffix;

            var join = GetOptInJoinLine(__instance);
            if (!string.IsNullOrEmpty(join))
                __result += join;
        }
        catch (System.Exception ex)
        {
            if (!_loggedHoverFault)
            {
                _loggedHoverFault = true;
                LockSmith.Log?.LogError($"Container.GetHoverText LockSmith postfix failed: {ex}");
            }
        }
    }

    private static string GetOptInJoinLine(Container container)
    {
        if (!LockSmithConfig.EnableOptInAccess)
            return string.Empty;

        ZNetView? nview = null;
        if (GroupAccessState.IsPrivateFamilyChest(container))
        {
            nview = GroupAccessState.GetNetView(container);
            if (!GroupAccessState.IsTeamMode(nview))
                return string.Empty;
        }
        else if (PieceAccessState.IsEligibleChest(container))
        {
            nview = PieceAccessState.GetNetView(container);
        }

        if (!PieceGuestAccess.IsOptInReady(nview))
            return string.Empty;

        var local = Player.m_localPlayer;
        if (local == null)
            return string.Empty;

        var id = local.GetPlayerID();
        if (PieceGuestAccess.IsGuest(nview, id))
            return string.Empty;

        if (GroupAccessState.IsPrivateFamilyChest(container) && GroupAccessState.IsCreator(container, id))
            return string.Empty;

        var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");
        return "\n" + useKey + " " + LockSmithLocalization.T(LockSmithLocalization.HoverJoinAccessToken);
    }
}

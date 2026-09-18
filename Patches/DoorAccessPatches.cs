using LockSmith.Access;
using HarmonyLib;

namespace LockSmith.Patches;

/// <summary>Thin Door adapters. Logic lives in <see cref="DoorAccessService"/>.</summary>
[HarmonyPatch(typeof(Door))]
public static class DoorAccessPatches
{
    private static bool _loggedHoverFault;
    private static bool _loggedInteractFault;
    private static bool _loggedAwakeFault;

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.DoorAwake)]
    private static void AwakePostfix(Door __instance)
    {
        try
        {
            DoorAccessService.RegisterRpc(__instance);
        }
        catch (System.Exception ex)
        {
            if (_loggedAwakeFault)
                return;
            _loggedAwakeFault = true;
            LockSmith.Log?.LogError($"Door.Awake LockSmith RPC register failed: {ex}");
        }
    }

    /// <summary>
    /// Prefix hijack: key in hand → never run vanilla open. Toggle public/private instead.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.DoorInteract)]
    private static bool InteractPrefix(
        Door __instance,
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

                        if (PieceGuestService.ShouldGuestKeyFallThroughOpen(__instance, character))
                        {
                            if (DoorAccessService.ShouldBypassWardCheck(__instance)
                                && __instance.m_checkGuardStone)
                            {
                                __instance.m_checkGuardStone = false;
                                __state = true;
                            }

                            return true;
                        }

                        DoorAccessService.TryKeyInteract(__instance, character, alt);
                    }

                    __result = true;
                    return false;
                }
                catch (System.Exception ex)
                {
                    // Never swallow Use while broken — let vanilla run so place/open aren't soft-locked.
                    LockSmith.Log?.LogError($"LockSmith door toggle failed: {ex}");
                    return true;
                }
            }

            if (PieceGuestService.TryHandleGuestPublicToggle(__instance, character, hold, alt))
            {
                __result = true;
                return false;
            }

            if (PieceGuestService.TryHandleOptInInteract(__instance, character, hold, alt))
            {
                __result = true;
                return false;
            }

            if (!DoorAccessService.ShouldBypassWardCheck(__instance))
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
                LockSmith.Log?.LogError($"Door.Interact LockSmith prefix failed: {ex}");
            }
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.DoorInteract)]
    private static void InteractPostfix(Door __instance, bool __state)
    {
        if (!__state)
            return;

        try
        {
            // Only restore if ZDO still says private; public stays off.
            if (!DoorAccessService.ShouldBypassWardCheck(__instance))
                __instance.m_checkGuardStone = true;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Door.Interact LockSmith postfix failed: {ex}");
        }
    }

    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.DoorGetHoverText)]
    private static bool GetHoverTextPrefix(Door __instance, ref string __result, ref bool __state)
    {
        __state = false;
        try
        {
            PieceAccessState.SyncGuardStoneFromZdo(__instance);

            if (PieceGuestService.TryBuildGuestKeyHover(__instance, out var guestKeyHover))
            {
                __result = guestKeyHover;
                __state = true;
                return false;
            }

            if (DoorAccessService.TryBuildKeyModeHover(__instance, out var hover))
            {
                __result = hover;
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
            // Hover runs every frame — never spam.
            if (!_loggedHoverFault)
            {
                _loggedHoverFault = true;
                LockSmith.Log?.LogError($"Door.GetHoverText LockSmith prefix failed: {ex}");
            }
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.DoorGetHoverText)]
    private static void GetHoverTextPostfix(Door __instance, ref string __result, bool __state)
    {
        if (__state)
            return;

        try
        {
            if (ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
                return;

            var suffix = DoorAccessService.GetPublicStatusSuffix(__instance);
            if (!string.IsNullOrEmpty(suffix))
                __result += suffix;

            if (!LockSmithConfig.EnableOptInAccess)
                return;

            var nview = PieceAccessState.GetNetView(__instance);
            if (!PieceGuestAccess.IsOptInReady(nview))
                return;

            var local = Player.m_localPlayer;
            if (local == null || PieceGuestAccess.IsGuest(nview, local.GetPlayerID()))
                return;

            var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");
            __result += "\n" + useKey + " "
                        + LockSmithLocalization.T(LockSmithLocalization.HoverJoinAccessToken);
        }
        catch (System.Exception ex)
        {
            if (!_loggedHoverFault)
            {
                _loggedHoverFault = true;
                LockSmith.Log?.LogError($"Door.GetHoverText LockSmith postfix failed: {ex}");
            }
        }
    }
}

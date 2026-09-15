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
                        DoorAccessService.TryToggleDoor(__instance, character);
                }
                catch (System.Exception ex)
                {
                    LockSmith.Log?.LogError($"LockSmith door toggle failed: {ex}");
                }

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
    private static bool GetHoverTextPrefix(Door __instance, ref string __result)
    {
        try
        {
            PieceAccessState.SyncGuardStoneFromZdo(__instance);

            if (DoorAccessService.TryBuildKeyModeHover(__instance, out var hover))
            {
                __result = hover;
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
    private static void GetHoverTextPostfix(Door __instance, ref string __result)
    {
        try
        {
            if (ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
                return;

            var suffix = DoorAccessService.GetPublicStatusSuffix(__instance);
            if (!string.IsNullOrEmpty(suffix))
                __result += suffix;
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

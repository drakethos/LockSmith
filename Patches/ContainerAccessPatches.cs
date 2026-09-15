using LockSmith.Access;
using HarmonyLib;

namespace LockSmith.Patches;

/// <summary>Thin Container adapters. Logic lives in <see cref="ChestAccessService"/>.</summary>
[HarmonyPatch(typeof(Container))]
public static class ContainerAccessPatches
{
    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.ContainerAwake)]
    private static void AwakePostfix(Container __instance)
    {
        try
        {
            ChestAccessService.RegisterRpc(__instance);
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.Awake LockSmith RPC register failed: {ex}");
        }
    }

    /// <summary>
    /// Prefix hijack: key in hand → never run vanilla Open. Toggle public/private instead.
    /// Must not fall through to Open if toggle throws.
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.ContainerInteract)]
    private static bool InteractPrefix(
        Container __instance,
        Humanoid character,
        bool hold,
        ref bool __result,
        ref bool __state)
    {
        __state = false;
        try
        {
            // Keep instance ward flag aligned with replicated ZDO (peers).
            PieceAccessState.SyncGuardStoneFromZdo(__instance);

            if (ChestAccessService.IsHoldingLocksmithKey(character))
            {
                try
                {
                    if (!hold)
                        ChestAccessService.TryToggleChest(__instance, character);
                }
                catch (System.Exception ex)
                {
                    LockSmith.Log?.LogError($"LockSmith chest toggle failed: {ex}");
                }

                __result = true;
                return false;
            }

            // Public pieces already have m_checkGuardStone=false; no temp flip needed.
            // Keep a safety bypass if ZDO says public but component was not synced yet.
            if (!ChestAccessService.ShouldBypassWardCheck(__instance))
                return true;

            if (!__instance.m_checkGuardStone)
                return true;

            __instance.m_checkGuardStone = false;
            __state = true;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.Interact LockSmith prefix failed: {ex}");
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
            // Only restore if ZDO still says private; public stays off.
            if (!ChestAccessService.ShouldBypassWardCheck(__instance))
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
            if (!ChestAccessService.ShouldBypassWardCheck(__instance))
                __instance.m_checkGuardStone = true;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.TakeAll LockSmith postfix failed: {ex}");
        }
    }

    /// <summary>
    /// Prefix hijack: key in hand → skip vanilla hover entirely (no Open line).
    /// </summary>
    [HarmonyPrefix]
    [HarmonyPatch(GameHookTargets.ContainerGetHoverText)]
    private static bool GetHoverTextPrefix(Container __instance, ref string __result)
    {
        try
        {
            PieceAccessState.SyncGuardStoneFromZdo(__instance);

            if (ChestAccessService.TryBuildKeyModeHover(__instance, out var hover))
            {
                __result = hover;
                return false;
            }
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.GetHoverText LockSmith prefix failed: {ex}");
        }

        return true;
    }

    [HarmonyPostfix]
    [HarmonyPatch(GameHookTargets.ContainerGetHoverText)]
    private static void GetHoverTextPostfix(Container __instance, ref string __result)
    {
        try
        {
            // Key mode already replaced the whole string in the prefix.
            if (ChestAccessService.IsHoldingLocksmithKey(Player.m_localPlayer))
                return;

            var suffix = ChestAccessService.GetPublicStatusSuffix(__instance);
            if (!string.IsNullOrEmpty(suffix))
                __result += suffix;
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"Container.GetHoverText LockSmith postfix failed: {ex}");
        }
    }
}

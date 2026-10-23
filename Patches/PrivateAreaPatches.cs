using System;
using HarmonyLib;
using LockSmith.Access;

namespace LockSmith.Patches;

/// <summary>
/// Binds LockSmith's ward RPCs whenever a vanilla ward (PrivateArea) wakes up. ZNetView is recreated on chunk reload, so this runs each time.
/// </summary>
[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.Awake))]
internal static class PrivateAreaAwakePatch
{
    private static void Postfix(PrivateArea __instance)
    {
        try
        {
            WardService.RegisterRpcs(__instance.GetComponent<ZNetView>());
            TeamAccess.RegisterRpc(__instance.GetComponent<ZNetView>());
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Ward RPC bind failed: {ex}");
        }
    }
}

/// <summary>
/// Team access on a ward: the local player counts as permitted when the owner opened the ward to its team and they are on it.
/// HaveLocalAccess is what vanilla CheckAccess (building, doors, chests under the ward) asks for every ward covering a point.
/// </summary>
[HarmonyPatch(typeof(PrivateArea), "HaveLocalAccess")]
internal static class PrivateAreaTeamAccessPatch
{
    private static void Postfix(PrivateArea __instance, ref bool __result)
    {
        if (__result || !TeamAccess.Available)
            return;
        try
        {
            if (TeamAccess.HasLocalAccess(__instance.GetComponent<ZNetView>()))
                __result = true;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Ward team access: {ex.Message}");
        }
    }
}

[HarmonyPatch(typeof(PrivateArea), nameof(PrivateArea.GetHoverText))]
internal static class PrivateAreaTeamHoverPatch
{
    private static void Postfix(PrivateArea __instance, ref string __result)
    {
        try
        {
            __result += TeamAccess.HoverSuffix(__instance.GetComponent<ZNetView>());
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Ward team hover: {ex.Message}");
        }
    }
}

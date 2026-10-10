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
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Ward RPC bind failed: {ex}");
        }
    }
}

using HarmonyLib;

namespace LockSmith.Patches;

[HarmonyPatch(typeof(Player))]
public static class KeyPassClipboardPatch
{
    [HarmonyPatch(nameof(Player.Update))]
    [HarmonyPostfix]
    static void Postfix(Player __instance)
    {
        try
        {
            KeyPassClipboardInput.Tick(__instance);
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"KeyPassClipboardPatch failed: {ex}");
        }
    }
}

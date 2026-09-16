using HarmonyLib;
using UnityEngine;

namespace LockSmith.Patches;

/// <summary>
/// If a key ZDO fails to bind (ZNetView never claims m_initZDO), Valheim still returns a GO
/// and retries forever — boot loop. Destroy the orphan GO + ZDO so load can continue.
/// </summary>
[HarmonyPatch(typeof(ZNetScene), nameof(ZNetScene.CreateObject))]
internal static class ZNetSceneCreateObjectPatch
{
    private static void Postfix(ZDO zdo, GameObject __result)
    {
        if (zdo == null || !__result)
            return;

        // Only our key family — don't touch vanilla / other mods.
        if (!ContentRegistration.IsKeyPrefab(__result.name))
            return;

        if (zdo.Created)
            return;

        LockSmith.Log?.LogWarning(
            $"Orphan key ZDO {zdo.m_uid} failed to bind on '{__result.name}'; removing so world can load.");

        Object.Destroy(__result);

        try
        {
            if (ZDOMan.instance == null)
                return;

            zdo.SetOwner(ZDOMan.GetSessionID());
            ZDOMan.instance.DestroyZDO(zdo);
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogWarning($"Failed to destroy orphan key ZDO: {ex.Message}");
        }
    }
}

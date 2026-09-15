using System;
using HarmonyLib;
using LockSmith.Access;
using LockSmith.UI;

namespace LockSmith.DebugTemp;

/// <summary>
/// TEMPORARY — REMOVE BEFORE FINAL RELEASE.
/// Gives the Locksmith key on local player spawn when inventory has none,
/// so Phase 1 chest toggle can be tested without crafting at KeyMaker.
/// Delete this file and its Harmony patch when the user says to remove it.
/// </summary>
public static class TempGiveKeyOnLoad
{
    /// <summary>Flip to false or delete this class before shipping.</summary>
    public const bool Enabled = true;

    public const string RemoveBeforeReleaseTag = "REMOVE_BEFORE_RELEASE:TempGiveKeyOnLoad";

    private static bool _attemptedThisSession;

    public static void TryGiveToLocalPlayer(Player player)
    {
        if (!Enabled || player == null || Player.m_localPlayer != player)
            return;

        if (!LockSmithConfig.EnableKeyMode)
            return;

        // One attempt per process; inventory may still be syncing on first frame.
        if (_attemptedThisSession)
            return;

        _attemptedThisSession = true;

        try
        {
            if (PlayerAlreadyHasKey(player))
            {
                LockSmith.Log?.LogInfo($"[{RemoveBeforeReleaseTag}] Local player already has Locksmith key.");
                return;
            }

            var prefabName = ContentRegistration.RegisteredKeyPrefab;
            if (string.IsNullOrEmpty(prefabName))
            {
                LockSmith.Log?.LogWarning($"[{RemoveBeforeReleaseTag}] Key prefab not registered yet; cannot give.");
                _attemptedThisSession = false;
                return;
            }

            var inv = player.GetInventory();
            if (inv == null)
            {
                _attemptedThisSession = false;
                return;
            }

            var prefab = ObjectDB.instance.GetItemPrefab(prefabName);
            if (prefab == null)
            {
                LockSmith.Log?.LogWarning($"[{RemoveBeforeReleaseTag}] ObjectDB missing '{prefabName}'.");
                _attemptedThisSession = false;
                return;
            }

            if (!inv.AddItem(prefab, 1))
            {
                LockSmith.Log?.LogWarning($"[{RemoveBeforeReleaseTag}] AddItem('{prefabName}') failed.");
                _attemptedThisSession = false;
                return;
            }

            // Find the just-added stack for pickup UX.
            ItemDrop.ItemData? added = null;
            foreach (var item in inv.GetAllItems())
            {
                if (ChestAccessService.IsLocksmithKey(item))
                {
                    added = item;
                    break;
                }
            }

            if (added != null)
                player.ShowPickupMessage(added, 1);

            AccessFeedback.Show(player, LockSmithLocalization.MsgTempKeyGivenToken);
            LockSmith.Log?.LogWarning(
                $"[{RemoveBeforeReleaseTag}] Gave '{prefabName}' to local player for testing. Remove before release.");
        }
        catch (Exception ex)
        {
            _attemptedThisSession = false;
            LockSmith.Log?.LogError($"[{RemoveBeforeReleaseTag}] Give key failed: {ex}");
        }
    }

    private static bool PlayerAlreadyHasKey(Player player)
    {
        var inv = player.GetInventory();
        if (inv == null)
            return false;

        foreach (var item in inv.GetAllItems())
        {
            if (ChestAccessService.IsLocksmithKey(item))
                return true;
        }

        return false;
    }
}

/// <summary>TEMPORARY — REMOVE BEFORE FINAL RELEASE (with <see cref="TempGiveKeyOnLoad"/>).</summary>
[HarmonyPatch(typeof(Player), nameof(Player.OnSpawned))]
internal static class TempGiveKeyOnLoad_PlayerOnSpawned
{
    private static void Postfix(Player __instance)
    {
        try
        {
            TempGiveKeyOnLoad.TryGiveToLocalPlayer(__instance);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"[{TempGiveKeyOnLoad.RemoveBeforeReleaseTag}] OnSpawned patch failed: {ex}");
        }
    }
}

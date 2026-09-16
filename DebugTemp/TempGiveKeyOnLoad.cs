using System;
using HarmonyLib;
using LockSmith.UI;

namespace LockSmith.DebugTemp;

/// <summary>
/// TEMPORARY — REMOVE BEFORE FINAL RELEASE.
/// Gives the official Locksmith key (CryptKey + skull) on local player spawn.
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

        if (_attemptedThisSession)
            return;

        _attemptedThisSession = true;

        try
        {
            var inv = player.GetInventory();
            if (inv == null)
            {
                _attemptedThisSession = false;
                return;
            }

            var prefabName = ContentRegistration.RegisteredKeyPrefab;
            if (string.IsNullOrEmpty(prefabName))
            {
                LockSmith.Log?.LogWarning($"[{RemoveBeforeReleaseTag}] Official key not registered yet; cannot give.");
                _attemptedThisSession = false;
                return;
            }

            if (ObjectDB.instance == null)
            {
                _attemptedThisSession = false;
                return;
            }

            var prefab = ObjectDB.instance.GetItemPrefab(prefabName!);
            if (prefab == null)
            {
                LockSmith.Log?.LogWarning($"[{RemoveBeforeReleaseTag}] ObjectDB missing '{prefabName}'.");
                _attemptedThisSession = false;
                return;
            }

            // Drop stale clones (LeatherScraps art, CryptKey swamp key, etc.).
            var removed = RemoveAllPrefab(inv, prefabName!);
            removed += RemoveAllPrefab(inv, "masterkey");
            removed += RemoveAllPrefab(inv, "MasterKey");
            removed += RemoveAllPrefab(inv, "LockSmithKey");
            if (removed > 0)
                LockSmith.Log?.LogInfo($"[{RemoveBeforeReleaseTag}] Removed {removed} stale key item(s).");

            if (!inv.AddItem(prefab, 1))
            {
                LockSmith.Log?.LogWarning($"[{RemoveBeforeReleaseTag}] AddItem('{prefabName}') failed.");
                _attemptedThisSession = false;
                return;
            }

            var added = FindInventoryItem(inv, prefabName!);
            if (added != null)
                player.ShowPickupMessage(added, 1);

            AccessFeedback.Show(player, LockSmithLocalization.MsgTempKeyGivenToken);
            LockSmith.Log?.LogWarning(
                $"[{RemoveBeforeReleaseTag}] Gave '{prefabName}' (keys.bundle MasterKey) for testing. Remove before release.");
        }
        catch (Exception ex)
        {
            _attemptedThisSession = false;
            LockSmith.Log?.LogError($"[{RemoveBeforeReleaseTag}] Give key failed: {ex}");
        }
    }

    private static int RemoveAllPrefab(Inventory inv, string prefabName)
    {
        var doomed = new System.Collections.Generic.List<ItemDrop.ItemData>();
        foreach (var item in inv.GetAllItems())
        {
            if (item?.m_dropPrefab == null)
                continue;
            if (item.m_dropPrefab.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase)
                || item.m_dropPrefab.name.StartsWith(prefabName + "(", StringComparison.OrdinalIgnoreCase))
                doomed.Add(item);
        }

        foreach (var item in doomed)
            inv.RemoveItem(item);

        return doomed.Count;
    }

    private static ItemDrop.ItemData? FindInventoryItem(Inventory inv, string prefabName)
    {
        foreach (var item in inv.GetAllItems())
        {
            if (item?.m_dropPrefab == null)
                continue;
            if (item.m_dropPrefab.name.Equals(prefabName, StringComparison.OrdinalIgnoreCase)
                || item.m_dropPrefab.name.StartsWith(prefabName + "(", StringComparison.OrdinalIgnoreCase))
                return item;
        }

        return null;
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

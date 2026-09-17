using HarmonyLib;
using LockSmith.Access;

namespace LockSmith.Patches;

/// <summary>
/// One-shot migrate for Locksmith keys that predate the prefab hard-desc stamp.
/// Source of truth is the masterkey prefab; this only fills missing tags (no-op when correct).
/// </summary>
[HarmonyPatch]
public static class LocksmithKeyRenameHandOffPatch
{
    [HarmonyPatch(typeof(Inventory), nameof(Inventory.AddItem), typeof(ItemDrop.ItemData))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void AfterAddItemData(ItemDrop.ItemData? item, bool __result)
    {
        if (!__result || item == null)
            return;
        ChestAccessService.EnsureRenameHandOff(item);
    }

    /// <summary>Migrate old backpack keys once when inventory opens (early-out if already stamped).</summary>
    [HarmonyPatch(typeof(InventoryGui), nameof(InventoryGui.Show))]
    [HarmonyPostfix]
    [HarmonyPriority(Priority.Last)]
    static void AfterInventoryShow(InventoryGui __instance)
    {
        try
        {
            var player = Player.m_localPlayer;
            if (!player)
                return;
            var inv = player.GetInventory();
            if (inv == null)
                return;

            foreach (var item in inv.GetAllItems())
            {
                if (ChestAccessService.IsLocksmithKey(item))
                    ChestAccessService.EnsureRenameHandOff(item);
            }
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogDebug($"Rename hand-off migrate on inventory show failed: {ex.Message}");
        }
    }
}

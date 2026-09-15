using System;
using System.Collections.Generic;
using System.Globalization;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace LockSmith;

/// <summary>Registers KeyMaker + one key from the drake bundle. Unrelated prefabs stay unloaded.</summary>
public static class ContentRegistration
{
    public const string KeyMakerPrefab = "KeyMaker";
    public const string PreferredKeyPrefab = "MasterKey";
    public const string FallbackKeyPrefab = "PublicKey";

    private static string? _registeredKeyPrefab;

    public static string? RegisteredKeyPrefab => _registeredKeyPrefab;

    public static bool IsKeyPrefab(string? name)
    {
        if (string.IsNullOrEmpty(name) || _registeredKeyPrefab == null)
            return false;

        var key = _registeredKeyPrefab;
        return name!.Equals(key, StringComparison.OrdinalIgnoreCase)
               || name.StartsWith(key + "(", StringComparison.OrdinalIgnoreCase);
    }

    public static void RegisterFromDrakeBundle(AssetBundle? bundle)
    {
        if (bundle == null)
        {
            LockSmith.Log?.LogError("Drake bundle is null; KeyMaker and key were not registered.");
            return;
        }

        RegisterKeyMaker(bundle);
        RegisterKey(bundle);
    }

    private static void RegisterKeyMaker(AssetBundle bundle)
    {
        try
        {
            var prefab = bundle.LoadAsset<GameObject>(KeyMakerPrefab);
            if (prefab == null)
            {
                LockSmith.Log?.LogError($"Failed to load prefab '{KeyMakerPrefab}' from drake bundle.");
                return;
            }

            var piece = new CustomPiece(prefab, fixReference: true, new PieceConfig
            {
                Name = "Key Maker",
                Description = "A station for crafting Locksmith keys.",
                PieceTable = "Hammer",
                Category = "Crafting",
                Enabled = true,
                Requirements = new[]
                {
                    new RequirementConfig("Wood", 10, 0, true),
                    new RequirementConfig("Stone", 5, 0, true),
                },
            });

            PieceManager.Instance.AddPiece(piece);
            LockSmith.Log?.LogInfo("Registered KeyMaker piece.");
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Failed to register KeyMaker: {ex}");
        }
    }

    private static void RegisterKey(AssetBundle bundle)
    {
        try
        {
            var prefabName = PreferredKeyPrefab;
            var prefab = bundle.LoadAsset<GameObject>(prefabName);
            if (prefab == null)
            {
                prefabName = FallbackKeyPrefab;
                prefab = bundle.LoadAsset<GameObject>(prefabName);
            }

            if (prefab == null)
            {
                LockSmith.Log?.LogError(
                    $"Failed to load key prefab '{PreferredKeyPrefab}' or '{FallbackKeyPrefab}' from drake bundle.");
                return;
            }

            var itemConfig = new ItemConfig
            {
                Name = "$" + LockSmithLocalization.KeyNameToken,
                Description = "$" + LockSmithLocalization.KeyDescToken,
                Enabled = LockSmithConfig.EnableKeyMode,
                Amount = 1,
                CraftingStation = string.IsNullOrWhiteSpace(LockSmithConfig.KeyCraftingStation)
                    ? KeyMakerPrefab
                    : LockSmithConfig.KeyCraftingStation.Trim(),
            };

            foreach (var (reqPrefab, amount) in ParseMaterials(LockSmithConfig.KeyMaterials))
                itemConfig.AddRequirement(reqPrefab, amount, 0);

            var item = new CustomItem(prefab, fixReference: true, itemConfig);
            ItemManager.Instance.AddItem(item);
            _registeredKeyPrefab = prefab.name;
            SanitizeKeyItem(item);
            LockSmith.Log?.LogInfo($"Registered key item from prefab '{_registeredKeyPrefab}'.");
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Failed to register key item: {ex}");
        }
    }

    /// <summary>
    /// Make the key a hotbar Tool so it can sit in hand and be used on chests (not a Misc "use on what").
    /// </summary>
    private static void SanitizeKeyItem(CustomItem item)
    {
        var drop = item.ItemDrop;
        if (drop?.m_itemData?.m_shared == null)
            return;

        var shared = drop.m_itemData.m_shared;
        shared.m_name = "$" + LockSmithLocalization.KeyNameToken;
        shared.m_description = "$" + LockSmithLocalization.KeyDescToken;
        shared.m_itemType = ItemDrop.ItemData.ItemType.Tool;
        shared.m_maxStackSize = 1;
        shared.m_variants = 1;
        // Avoid accidental combat swings while holding the key.
        shared.m_attack = new Attack();
        shared.m_secondaryAttack = new Attack();
        shared.m_useDurability = false;

        if (drop.m_itemData.m_dropPrefab == null && item.ItemPrefab != null)
            drop.m_itemData.m_dropPrefab = item.ItemPrefab;
    }

    internal static List<(string PrefabName, int Amount)> ParseMaterials(string? raw)
    {
        var list = new List<(string PrefabName, int Amount)>();
        if (string.IsNullOrWhiteSpace(raw))
            return list;

        var costRaw = raw!;
        foreach (var segment in costRaw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var s = segment.Trim();
            var idx = s.LastIndexOf(':');
            if (idx <= 0 || idx >= s.Length - 1)
            {
                LockSmith.Log?.LogWarning($"Ignoring invalid key material segment: \"{s}\"");
                continue;
            }

            var namePart = s.Substring(0, idx).Trim();
            var amtPart = s.Substring(idx + 1).Trim();
            if (!int.TryParse(amtPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                LockSmith.Log?.LogWarning($"Ignoring invalid key material amount: \"{s}\"");
                continue;
            }

            list.Add((namePart, amount));
        }

        return list;
    }
}

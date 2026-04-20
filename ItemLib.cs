using System.Collections.Generic;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;

namespace DrakeLabs
{
    public class ItemLib
    {
        private ManualLogSource Log => Plugin.Log;

        // ------------------------------------------------------------------
        // Item registration
        // ------------------------------------------------------------------

        private void makeItem(string name, ItemConfig itemConfig, string prefab)
        {
            makeItem(name, itemConfig.Name, itemConfig.Description, prefab,
                new List<RequirementConfig>(itemConfig.Requirements), itemConfig.CraftingStation);
        }

        private void makeItem(ItemConfig itemConfig, GameObject prefab)
        {
            CustomItem customItem = new CustomItem(prefab, true, itemConfig);
            ItemManager.Instance.AddItem(customItem);
        }

        private void makeItem(string name, string gameName, string description, string prefab,
            List<RequirementConfig> requirements, string craftingStation = "piece_workbench")
        {
            ItemConfig itemConfig = new ItemConfig();
            itemConfig.Name = gameName;
            itemConfig.Description = description;
            itemConfig.CraftingStation = craftingStation;
            foreach (var requirement in requirements)
            {
                itemConfig.AddRequirement(requirement);
            }

            CustomItem customItem = new CustomItem(name, prefab, itemConfig);
            ItemManager.Instance.AddItem(customItem);
        }

        // ------------------------------------------------------------------
        // Ward Key item
        // Carrying this item grants access to any private chest/door on a ward
        // even when the player is not on the ward's permission list.
        // ------------------------------------------------------------------

        public void AddKeyItem()
        {
            ItemConfig keyConfig = new ItemConfig
            {
                Name        = "$dl_ward_key_name",
                Description = "$dl_ward_key_desc",
                CraftingStation = "piece_workbench",
                RepairStation   = "piece_workbench",
                MaxStackSize = 1
            };
            keyConfig.AddRequirement(new RequirementConfig("Wood",      2));
            keyConfig.AddRequirement(new RequirementConfig("Iron",      1));
            keyConfig.AddRequirement(new RequirementConfig("SurtlingCore", 1));

            // Clone from the Surtling Core icon/shape – close enough for a key item.
            // Replace "SurtlingCore" with a dedicated prefab when artwork is ready.
            CustomItem keyItem = new CustomItem(AccessControl.KeyItemName, "SurtlingCore", keyConfig);
            ItemManager.Instance.AddItem(keyItem);
            Log.LogInfo("Ward Key item registered.");
        }

        // ------------------------------------------------------------------
        // Piece registration (public variants)
        // ------------------------------------------------------------------

        private void makePiece(string name, string gameName, string prefab)
        {
            PieceConfig pieceConfig = new PieceConfig
            {
                PieceTable = "Hammer",
                Category   = "Misc",
                Enabled    = true,
                Name       = gameName
            };
            CustomPiece customPiece = new CustomPiece(name, prefab, pieceConfig);

            Door door = customPiece.Piece.GetComponentInChildren<Door>();
            if (door != null)
            {
                Log.LogDebug($"Registering public Door piece: {customPiece.Piece.name}");
                door.m_checkGuardStone = false;
            }

            Container container = customPiece.Piece.GetComponentInChildren<Container>();
            if (container != null)
            {
                Log.LogDebug($"Registering public Container piece: {customPiece.Piece.name}");
                container.m_checkGuardStone = false;
            }

            PieceManager.Instance.AddPiece(customPiece);
        }

        public void addPublicPieces()
        {
            makePiece("piece_chest_wood_public", "Chest (Public)",            "piece_chest_wood");
            makePiece("piece_chest_public",      "Reinforced Chest (Public)", "piece_chest");
            makePiece("wood_door_public",         "Wood Door (Public)",        "wood_door");
            makePiece("wood_gate_public",         "Wood Gate (Public)",        "wood_gate");
        }
    }
}

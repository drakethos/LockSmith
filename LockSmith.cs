using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn;
using Jotunn.Configs;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using Logger = BepInEx.Logging.Logger;
using Paths = BepInEx.Paths;

namespace LockSmith
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class LockSmith : BaseUnityPlugin
    {
        public const string CompanyName = "DrakeMods";
        public const string ModName = "Locksmith";
        public const string Version = "0.0.1";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public ConfigEntry<string> PublicPiecesConfig; // Config entry for public pieces list
        public static readonly char[] ConfigSeparator = { ',' }; // Separator for config entries
        public static AssetBundle box;


        private readonly Harmony harmony = new Harmony("drakesmod.LockSmith");

        private readonly ItemLib _itemLib;

        public const string PublicKey = "Public Key";
        public const string PrivateChest = "piece_chest_private(Clone)";
        public const string PersonalKey = "Personal Key";

        public LockSmith()
        {
            _itemLib = new ItemLib();
        }

        public ItemLib ItemLib
        {
            get { return _itemLib; }
        }

        private void Awake()
        {
            addBundles();
            PublicPiecesConfig = Config.Bind(
                "General",
                "PublicPieces",
                "piece_chest_wood:Chest (public),piece_chest:Reinforced Chest (public),wood_door:Wood Door (public),wood_gate:Wood Gate (public)",
                new ConfigDescription(
                    "List of items to make public. Format: original_name:display_name, separated by commas.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true })
            );
            PrefabManager.OnVanillaPrefabsAvailable += addWall;
            PrefabManager.OnVanillaPrefabsAvailable += addBronze;
            PrefabManager.OnVanillaPrefabsAvailable += addBar;
            PrefabManager.OnVanillaPrefabsAvailable += addBox;
            //Localizations();
            //     PrefabManager.OnVanillaPrefabsAvailable += ItemLib.makeKeyItems;
            PrefabManager.OnVanillaPrefabsAvailable += ItemLib.addPublicPieces;
            PrefabManager.OnVanillaPrefabsAvailable += ItemLib.recolor;
            harmony.PatchAll();
        }

        private void addBundles()
        {
            string folder = "DrakeMod-LockSmith";
            string assetBundlePath = "DrakeMod-LockSmith/Assets/drake";
            // JotunnPatches.bandage = AssetUtils.LoadAssetBundle("1010101110-vrp/Assets/bandage");

            Debug.Log($"Loading Assets skippity do ${assetBundlePath} ");
            box = AssetUtils.LoadAssetBundle(assetBundlePath);
            if (box == null)
            {
                Logger.LogError($"Failed to load AssetBundle from {assetBundlePath}");
            }
        }

        private void addBar()
        {
            try
            {
                GameObject flamebar = LoadPrefab("Flamebar", box);
                Debug.Log("Attempting to add assets");
                if (flamebar != null)
                {
                    ItemManager.Instance.AddItem(new CustomItem(box, "Flamebar", true, new ItemConfig
                    {
                        Name = "Flamebar",
                        Description = "Its flamy",
                        // CraftingStation = CraftingStations.Workbench,
                        //    Requirements = new[] { new RequirementConfig("Stone", 1) }
                    }));
                }
            }

            catch (Exception ex)
            {
                Debug.LogError("Unable to load firemetal");
            }
        }

        private void addWall()
        {
            addStatue();
            GameObject wall = LoadPrefab("woodwall_white", box);
            GameObject wallhalf = LoadPrefab("wood_wall_white_half", box);
            GameObject gwall = LoadPrefab("gold_wall_4x2", box);
            GameObject gmwall = LoadPrefab("goldmarble_1x1", box);
            Debug.Log("Attempting to add assets");
            if (wall != null)
            {
                var piece = new CustomPiece(box, "woodwall_white", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 1, 0, true)
                    },
                    Name = "Wood Wall White",
                    Category = PieceCategories.Building,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece);
                var woodWall_half = new CustomPiece(box, "wood_wall_white_half", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Wood", 1, 0, true)
                    },
                    Name = "Wood Wall White Half",
                    Category = PieceCategories.Building,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(woodWall_half);
                var piece2 = new CustomPiece(box, "gold_wall_4x2", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Coin", 50, 0, true)
                    },
                    Name = "Gold Wall",
                    Category = PieceCategories.Building,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece2);

                var piece3 = new CustomPiece(box, "goldmarble_1x1", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Coin", 50, 0, true)
                    },
                    Name = "Gold Marble Wall",
                    Category = PieceCategories.Building,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece3);
            }
            else
            {
                Debug.LogError("Failed to load asset");
            }
        }

        private void addStatue()
        {
            GameObject statue = LoadPrefab("Odin_Statue", box);
            Debug.Log("Attempting to add assets");
            if (statue != null)
            {
                var piece = new CustomPiece(box, "Odin_Statue", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Stone", 5, 0, true)
                    },
                    Name = "Stone Statue",
                    Category = PieceCategories.Building,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece);
                // PrefabManager.Instance.AddPrefab(wall);
            }
            else
            {
                Debug.LogError("Failed to load asset");
            }
        }

        private void addBronze()
        {
            GameObject wall = LoadPrefab("bronze_wall_2x2", box);
            Debug.Log("Attempting to add assets");
            if (wall != null)
            {
                var piece = new CustomPiece(box, "bronze_wall_2x2", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Bronze", 2, 0, true)
                    },
                    Name = "Bronze Bar 2x2",
                    Category = PieceCategories.HeavyBuild,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece);
                // PrefabManager.Instance.AddPrefab(wall);
            }
            else
            {
                Debug.LogError("Failed to load asset");
            }
        }


        private void addBox()
        {
            GameObject green_box = LoadPrefab("piece_chest_green", box);
            Debug.Log("Attempting to add assets");
            if (green_box != null)
            {
                var piece = new CustomPiece(box, "piece_chest_green", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("FineWood", 20, 0, true), new RequirementConfig("Copper", 2, 0, true)
                    },
                    Name = "Cool Chest",
                    Category = PieceCategories.Furniture,
                    PieceTable = PieceTables.Hammer

                });
                PieceManager.Instance.AddPiece(piece);
            }
            else
            {
                Debug.LogError("Failed to load asset");
            }
        }

        public GameObject LoadPrefab(string prefabName, AssetBundle bundle)
        {
            if (bundle == null) return null;

            var prefab = bundle.LoadAsset<GameObject>(prefabName);
            if (prefab != null)
            {
                Debug.Log($"Successfully loaded prefab: {prefabName}");
            }
            else
            {
                Debug.LogError($"Failed to load prefab: {prefabName}");
            }

            return prefab;
        }


        private static void addMaterial(GameObject prefab, string name)
        {
            try
            {
                if (prefab != null)
                {
                    var newPrefab = new CustomPrefab(prefab, true);
                    PrefabManager.Instance.AddPrefab(newPrefab);
                    ZLog.Log((object)("locksmith added material " + prefab));

                    ItemManager.Instance.AddItem(new CustomItem(box, "Flamebar", true, new ItemConfig
                    {
                        Name = name,
                        Description = "Its flamy",
                        CraftingStation = CraftingStations.Workbench,
                        Requirements = new[] { new RequirementConfig("Stone", 1) }
                    }));
                }
                else
                    ZLog.LogWarning((object)("locksmith did not find prefab " + name));
            }
            catch (Exception ex)
            {
                Debug.LogError((object)("locksmith failed to add item " + name));
                Debug.LogError((object)ex);
            }
        }

        private static void addPiece(string name,
            GameObject prefab,
            RequirementConfig[] reqs,
            string category = "Misc")
        {
            try
            {
                if (prefab == null)
                {
                    Debug.LogError("Unable to add item");
                    return;
                }

                Debug.Log("Attempting custom piece");
                CustomPiece customPiece = new CustomPiece(prefab, true, new PieceConfig
                {
                    PieceTable = "Hammer",
                    Category = category,
                    Requirements = reqs,
                    Name = name
                });
                if (customPiece != null)
                {
                    ZLog.Log("locksmith added piece " + prefab.name);
                    PieceManager.Instance.AddPiece(customPiece);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("locksmith failed to add piece " + prefab);
                Debug.LogError(ex);
            }
        }
    }
}
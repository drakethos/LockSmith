using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using Jotunn.Configs;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

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


        public static AssetBundle DrakeBundle;
        public static AssetBundle PloamBundle;
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
            PrefabManager.OnVanillaPrefabsAvailable += addBox;
            PrefabManager.OnVanillaPrefabsAvailable += addKeys;
            PrefabManager.OnVanillaPrefabsAvailable += makeCustom;
            PrefabManager.OnVanillaPrefabsAvailable += ItemLib.makeKeyItems;
            PrefabManager.OnVanillaPrefabsAvailable += ItemLib.addPublicPieces;
            harmony.PatchAll();
        }

        private void makeCustom()
        {
            GameObject ploam_prefab = LoadPrefab("Ploam", PloamBundle);
            string folder = "DrakeMod-LockSmith";
            string assetBundlePath = "DrakeMod-LockSmith/Assets/drake";
            box = AssetUtils.LoadAssetBundle(assetBundlePath);
            if (box == null)
            {
                Logger.LogError($"Failed to load AssetBundle from {assetBundlePath}");
            }

            var ploam = new CustomItem(ploam_prefab, fixReference: true);
            ItemManager.Instance.AddItem(ploam);

            GameObject floam_barrel = LoadPrefab("barell_floam", PloamBundle);
            GameObject gloam_barrel = LoadPrefab("barell_gloam", PloamBundle);
            GameObject ploam_barrel = LoadPrefab("barell_ploam", PloamBundle);
            GameObject ploam_ball = LoadPrefab("ploam_ball", PloamBundle);
            GameObject bandit_mask = LoadPrefab("BanditMask", PloamBundle);

            var floamReq = new[] { new RequirementConfig("Ploam", 20) };
            var maskReq = new[] { new RequirementConfig("DeerHide", 5) };


            addPiece("Floam Barrel", floam_barrel, floamReq);
            addPiece("Gloam Barrel", gloam_barrel, floamReq);
            addPiece("Ploam Barrel", ploam_barrel, floamReq);
            addPiece("Ploam Ball", ploam_ball, floamReq);
            addItem("Bandit Mask", bandit_mask, maskReq);
        }

        private void addItem(string name, GameObject prefab, RequirementConfig[] requirements)
        {
            try
            {
                if (prefab == null)
                {
                    Debug.LogError("Unable to add item " + name);
                    return;
                }

                Debug.Log("Attempting custom item");

                var itemConfig = new ItemConfig();
                itemConfig.Requirements = requirements;
                itemConfig.CraftingStation = CraftingStations.Workbench;
                var item = new CustomItem(prefab, true, itemConfig);

                if (item != null)
                {
                    ZLog.Log("locksmith added item " + prefab.name);
                    ItemManager.Instance.AddItem(item);
                }
            }
            catch (Exception ex)
            {
                Debug.LogError("locksmith failed to add piece " + prefab);
                Debug.LogError(ex);
            }
        }

        private void addBundles()
        {
            string folder = "DrakeMod-LockSmith";
            string assetBundlePath = "DrakeMod-LockSmith/Assets/ploam";
            string assetBundlePath2 = "DrakeMod-LockSmith/Assets/drake";
            Debug.Log($"Loading Assets skippity do ${assetBundlePath} ");
            PloamBundle = AssetUtils.LoadAssetBundle(assetBundlePath);
            if (PloamBundle == null)
            {
                Logger.LogError($"Failed to load AssetBundle from {assetBundlePath}");
            }
        }


        private void addWall()
        {
            addStatue();
            GameObject wall = LoadPrefab("woodwall_white", box);
            GameObject wallhalf = LoadPrefab("wood_wall_white_half", box);

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
                var wallTorch = new CustomPiece(box, "piece_walltorch", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Iron", 1, 0, true), new RequirementConfig("Copper", 2, 0, true)
                    },
                    Name = "IronTorch",
                    Category = PieceCategories.Furniture,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece);
                PieceManager.Instance.AddPiece(wallTorch);
            }
            else
            {
                Debug.LogError("Failed to load asset");
            }
        }

        private void addKeys()
        {
            GameObject KeyMaker = LoadPrefab("KeyMaker", box);
            Debug.Log("Attempting to add assets");
            if (KeyMaker != null)
            {
                var piece = new CustomPiece(box, "KeyMaker", true, new PieceConfig
                {
                    Requirements = new[]
                    {
                        new RequirementConfig("Rock", 5, 0, true), new RequirementConfig("Copper", 2, 0, true)
                    },
                    Name = "KeyMaker",
                    Category = PieceCategories.Crafting,
                    PieceTable = PieceTables.Hammer
                });
                PieceManager.Instance.AddPiece(piece);
                CustomPieceTable keyMaker = new CustomPieceTable(piece.PiecePrefab);
            }
            else
            {
                Debug.LogError("Failed to load asset");
            }
        }

        public static GameObject LoadPrefab(string prefabName, AssetBundle bundle)
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

        private static void addPiece(string name,
            GameObject prefab,
            RequirementConfig[] reqs,
            string category = "Misc")
        {
            try
            {
                if (prefab == null)
                {
                    Debug.LogError("Unable to add piece " + name);
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
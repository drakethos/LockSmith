using System.Collections.Generic;
using BepInEx.Logging;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;

namespace LockSmith
{
    public class ItemLib
    {
        // Your mod's custom localization
        private CustomLocalization Localization;

        private void Localizations()
        {
            Localization = LocalizationManager.Instance.GetLocalization();
            LocalizationManager.Instance.AddLocalization(Localization);

            Localization.AddTranslation("English", new Dictionary<string, string>
            {
                { "item_setAccessKey", "Give Access Key" },
                {
                    "item_setAccessKey_desc",
                    "Use this key on an door or box to allow a player to access it without ward access"
                },
                { "locksmith_giveaccess", "Set Access" },
                { "locksmith_access_message", "Ready to set, other player touch the object!" },
            });
        }

        public void makeKeyItems()
        {
            PieceConfig keyCutter = new PieceConfig();
            keyCutter.PieceTable = PieceTables.Hammer;
            keyCutter.Name = "Key Cutter";
            keyCutter.Description = "Creates keys for unique abilities!";
            keyCutter.Requirements = new[] { new RequirementConfig("Stone", 15), new RequirementConfig("Wood", 10) };
            CustomPiece keyCutterPiece = new CustomPiece("piece_key_cutter", "piece_workbench", keyCutter);
            
            Sprite var1 = AssetUtils.LoadSpriteFromFile("LockSmith/Assets/BlueKey.jpg");
            Sprite var2 = AssetUtils.LoadSpriteFromFile("LockSmith/Assets/BrownKey.jpg");
            Sprite var3 = AssetUtils.LoadSpriteFromFile("LockSmith/Assets/GoldKey.jpg");
            Sprite var4 = AssetUtils.LoadSpriteFromFile("LockSmith/Assets/IronKey.jpg");
            
            Object.Destroy(keyCutterPiece.PiecePrefab.GetComponent("StationExtension"));
            keyCutterPiece.PiecePrefab.AddComponent<CraftingStation>();
            keyCutterPiece.PiecePrefab.GetComponent<CraftingStation>().m_showBasicRecipies = false;
            PieceManager.Instance.AddPiece(keyCutterPiece);
            ItemConfig setAccessKeyConfig = new ItemConfig();
            setAccessKeyConfig.Name = "Set Access Key"; //"$item_setAccessKey";
            setAccessKeyConfig.Description = "use this key on a door or lock"; //"$item_setAccessKey_desc";
            setAccessKeyConfig.CraftingStation = "piece_key_cutter";
            setAccessKeyConfig.AddRequirement(new RequirementConfig("Stone", 2));
            setAccessKeyConfig.AddRequirement(new RequirementConfig("Wood", 1));
            makeItem("setAccessKey", setAccessKeyConfig, "CryptKey");
            // ItemManager.Instance.AddRecipesFromJson("LockSmith/Assets/recipes.json");

            /*
            setAccessKeyConfig.Name = "Public Key"; //"$item_setAccessKey";
            setAccessKeyConfig.Description = "place this key in a box to make it public"; //"$item_setAccessKey_desc";
            */
//            makeItem("publicKey", setAccessKeyConfig, "CryptKey");
            setAccessKeyConfig.Icons = new[] { var1, var2, var3, var4 };
            setAccessKeyConfig.Name = "Personal Key"; //"$item_setAccessKey";
            setAccessKeyConfig.Description =
                "place this key in a box to make the owner, have access"; //"$item_setAccessKey_desc";
            makeItem("personalKey", setAccessKeyConfig, "CryptKey");
            PrefabManager.OnVanillaPrefabsAvailable -= makeKeyItems;

            CreateKeyHints();
        }

        private void CreateKeyHints()
        {
            // Override "default" KeyHint with an empty config
            KeyHintConfig KHC_base = new KeyHintConfig
            {
                Item = "setAccessKey"
            };
            KeyHintManager.Instance.AddKeyHint(KHC_base);
            
        }

        private void makeItem(string name, ItemConfig itemConfig, string prefab)
        {
            makeItem(name, itemConfig.Name, itemConfig.Description, prefab,
                new List<RequirementConfig>(itemConfig.Requirements), itemConfig.CraftingStation);
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
    }
}
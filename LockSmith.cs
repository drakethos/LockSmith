
using System;
using System.Collections.Generic;
using BepInEx;
using HarmonyLib;
using Jotunn.Configs;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;
namespace LockSmith
{
    [BepInPlugin("com.drakemods.LockSmith", "LockSmith", "0.0.1")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [BepInProcess("valheim.exe")]
    public class LockSmith : BaseUnityPlugin
    {
        private readonly Harmony harmony = new Harmony("drakesmod.LockSmith");
        // Your mod's custom localization
        private CustomLocalization Localization;
        private void Awake()
        {
            //Localizations();
            PrefabManager.OnVanillaPrefabsAvailable += makeKeyItems;

            harmony.PatchAll();
            Localizations();
            makeKeyItems();
        }

        private void Localizations()
        {
            Localization = new CustomLocalization();
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

        private void makeKeyItems()
        {
            ItemConfig setAccessKeyConfig = new ItemConfig();
            setAccessKeyConfig.Name = "Set Access Key"; //"$item_setAccessKey";
            setAccessKeyConfig.Description = "use this key on a door or lock"; //"$item_setAccessKey_desc";
            setAccessKeyConfig.CraftingStation = "piece_workbench";
            setAccessKeyConfig.AddRequirement(new RequirementConfig("Stone", 1));
            setAccessKeyConfig.AddRequirement(new RequirementConfig("Wood", 1));
            makeItem("setAccessKey", setAccessKeyConfig, "CryptKey");
            // ItemManager.Instance.AddRecipesFromJson("LockSmith/Assets/recipes.json");

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

            // Add custom KeyHints for specific pieces
            KeyHintConfig KHC_make = new KeyHintConfig
            {
                Item = "setAccessKey",
                //Piece = "make_testblueprint",
                ButtonConfigs = new[]
                {
                    // Override vanilla "Attack" key text
                    new ButtonConfig { Name = "Attack", HintToken = "$bprune_make" }
                }
            };
            KeyHintManager.Instance.AddKeyHint(KHC_make);
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


        [HarmonyPatch(typeof(Container),"CheckAccess" )]
        class Private_mod
        {
            static void Postfix(ref Container.PrivacySetting ___m_privacy)
            {
         
                ___m_privacy = Container.PrivacySetting.Private;
                Debug.Log($"Modified jump force: {___m_privacy}");
                if (___m_privacy == Container.PrivacySetting.Private)
                {
                    Debug.Log($"privacy set to: {___m_privacy}");
                }
            }

            
        }
    }
}
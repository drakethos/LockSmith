
using System;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.GUI;
using UnityEngine;
using UnityEngine.SceneManagement;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;

using Jotunn.Managers;
using Jotunn.Utils;

namespace LockSmith
{
[BepInPlugin("drakemods.VModItem", "Drake VMod - Items", "0.0.1")]
//[BepInProcess("valheim.exe")]
[BepInDependency(Jotunn.Main.ModGuid)]
[NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
[BepInDependency("cinnabun.backpacks-v1.0.0", BepInDependency.DependencyFlags.SoftDependency)]

    public class Items : BaseUnityPlugin
    {
        private void Awake()
        {
            
        PrefabManager.OnVanillaPrefabsAvailable += AddClonedItems;

        }

        private void AddClonedItems()
        {
            // Create a custom resource based on Wood
            ItemConfig customWoodConfig = new ItemConfig();
            customWoodConfig.Name = "Super Wood";
            customWoodConfig.Description = "Its super duper wood";
            customWoodConfig.AddRequirement(new RequirementConfig("Wood", 1));
            CustomItem recipeComponent = new CustomItem("CustomWood", "Wood", customWoodConfig);
            ItemManager.Instance.AddItem(recipeComponent);

            // Create and add a custom item based on SwordBlackmetal
            ItemConfig evilSwordConfig = new ItemConfig();
            evilSwordConfig.Name = "$item_evilsword";
            evilSwordConfig.Description = "$item_evilsword_desc";
            evilSwordConfig.CraftingStation = "piece_workbench";
            evilSwordConfig.AddRequirement(new RequirementConfig("Stone", 1));
            evilSwordConfig.AddRequirement(new RequirementConfig("CustomWood", 1));

            CustomItem evilSword = new CustomItem("EvilSword", "SwordBlackmetal", evilSwordConfig);
            ItemManager.Instance.AddItem(evilSword);

            // You want that to run only once, Jotunn has the item cached for the game session
            PrefabManager.OnVanillaPrefabsAvailable -= AddClonedItems;
        }

    }
}
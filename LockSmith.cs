using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Configs;
using UnityEngine;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;

namespace LockSmith
{
    [BepInPlugin("com.drakemods.LockSmith", "LockSmith", "0.0.1")]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    [BepInProcess("valheim.exe")]
    public class LockSmith : BaseUnityPlugin
    {
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
            //Localizations();
            PrefabManager.OnVanillaPrefabsAvailable += ItemLib.makeKeyItems;
            PrefabManager.OnVanillaPrefabsAvailable += ItemLib.addPublicPieces;
            harmony.PatchAll();
        }

        //
        // [HarmonyPatch(typeof(Container), "Awake")]
        // private class makeContainerPublic
        // {
        //     private static void Postfix(ref Container __instance)
        //     {
        //         if (__instance.name.Contains("public"))
        //         {
        //             Debug.Log("Found a public chest");
        //             __instance.m_checkGuardStone = false;
        //         }
        //     }
        // }
        //
        // [HarmonyPatch(typeof(Door), "Awake")]
        // private class makeDoorPublic
        // {
        //     private static void Postfix(ref Door __instance)
        //     {
        //         if (__instance.name.Contains("public"))
        //         {
        //             Debug.Log("Found a public door");
        //             __instance.m_checkGuardStone = false;
        //         }
        //     }
        // }
    }
}
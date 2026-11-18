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
        public const string ModName = "MyMod";
        public const string Version = "0.0.1";
        public const string GUID = "com." + CompanyName + "." + ModName;
        public ConfigEntry<string> PublicPiecesConfig; // Config entry for public pieces list
        public static readonly char[] ConfigSeparator = { ',' }; // Separator for config entries
        public static AssetBundle box;


        private readonly Harmony harmony = new Harmony("drakesmod.Mod");



 

        private void Awake()
        {
      
        
            
            harmony.PatchAll();
        }

    }
}
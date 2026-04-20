using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using Jotunn.Managers;
using Jotunn.Utils;

namespace DrakeLabs
{
    [BepInPlugin(PluginGUID, PluginName, PluginVersion)]
    [BepInDependency(Jotunn.Main.ModGuid)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public class Plugin : BaseUnityPlugin
    {
        public const string PluginGUID    = "com.drakemod.drakelabs";
        public const string PluginName    = "DrakeLabs";
        public const string PluginVersion = "1.2.0";

        internal static ManualLogSource Log;
        private static readonly Harmony harmony = new Harmony(PluginGUID);
        private static ItemLib itemLib;

        // ------------------------------------------------------------------
        // Config
        // ------------------------------------------------------------------

        /// <summary>
        /// When true, ALL doors inside a ward (even those in Public mode) require
        /// the player to carry a DL_WardKey to open them.
        /// Ward members are always exempt.
        /// </summary>
        internal static ConfigEntry<bool> RequireKeyForDoors;

        /// <summary>
        /// When true, ALL chests inside a ward (even those in Public mode) require
        /// the player to carry a DL_WardKey to open them.
        /// Ward members are always exempt.
        /// </summary>
        internal static ConfigEntry<bool> RequireKeyForChests;

        private void Awake()
        {
            Log = Logger;
            itemLib = new ItemLib();

            // Bind config entries before patches run so the values are available.
            RequireKeyForDoors = Config.Bind(
                "Access",
                "RequireKeyForDoors",
                false,
                new ConfigDescription(
                    "If true, doors inside a ward always require the DL_WardKey item to open, " +
                    "regardless of the door's individual access mode. Ward members are exempt.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            RequireKeyForChests = Config.Bind(
                "Access",
                "RequireKeyForChests",
                false,
                new ConfigDescription(
                    "If true, chests inside a ward always require the DL_WardKey item to open, " +
                    "regardless of the chest's individual access mode. Ward members are exempt.",
                    null,
                    new ConfigurationManagerAttributes { IsAdminOnly = true }));

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabsAvailable;
            harmony.PatchAll();
            Patches.RegisterCommands();
            Log.LogInfo($"{PluginName} v{PluginVersion} loaded.");
        }

        private void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        private void OnVanillaPrefabsAvailable()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabsAvailable;
            itemLib.AddKeyItem();
            itemLib.addPublicPieces();
        }
    }
}

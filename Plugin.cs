using BepInEx;
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
        public const string PluginGUID = "com.drakemod.drakelabs";
        public const string PluginName = "DrakeLabs";
        public const string PluginVersion = "1.1.0";

        internal static ManualLogSource Log;
        private static readonly Harmony harmony = new Harmony(PluginGUID);
        private static ItemLib itemLib;

        private void Awake()
        {
            Log = Logger;
            itemLib = new ItemLib();

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

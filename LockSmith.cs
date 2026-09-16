using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using DrakeModsLibs;
using DrakeModsLibs.Art;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;

namespace LockSmith
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Main.ModGuid)]
    [BepInDependency(CustomizeLibsPlugin.GUID)]
    [NetworkCompatibility(CompatibilityLevel.EveryoneMustHaveMod, VersionStrictness.Minor)]
    public partial class LockSmith : BaseUnityPlugin
    {
        public static LockSmith Instance { get; private set; } = null!;

        public static ManualLogSource? Log { get; private set; }

        private readonly Harmony _harmony = new Harmony(GUID);

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            LockSmithConfig.Bind(Config, Logger);

            var pluginDir = Path.GetDirectoryName(Info.Location) ?? "";
            // Official key only: Assets/Items/keys/masterkey.json + keys.bundle (MasterKey).
            ArtItemLoader.Register(Logger, pluginDir, Config, ContentRegistration.CustomizeMasterKeyArtItem);

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            _harmony.PatchAll();
            Logger.LogInfo($"{ModName} {Version} Awake (official key = keys.bundle MasterKey).");
        }

        private void OnVanillaPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            try
            {
                LockSmithLocalization.Register();
                // ArtItemLoader also hooks this event and subscribed first — masterkey should exist now.
                ContentRegistration.FinalizeOfficialKeyFromKeysPack();
            }
            catch (Exception ex)
            {
                Logger.LogError($"LockSmith content registration failed: {ex}");
            }
        }
    }
}

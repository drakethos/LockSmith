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
            // Gale/some managers flatten Thunderstore zips (keys.bundle at plugin root).
            // ArtItemLoader expects Assets/Items/keys/ — repair before register.
            RepairFlattenedArtLayout(pluginDir);
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

        /// <summary>
        /// Some mod managers flatten the Thunderstore zip so keys.bundle/masterkey.json sit next to the DLL.
        /// Copy them into Assets/Items/keys/ so ArtItemLoader can find the folder pack.
        /// </summary>
        private static void RepairFlattenedArtLayout(string pluginDir)
        {
            if (string.IsNullOrEmpty(pluginDir))
                return;

            var keysDir = Path.Combine(pluginDir, "Assets", "Items", "keys");
            var nestedJson = Path.Combine(keysDir, "masterkey.json");
            if (File.Exists(nestedJson) && File.Exists(Path.Combine(keysDir, "keys.bundle")))
                return;

            var flatJson = Path.Combine(pluginDir, "masterkey.json");
            var flatBundle = Path.Combine(pluginDir, "keys.bundle");
            if (!File.Exists(flatJson) || !File.Exists(flatBundle))
                return;

            try
            {
                Directory.CreateDirectory(keysDir);
                File.Copy(flatJson, Path.Combine(keysDir, "masterkey.json"), overwrite: true);
                File.Copy(flatBundle, Path.Combine(keysDir, "keys.bundle"), overwrite: true);

                var flatPng = Path.Combine(pluginDir, "masterkey.png");
                if (File.Exists(flatPng))
                    File.Copy(flatPng, Path.Combine(keysDir, "masterkey.png"), overwrite: true);

                var flatIcon = Path.Combine(pluginDir, "masterkey_icon.png");
                if (File.Exists(flatIcon))
                {
                    var assetsDir = Path.Combine(pluginDir, "Assets");
                    Directory.CreateDirectory(assetsDir);
                    File.Copy(flatIcon, Path.Combine(assetsDir, "masterkey_icon.png"), overwrite: true);
                }

                Log?.LogWarning(
                    "Repaired flattened key art into Assets/Items/keys (Gale/manager zip layout).");
            }
            catch (Exception ex)
            {
                Log?.LogError($"Failed to repair flattened key art layout: {ex.Message}");
            }
        }
    }
}

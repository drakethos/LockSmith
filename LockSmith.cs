using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using DrakeModsLibs;
using HarmonyLib;
using Jotunn;
using Jotunn.Managers;
using Jotunn.Utils;
using UnityEngine;
using Paths = BepInEx.Paths;

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

        /// <summary>Loaded drake bundle (KeyMaker + keys).</summary>
        public static AssetBundle? DrakeBundle { get; private set; }

        private readonly Harmony _harmony = new Harmony(GUID);

        private void Awake()
        {
            Instance = this;
            Log = Logger;

            LockSmithConfig.Bind(Config, Logger);
            LoadDrakeBundle();

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            _harmony.PatchAll();
            Logger.LogInfo($"{ModName} {Version} Awake (0.3: chests/doors public + group private chests).");
        }

        private void OnVanillaPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            try
            {
                LockSmithLocalization.Register();
                ContentRegistration.RegisterFromDrakeBundle(DrakeBundle);
            }
            catch (Exception ex)
            {
                Logger.LogError($"LockSmith content registration failed: {ex}");
            }
        }

        private void LoadDrakeBundle()
        {
            var pluginDir = Path.GetDirectoryName(Info.Location);
            if (string.IsNullOrEmpty(pluginDir))
            {
                Logger.LogError("Plugin directory is missing; cannot load the drake bundle.");
                return;
            }

            var bundleFile = Path.Combine(pluginDir, "Assets", "drake");
            var pluginsRoot = Paths.PluginPath;
            if (string.IsNullOrEmpty(pluginsRoot))
            {
                Logger.LogError("BepInEx plugin path is missing; cannot load the drake bundle.");
                return;
            }

            var root = pluginsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            if (!bundleFile.StartsWith(root, StringComparison.OrdinalIgnoreCase))
            {
                Logger.LogError($"Drake bundle is not under the BepInEx plugins folder: {bundleFile}");
                return;
            }

            var relative = bundleFile.Substring(root.Length).Replace('\\', '/');
            DrakeBundle = AssetUtils.LoadAssetBundle(relative);
            if (DrakeBundle == null)
                Logger.LogError($"Failed to load asset bundle from {relative}");
            else
                Logger.LogInfo($"Loaded drake bundle from {relative}");
        }
    }
}

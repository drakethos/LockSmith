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
using LockSmith.Access;
using LockSmith.Compat;

namespace LockSmith
{
    [BepInPlugin(GUID, ModName, Version)]
    [BepInDependency(Main.ModGuid)]
    [BepInDependency(CustomizeLibsPlugin.GUID)]
    // SoftDependency = optional load-order only. Missing plugins do not block LockSmith.
    [BepInDependency(CompatibilityManager.SoftGuids.WardIsLove, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(CompatibilityManager.SoftGuids.ProtectiveWards, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(CompatibilityManager.SoftGuids.ArcaneWard, BepInDependency.DependencyFlags.SoftDependency)]
    [BepInDependency(CompatibilityManager.SoftGuids.DevCommands, BepInDependency.DependencyFlags.SoftDependency)]
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

            UI.KeyPassMenu.RegisterTab();

            // UseKey off = simple mode: the key item is never registered (restart after changing,
            // like EnablePieceMode). SyncKeyRecipe still hides the recipe if a client's local value
            // disagrees with the server's.
            if (LockSmithConfig.UseKey)
            {
                var pluginDir = Path.GetDirectoryName(Info.Location) ?? "";
                // Gale/some managers flatten Thunderstore zips (keys.bundle at plugin root).
                // ArtItemLoader expects Assets/Items/keys/ — repair before register.
                RepairFlattenedArtLayout(pluginDir);
                // Official key only: Assets/Items/keys/masterkey.json + keys.bundle (MasterKey).
                // Pass config: null so ArtItemLoader does not create a duplicate "masterkey" section
                // (Display name / Description / Materials). Recipe + name live under LockSmith → 03 Key.
                ArtItemLoader.Register(
                    Logger,
                    pluginDir,
                    config: null,
                    ContentRegistration.CustomizeMasterKeyArtItem);
            }
            else
            {
                Logger.LogInfo("UseKey is off — Locksmith key not registered (simple mode).");
            }

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            _harmony.PatchAll();
            // One-liner: scan optional ward/cheat mods and apply their soft patches.
            CompatibilityManager.Initialize(_harmony);
            Logger.LogInfo($"{ModName} {Version} Awake (official key = keys.bundle MasterKey).");
        }

        private void Update()
        {
            try
            {
                PieceRpc.Tick();
            }
            catch (Exception ex)
            {
                Logger.LogError($"PieceRpc tick failed: {ex}");
                PieceRpc.Clear();
            }

            try
            {
                PieceMenuLock.Tick();
                UI.PieceAccessMenu.Tick();
                ContentRegistration.SyncKeyRecipe();
            }
            catch (Exception ex)
            {
                Logger.LogError($"LockSmith menu/recipe tick failed: {ex}");
                UI.PieceAccessMenu.Close();
            }
        }

        private void OnVanillaPrefabs()
        {
            PrefabManager.OnVanillaPrefabsAvailable -= OnVanillaPrefabs;
            try
            {
                LockSmithLocalization.Register();
                // ArtItemLoader also hooks this event and subscribed first — masterkey should exist now.
                if (LockSmithConfig.UseKey)
                    ContentRegistration.FinalizeOfficialKeyFromKeysPack();
                PublicPieceRegistration.TryRegisterAll("OnVanillaPrefabsAvailable");
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

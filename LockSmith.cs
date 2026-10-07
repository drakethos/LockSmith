using System;
using System.IO;
using BepInEx;
using BepInEx.Logging;
using DrakeModsLibs;
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

            // The key is temporarily removed while it's rebuilt with Drakes Asset Forge: LockSmithConfig.UseKey
            // is always false, so the key item is never registered and everything goes through AltPlace+E.
            Logger.LogInfo("Locksmith key is temporarily removed — simple mode (AltPlace+E).");

            PrefabManager.OnVanillaPrefabsAvailable += OnVanillaPrefabs;
            _harmony.PatchAll();
            // One-liner: scan optional ward/cheat mods and apply their soft patches.
            CompatibilityManager.Initialize(_harmony);
            Logger.LogInfo($"{ModName} {Version} Awake.");
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
                // Key is temporarily removed (UseKey is always false); kept for when it returns.
                if (LockSmithConfig.UseKey)
                    ContentRegistration.FinalizeOfficialKeyFromKeysPack();
                PublicPieceRegistration.TryRegisterAll("OnVanillaPrefabsAvailable");
            }
            catch (Exception ex)
            {
                Logger.LogError($"LockSmith content registration failed: {ex}");
            }
        }
    }
}

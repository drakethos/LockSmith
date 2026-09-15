using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using DrakeModsLibs;
using DrakeModsLibs.Sync;

namespace LockSmith;

/// <summary>Synced gameplay config. Count must match <see cref="ExpectedSyncedEntryCount"/>.</summary>
public static class LockSmithConfig
{
    /// <summary>Admin lock + Phase 1 feature/key entries. Bump when adding synced binds.</summary>
    public const int ExpectedSyncedEntryCount = 9;

    private const string SectionAdmin = "01 Admin";
    private const string SectionFeatures = "02 Features";
    private const string SectionKey = "03 Key";

    private const string DisplayAdmin = "Admin";
    private const string DisplayFeatures = "Features";
    private const string DisplayKey = "Key";

    private static readonly DrakeConfigSync Sync = DrakeConfigSync.Create(
        LockSmith.ModName,
        LockSmith.ModName,
        LockSmith.Version);

    private static ConfigEntry<bool> _lockSyncedConfig = null!;
    private static ConfigEntry<bool> _enableChests = null!;
    private static ConfigEntry<bool> _enableDoors = null!;
    private static ConfigEntry<bool> _enableKeyMode = null!;
    private static ConfigEntry<bool> _enablePieceMode = null!;
    private static ConfigEntry<string> _keyName = null!;
    private static ConfigEntry<string> _keyDescription = null!;
    private static ConfigEntry<string> _keyCraftingStation = null!;
    private static ConfigEntry<string> _keyMaterials = null!;

    public static bool LockSyncedConfig => _lockSyncedConfig.Value;
    public static bool EnableChests => _enableChests.Value;
    public static bool EnableDoors => _enableDoors.Value;
    public static bool EnableKeyMode => _enableKeyMode.Value;
    public static bool EnablePieceMode => _enablePieceMode.Value;
    public static string KeyName => _keyName.Value;
    public static string KeyDescription => _keyDescription.Value;
    public static string KeyCraftingStation => _keyCraftingStation.Value;
    public static string KeyMaterials => _keyMaterials.Value;

    public static void Bind(ConfigFile config, ManualLogSource log)
    {
        _lockSyncedConfig = Sync.BindSynced(
            config,
            SectionAdmin,
            DisplayAdmin,
            "LockSyncedConfig",
            true,
            "When true, only the host/admin can change synced LockSmith settings.");

        _enableChests = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableChests",
            true,
            "Allow LockSmith public/private on player-built chests (Phase 1).");

        _enableDoors = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableDoors",
            true,
            "Allow LockSmith public/private on player-built doors and gates (Phase 2).");

        _enableKeyMode = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableKeyMode",
            true,
            "Craft and use the LockSmith key to toggle Public/Private on enabled pieces.");

        _enablePieceMode = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnablePieceMode",
            false,
            "Reserved for Phase 3. Public hammer clones and per-person key storage are not implemented yet.");

        _keyName = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "KeyName",
            "Locksmith Key",
            "Display name for the craftable key.");

        _keyDescription = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "KeyDescription",
            "Equip to change chest/door interact: press E to toggle public access.",
            "Tooltip description for the craftable key.");

        _keyCraftingStation = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "KeyCraftingStation",
            "KeyMaker",
            "Crafting station prefab name. Default KeyMaker from this mod.");

        _keyMaterials = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "KeyMaterials",
            "Bronze:2,Wood:4",
            "Recipe requirements as Prefab:Amount pairs, separated by commas.");

        Sync.AddLockingConfigEntry(_lockSyncedConfig);
        Sync.FinalizeBinding(log, ExpectedSyncedEntryCount, () => LockSyncedConfig);
    }
}

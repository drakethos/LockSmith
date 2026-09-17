using System;
using BepInEx.Configuration;
using BepInEx.Logging;
using DrakeModsLibs;
using DrakeModsLibs.Sync;

namespace LockSmith;

/// <summary>Keyboard modifier for LockSmith custom chords (local preference).</summary>
public enum LockSmithModifier
{
    Alt,
    Shift,
    Control
}

/// <summary>Synced gameplay config. Count must match <see cref="ExpectedSyncedEntryCount"/>.</summary>
public static class LockSmithConfig
{
    /// <summary>Admin lock + feature/key entries. Bump when adding synced binds.</summary>
    public const int ExpectedSyncedEntryCount = 16;

    private const string SectionAdmin = "01 Admin";
    private const string SectionFeatures = "02 Features";
    private const string SectionKey = "03 Key";
    private const string SectionDisplay = "04 Display";

    private const string DisplayAdmin = "Admin";
    private const string DisplayFeatures = "Features";
    private const string DisplayKey = "Key";
    private const string DisplayDisplay = "Display";

    private static readonly DrakeConfigSync Sync = DrakeConfigSync.Create(
        LockSmith.ModName,
        LockSmith.ModName,
        LockSmith.Version);

    private static ConfigEntry<bool> _lockSyncedConfig = null!;
    private static ConfigEntry<bool> _enableChests = null!;
    private static ConfigEntry<bool> _enableDoors = null!;
    private static ConfigEntry<bool> _enableGroupChests = null!;
    private static ConfigEntry<bool> _enablePersonalPause = null!;
    private static ConfigEntry<bool> _enablePieceGuests = null!;
    private static ConfigEntry<bool> _enableOptInAccess = null!;
    private static ConfigEntry<bool> _enableGuestPublicToggle = null!;
    private static ConfigEntry<bool> _enableDesignate = null!;
    private static ConfigEntry<bool> _enableKeyMode = null!;
    private static ConfigEntry<bool> _enablePieceMode = null!;
    private static ConfigEntry<bool> _enableKeyPasses = null!;
    private static ConfigEntry<string> _keyName = null!;
    private static ConfigEntry<string> _keyDescription = null!;
    private static ConfigEntry<string> _keyCraftingStation = null!;
    private static ConfigEntry<string> _keyMaterials = null!;
    private static ConfigEntry<string> _teamLabelColor = null!;
    private static ConfigEntry<LockSmithModifier> _clearModifier = null!;
    private static ConfigEntry<LockSmithModifier> _setupModifier = null!;

    public static bool LockSyncedConfig => _lockSyncedConfig.Value;
    public static bool EnableChests => _enableChests.Value;
    public static bool EnableDoors => _enableDoors.Value;
    public static bool EnableGroupChests => _enableGroupChests.Value;
    /// <summary>When true, creator can pause team sharing (Personal) without clearing names. Off = team-only add people.</summary>
    public static bool EnablePersonalPause => _enablePersonalPause.Value;
    public static bool EnablePieceGuests => _enablePieceGuests.Value;
    public static bool EnableOptInAccess => _enableOptInAccess.Value;
    public static bool EnableGuestPublicToggle => _enableGuestPublicToggle.Value;
    public static bool EnableDesignate => _enableDesignate.Value;
    public static bool EnableKeyMode => _enableKeyMode.Value;
    public static bool EnablePieceMode => _enablePieceMode.Value;
    public static bool EnableKeyPasses => _enableKeyPasses.Value;
    public static string KeyName => _keyName.Value;
    public static string KeyDescription => _keyDescription.Value;
    public static string KeyCraftingStation => _keyCraftingStation.Value;
    public static string KeyMaterials => _keyMaterials.Value;

    /// <summary>Local-only hex color for Team / Guests labels (e.g. #FF00FF).</summary>
    public static string TeamLabelColorHex => NormalizeHexColor(_teamLabelColor.Value);

    /// <summary>Local: modifier+E with key clears LockSmith (default Alt).</summary>
    public static LockSmithModifier ClearModifier => _clearModifier.Value;

    /// <summary>Local: modifier+E with key opens/closes Join (default Alt). Keep different from Clear.</summary>
    public static LockSmithModifier SetupModifier => _setupModifier.Value;

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
            "Allow LockSmith public/private on player-built chests.");

        _enableDoors = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableDoors",
            true,
            "Allow LockSmith public/private on player-built doors and gates.");

        _enableGroupChests = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableGroupChests",
            true,
            "Team sharing on private-family chests (Join list / guests).");

        _enablePersonalPause = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnablePersonalPause",
            false,
            "When true, key E toggles Personal↔Team as a pause switch (keeps names; friends locked out while Personal). When false (default), that toggle is hidden — chests stay team-shared and you only add people via Join.");

        _enablePieceGuests = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnablePieceGuests",
            true,
            "Partial ward access: guests on a chest/door can open without being on the ward.");

        _enableOptInAccess = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableOptInAccess",
            true,
            "Ward-style opt-in: creator opens Join access; other players press E to join (solo-testable).");

        _enableGuestPublicToggle = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableGuestPublicToggle",
            true,
            "Permitted players (ward or guest) Alt+E public/private without the key. With EnableDesignate, only on designated pieces. Strangers never can. Not for private chests.");

        _enableDesignate = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableDesignate",
            true,
            "Key must Enable LockSmith on a piece first (locksmith_managed). After that, permitted Alt+E works without holding the key. Off = key E toggles immediately; no designate gate.");

        _enableKeyMode = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableKeyMode",
            true,
            "Craft and use the LockSmith key to designate pieces and change Team/Join settings.");

        _enablePieceMode = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnablePieceMode",
            false,
            "Reserved for Phase 4. Public hammer clones are not implemented yet.");

        _enableKeyPasses = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableKeyPasses",
            true,
            "Key pass clipboard: inventory menu on the Locksmith key (Relabel / grab / pull / clear / clone). Ctrl+C/V world copy-paste follows.");

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
            "Equip to designate a chest/door. After that, <color=#ffff00><b>Alt+E</b></color> toggles public/private if you have access. Key required for Team / Join setup. <color=#ffff00><b>Shift+Right-click</b></color> the key: Relabel, grab/pull names, clear, or clone.",
            "Tooltip description for the craftable key. Yellow-tag Alt+E / Shift+Right-click here. Ctrl+C/V stay in interact hints only — do not repeat them here.");

        _keyCraftingStation = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "KeyCraftingStation",
            "",
            "Crafting station prefab name. Empty = craft anywhere.");

        _keyMaterials = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "KeyMaterials",
            "Wood:1",
            "Recipe requirements as Prefab:Amount pairs, separated by commas.");

        // Local display / binds only — not DrakeConfigSync.
        _teamLabelColor = config.Bind(
            SectionDisplay,
            "TeamLabelColor",
            "#FF00FF",
            new ConfigDescription(
                "Local color for [Team] / Guests labels (hex like #FF00FF). Not synced."));

        _clearModifier = config.Bind(
            SectionDisplay,
            "ClearModifier",
            LockSmithModifier.Alt,
            new ConfigDescription(
                "Local: with key, this modifier+E clears LockSmith. Default Alt. Do not match SetupModifier (or your Join / AltPlace bind)."));

        _setupModifier = config.Bind(
            SectionDisplay,
            "SetupModifier",
            LockSmithModifier.Shift,
            new ConfigDescription(
                "Local: with key, this modifier+E opens/closes Join. Default Shift so it stays clear of Alt+E Clear. Guest Leave / no-key public toggle still use the game AltPlace bind."));

        Sync.AddLockingConfigEntry(_lockSyncedConfig);
        Sync.FinalizeBinding(log, ExpectedSyncedEntryCount, () => LockSyncedConfig);
    }

    private static string NormalizeHexColor(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
            return "#FF00FF";

        var s = raw!.Trim();
        if (s.StartsWith("#", StringComparison.Ordinal))
            s = s.Substring(1);

        if (s.Length != 6)
            return "#FF00FF";

        foreach (var c in s)
        {
            var hex = (c >= '0' && c <= '9')
                      || (c >= 'a' && c <= 'f')
                      || (c >= 'A' && c <= 'F');
            if (!hex)
                return "#FF00FF";
        }

        return "#" + s.ToUpperInvariant();
    }
}

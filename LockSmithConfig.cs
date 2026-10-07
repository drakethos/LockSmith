using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using BepInEx.Logging;
using DrakeModsLibs;
using DrakeModsLibs.Sync;
using HarmonyLib;

namespace LockSmith;

/// <summary>Synced gameplay config. Count must match <see cref="ExpectedSyncedEntryCount"/>.</summary>
public static class LockSmithConfig
{
    /// <summary>Admin lock + feature/key entries. Bump when adding synced binds.</summary>
    public const int ExpectedSyncedEntryCount = 25;

    private const string SectionAdmin = "01 Admin";
    private const string SectionFeatures = "02 Features";
    private const string SectionKey = "03 Key";
    private const string SectionPieceMode = "05 PieceMode";
    private const string SectionDisplay = "04 Display";

    private const string DisplayAdmin = "Admin";
    private const string DisplayFeatures = "Features";
    private const string DisplayKey = "Key (advanced)";
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
    private static ConfigEntry<bool> _enableManagedAccess = null!;
    private static ConfigEntry<bool> _enablePieceMode = null!;
    private static ConfigEntry<bool> _enableKeyPasses = null!;
    private static ConfigEntry<bool> _enableKeyExtras = null!;
    private static ConfigEntry<bool> _requireKeyForSetup = null!;
    private static ConfigEntry<bool> _useKey = null!;
    private static ConfigEntry<bool> _enableAddNearby = null!;
    private static ConfigEntry<bool> _requireActiveWard = null!;
    private static ConfigEntry<string> _publicPieceAllowList = null!;
    private static ConfigEntry<string> _publicPieceDenyList = null!;
    private static ConfigEntry<bool> _publicPieceNameSuffix = null!;
    private static ConfigEntry<string> _publicPieceHoverText = null!;
    private static ConfigEntry<string> _keyName = null!;
    private static ConfigEntry<string> _keyDescription = null!;
    private static ConfigEntry<string> _keyCraftingStation = null!;
    private static ConfigEntry<string> _keyMaterials = null!;
    private static ConfigEntry<string> _teamLabelColor = null!;

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
    public static bool EnableManagedAccess => _enableManagedAccess.Value;
    public static bool EnablePieceMode => _enablePieceMode.Value;
    public static bool EnableKeyPasses => _enableKeyPasses.Value;
    public static bool EnableKeyExtras => _enableKeyExtras.Value;
    /// <summary>On only when the key is in play; off means the menu is the whole tool.</summary>
    public static bool RequireKeyForSetup => UseKey && _requireKeyForSetup.Value;
    /// <summary>
    /// Always false for now: the key is temporarily removed while it's rebuilt with Drakes Asset Forge.
    /// The UseKey setting stays bound (so synced configs line up) but is not read.
    /// </summary>
    public static bool UseKey => false;
    public static bool EnableAddNearby => _enableAddNearby.Value;
    /// <summary>Comma-separated donor prefab names force-included in public clones (restart required).</summary>
    public static string PublicPieceAllowList => _publicPieceAllowList.Value;
    /// <summary>Comma-separated donor prefab names never cloned (restart required).</summary>
    public static string PublicPieceDenyList => _publicPieceDenyList.Value;
    /// <summary>When true, public clones append a localized (public) suffix to the display name.</summary>
    public static bool PublicPieceNameSuffix => _publicPieceNameSuffix.Value;
    /// <summary>Plain hover line on placed public hammer doors and chests. Default [Public].</summary>
    public static string PublicPieceHoverText
    {
        get
        {
            var text = (_publicPieceHoverText.Value ?? string.Empty).Trim();
            return text.Length == 0 ? "[Public]" : text;
        }
    }
    /// <summary>
    /// When true, LockSmith cannot manage or toggle normal chests/doors unless inside an enabled ward
    /// (including already-managed pieces). Personal/Team private-family chests are exempt.
    /// </summary>
    public static bool RequireActiveWard => _requireActiveWard.Value;
    public static string KeyName => _keyName.Value;
    public static string KeyDescription => _keyDescription.Value;
    public static string KeyCraftingStation => _keyCraftingStation.Value;
    public static string KeyMaterials => _keyMaterials.Value;

    /// <summary>Local-only hex color for Team / Guests labels (e.g. #FF00FF).</summary>
    public static string TeamLabelColorHex => NormalizeHexColor(_teamLabelColor.Value);

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
            "When true, private-chest owners get Make personal / Make team in the Lock menu: a pause switch that keeps the names but locks friends out while Personal. When false (default), chests stay team-shared.");

        _enablePieceGuests = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnablePieceGuests",
            true,
            "Guests: players on a door's or chest's guest list can open it without being on the ward.");

        _enableOptInAccess = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableOptInAccess",
            true,
            "Join (ward-style opt-in): an owner uses Open Join in the Lock menu, then other players press AltPlace+E → Join access.");

        _enableGuestPublicToggle = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableGuestPublicToggle",
            true,
            "Ward members and guests get Make public / Make private in the Lock menu. Strangers never do. Not for private chests.");

        _enableDesignate = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableDesignate",
            true,
            "A ward member must press Enable LockSmith in the Lock menu before a piece gets the other options. Off = every eligible door and chest is LockSmith-ready straight away.");

        _enableManagedAccess = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableManagedAccess",
            true,
            "Master switch for managed (dynamic) access on placed doors and chests: the Lock menu, public/private, Join and guests, Team chests. Off = LockSmith leaves placed pieces alone (use EnablePieceMode for always-public Hammer pieces). Was EnableKeyMode; the old value carries over.");
        MigrateRenamedBool(config, SectionFeatures, "EnableKeyMode", _enableManagedAccess);

        _enablePieceMode = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnablePieceMode",
            false,
            "When true, discovers ward-locked chests/doors (vanilla + mods) and registers Hammer Public tab clones that are always open (no key / no ZDO). Restart required after changing.");

        _enableKeyPasses = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableKeyPasses",
            true,
            "Copy guests / Paste guests buttons in the Lock menu. Without the key, copied names last for the play session; with the key in hand they are saved on the key and Ctrl+C / Ctrl+V work.");

        _enableKeyExtras = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "EnableKeyExtras",
            false,
            "Advanced: extra tools in the key's inventory menu (Shift+Right-click): Relabel, Grab nearby person, Clear names, Clone key. Needs UseKey. Names on a key never grant access by themselves.");

        _useKey = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "UseKey",
            true,
            "(No effect for now: the key is temporarily removed and LockSmith runs in simple mode.) Advanced mode. The key is a physical item that gives finer control: holding it and pressing E opens the Lock menu, its hover shows guest names, copied guests are saved on the key (Ctrl+C / Ctrl+V), and RequireKeyForSetup / EnableKeyExtras apply. Off = simple mode: the key is never added to the game, hovers stay minimal, and everything goes through AltPlace+E. Restart required; keys already in inventories disappear while it is off.");

        _enableAddNearby = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableAddNearby",
            true,
            "Lock menu button that adds the closest player (within 5m) straight onto the guest list, so owners don't need Join for someone standing right there. Works with or without the key.");

        _requireKeyForSetup = Sync.BindSynced(
            config,
            SectionKey,
            DisplayKey,
            "RequireKeyForSetup",
            false,
            "Advanced: owners must hold the key for Enable LockSmith / Join / Personal-Team / Paste / Remove. Make public/private and Leave still work without it. Needs UseKey. Off (default) = everything works from AltPlace+E.");

        _requireActiveWard = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "RequireActiveWard",
            true,
            "When true (default), LockSmith cannot manage or toggle normal chests/doors unless they are inside an enabled ward — including already-managed pieces. Personal/Team private-family chests are exempt. Doors always need the ward up.");

        _publicPieceAllowList = Sync.BindSynced(
            config,
            SectionPieceMode,
            "PieceMode",
            "PublicPieceAllowList",
            "",
            "Comma-separated donor prefab names to force-clone into the Public hammer tab (in addition to auto-discovery). Restart required. Still skips private-family and already-public donors.");

        _publicPieceDenyList = Sync.BindSynced(
            config,
            SectionPieceMode,
            "PieceMode",
            "PublicPieceDenyList",
            "",
            "Comma-separated donor prefab names never cloned (escape hatch for broken mod pieces). Restart required.");

        _publicPieceNameSuffix = Sync.BindSynced(
            config,
            SectionPieceMode,
            "PieceMode",
            "PublicPieceNameSuffix",
            true,
            "When true (default), public clones show a localized (public) suffix on hammer and hover names. When false, keep the donor display name (still listed under the Public tab). Restart required.");

        _publicPieceHoverText = Sync.BindSynced(
            config,
            SectionPieceMode,
            "PieceMode",
            "PublicPieceHoverText",
            "[Public]",
            "Hover line on placed Public hammer doors and chests. Plain text, no color. Default [Public]. Empty uses [Public].");

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
            LockSmithLocalization.DefaultKeyDescription,
            "Tooltip description for the craftable key.");

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

        Sync.AddLockingConfigEntry(_lockSyncedConfig);
        Sync.FinalizeBinding(log, ExpectedSyncedEntryCount, () => LockSyncedConfig);
    }

    /// <summary>Carry a renamed bool's saved value over from its old key (one-time, then drop the orphan).</summary>
    private static void MigrateRenamedBool(ConfigFile config, string section, string oldKey, ConfigEntry<bool> entry)
    {
        try
        {
            var orphans = AccessTools.Property(typeof(ConfigFile), "OrphanedEntries")?.GetValue(config)
                as Dictionary<ConfigDefinition, string>;
            var old = new ConfigDefinition(section, oldKey);
            if (orphans == null || !orphans.TryGetValue(old, out var raw))
                return;

            if (bool.TryParse(raw, out var value))
                entry.Value = value;
            orphans.Remove(old);
            config.Save();
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Config migrate {oldKey} skipped: {ex.Message}");
        }
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

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
    /// <summary>Admin lock + feature entries. Bump when adding synced binds.</summary>
    public const int ExpectedSyncedEntryCount = 25;

    private const string SectionAdmin = "01 Admin";
    private const string SectionFeatures = "02 Features";
    private const string SectionPieceMode = "05 PieceMode";
    private const string SectionDisplay = "04 Display";

    private const string DisplayAdmin = "Admin";
    private const string DisplayFeatures = "Features";
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
    private static ConfigEntry<bool> _enableAddNearby = null!;
    private static ConfigEntry<bool> _requireActiveWard = null!;
    private static ConfigEntry<string> _publicPieceAllowList = null!;
    private static ConfigEntry<string> _publicPieceDenyList = null!;
    private static ConfigEntry<bool> _publicPieceNameSuffix = null!;
    private static ConfigEntry<string> _publicPieceHoverText = null!;
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
    /// <summary>
    /// </summary>
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

        _enableAddNearby = Sync.BindSynced(
            config,
            SectionFeatures,
            DisplayFeatures,
            "EnableAddNearby",
            true,
            "Lock menu button that adds the closest player (within 5m) straight onto the guest list, so owners don't need Join for someone standing right there.  Works from AltPlace+E.");

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

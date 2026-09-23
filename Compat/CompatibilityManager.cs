using System;
using System.Collections.Generic;
using DrakeModsLibs.Compat;
using HarmonyLib;
using LockSmith.Compat.ArcaneWard;
using LockSmith.Compat.DevCommands;
using LockSmith.Compat.ProtectiveWards;
using LockSmith.Compat.Vanilla;
using LockSmith.Compat.WardIsLove;
using UnityEngine;

namespace LockSmith.Compat;

/// <summary>
/// LockSmith ward compatibility facade over a per-plugin <see cref="CompatHost"/>.
/// Domain queries (ward coverage) stay here; register/priority/patches are Libs.
/// </summary>
public static class CompatibilityManager
{
    private static CompatHost? _host;

    /// <summary>
    /// SoftDependency GUIDs for known optional plugins (compile-time BepInEx attributes).
    /// SoftDependency never blocks load when the mod is missing — only helps load order.
    /// </summary>
    public static class SoftGuids
    {
        public const string WardIsLove = WardIsLoveModule.PluginGuid;
        public const string ProtectiveWards = ProtectiveWardsModule.PluginGuid;
        public const string ArcaneWard = ArcaneWardModule.PluginGuid;
        public const string DevCommands = DevCommandsModule.PluginGuid;
    }

    private static CompatHost Host =>
        _host ??= CompatHosts.GetOrCreate(API.LockSmithCompatApi.LockSmithGuid, LockSmith.Log);

    /// <summary>True after built-in scan + initial patch pass.</summary>
    public static bool IsInitialized => Host.IsInitialized;

    /// <summary>Raised once after <see cref="Initialize"/> finishes.</summary>
    public static event Action? Initialized
    {
        add => Host.Initialized += value;
        remove => Host.Initialized -= value;
    }

    /// <summary>
    /// Register built-in ward modules, then initialize the Libs host (vanilla patches first).
    /// Call once from plugin Awake after <c>PatchAll</c>.
    /// </summary>
    public static void Initialize(Harmony harmony)
    {
        if (harmony == null)
            return;

        // Present modules only. Mutual exclusivity among foreign mods is their problem.
        Host.Register(new VanillaPrivateAreaModule());
        Host.Register(new WardIsLoveModule());
        Host.Register(new ProtectiveWardsModule());
        Host.Register(new ArcaneWardModule());
        Host.Register(new DevCommandsModule());
        Host.Initialize(harmony);
    }

    /// <summary>Register a module (built-in or third-party) on LockSmith's host.</summary>
    public static bool Register(ICompatModule module) => Host.Register(module);

    /// <summary>One-liner: re-apply soft patches on LockSmith's host.</summary>
    public static void ApplyAllPatches(Harmony harmony) => Host.ApplyAllPatches(harmony);

    /// <summary>OR across active ward modules — any enabled covering ward.</summary>
    public static bool IsInsideEnabledWard(Vector3 position)
    {
        foreach (var module in Host.GetActiveModulesOfType<IWardCompatModule>())
        {
            try
            {
                if (module.IsInsideEnabledWard(position))
                    return true;
            }
            catch (Exception ex)
            {
                LockSmith.Log?.LogDebug($"Compat {module.Id} IsInsideEnabledWard: {ex.Message}");
            }
        }

        return false;
    }

    /// <summary>
    /// Highest-priority covering ward module wins (mods over vanilla when both apply).
    /// If nothing covers → unrestricted.
    /// </summary>
    public static bool HasLocalWardAccess(Vector3 position, bool flash = false) =>
        AggregateAccess(position, flash, playerId: null);

    /// <summary>Same as <see cref="HasLocalWardAccess"/> for a specific player id.</summary>
    public static bool HasWardAccessForPlayer(Vector3 position, long playerId, bool flash = false) =>
        AggregateAccess(position, flash, playerId);

    public static bool HasActiveModule(string id) => Host.HasActiveModule(id);

    private static bool AggregateAccess(Vector3 position, bool flash, long? playerId)
    {
        var modules = Host.GetActiveModulesOfType<IWardCompatModule>();
        modules.Sort((a, b) => b.Priority.CompareTo(a.Priority));

        foreach (var module in modules)
        {
            try
            {
                var kind = playerId.HasValue
                    ? module.QueryPlayerAccess(playerId.Value, position, flash)
                    : module.QueryLocalAccess(position, flash);

                if (kind == WardCoverageKind.Unrelated)
                    continue;

                return kind == WardCoverageKind.Allowed;
            }
            catch (Exception ex)
            {
                LockSmith.Log?.LogDebug($"Compat {module.Id} access query: {ex.Message}");
            }
        }

        return true;
    }
}

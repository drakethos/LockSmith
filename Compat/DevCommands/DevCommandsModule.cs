using System;
using BepInEx.Bootstrap;
using DrakeModsLibs.Compat;
using HarmonyLib;
using UnityEngine;

namespace LockSmith.Compat.DevCommands;

/// <summary>
/// Soft DevCommands detection. Cheats / admin tools can open through wards and fight
/// Interact prefixes — registered so we have one place to extend when behavior conflicts appear.
/// Does not add ward coverage by itself; returns <see cref="WardCoverageKind.Unrelated"/>
/// until a concrete bypass rule is wired.
/// </summary>
internal sealed class DevCommandsModule : IWardCompatModule
{
    public const string ModuleId = "DevCommands";

    /// <summary>JereKuusela DevCommands (Thunderstore). SoftDependency only.</summary>
    public const string PluginGuid = "devcommands";

    public string Id => ModuleId;
    public string? SoftDependencyGuid => PluginGuid;
    public int Priority => CompatPriority.BuiltIn;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        IsActive = IsPluginLoaded(PluginGuid)
                   || AccessTools.TypeByName("DevCommands.DevCommands") != null
                   || AccessTools.TypeByName("DevCommands") != null;
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        // Hook point: when we learn a specific DevCommands Interact/ward conflict,
        // soft-patch from here (same pattern as WardIsLoveModule).
        if (IsActive)
            LockSmith.Log?.LogDebug("Compat DevCommands: present (no soft patches yet).");
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        _ = position;
        return false;
    }

    public WardCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        _ = position;
        _ = flash;
        // Future: if DevCommands exposes a reliable "ignore wards / god open" flag,
        // return Allowed here so LockSmith key/access paths stay consistent.
        return WardCoverageKind.Unrelated;
    }

    public WardCoverageKind QueryPlayerAccess(long playerId, Vector3 position, bool flash)
    {
        _ = playerId;
        _ = position;
        _ = flash;
        return WardCoverageKind.Unrelated;
    }

    private static bool IsPluginLoaded(string guid)
    {
        if (string.IsNullOrEmpty(guid))
            return false;

        try
        {
            return Chainloader.PluginInfos != null
                   && Chainloader.PluginInfos.ContainsKey(guid);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat DevCommands Chainloader probe: {ex.Message}");
            return false;
        }
    }
}

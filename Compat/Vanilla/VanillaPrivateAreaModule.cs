using System;
using System.Collections.Generic;
using System.Reflection;
using DrakeModsLibs.Compat;
using HarmonyLib;
using UnityEngine;

namespace LockSmith.Compat.Vanilla;

/// <summary>Baseline vanilla <see cref="PrivateArea"/> stack — always active.</summary>
internal sealed class VanillaPrivateAreaModule : IWardCompatModule
{
    public const string ModuleId = "VanillaPrivateArea";

    private FieldInfo? _allAreasField;
    private MethodInfo? _isEnabled;
    private MethodInfo? _isInside;
    private MethodInfo? _isPermitted;
    private bool _resolved;

    public string Id => ModuleId;
    public string? SoftDependencyGuid => null;
    public int Priority => CompatPriority.Vanilla;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        EnsureResolved();
        IsActive = _allAreasField != null && _isEnabled != null && _isInside != null && _isPermitted != null;
        if (!IsActive)
            LockSmith.Log?.LogError("Compat VanillaPrivateArea: failed to resolve PrivateArea members.");
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        // Vanilla needs no soft patches from LockSmith.
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        EnsureResolved();
        var areas = GetAllAreas();
        if (areas == null || areas.Count == 0)
            return false;

        foreach (var area in areas)
        {
            if (area == null)
                continue;
            if (!InvokeBool(_isEnabled, area))
                continue;
            if (InvokeBool(_isInside, area, position, 0f))
                return true;
        }

        return false;
    }

    public WardCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsInsideEnabledWard(position))
            return WardCoverageKind.Unrelated;

        // Covered by at least one enabled PrivateArea — defer to vanilla CheckAccess.
        return PrivateArea.CheckAccess(position, 0f, flash, wardCheck: false)
            ? WardCoverageKind.Allowed
            : WardCoverageKind.Denied;
    }

    public WardCoverageKind QueryPlayerAccess(long playerId, Vector3 position, bool flash)
    {
        _ = flash;
        EnsureResolved();

        var areas = GetAllAreas();
        if (areas == null || areas.Count == 0)
            return WardCoverageKind.Unrelated;

        var insideAny = false;
        foreach (var area in areas)
        {
            if (area == null)
                continue;
            if (!InvokeBool(_isEnabled, area))
                continue;
            if (!InvokeBool(_isInside, area, position, 0f))
                continue;

            insideAny = true;

            var piece = area.GetComponent<Piece>();
            if (piece != null && piece.GetCreator() == playerId)
                return WardCoverageKind.Allowed;

            if (InvokeBool(_isPermitted, area, playerId))
                return WardCoverageKind.Allowed;
        }

        return insideAny ? WardCoverageKind.Denied : WardCoverageKind.Unrelated;
    }

    private void EnsureResolved()
    {
        if (_resolved)
            return;

        _resolved = true;
        var t = typeof(PrivateArea);
        _allAreasField = AccessTools.Field(t, "m_allAreas");
        _isEnabled = AccessTools.Method(t, "IsEnabled");
        _isInside = AccessTools.Method(t, "IsInside", new[] { typeof(Vector3), typeof(float) });
        _isPermitted = AccessTools.Method(t, "IsPermitted", new[] { typeof(long) });
    }

    private List<PrivateArea>? GetAllAreas()
    {
        EnsureResolved();
        if (_allAreasField == null)
            return null;

        try
        {
            return _allAreasField.GetValue(null) as List<PrivateArea>;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat VanillaPrivateArea m_allAreas: {ex.Message}");
            return null;
        }
    }

    private static bool InvokeBool(MethodInfo? method, object target, params object[] args)
    {
        if (method == null || target == null)
            return false;

        try
        {
            return method.Invoke(target, args) is true;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat VanillaPrivateArea {method.Name}: {ex.Message}");
            return false;
        }
    }
}

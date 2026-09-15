using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Ward helpers. Most PrivateArea APIs are private at runtime — call via reflection.
/// Creator is allowed even when not on the permitted list (same as vanilla HaveLocalAccess).
/// </summary>
public static class WardAccess
{
    private static FieldInfo? _allAreasField;
    private static MethodInfo? _isEnabled;
    private static MethodInfo? _isInside;
    private static MethodInfo? _isPermitted;
    private static bool _resolved;

    public static bool HasLocalWardAccess(Vector3 position) =>
        PrivateArea.CheckAccess(position, 0f, flash: false, wardCheck: false);

    public static bool HasWardAccessForPlayer(Vector3 position, long playerId)
    {
        EnsureResolved();

        var areas = GetAllAreas();
        if (areas == null || areas.Count == 0)
            return true;

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

            // Match vanilla HaveLocalAccess: creator OR permitted list.
            var piece = area.GetComponent<Piece>();
            if (piece != null && piece.GetCreator() == playerId)
                return true;

            if (InvokeBool(_isPermitted, area, playerId))
                return true;
        }

        return !insideAny;
    }

    private static void EnsureResolved()
    {
        if (_resolved)
            return;

        _resolved = true;
        var t = typeof(PrivateArea);
        _allAreasField = AccessTools.Field(t, "m_allAreas");
        _isEnabled = AccessTools.Method(t, "IsEnabled");
        _isInside = AccessTools.Method(t, "IsInside", new[] { typeof(Vector3), typeof(float) });
        _isPermitted = AccessTools.Method(t, "IsPermitted", new[] { typeof(long) });

        if (_allAreasField == null || _isEnabled == null || _isInside == null || _isPermitted == null)
            LockSmith.Log?.LogError("LockSmith failed to resolve PrivateArea reflection members for ward checks.");
    }

    private static List<PrivateArea>? GetAllAreas()
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
            LockSmith.Log?.LogDebug($"m_allAreas read failed: {ex.Message}");
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
            LockSmith.Log?.LogDebug($"PrivateArea invoke {method.Name} failed: {ex.Message}");
            return false;
        }
    }
}

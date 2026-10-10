using System;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Reads vanilla <see cref="PrivateArea"/> members that are private in the live game. The compile references show them as public,
/// so calling them directly compiles but throws FieldAccessException/MethodAccessException in game. Everything goes through here.
/// </summary>
internal static class PrivateAreaAccess
{
    static readonly FieldInfo? AllAreasField = AccessTools.Field(typeof(PrivateArea), "m_allAreas");
    static readonly MethodInfo? IsEnabledMethod = AccessTools.Method(typeof(PrivateArea), "IsEnabled");
    static readonly MethodInfo? IsPermittedMethod = AccessTools.Method(typeof(PrivateArea), "IsPermitted", new[] { typeof(long) });
    static readonly MethodInfo? GetPermittedMethod = AccessTools.Method(typeof(PrivateArea), "GetPermittedPlayers");
    static readonly MethodInfo? IsInsideMethod = AccessTools.Method(typeof(PrivateArea), "IsInside", new[] { typeof(Vector3), typeof(float) });
    static readonly MethodInfo? RemovePermittedMethod = AccessTools.Method(typeof(PrivateArea), "RemovePermitted", new[] { typeof(long) });
    static readonly MethodInfo? SetEnabledMethod = AccessTools.Method(typeof(PrivateArea), "SetEnabled", new[] { typeof(bool) });

    public static List<PrivateArea> AllAreas()
    {
        try
        {
            return AllAreasField?.GetValue(null) as List<PrivateArea> ?? new List<PrivateArea>();
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"PrivateArea.m_allAreas: {ex.Message}");
            return new List<PrivateArea>();
        }
    }

    public static bool IsEnabled(PrivateArea area) => Invoke(IsEnabledMethod, area) is true;

    public static bool IsPermitted(PrivateArea area, long playerId) => Invoke(IsPermittedMethod, area, playerId) is true;

    public static bool IsInside(PrivateArea area, Vector3 point, float radius) => Invoke(IsInsideMethod, area, point, radius) is true;

    public static List<KeyValuePair<long, string>> GetPermittedPlayers(PrivateArea area) =>
        Invoke(GetPermittedMethod, area) as List<KeyValuePair<long, string>> ?? new List<KeyValuePair<long, string>>();

    public static void RemovePermitted(PrivateArea area, long playerId) => Invoke(RemovePermittedMethod, area, playerId);

    public static void SetEnabled(PrivateArea area, bool enabled) => Invoke(SetEnabledMethod, area, enabled);

    static object? Invoke(MethodInfo? method, PrivateArea area, params object[] args)
    {
        if (method == null || area == null)
            return null;
        try
        {
            return method.Invoke(area, args);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"PrivateArea.{method.Name}: {ex.InnerException?.Message ?? ex.Message}");
            return null;
        }
    }
}

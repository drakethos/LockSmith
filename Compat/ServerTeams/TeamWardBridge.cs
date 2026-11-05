using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LockSmith.Compat.ServerTeams;

/// <summary>
/// Soft link to DrakesServerTeams. LockSmith does not reference that mod; if it is missing every call says so and the
/// Team Ward menu stays closed. The game types live in one place so a Teams update breaks here first.
/// </summary>
internal static class TeamWardBridge
{
    const string Api = "DrakesServerTeams.TeamsAPI";
    const string Ward = "DrakesServerTeams.TeamWard";

    static bool _looked;
    static MethodInfo? _isTeamWard;
    static MethodInfo? _localIsAdmin;
    static MethodInfo? _teamOf;
    static MethodInfo? _getTeam;
    static MethodInfo? _getTeams;
    static MethodInfo? _protects;
    static MethodInfo? _runAdmin;
    static Type? _kind;
    static FieldInfo? _teamId;
    static FieldInfo? _teamName;

    public static bool IsTeamWard(PrivateArea? area)
    {
        if (!area)
            return false;
        Ensure();
        try
        {
            return _isTeamWard?.Invoke(null, new object[] { area }) is true;
        }
        catch
        {
            return false;
        }
    }

    public static bool LocalIsAdmin()
    {
        Ensure();
        try
        {
            return _localIsAdmin?.Invoke(null, null) is true;
        }
        catch
        {
            return false;
        }
    }

    public static string TeamName(PrivateArea? area)
    {
        var id = TeamId(area);
        if (string.IsNullOrEmpty(id))
            return "No team";
        Ensure();
        try
        {
            var team = _getTeam?.Invoke(null, new object?[] { id });
            var name = team != null ? _teamName?.GetValue(team) as string : null;
            return string.IsNullOrEmpty(name) ? id! : name!;
        }
        catch
        {
            return id!;
        }
    }

    public static string? TeamId(PrivateArea? area)
    {
        if (!area)
            return null;
        Ensure();
        try
        {
            var id = _teamOf?.Invoke(null, new object[] { area }) as string;
            return string.IsNullOrEmpty(id) ? null : id;
        }
        catch
        {
            return null;
        }
    }

    public static bool Protects(PrivateArea? area, string kind)
    {
        if (!area || _protects == null || _kind == null)
        {
            Ensure();
            if (!area || _protects == null || _kind == null)
                return false;
        }

        try
        {
            var value = Enum.Parse(_kind, kind);
            return _protects.Invoke(null, new[] { (object)area, value }) is true;
        }
        catch
        {
            return false;
        }
    }

    public static void RunAdmin(string line)
    {
        if (string.IsNullOrEmpty(line))
            return;
        Ensure();
        try
        {
            _runAdmin?.Invoke(null, new object[] { line });
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"Team Ward command '{line}' failed: {ex.Message}");
        }
    }

    public static List<(string id, string name)> Teams()
    {
        var result = new List<(string, string)>();
        Ensure();
        if (_getTeams == null || _teamId == null || _teamName == null)
            return result;
        try
        {
            if (_getTeams.Invoke(null, null) is not IEnumerable list)
                return result;
            foreach (var team in list)
            {
                if (team == null)
                    continue;
                var id = _teamId.GetValue(team) as string ?? "";
                if (id.Length == 0)
                    continue;
                var name = _teamName.GetValue(team) as string;
                result.Add((id, string.IsNullOrEmpty(name) ? id : name!));
            }
        }
        catch
        {
            result.Clear();
        }

        return result;
    }

    static void Ensure()
    {
        if (_looked)
            return;
        _looked = true;
        _isTeamWard = AccessTools.Method(Api + ":IsTeamWard");
        _localIsAdmin = AccessTools.Method(Ward + ":LocalIsAdmin");
        _teamOf = AccessTools.Method(Api + ":GetTeamWardTeam");
        _getTeam = AccessTools.Method(Api + ":GetTeam");
        _getTeams = AccessTools.Method(Api + ":GetTeams");
        _protects = AccessTools.Method(Ward + ":Protects");
        _runAdmin = AccessTools.Method("DrakesServerTeams.TeamsNet:RunAdmin");
        _kind = AccessTools.TypeByName("DrakesServerTeams.WardKind");
        var def = AccessTools.TypeByName("DrakesServerTeams.TeamDefinition");
        _teamId = def?.GetField("Id");
        _teamName = def?.GetField("DisplayName");
    }
}

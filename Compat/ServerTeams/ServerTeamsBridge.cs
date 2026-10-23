using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace LockSmith.Compat.ServerTeams;

/// <summary>
/// Soft link to DrakesServerTeams (<c>com.drakemods.serverteams</c>). Everything goes through reflection, so LockSmith
/// neither references nor requires that DLL; without it <see cref="IsPresent"/> is false and nothing here runs.
/// </summary>
internal static class ServerTeamsBridge
{
    public const string Guid = "com.drakemods.serverteams";

    static bool _resolved;
    static MethodInfo? _sameTeamAsCreator;
    static MethodInfo? _teamIdOfPlayer;
    static MethodInfo? _getTeam;
    static FieldInfo? _displayName;

    public static bool IsPresent
    {
        get
        {
            Resolve();
            return _sameTeamAsCreator != null;
        }
    }

    /// <summary>True when <paramref name="player"/> is on the team the piece was built under.</summary>
    public static bool IsSameTeamAsCreator(Player? player, ZNetView? piece)
    {
        if (player == null || piece == null)
            return false;
        Resolve();
        return Call(_sameTeamAsCreator, player, piece) is true;
    }

    /// <summary>Display name of the player's team, or null (no team, or Teams missing).</summary>
    public static string? TeamNameOf(Player? player)
    {
        if (player == null)
            return null;
        Resolve();
        var id = Call(_teamIdOfPlayer, player) as string;
        if (string.IsNullOrEmpty(id))
            return null;
        var team = Call(_getTeam, id!);
        return team != null ? _displayName?.GetValue(team) as string ?? id : id;
    }

    static object? Call(MethodInfo? method, params object[] args)
    {
        if (method == null)
            return null;
        try
        {
            return method.Invoke(null, args);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"ServerTeams bridge {method.Name}: {ex.InnerException?.Message ?? ex.Message}");
            return null;
        }
    }

    static void Resolve()
    {
        if (_resolved)
            return;
        _resolved = true;
        try
        {
            if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
                return;
            var api = info.Instance.GetType().Assembly.GetType("DrakesServerTeams.TeamsAPI");
            if (api == null)
                return;
            _sameTeamAsCreator = api.GetMethod("IsSameTeamAsCreator", new[] { typeof(Player), typeof(ZNetView) });
            _teamIdOfPlayer = api.GetMethod("GetTeamId", new[] { typeof(Player) });
            _getTeam = api.GetMethod("GetTeam", new[] { typeof(string) });
            _displayName = _getTeam?.ReturnType.GetField("DisplayName");
            LockSmith.Log?.LogInfo(_sameTeamAsCreator != null
                ? "DrakesServerTeams found: Team access option available."
                : "DrakesServerTeams found but its API did not match; Team access option off.");
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"ServerTeams bridge resolve failed: {ex.Message}");
        }
    }
}

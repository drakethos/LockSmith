using System;
using System.Reflection;
using BepInEx.Bootstrap;

namespace LockSmith.Compat.ServerTeams;

/// <summary>
/// Soft dependency on DrakesServerTeams, by reflection only: LockSmith neither references nor requires it.
/// Every call into Teams goes through here. Without Teams everything answers "no team".
/// </summary>
internal static class ServerTeamsBridge
{
    public const string Guid = "com.drakemods.serverteams";

    static Func<ZNetView?, string?>? _getPieceTeamId;
    static Func<long, ZNetView?, bool>? _isOnPieceTeam;
    static MethodInfo? _getTeam;
    static FieldInfo? _displayName;

    /// <summary>Teams is installed and its API resolved.</summary>
    public static bool Available { get; private set; }

    /// <summary>Find Teams and turn on its piece teams. Call once from Awake (Teams loads first: soft dependency).</summary>
    public static void Initialize()
    {
        if (!Chainloader.PluginInfos.TryGetValue(Guid, out var info) || info.Instance == null)
            return;
        try
        {
            var api = info.Instance.GetType().Assembly.GetType("DrakesServerTeams.TeamsAPI", throwOnError: true)!;
            _getPieceTeamId = (Func<ZNetView?, string?>)Delegate.CreateDelegate(typeof(Func<ZNetView?, string?>),
                api.GetMethod("GetPieceTeamId", new[] { typeof(ZNetView) })!);
            _isOnPieceTeam = (Func<long, ZNetView?, bool>)Delegate.CreateDelegate(typeof(Func<long, ZNetView?, bool>),
                api.GetMethod("IsOnPieceTeam", new[] { typeof(long), typeof(ZNetView) })!);
            _getTeam = api.GetMethod("GetTeam", new[] { typeof(string) });
            _displayName = api.Assembly.GetType("DrakesServerTeams.TeamDefinition")?.GetField("DisplayName");
            api.GetMethod("UsePieceTeams", Type.EmptyTypes)!.Invoke(null, null);
            Available = true;
            LockSmith.Log?.LogInfo("DrakesServerTeams found: team access on wards, chests and doors.");
        }
        catch (Exception ex)
        {
            Available = false;
            LockSmith.Log?.LogWarning($"DrakesServerTeams found but its API did not match ({ex.GetType().Name}: {ex.Message}). Team access is off.");
        }
    }

    public static string? GetPieceTeamId(ZNetView? view)
    {
        if (!Available || _getPieceTeamId == null)
            return null;
        try { return _getPieceTeamId(view); }
        catch { return null; }
    }

    public static bool IsOnPieceTeam(long playerId, ZNetView? view)
    {
        if (!Available || _isOnPieceTeam == null)
            return false;
        try { return _isOnPieceTeam(playerId, view); }
        catch { return false; }
    }

    /// <summary>The team's display name, or the id when Teams does not know it here.</summary>
    public static string TeamName(string teamId)
    {
        try
        {
            var team = _getTeam?.Invoke(null, new object[] { teamId });
            if (team != null && _displayName?.GetValue(team) is string name && name.Length > 0)
                return name;
        }
        catch
        {
            // fall through to the id
        }

        return teamId;
    }
}

using LockSmith.Compat.ServerTeams;

namespace LockSmith.Access;

/// <summary>
/// "Team" access on a managed piece: players on the same server team as whoever built it (DrakesServerTeams) get in
/// like a guest, without being on a list. Off by default per piece; only offered when DrakesServerTeams is installed.
/// The check compares the player's team with the team stamped on the piece when it was placed, so no player ids are involved.
/// </summary>
public static class TeamAccess
{
    private static readonly int AllowTeamHash = GameHookTargets.ZdoAllowTeam.GetStableHashCode();

    /// <summary>The option can be offered at all (Teams installed and guests enabled).</summary>
    public static bool Available => PieceGuestService.GuestsEnabled && ServerTeamsBridge.IsPresent;

    public static bool IsOn(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;
        var zdo = nview.GetZDO();
        return zdo != null && zdo.GetInt(AllowTeamHash, 0) == 1;
    }

    /// <summary>The local player gets in through Team access on this piece.</summary>
    public static bool AllowsLocal(ZNetView? nview)
    {
        if (!Available || !IsOn(nview) || !PieceAccessState.IsManaged(nview))
            return false;
        return ServerTeamsBridge.IsSameTeamAsCreator(Player.m_localPlayer, nview);
    }

    public static string? LocalTeamName() => ServerTeamsBridge.TeamNameOf(Player.m_localPlayer);

    public static void RegisterRpc(ZNetView nview)
    {
        if (nview == null || !nview.IsValid())
            return;
        nview.Unregister(GameHookTargets.RpcSetAllowTeam);
        nview.Register<int, long>(GameHookTargets.RpcSetAllowTeam, (long sender, int flag, long playerId) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcSetAllowTeam, sender, playerId))
                ApplySet(nview, flag == 1, playerId);
        });
    }

    public static void RequestSet(ZNetView nview, bool on, long playerId)
    {
        PieceRpc.Request(
            nview,
            GameHookTargets.RpcSetAllowTeam,
            new object[] { on ? 1 : 0, playerId },
            view => ApplySet(view, on, playerId),
            view => IsOn(view) == on,
            onFailed: PieceGuestAccess.ShowSyncFailed);
    }

    public static void ApplySet(ZNetView nview, bool on, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;
        if (!Available || !LockSmithConfig.EnableManagedAccess)
            return;
        // Same gate as Join admin: the owner or a ward member.
        if (!PieceGuestAccess.CanManageGuests(nview, playerId))
        {
            LockSmith.Log?.LogWarning($"Rejected Team access toggle for player {playerId} (not allowed to manage this piece).");
            return;
        }

        nview.GetZDO().Set(AllowTeamHash, on ? 1 : 0, false);
        PieceAccessState.MarkManaged(nview);
    }
}

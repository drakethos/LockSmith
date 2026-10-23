using LockSmith.Compat.ServerTeams;

namespace LockSmith.Access;

/// <summary>
/// Team access (needs DrakesServerTeams): a ward, chest or door the owner opened to their team lets in everyone on the
/// team the piece was built under. That team is stamped and signed by the Teams server, never by players; LockSmith only
/// stores the owner's on/off choice (ZDO <c>locksmith_team_access</c>).
/// </summary>
public static class TeamAccess
{
    static readonly int FlagHash = GameHookTargets.ZdoTeamAccess.GetStableHashCode();
    static readonly int CreatorHash = "creator".GetStableHashCode();

    /// <summary>Teams is installed and the server allows team access.</summary>
    public static bool Available => LockSmithConfig.EnableTeamAccess && ServerTeamsBridge.Available;

    public static bool IsOn(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;
        var flag = nview.GetZDO().GetInt(FlagHash, -1);
        return flag == -1 ? LockSmithConfig.TeamAccessByDefault : flag == 1;
    }

    /// <summary>The team the piece was built under, or null.</summary>
    public static string? PieceTeam(ZNetView? nview) => Available ? ServerTeamsBridge.GetPieceTeamId(nview) : null;

    public static bool HasAccess(ZNetView? nview, long playerId) =>
        Available && playerId != 0L && IsOn(nview) && ServerTeamsBridge.IsOnPieceTeam(playerId, nview);

    public static bool HasLocalAccess(ZNetView? nview)
    {
        var local = Player.m_localPlayer;
        return local != null && HasAccess(nview, local.GetPlayerID());
    }

    /// <summary>Only the builder switches team access, and only for a piece that has a team.</summary>
    public static bool CanSet(ZNetView? nview, long playerId) =>
        Available && nview != null && nview.IsValid() && playerId != 0L
        && nview.GetZDO().GetLong(CreatorHash, 0L) == playerId
        && PieceTeam(nview) != null;

    /// <summary>Hover line such as "[Team: Red]" while team access is on; empty otherwise.</summary>
    public static string HoverSuffix(ZNetView? nview)
    {
        if (!IsOn(nview))
            return string.Empty;
        var team = PieceTeam(nview);
        return team == null ? string.Empty : "\n[Team: " + ServerTeamsBridge.TeamName(team) + "]";
    }

    public static void RequestSet(ZNetView nview, bool on, long playerId)
    {
        PieceRpc.Request(
            nview,
            GameHookTargets.RpcSetTeamAccess,
            new object[] { on ? 1 : 0, playerId },
            view => Apply(view, on, playerId),
            view => IsOn(view) == on,
            onFailed: PieceGuestAccess.ShowSyncFailed);
    }

    public static void RegisterRpc(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return;
        nview.Unregister(GameHookTargets.RpcSetTeamAccess);
        nview.Register<int, long>(GameHookTargets.RpcSetTeamAccess, (long sender, int flag, long playerId) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcSetTeamAccess, sender, playerId))
                Apply(nview, flag == 1, playerId);
        });
    }

    /// <summary>Owner side: checks the rule again before writing.</summary>
    static void Apply(ZNetView nview, bool on, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;
        if (!CanSet(nview, playerId))
        {
            LockSmith.Log?.LogWarning("Rejected team access change (not the builder, or the piece has no team).");
            return;
        }

        nview.GetZDO().Set(FlagHash, on ? 1 : 0);
    }
}

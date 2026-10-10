using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>Who a ward member is. Higher number = more say. Owner is the ward's creator.</summary>
public enum WardLevel
{
    None = 0,
    Guest = 1,
    Team = 2,
    Owner = 3,
}

/// <summary>
/// Ward management on top of vanilla <see cref="PrivateArea"/>: the permitted list is vanilla's; roles, the switch level,
/// per-person remove grants and the guest-off flag live on the ward's ZDO. Only the ZDO owner writes; requests come in as RPCs.
/// </summary>
public static class WardService
{
    public const int SwitchOwnerOnly = 3;
    public const int SwitchOwnerAndTeam = 2;
    public const int SwitchEveryone = 1;

    static readonly int RolesHash = "locksmith_ward_roles".GetStableHashCode();
    static readonly int RemoversHash = "locksmith_ward_removers".GetStableHashCode();
    static readonly int SwitchHash = "locksmith_ward_switch_min".GetStableHashCode();
    static readonly int GuestOffHash = "locksmith_ward_guest_off".GetStableHashCode();

    // ---- lookup -----------------------------------------------------------------------------

    public static PrivateArea? AreaOf(ZNetView? nview) =>
        nview != null && nview.IsValid() ? nview.GetComponent<PrivateArea>() : null;

    public static long CreatorOf(PrivateArea area)
    {
        var piece = area.GetComponent<Piece>();
        return piece != null ? piece.GetCreator() : 0L;
    }

    /// <summary>The ward level of <paramref name="playerId"/> on this ward (owner, team, guest, or none).</summary>
    public static WardLevel LevelOf(PrivateArea area, long playerId)
    {
        if (area == null || playerId == 0L)
            return WardLevel.None;
        if (CreatorOf(area) == playerId)
            return WardLevel.Owner;
        if (!PrivateAreaAccess.IsPermitted(area, playerId))
            return WardLevel.None;
        return RolesOf(area.GetComponent<ZNetView>()).TryGetValue(playerId, out var role) ? role : WardLevel.Team;
    }

    /// <summary>Everyone on the ward list except the owner, with their level.</summary>
    public static List<(long Id, string Name, WardLevel Level)> Members(PrivateArea area)
    {
        var result = new List<(long, string, WardLevel)>();
        var creator = CreatorOf(area);
        foreach (var entry in PrivateAreaAccess.GetPermittedPlayers(area))
        {
            if (entry.Key == 0L || entry.Key == creator)
                continue;
            result.Add((entry.Key, entry.Value ?? string.Empty, LevelOf(area, entry.Key)));
        }

        return result;
    }

    public static Dictionary<long, WardLevel> RolesOf(ZNetView? nview)
    {
        var roles = new Dictionary<long, WardLevel>();
        var raw = Read(nview, RolesHash);
        foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var pipe = part.IndexOf('|');
            if (pipe < 0)
                continue;
            if (!long.TryParse(part.Substring(0, pipe), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                continue;
            roles[id] = part.Substring(pipe + 1) == "G" ? WardLevel.Guest : WardLevel.Team;
        }

        return roles;
    }

    public static bool IsRemover(ZNetView? nview, long playerId) =>
        playerId != 0L && IdList(Read(nview, RemoversHash)).Contains(playerId);

    /// <summary>Lowest level allowed to flip the ward on or off.</summary>
    public static int SwitchMinimum(ZNetView? nview)
    {
        var value = nview != null && nview.IsValid() ? nview.GetZDO()?.GetInt(SwitchHash, SwitchEveryone) : null;
        return value is >= 1 and <= 3 ? value.Value : SwitchEveryone;
    }

    public static bool GuestTurnedOff(ZNetView? nview) =>
        nview != null && nview.IsValid() && nview.GetZDO()?.GetInt(GuestOffHash, 0) == 1;

    /// <summary>True when any ward covering <paramref name="pos"/> was switched off by a guest. Nobody can opt in there.</summary>
    public static bool BlocksJoin(Vector3 pos)
    {
        var areas = PrivateAreaAccess.AllAreas();
        if (areas == null)
            return false;
        foreach (var area in areas)
        {
            if (area == null || !PrivateAreaAccess.IsInside(area, pos, 0f))
                continue;
            if (GuestTurnedOff(area.GetComponent<ZNetView>()))
                return true;
        }

        return false;
    }

    /// <summary>Wards around a point where <paramref name="playerId"/> has any level (on or off).</summary>
    public static List<PrivateArea> WardsFor(Vector3 pos, long playerId)
    {
        var result = new List<PrivateArea>();
        var areas = PrivateAreaAccess.AllAreas();
        if (areas == null)
            return result;
        foreach (var area in areas)
        {
            if (area == null || !PrivateAreaAccess.IsInside(area, pos, 0f))
                continue;
            if (LevelOf(area, playerId) != WardLevel.None)
                result.Add(area);
        }

        return result;
    }

    // ---- RPC registration ------------------------------------------------------------------

    public static void RegisterRpcs(ZNetView nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        nview.Unregister(GameHookTargets.RpcWardRemove);
        nview.Unregister(GameHookTargets.RpcWardSetRole);
        nview.Unregister(GameHookTargets.RpcWardSetEnabled);
        nview.Unregister(GameHookTargets.RpcWardSetSwitch);
        nview.Unregister(GameHookTargets.RpcWardSetRemover);

        nview.Register<long, long>(GameHookTargets.RpcWardRemove, (long sender, long target, long actor) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcWardRemove, sender, actor))
                ApplyRemove(nview, target, actor);
        });
        nview.Register<long, int, long>(GameHookTargets.RpcWardSetRole, (long sender, long target, int role, long actor) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcWardSetRole, sender, actor))
                ApplySetRole(nview, target, role, actor);
        });
        nview.Register<int, long>(GameHookTargets.RpcWardSetEnabled, (long sender, int on, long actor) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcWardSetEnabled, sender, actor))
                ApplySetEnabled(nview, on == 1, actor);
        });
        nview.Register<int, long>(GameHookTargets.RpcWardSetSwitch, (long sender, int level, long actor) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcWardSetSwitch, sender, actor))
                ApplySetSwitch(nview, level, actor);
        });
        nview.Register<long, int, long>(GameHookTargets.RpcWardSetRemover, (long sender, long target, int grant, long actor) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcWardSetRemover, sender, actor))
                ApplySetRemover(nview, target, grant == 1, actor);
        });
    }

    // ---- requests (any player) --------------------------------------------------------------

    public static void RequestRemove(ZNetView nview, long target, long actor, Action? onConfirmed = null)
    {
        PieceRpc.Request(nview, GameHookTargets.RpcWardRemove, new object[] { target, actor },
            view => ApplyRemove(view, target, actor),
            view => AreaOf(view) is { } area && !PrivateAreaAccess.IsPermitted(area, target),
            onConfirmed, PieceGuestAccess.ShowSyncFailed);
    }

    public static void RequestSetRole(ZNetView nview, long target, WardLevel role, long actor)
    {
        PieceRpc.Request(nview, GameHookTargets.RpcWardSetRole, new object[] { target, (int)role, actor },
            view => ApplySetRole(view, target, (int)role, actor),
            view => RolesOf(view).TryGetValue(target, out var now) ? now == role : role == WardLevel.Team,
            null, PieceGuestAccess.ShowSyncFailed);
    }

    public static void RequestSetEnabled(ZNetView nview, bool on, long actor)
    {
        PieceRpc.Request(nview, GameHookTargets.RpcWardSetEnabled, new object[] { on ? 1 : 0, actor },
            view => ApplySetEnabled(view, on, actor),
            view => AreaOf(view) is { } area && PrivateAreaAccess.IsEnabled(area) == on,
            null, PieceGuestAccess.ShowSyncFailed);
    }

    public static void RequestSetSwitch(ZNetView nview, int level, long actor)
    {
        PieceRpc.Request(nview, GameHookTargets.RpcWardSetSwitch, new object[] { level, actor },
            view => ApplySetSwitch(view, level, actor),
            view => SwitchMinimum(view) == level,
            null, PieceGuestAccess.ShowSyncFailed);
    }

    public static void RequestSetRemover(ZNetView nview, long target, bool grant, long actor)
    {
        PieceRpc.Request(nview, GameHookTargets.RpcWardSetRemover, new object[] { target, grant ? 1 : 0, actor },
            view => ApplySetRemover(view, target, grant, actor),
            view => IsRemover(view, target) == grant,
            null, PieceGuestAccess.ShowSyncFailed);
    }

    // ---- owner-side apply (checks the rules again) ------------------------------------------

    static void ApplyRemove(ZNetView nview, long target, long actor)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;
        var area = AreaOf(nview);
        if (area == null || target == 0L || target == CreatorOf(area))
            return;

        var actorLevel = LevelOf(area, actor);
        var targetLevel = LevelOf(area, target);
        if (targetLevel == WardLevel.None)
            return;

        // Owner removes anyone below owner. A remove grant only reaches people strictly below the grant holder's level.
        var allowed = actorLevel == WardLevel.Owner
                      || (IsRemover(nview, actor) && actorLevel > WardLevel.None && actorLevel > targetLevel);
        if (!allowed)
            return;

        PrivateAreaAccess.RemovePermitted(area, target);
        var roles = RolesOf(nview);
        roles.Remove(target);
        WriteRoles(nview, roles);
        var removers = IdList(Read(nview, RemoversHash));
        removers.Remove(target);
        WriteIds(nview, RemoversHash, removers);
        LockSmith.Log?.LogInfo($"Ward: {actor} removed {target} ({targetLevel}) from {nview.name}.");
    }

    static void ApplySetRole(ZNetView nview, long target, int role, long actor)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;
        var area = AreaOf(nview);
        if (area == null || target == 0L || target == CreatorOf(area) || LevelOf(area, actor) != WardLevel.Owner)
            return;
        if (!PrivateAreaAccess.IsPermitted(area, target) || (role != (int)WardLevel.Team && role != (int)WardLevel.Guest))
            return;

        var roles = RolesOf(nview);
        roles[target] = (WardLevel)role;
        WriteRoles(nview, roles);
    }

    static void ApplySetEnabled(ZNetView nview, bool on, long actor)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;
        var area = AreaOf(nview);
        if (area == null)
            return;

        var actorLevel = LevelOf(area, actor);
        if (actorLevel == WardLevel.None || (int)actorLevel < SwitchMinimum(nview))
            return;

        // A guest switching it off blocks opt-in until a team member or the owner turns it back on.
        var guestOff = !on && actorLevel == WardLevel.Guest;
        nview.GetZDO().Set(GuestOffHash, guestOff ? 1 : 0);
        PrivateAreaAccess.SetEnabled(area, on);
        LockSmith.Log?.LogInfo($"Ward {nview.name}: {(on ? "on" : "off")} by {actor} ({actorLevel}).");
    }

    static void ApplySetSwitch(ZNetView nview, int level, long actor)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner() || level < 1 || level > 3)
            return;
        var area = AreaOf(nview);
        if (area == null || LevelOf(area, actor) != WardLevel.Owner)
            return;
        nview.GetZDO().Set(SwitchHash, level);
    }

    static void ApplySetRemover(ZNetView nview, long target, bool grant, long actor)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner() || target == 0L)
            return;
        var area = AreaOf(nview);
        if (area == null || LevelOf(area, actor) != WardLevel.Owner)
            return;
        var level = LevelOf(area, target);
        if (level != WardLevel.Team && level != WardLevel.Guest)
            return;

        var removers = IdList(Read(nview, RemoversHash));
        if (grant)
        {
            if (!removers.Contains(target))
                removers.Add(target);
        }
        else
        {
            removers.Remove(target);
        }

        WriteIds(nview, RemoversHash, removers);
    }

    /// <summary>Join (any opt-in) is refused on pieces under a ward a guest switched off.</summary>
    public static bool JoinBlockedHere(Vector3 pos) => BlocksJoin(pos);

    // ---- ZDO helpers ------------------------------------------------------------------------

    static string Read(ZNetView? nview, int hash) =>
        nview != null && nview.IsValid() && nview.GetZDO() is { } zdo ? zdo.GetString(hash, string.Empty) : string.Empty;

    static void WriteRoles(ZNetView nview, Dictionary<long, WardLevel> roles)
    {
        var sb = new StringBuilder();
        foreach (var pair in roles)
        {
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append('|').Append(pair.Value == WardLevel.Guest ? 'G' : 'T');
        }

        nview.GetZDO().Set(RolesHash, sb.ToString());
    }

    static void WriteIds(ZNetView nview, int hash, List<long> ids)
    {
        var sb = new StringBuilder();
        foreach (var id in ids)
        {
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(id.ToString(CultureInfo.InvariantCulture));
        }

        nview.GetZDO().Set(hash, sb.ToString());
    }

    static List<long> IdList(string raw)
    {
        var ids = new List<long>();
        foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
            if (long.TryParse(part, NumberStyles.Integer, CultureInfo.InvariantCulture, out var id))
                ids.Add(id);
        return ids;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace LockSmith.Access;

/// <summary>What a guest may do on a piece beyond using it. A plain guest has neither.</summary>
[Flags]
public enum GuestPerm
{
    None = 0,

    /// <summary>Let others on: add people, open and close Join, add nearby players.</summary>
    Add = 1,

    /// <summary>Switch the piece between public and private.</summary>
    Toggle = 2,
}

/// <summary>
/// Per-person permissions on a piece, stored on its ZDO (<c>id|flags;id|flags</c>). Only ward members and the owner can change them;
/// the change is applied on the owner's side like every other LockSmith write.
/// </summary>
public static class GuestPerms
{
    static readonly int PermsHash = "locksmith_guest_perms".GetStableHashCode();

    public static int FlagsOf(ZNetView? nview, long playerId) =>
        playerId != 0L && Read(nview).TryGetValue(playerId, out var flags) ? flags : 0;

    /// <summary>True when the player is a guest on this piece and holds the permission.</summary>
    public static bool HasPerm(ZNetView? nview, long playerId, GuestPerm perm) =>
        PieceGuestAccess.IsGuest(nview, playerId) && (FlagsOf(nview, playerId) & (int)perm) != 0;

    /// <summary>Ward members and the owner, or a guest who was given "let others on".</summary>
    public static bool CanAdd(ZNetView? nview, long playerId) =>
        nview != null && nview.IsValid()
        && (PieceGuestAccess.CanManageGuests(nview, playerId) || HasPerm(nview, playerId, GuestPerm.Add));

    // ---- RPC ----------------------------------------------------------------------------------

    public static void RegisterRpc(ZNetView nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        nview.Unregister(GameHookTargets.RpcSetGuestPerms);
        nview.Register<string, long>(GameHookTargets.RpcSetGuestPerms, (long sender, string raw, long actor) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcSetGuestPerms, sender, actor))
                Apply(nview, raw, actor);
        });
    }

    /// <summary>Set permissions for one or more people in one request (a group adds many at once).</summary>
    public static void Request(ZNetView nview, IEnumerable<KeyValuePair<long, int>> changes, long actor, Action? onConfirmed = null)
    {
        if (nview == null || !nview.IsValid())
            return;

        var list = new List<KeyValuePair<long, int>>(changes);
        if (list.Count == 0)
            return;

        var raw = Encode(list);
        PieceRpc.Request(nview, GameHookTargets.RpcSetGuestPerms, new object[] { raw, actor },
            view => Apply(view, raw, actor),
            view => list.TrueForAll(c => FlagsOf(view, c.Key) == c.Value),
            onConfirmed, PieceGuestAccess.ShowSyncFailed);
    }

    public static void RequestOne(ZNetView nview, long target, int flags, long actor, Action? onConfirmed = null) =>
        Request(nview, new[] { new KeyValuePair<long, int>(target, flags) }, actor, onConfirmed);

    static void Apply(ZNetView nview, string raw, long actor)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;
        if (!PieceGuestAccess.CanManageGuests(nview, actor))
        {
            LockSmith.Log?.LogWarning($"Rejected permission change by {actor} on {nview.name}.");
            return;
        }

        var perms = Read(nview);
        foreach (var change in Decode(raw))
        {
            var flags = change.Value & (int)(GuestPerm.Add | GuestPerm.Toggle);
            if (flags == 0)
                perms.Remove(change.Key);
            else
                perms[change.Key] = flags;
        }

        Write(nview, perms);
    }

    /// <summary>Called when someone leaves or is removed, so a later re-join starts as a plain guest.</summary>
    public static void Clear(ZNetView nview, long playerId)
    {
        if (nview == null || !nview.IsValid())
            return;
        var perms = Read(nview);
        if (perms.Remove(playerId))
            Write(nview, perms);
    }

    // ---- storage ------------------------------------------------------------------------------

    static Dictionary<long, int> Read(ZNetView? nview)
    {
        var result = new Dictionary<long, int>();
        if (nview == null || !nview.IsValid())
            return result;
        var raw = nview.GetZDO()?.GetString(PermsHash, string.Empty) ?? string.Empty;
        foreach (var pair in Decode(raw))
            result[pair.Key] = pair.Value;
        return result;
    }

    static void Write(ZNetView nview, Dictionary<long, int> perms)
    {
        var list = new List<KeyValuePair<long, int>>(perms);
        nview.GetZDO().Set(PermsHash, Encode(list));
    }

    static string Encode(List<KeyValuePair<long, int>> list)
    {
        var sb = new StringBuilder();
        foreach (var pair in list)
        {
            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(pair.Key.ToString(CultureInfo.InvariantCulture)).Append('|').Append(pair.Value.ToString(CultureInfo.InvariantCulture));
        }

        return sb.ToString();
    }

    static List<KeyValuePair<long, int>> Decode(string raw)
    {
        var list = new List<KeyValuePair<long, int>>();
        foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var pipe = part.IndexOf('|');
            if (pipe < 0)
                continue;
            if (long.TryParse(part.Substring(0, pipe), NumberStyles.Integer, CultureInfo.InvariantCulture, out var id)
                && int.TryParse(part.Substring(pipe + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out var flags))
                list.Add(new KeyValuePair<long, int>(id, flags));
        }

        return list;
    }
}

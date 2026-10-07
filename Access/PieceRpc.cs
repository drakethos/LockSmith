using System;
using System.Collections.Generic;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Owner-routed piece requests that hold up in multiplayer.
/// <list type="bullet">
/// <item>Only the ZDO owner writes LockSmith keys. A non-owner write bumps the data revision in
/// parallel with the owner and peers keep whichever copy reaches them first, so guest lists and
/// flags silently diverge between players.</item>
/// <item>An unowned ZDO (owner 0) broadcasts the RPC and every handler drops it on
/// <c>IsOwner</c>, so the requester claims it first (vanilla Sign/Container pattern).</item>
/// <item>Ownership can move while an RPC is in flight (old owner drops it), and a new owner can
/// write over a change it never received. The requester re-sends until its own ZDO copy shows the
/// result. Every request is idempotent (set-to-value / add / remove), so a re-send is safe.</item>
/// </list>
/// </summary>
public static class PieceRpc
{
    private const float ResendSeconds = 1f;
    private const float GiveUpSeconds = 6f;

    private sealed class Pending
    {
        public ZDOID Id;
        public string Rpc = string.Empty;
        public Func<ZNetView, bool> IsDone = null!;
        public Action<ZNetView> Send = null!;
        public Action? OnConfirmed;
        public Action? OnFailed;
        public float NextSend;
        public float Until;
    }

    private static readonly List<Pending> PendingRequests = new List<Pending>();

    /// <summary>
    /// Send <paramref name="rpc"/> to the piece owner (or apply locally when we own it), then keep
    /// re-sending until <paramref name="isDone"/> holds on the local ZDO copy or the request times out.
    /// A newer request with the same rpc on the same piece replaces the old one.
    /// </summary>
    public static void Request(
        ZNetView nview,
        string rpc,
        object[] args,
        Action<ZNetView> applyAsOwner,
        Func<ZNetView, bool> isDone,
        Action? onConfirmed = null,
        Action? onFailed = null)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        PendingRequests.RemoveAll(p => p.Id == zdo.m_uid && p.Rpc == rpc);

        // Fresh view each send: chunk reloads recreate the ZNetView under the same ZDOID.
        void Send(ZNetView view)
        {
            if (!view.HasOwner())
                view.ClaimOwnership();

            if (view.IsOwner())
                applyAsOwner(view);
            else
                view.InvokeRPC(rpc, args);
        }

        var now = Time.time;
        PendingRequests.Add(new Pending
        {
            Id = zdo.m_uid,
            Rpc = rpc,
            IsDone = isDone,
            Send = Send,
            OnConfirmed = onConfirmed,
            OnFailed = onFailed,
            NextSend = now + ResendSeconds,
            Until = now + GiveUpSeconds
        });

        Send(nview);
    }

    /// <summary>True while a request of this rpc on this piece is still waiting for confirmation.</summary>
    public static bool HasPending(ZNetView? nview, string rpc)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return false;

        foreach (var p in PendingRequests)
        {
            if (p.Id == zdo.m_uid && p.Rpc == rpc)
                return true;
        }

        return false;
    }

    /// <summary>Drive confirmation / re-send. Called every frame from the plugin.</summary>
    public static void Tick()
    {
        if (PendingRequests.Count == 0)
            return;

        var now = Time.time;
        for (var i = PendingRequests.Count - 1; i >= 0; i--)
        {
            if (i >= PendingRequests.Count)
                continue;

            var p = PendingRequests[i];
            var view = FindView(p.Id);
            if (view == null)
            {
                // Piece unloaded or destroyed — nothing left to confirm against.
                PendingRequests.RemoveAt(i);
                continue;
            }

            if (p.IsDone(view))
            {
                PendingRequests.RemoveAt(i);
                p.OnConfirmed?.Invoke();
                continue;
            }

            if (now >= p.Until)
            {
                PendingRequests.RemoveAt(i);
                LockSmith.Log?.LogWarning(
                    $"{p.Rpc} on {view.name} was not confirmed by owner {view.GetZDO()?.GetOwner()}.");
                p.OnFailed?.Invoke();
                continue;
            }

            if (now >= p.NextSend)
            {
                p.NextSend = now + ResendSeconds;
                p.Send(view);
            }
        }
    }

    public static void Clear() => PendingRequests.Clear();

    /// <summary>
    /// Owner-side check that the routed sender is the player it claims to be.
    /// Unresolvable senders (character not loaded here) pass — ward/guest checks still apply.
    /// </summary>
    public static bool IsAuthenticSender(ZNetView nview, string rpc, long sender, long claimedPlayerId)
    {
        var actual = ResolveSenderPlayerId(sender);
        if (actual == 0L || actual == claimedPlayerId)
            return true;

        LockSmith.Log?.LogWarning(
            $"Rejected {rpc} on {nview.name}: peer {sender} is player {actual}, claimed {claimedPlayerId}.");
        return false;
    }

    private static long ResolveSenderPlayerId(long sender)
    {
        if (sender == ZDOMan.GetSessionID())
        {
            var local = Player.m_localPlayer;
            return local != null ? local.GetPlayerID() : 0L;
        }

        // A player's character ZDO is owned by that player's peer.
        // GetComponent, not Character.m_nview: that field is protected in the live game
        // (FieldAccessException there; the publicized build refs hide it).
        foreach (var p in Player.GetAllPlayers())
        {
            var view = p != null ? p.GetComponent<ZNetView>() : null;
            if (view == null || !view.IsValid())
                continue;

            if (view.GetZDO().GetOwner() == sender)
                return p!.GetPlayerID();
        }

        return 0L;
    }

    private static ZNetView? FindView(ZDOID id)
    {
        if (ZDOMan.instance == null || ZNetScene.instance == null)
            return null;

        var zdo = ZDOMan.instance.GetZDO(id);
        if (zdo == null)
            return null;

        var view = ZNetScene.instance.FindInstance(zdo);
        return view != null && view.IsValid() ? view : null;
    }
}

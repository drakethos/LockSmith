using System;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// One player at a time per piece, like vanilla chests (<c>Container.RPC_RequestOpen</c>).
/// <list type="bullet">
/// <item>Opening the Lock menu asks the ZDO owner for a lease. On yes the owner hands ZDO
/// ownership to the requester, so every menu action is a local owner write (no RPC round trips).</item>
/// <item>While someone holds the lease, other players' menu / Join / paste get "In use by …".</item>
/// <item>The holder renews while the menu (or its Yes/No) is open, and releases on close. A crashed
/// holder's lease runs out after <see cref="LeaseSeconds"/>.</item>
/// </list>
/// </summary>
public static class PieceMenuLock
{
    public const float RenewSeconds = 3f;
    private const double LeaseSeconds = 10d;
    private const float ResponseTimeoutSeconds = 4f;

    private static readonly int HolderHash = GameHookTargets.ZdoMenuHolder.GetStableHashCode();
    private static readonly int HolderNameHash = GameHookTargets.ZdoMenuHolderName.GetStableHashCode();
    private static readonly int UntilHash = GameHookTargets.ZdoMenuUntil.GetStableHashCode();

    private static ZDOID _pendingId = ZDOID.None;
    private static Action? _pendingGranted;
    private static Action? _pendingDenied;
    private static float _pendingUntil;

    public static void RegisterRpcs(ZNetView nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        nview.Unregister(GameHookTargets.RpcMenuRequest);
        nview.Register<long, string>(GameHookTargets.RpcMenuRequest, (long sender, long playerId, string name) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcMenuRequest, sender, playerId))
                OnRequest(nview, sender, playerId, name);
        });

        nview.Unregister(GameHookTargets.RpcMenuResponse);
        nview.Register<bool, string>(GameHookTargets.RpcMenuResponse, (long sender, bool granted, string holderName) =>
            OnResponse(nview, granted, holderName));

        nview.Unregister(GameHookTargets.RpcMenuRelease);
        nview.Register<long>(GameHookTargets.RpcMenuRelease, (long sender, long playerId) =>
        {
            if (PieceRpc.IsAuthenticSender(nview, GameHookTargets.RpcMenuRelease, sender, playerId))
                ClearIfHolder(nview, playerId);
        });
    }

    /// <summary>Another player holds a live lease on this piece.</summary>
    public static bool IsHeldByOther(ZNetView? nview, long playerId, out string holderName)
    {
        holderName = string.Empty;
        var zdo = nview != null && nview.IsValid() ? nview.GetZDO() : null;
        if (zdo == null)
            return false;

        var holder = zdo.GetLong(HolderHash, 0L);
        if (holder == 0L || holder == playerId || zdo.GetLong(UntilHash, 0L) <= NowTicks())
            return false;

        holderName = zdo.GetString(HolderNameHash, string.Empty);
        return true;
    }

    /// <summary>Shows "In use by …" and returns true when another player holds the piece.</summary>
    public static bool BlockIfHeldByOther(ZNetView? nview, Humanoid? user)
    {
        var player = user as Player;
        if (player == null || !IsHeldByOther(nview, player.GetPlayerID(), out var name))
            return false;

        ShowInUse(name);
        return true;
    }

    /// <summary>Take the lease (and ZDO ownership). Calls back once the owner answers.</summary>
    public static void Request(ZNetView nview, Player player, Action onGranted, Action onDenied)
    {
        var playerId = player.GetPlayerID();
        if (IsHeldByOther(nview, playerId, out var holderName))
        {
            ShowInUse(holderName);
            onDenied();
            return;
        }

        if (!nview.HasOwner())
            nview.ClaimOwnership();

        if (nview.IsOwner())
        {
            Write(nview, playerId, player.GetPlayerName());
            onGranted();
            return;
        }

        _pendingId = nview.GetZDO().m_uid;
        _pendingGranted = onGranted;
        _pendingDenied = onDenied;
        _pendingUntil = Time.time + ResponseTimeoutSeconds;
        nview.InvokeRPC(GameHookTargets.RpcMenuRequest, playerId, player.GetPlayerName());
    }

    /// <summary>Keep the lease alive while the menu is open. Re-asks if ownership moved away.</summary>
    public static void Renew(ZNetView nview, Player player)
    {
        if (nview == null || !nview.IsValid())
            return;

        if (nview.IsOwner())
            Write(nview, player.GetPlayerID(), player.GetPlayerName());
        else
            nview.InvokeRPC(GameHookTargets.RpcMenuRequest, player.GetPlayerID(), player.GetPlayerName());
    }

    public static void Release(ZNetView? nview, long playerId)
    {
        if (nview == null || !nview.IsValid() || playerId == 0L)
            return;

        if (nview.IsOwner())
            ClearIfHolder(nview, playerId);
        else
            nview.InvokeRPC(GameHookTargets.RpcMenuRelease, playerId);
    }

    /// <summary>Requester side: give up on an owner that never answered.</summary>
    public static void Tick()
    {
        if (_pendingId == ZDOID.None || Time.time < _pendingUntil)
            return;

        var denied = _pendingDenied;
        ClearPending();
        AccessFeedback.Show(Player.m_localPlayer, LockSmithLocalization.MsgSyncFailedToken);
        denied?.Invoke();
    }

    private static void OnRequest(ZNetView nview, long sender, long playerId, string name)
    {
        // Ownership already moved on; the requester times out and can try again.
        if (!nview.IsOwner())
            return;

        if (IsHeldByOther(nview, playerId, out var holderName))
        {
            nview.InvokeRPC(sender, GameHookTargets.RpcMenuResponse, false, holderName);
            return;
        }

        Write(nview, playerId, name);
        // Vanilla Container.RPC_RequestOpen order: push the ZDO, hand ownership over, then answer.
        ZDOMan.instance.ForceSendZDO(sender, nview.GetZDO().m_uid);
        nview.GetZDO().SetOwner(sender);
        nview.InvokeRPC(sender, GameHookTargets.RpcMenuResponse, true, string.Empty);
    }

    private static void OnResponse(ZNetView nview, bool granted, string holderName)
    {
        var zdo = nview.GetZDO();
        if (zdo == null || zdo.m_uid != _pendingId)
            return; // renew answers land here too — nothing waiting

        var onGranted = _pendingGranted;
        var onDenied = _pendingDenied;
        ClearPending();

        if (granted)
        {
            onGranted?.Invoke();
            return;
        }

        ShowInUse(holderName);
        onDenied?.Invoke();
    }

    private static void Write(ZNetView nview, long playerId, string name)
    {
        var zdo = nview.GetZDO();
        zdo.Set(HolderHash, playerId);
        zdo.Set(HolderNameHash, name ?? string.Empty);
        zdo.Set(UntilHash, NowTicks() + TimeSpan.FromSeconds(LeaseSeconds).Ticks);
    }

    private static void ClearIfHolder(ZNetView nview, long playerId)
    {
        if (!nview.IsOwner())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null || zdo.GetLong(HolderHash, 0L) != playerId)
            return;

        zdo.Set(HolderHash, 0L);
        zdo.Set(UntilHash, 0L);
    }

    private static void ClearPending()
    {
        _pendingId = ZDOID.None;
        _pendingGranted = null;
        _pendingDenied = null;
    }

    private static void ShowInUse(string holderName) =>
        AccessFeedback.ShowRaw(
            Player.m_localPlayer,
            string.IsNullOrEmpty(holderName)
                ? "Someone else is using this — try again in a moment"
                : holderName + " is using this — try again in a moment");

    // Network time, so every peer agrees on when a lease runs out.
    private static long NowTicks() => ZNet.instance != null ? ZNet.instance.GetTime().Ticks : 0L;
}

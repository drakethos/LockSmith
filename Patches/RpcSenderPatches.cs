using System;
using HarmonyLib;

namespace LockSmith.Patches;

/// <summary>
/// Server-side: a client's ZRoutedRpc always stamps its own peer id as the sender, but the server passes the id on
/// unchecked, so a modified client can pose as another player (or the host) and LockSmith's owner-side checks would
/// believe it. Drop routed RPCs whose sender is not the client they came from. (VerifyRpcSenders; shareable: a candidate
/// for DrakeModsLibs.)
/// </summary>
[HarmonyPatch(typeof(ZRoutedRpc), "RPC_RoutedRPC")]
internal static class RoutedRpcSenderPatch
{
    private static bool Prefix(ZRpc rpc, ZPackage pkg)
    {
        try
        {
            if (rpc == null || pkg == null || ZNet.instance == null || !ZNet.instance.IsServer() || !LockSmithConfig.VerifyRpcSenders)
                return true;

            // ZNet.GetPeer(ZRpc) is private in the live game: find the peer through the public list
            ZNetPeer? peer = null;
            foreach (var p in ZNet.instance.GetPeers())
            {
                if (p.m_rpc == rpc)
                {
                    peer = p;
                    break;
                }
            }

            if (peer == null || peer.m_uid == 0L)
                return true;

            // RoutedRPCData starts with msgID, senderPeerID: peek, then rewind for the real handler
            var pos = pkg.GetPos();
            pkg.ReadLong();
            var sender = pkg.ReadLong();
            pkg.SetPos(pos);
            if (sender == peer.m_uid)
                return true;

            LockSmith.Log?.LogWarning($"Dropped a routed RPC from {peer.m_playerName} (peer {peer.m_uid}) that claimed to come from peer {sender}.");
            return false;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"Routed RPC sender check skipped: {ex.Message}");
            return true;
        }
    }
}

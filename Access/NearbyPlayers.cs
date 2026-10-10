using System;
using System.Collections.Generic;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>Finds other players close enough to add to a piece's access list.</summary>
public static class NearbyPlayers
{
    const float NearbyRange = 5f;

    /// <summary>Closest other player within <see cref="NearbyRange"/> that passes <paramref name="accept"/>.</summary>
    public static Player? FindNearest(Player from, Func<Player, bool> accept)
    {
        Player? nearest = null;
        var best = NearbyRange;
        foreach (var p in Player.GetAllPlayers())
        {
            if (!p || p == from || !accept(p))
                continue;
            var d = Vector3.Distance(from.transform.position, p.transform.position);
            if (d > best)
                continue;
            best = d;
            nearest = p;
        }

        return nearest;
    }

    /// <summary>Every other player within <see cref="NearbyRange"/> that passes <paramref name="accept"/>, closest first.</summary>
    public static List<Player> FindAllNearby(Player from, Func<Player, bool> accept)
    {
        var found = new List<(Player Player, float Distance)>();
        foreach (var p in Player.GetAllPlayers())
        {
            if (!p || p == from || !accept(p))
                continue;
            var d = Vector3.Distance(from.transform.position, p.transform.position);
            if (d <= NearbyRange)
                found.Add((p, d));
        }

        found.Sort((a, b) => a.Distance.CompareTo(b.Distance));
        var result = new List<Player>(found.Count);
        foreach (var entry in found)
            result.Add(entry.Player);
        return result;
    }
}

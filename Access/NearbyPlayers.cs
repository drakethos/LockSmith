using System;
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
}

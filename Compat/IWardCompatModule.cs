using System;
using HarmonyLib;
using UnityEngine;
using LibsCompat = DrakeModsLibs.Compat;

namespace LockSmith.Compat;

/// <summary>
/// Ward stack (or cheat path that changes ward semantics). Queried via
/// <see cref="CompatibilityManager"/> / <see cref="API.LockSmithCompatApi"/>.
/// Scaffolding lives in DrakeModsLibs <see cref="LibsCompat.ICompatModule"/>.
/// </summary>
public interface IWardCompatModule : LibsCompat.ICompatModule
{
    /// <summary>True when this stack has an enabled ward covering <paramref name="position"/>.</summary>
    bool IsInsideEnabledWard(Vector3 position);

    /// <summary>Local-player access at <paramref name="position"/>.</summary>
    WardCoverageKind QueryLocalAccess(Vector3 position, bool flash);

    /// <summary>Access for <paramref name="playerId"/> at <paramref name="position"/>.</summary>
    WardCoverageKind QueryPlayerAccess(long playerId, Vector3 position, bool flash);
}

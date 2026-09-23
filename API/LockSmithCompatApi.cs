using System;
using DrakeModsLibs.Compat;
using UnityEngine;

namespace LockSmith.API;

/// <summary>
/// Public entry point for third-party mods to add a LockSmith compatibility module
/// without waiting for an upstream LockSmith release.
/// </summary>
/// <remarks>
/// SoftDependency LockSmith (<c>com.drakesworkshop.locksmith</c>). Implement
/// <see cref="Compat.IWardCompatModule"/> (or <see cref="ICompatModule"/>), then call
/// <see cref="Register"/> from your plugin Awake (or subscribe to <see cref="OnInitialized"/>).
/// Prefer opening an issue / PR so built-in support can be considered — external modules are fine.
/// </remarks>
public static class LockSmithCompatApi
{
    /// <summary>LockSmith BepInEx GUID — use with SoftDependency on your plugin.</summary>
    public const string LockSmithGuid = "com.drakesworkshop.locksmith";

    /// <summary>Suggested priority for third-party modules (runs after built-ins).</summary>
    public const int DefaultThirdPartyPriority = CompatPriority.ThirdParty;

    /// <summary>True after LockSmith has finished its built-in compat scan.</summary>
    public static bool IsReady => Compat.CompatibilityManager.IsInitialized;

    /// <summary>
    /// Fires once when LockSmith compat finishes initializing. Safe to subscribe before or after;
    /// if already ready, invoke your callback immediately.
    /// </summary>
    public static event Action? OnInitialized
    {
        add
        {
            if (value == null)
                return;

            if (Compat.CompatibilityManager.IsInitialized)
            {
                try
                {
                    value.Invoke();
                }
                catch
                {
                    // Caller responsibility; do not break LockSmith.
                }
            }

            Compat.CompatibilityManager.Initialized += value;
        }
        remove => Compat.CompatibilityManager.Initialized -= value;
    }

    /// <summary>
    /// Register and activate a compatibility module when its foreign mod is present.
    /// Returns false if <see cref="ICompatModule.TryActivate"/> fails or Id is duplicate.
    /// </summary>
    public static bool Register(ICompatModule module) =>
        Compat.CompatibilityManager.Register(module);

    /// <summary>Whether a module with this Id is active.</summary>
    public static bool HasModule(string id) =>
        Compat.CompatibilityManager.HasActiveModule(id);

    /// <summary>
    /// Local player ward access at <paramref name="position"/> across active ward modules
    /// (vanilla, WardIsLove, ProtectiveWards, …). True when unrestricted or permitted.
    /// </summary>
    public static bool HasLocalWardAccess(Vector3 position) =>
        Access.WardAccess.HasLocalWardAccess(position);

    /// <summary>
    /// True when <paramref name="position"/> is inside at least one enabled ward
    /// known to LockSmith's active compat modules.
    /// </summary>
    public static bool IsInsideEnabledWard(Vector3 position) =>
        Access.WardAccess.IsInsideEnabledWard(position);
}

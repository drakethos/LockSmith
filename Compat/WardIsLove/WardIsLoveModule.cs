using System;
using System.Collections;
using System.Reflection;
using DrakeModsLibs.Compat;
using HarmonyLib;
using LockSmith.Access;
using LockSmith.Patches;
using UnityEngine;

namespace LockSmith.Compat.WardIsLove;

/// <summary>
/// Soft WardIsLove stack (<c>WardMonoscript</c>). Absent → module not registered.
/// </summary>
internal sealed class WardIsLoveModule : IWardCompatModule
{
    public const string ModuleId = "WardIsLove";
    public const string PluginGuid = "Azumatt.WardIsLove";

    private MethodInfo? _apiIsLoaded;
    private MethodInfo? _apiIsInsideWard;
    private MethodInfo? _wardCheckAccess;
    private MethodInfo? _customCheckAccess;
    private MethodInfo? _checkInWardMonoscript;
    private bool _typesOk;
    private bool _patchesApplied;

    private static MethodInfo? _wilDoorBlock;
    private static MethodInfo? _wilContainerBlock;
    private static MethodInfo? _wilDoorHover;
    private static MethodInfo? _wilContainerHover;
    private static bool _loggedDoorAllow;
    private static bool _loggedContainerAllow;
    private static bool _loggedAllowFault;

    public string Id => ModuleId;
    public string? SoftDependencyGuid => PluginGuid;
    public int Priority => CompatPriority.BuiltIn;
    public bool IsActive { get; private set; }

    public bool TryActivate()
    {
        ResolveTypes();
        if (!_typesOk)
            return false;

        try
        {
            IsActive = _apiIsLoaded?.Invoke(null, null) is true;
        }
        catch
        {
            IsActive = false;
        }

        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        if (!IsActive || harmony == null || _patchesApplied)
            return;

        _patchesApplied = true;

        // Prefixing WIL's own methods does not stop the Door/Container.Interact
        // replacement WIL already installed. Remove those patches and gate the call.
        _wilDoorBlock = FindWilMethod("WardIsLove.PatchClasses.BlockUnpermittedDoorUse", "Prefix");
        _wilContainerBlock = FindWilMethod("WardIsLove.PatchClasses.BlockUnpermittedContainerUse", "Prefix");
        _wilDoorHover = FindWilMethod("WardIsLove.PatchClasses.ShowWardLockOnDoorHover", "Postfix");
        _wilContainerHover = FindWilMethod("WardIsLove.PatchClasses.ShowWardLockOnContainerHover", "Postfix");

        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(Door), GameHookTargets.DoorInteract, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _wilDoorBlock,
            nameof(DoorInteractGate));
        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(Container), GameHookTargets.ContainerInteract, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _wilContainerBlock,
            nameof(ContainerInteractGate));
        SwapPostfix(
            harmony,
            AccessTools.Method(typeof(Door), GameHookTargets.DoorGetHoverText),
            _wilDoorHover,
            nameof(DoorHoverGate));
        SwapPostfix(
            harmony,
            AccessTools.Method(typeof(Container), GameHookTargets.ContainerGetHoverText),
            _wilContainerHover,
            nameof(ContainerHoverGate));
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        if (!IsActive)
            return false;

        try
        {
            if (_apiIsInsideWard?.Invoke(null, new object[] { position }) is true)
                return true;
            return _checkInWardMonoscript?.Invoke(null, new object[] { position, false }) is true;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat WardIsLove IsInsideWard: {ex.Message}");
            return false;
        }
    }

    public WardCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsActive || !IsInsideEnabledWard(position))
            return WardCoverageKind.Unrelated;

        try
        {
            if (_wardCheckAccess != null
                && _wardCheckAccess.Invoke(null, new object[] { position, 0f, flash, false }) is bool ok)
                return ok ? WardCoverageKind.Allowed : WardCoverageKind.Denied;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat WardIsLove CheckAccess: {ex.Message}");
        }

        return WardCoverageKind.Denied;
    }

    public WardCoverageKind QueryPlayerAccess(long playerId, Vector3 position, bool flash)
    {
        if (!IsActive || !IsInsideEnabledWard(position))
            return WardCoverageKind.Unrelated;

        try
        {
            if (_customCheckAccess != null
                && _customCheckAccess.Invoke(null, new object[] { playerId, position, 0f, flash }) is bool ok)
                return ok ? WardCoverageKind.Allowed : WardCoverageKind.Denied;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat WardIsLove CustomCheck: {ex.Message}");
        }

        return WardCoverageKind.Denied;
    }

    /// <summary>
    /// Runs in place of WIL's door interact prefix. Public / guest pieces skip WIL.
    /// Everyone else still goes through WIL's original check.
    /// </summary>
    private static bool DoorInteractGate(Door __instance, Humanoid character, bool hold)
    {
        if (SafeAllowsDoor(__instance))
        {
            if (!_loggedDoorAllow)
            {
                _loggedDoorAllow = true;
                LockSmith.Log?.LogInfo("Compat WardIsLove: LockSmith door bypassed WIL interact block.");
            }

            return true;
        }

        return CallWilBool(_wilDoorBlock, __instance, character, hold);
    }

    private static bool ContainerInteractGate(Container __instance, Humanoid character, bool hold)
    {
        if (SafeAllowsContainer(__instance))
        {
            if (!_loggedContainerAllow)
            {
                _loggedContainerAllow = true;
                LockSmith.Log?.LogInfo("Compat WardIsLove: LockSmith chest bypassed WIL interact block.");
            }

            return true;
        }

        return CallWilBool(_wilContainerBlock, __instance, character, hold);
    }

    private static void DoorHoverGate(Door __instance, ref string __result)
    {
        if (SafeAllowsDoor(__instance) || _wilDoorHover == null)
            return;

        try
        {
            if (_wilDoorHover.Invoke(null, new object[] { __result, __instance }) is string next)
                __result = next;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat WardIsLove door hover: {ex.Message}");
        }
    }

    private static void ContainerHoverGate(Container __instance, ref string __result)
    {
        if (SafeAllowsContainer(__instance) || _wilContainerHover == null)
            return;

        try
        {
            if (_wilContainerHover.Invoke(null, new object[] { __result, __instance }) is string next)
                __result = next;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat WardIsLove container hover: {ex.Message}");
        }
    }

    private static bool CallWilBool(MethodInfo? wil, UnityEngine.Object piece, Humanoid character, bool hold)
    {
        if (wil == null || !piece)
            return true;

        try
        {
            return wil.Invoke(null, new object[] { piece, character, hold }) is not false;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat WardIsLove invoke {wil.DeclaringType?.Name}: {ex.Message}");
            return true;
        }
    }

    private static bool SafeAllowsDoor(Door door)
    {
        try
        {
            return AllowsDoor(door);
        }
        catch (Exception ex)
        {
            LogAllowFault(ex);
            return false;
        }
    }

    private static bool SafeAllowsContainer(Container container)
    {
        try
        {
            return AllowsContainer(container);
        }
        catch (Exception ex)
        {
            LogAllowFault(ex);
            return false;
        }
    }

    private static void LogAllowFault(Exception ex)
    {
        if (_loggedAllowFault)
            return;

        _loggedAllowFault = true;
        LockSmith.Log?.LogWarning($"Compat WardIsLove allow-check failed: {ex.Message}");
    }

    /// <summary>
    /// Public ZDO, public hammer clone, guest bypass, or Join open for a player WIL would deny.
    /// </summary>
    private static bool AllowsDoor(Door door)
    {
        if (!door)
            return false;

        if (PublicPieceRegistration.IsPublicPiece(door))
            return true;

        if (PieceAccessState.IsPublic(PieceAccessState.GetNetView(door)))
            return true;

        if (PieceGuestService.IsJoinOpenForStranger(door))
            return true;

        return DoorAccessService.ShouldBypassWardCheck(door);
    }

    /// <summary>
    /// Public ZDO, public hammer clone, team member, guest bypass, or Join open for a player WIL would deny.
    /// </summary>
    private static bool AllowsContainer(Container container)
    {
        if (!container)
            return false;

        if (PublicPieceRegistration.IsPublicPiece(container))
            return true;

        if (PieceAccessState.IsPublic(PieceAccessState.GetNetView(container)))
            return true;

        if (GroupChestService.ShouldBypassWard(container))
            return true;

        if (PieceGuestService.IsJoinOpenForStranger(container))
            return true;

        return ChestAccessService.ShouldBypassWardCheck(container);
    }

    private static MethodInfo? FindWilMethod(string typeName, string methodName)
    {
        var type = AccessTools.TypeByName(typeName);
        var method = type == null ? null : AccessTools.Method(type, methodName);
        if (method == null)
            LockSmith.Log?.LogWarning($"Compat WardIsLove: {typeName}.{methodName} not found.");
        return method;
    }

    private static void SwapPrefix(Harmony harmony, MethodInfo? original, MethodInfo? wilPatch, string ourName)
    {
        if (!TryRemoveWil(harmony, original, wilPatch, ourName))
            return;

        var ours = AccessTools.Method(typeof(WardIsLoveModule), ourName);
        if (ours == null || original == null)
            return;

        harmony.Patch(original, prefix: new HarmonyMethod(ours) { priority = HarmonyLib.Priority.First });
    }

    private static void SwapPostfix(Harmony harmony, MethodInfo? original, MethodInfo? wilPatch, string ourName)
    {
        if (!TryRemoveWil(harmony, original, wilPatch, ourName))
            return;

        var ours = AccessTools.Method(typeof(WardIsLoveModule), ourName);
        if (ours == null || original == null)
            return;

        harmony.Patch(original, postfix: new HarmonyMethod(ours) { priority = HarmonyLib.Priority.Last });
    }

    /// <summary>
    /// Drop WIL's patch from the game method. Returns false when there is nothing to gate.
    /// </summary>
    private static bool TryRemoveWil(Harmony harmony, MethodInfo? original, MethodInfo? wilPatch, string ourName)
    {
        if (original == null || wilPatch == null)
        {
            LockSmith.Log?.LogWarning($"Compat WardIsLove: cannot gate {ourName} (missing method).");
            return false;
        }

        var present = ListsWil(original, wilPatch);
        try
        {
            if (present)
                harmony.Unpatch(original, wilPatch);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"Compat WardIsLove unpatch {ourName}: {ex.Message}");
        }

        var still = ListsWil(original, wilPatch);
        LockSmith.Log?.LogInfo(
            $"Compat WardIsLove {ourName}: WIL patch {(present ? "found" : "absent")}, still attached={still}.");
        return true;
    }

    private static bool ListsWil(MethodBase original, MethodInfo wilPatch)
    {
        var info = Harmony.GetPatchInfo(original);
        if (info == null)
            return false;

        return ListsPatch(info.Prefixes, wilPatch) || ListsPatch(info.Postfixes, wilPatch);
    }

    private static bool ListsPatch(IEnumerable? patches, MethodInfo wilPatch)
    {
        if (patches == null)
            return false;

        foreach (var entry in patches)
        {
            if (entry is not Patch info)
                continue;

            if (SameMethod(info.PatchMethod, wilPatch))
                return true;
        }

        return false;
    }

    private static bool SameMethod(MethodInfo? candidate, MethodInfo expected)
    {
        if (candidate == null)
            return false;

        if (candidate == expected)
            return true;

        return candidate.Name == expected.Name
               && candidate.DeclaringType?.FullName == expected.DeclaringType?.FullName;
    }

    private void ResolveTypes()
    {
        if (_typesOk || _apiIsLoaded != null)
            return;

        try
        {
            var apiType = AccessTools.TypeByName("WardIsLove.API.API")
                           ?? AccessTools.TypeByName("WardIsLove.API");
            var wardType = AccessTools.TypeByName("WardIsLove.Util.WardMonoscript");
            var customType = AccessTools.TypeByName("WardIsLove.Util.CustomCheck");

            if (apiType == null || wardType == null)
                return;

            _apiIsLoaded = AccessTools.Method(apiType, "IsLoaded", Type.EmptyTypes);
            _apiIsInsideWard = AccessTools.Method(apiType, "IsInsideWard", new[] { typeof(Vector3) });
            _wardCheckAccess = AccessTools.Method(
                wardType,
                "CheckAccess",
                new[] { typeof(Vector3), typeof(float), typeof(bool), typeof(bool) });
            _checkInWardMonoscript = AccessTools.Method(
                wardType,
                "CheckInWardMonoscript",
                new[] { typeof(Vector3), typeof(bool) });

            if (customType != null)
            {
                _customCheckAccess = AccessTools.Method(
                    customType,
                    "CheckAccess",
                    new[] { typeof(long), typeof(Vector3), typeof(float), typeof(bool) });
            }

            _typesOk = _apiIsLoaded != null
                       && (_apiIsInsideWard != null || _checkInWardMonoscript != null)
                       && (_wardCheckAccess != null || _customCheckAccess != null);
        }
        catch (Exception ex)
        {
            _typesOk = false;
            LockSmith.Log?.LogDebug($"Compat WardIsLove resolve: {ex.Message}");
        }
    }
}

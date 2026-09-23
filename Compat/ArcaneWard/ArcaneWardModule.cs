using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using DrakeModsLibs.Compat;
using HarmonyLib;
using LockSmith.Access;
using LockSmith.Patches;
using UnityEngine;

namespace LockSmith.Compat.ArcaneWard;

/// <summary>
/// Soft KG Arcane Ward stack (<c>ArcaneWardComponent</c>). Absent → module not registered.
/// Gates Door/Container Interact only. Coverage via ward instance list (not PrivateArea).
/// </summary>
internal sealed class ArcaneWardModule : IWardCompatModule
{
    public const string ModuleId = "ArcaneWard";
    public const string PluginGuid = "kg.ArcaneWard";

    private const string ComponentTypeName = "kg_ArcaneWard.ArcaneWardComponent";
    private const string PatchesTypeName = "kg_ArcaneWard.WardProtectionPatches";
    private const string ProtectionTypeName = "kg_ArcaneWard.Protection";

    private FieldInfo? _instancesField;
    private MethodInfo? _checkFlag;
    private PropertyInfo? _isEnabled;
    private PropertyInfo? _radius;
    private MethodInfo? _isPermitted;
    private object? _protectionDoor;
    private object? _protectionContainer;
    private bool _typesOk;
    private bool _patchesApplied;

    private static MethodInfo? _awDoorBlock;
    private static MethodInfo? _awContainerBlock;
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
        IsActive = _typesOk;
        return IsActive;
    }

    public void ApplyHarmonyPatches(Harmony harmony)
    {
        if (!IsActive || harmony == null || _patchesApplied)
            return;

        _patchesApplied = true;

        _awDoorBlock = FindNestedPrefix("Door_Interact_Patch");
        _awContainerBlock = FindNestedPrefix("Container_Interact_Patch");

        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(Door), GameHookTargets.DoorInteract, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _awDoorBlock,
            nameof(DoorInteractGate));
        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(Container), GameHookTargets.ContainerInteract, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _awContainerBlock,
            nameof(ContainerInteractGate));
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        if (!IsActive)
            return false;

        try
        {
            foreach (var instance in EnumerateInstances())
            {
                if (!IsEnabledInstance(instance))
                    continue;
                if (IsPointInside(instance, position))
                    return true;
            }
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ArcaneWard IsInsideWard: {ex.Message}");
        }

        return false;
    }

    public WardCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsActive || !IsInsideEnabledWard(position))
            return WardCoverageKind.Unrelated;

        try
        {
            // CheckFlag true = blocked for local player.
            if (_checkFlag != null && _protectionDoor != null
                && _checkFlag.Invoke(null, new object[] { position, true, _protectionDoor, flash }) is true)
                return WardCoverageKind.Denied;

            return WardCoverageKind.Allowed;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ArcaneWard CheckFlag: {ex.Message}");
            return WardCoverageKind.Denied;
        }
    }

    public WardCoverageKind QueryPlayerAccess(long playerId, Vector3 position, bool flash)
    {
        _ = flash;
        if (!IsActive || !IsInsideEnabledWard(position))
            return WardCoverageKind.Unrelated;

        try
        {
            foreach (var instance in EnumerateInstances())
            {
                if (!IsEnabledInstance(instance) || !IsPointInside(instance, position))
                    continue;

                if (_isPermitted != null && _isPermitted.Invoke(instance, new object[] { playerId }) is true)
                    return WardCoverageKind.Allowed;

                return WardCoverageKind.Denied;
            }
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ArcaneWard player access: {ex.Message}");
        }

        return WardCoverageKind.Denied;
    }

    /// <summary>
    /// Runs in place of Arcane's door interact prefix. Public / guest pieces skip Arcane.
    /// </summary>
    private static bool DoorInteractGate(Door __instance, Humanoid character, bool hold)
    {
        _ = character;
        _ = hold;

        if (SafeAllowsDoor(__instance))
        {
            if (!_loggedDoorAllow)
            {
                _loggedDoorAllow = true;
                LockSmith.Log?.LogInfo("Compat ArcaneWard: LockSmith door bypassed Arcane interact block.");
            }

            return true;
        }

        return CallAwBool(_awDoorBlock, __instance);
    }

    private static bool ContainerInteractGate(Container __instance, Humanoid character, bool hold)
    {
        _ = character;
        _ = hold;

        if (SafeAllowsContainer(__instance))
        {
            if (!_loggedContainerAllow)
            {
                _loggedContainerAllow = true;
                LockSmith.Log?.LogInfo("Compat ArcaneWard: LockSmith chest bypassed Arcane interact block.");
            }

            return true;
        }

        return CallAwBool(_awContainerBlock, __instance);
    }

    private static bool CallAwBool(MethodInfo? aw, UnityEngine.Object piece)
    {
        if (aw == null || !piece)
            return true;

        try
        {
            // Arcane Prefix(Door/Container __instance) → !CheckFlag(...)
            return aw.Invoke(null, new object[] { piece }) is not false;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ArcaneWard invoke {aw.DeclaringType?.Name}: {ex.Message}");
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
        LockSmith.Log?.LogWarning($"Compat ArcaneWard allow-check failed: {ex.Message}");
    }

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

    private IEnumerable<object> EnumerateInstances()
    {
        if (_instancesField == null)
            yield break;

        object? raw;
        try
        {
            raw = _instancesField.GetValue(null);
        }
        catch
        {
            yield break;
        }

        if (raw is not IEnumerable list)
            yield break;

        foreach (var entry in list)
        {
            if (entry == null)
                continue;
            if (entry is UnityEngine.Object uo && !uo)
                continue;
            yield return entry;
        }
    }

    private bool IsEnabledInstance(object instance)
    {
        try
        {
            return _isEnabled?.GetValue(instance) is true;
        }
        catch
        {
            return false;
        }
    }

    private bool IsPointInside(object instance, Vector3 point)
    {
        try
        {
            if (instance is not Component component || !component)
                return false;

            var radius = 0;
            if (_radius?.GetValue(instance) is int r)
                radius = r;
            else if (_radius?.GetValue(instance) is float f)
                radius = Mathf.RoundToInt(f);

            return Vector3.Distance(point, component.transform.position) <= radius;
        }
        catch
        {
            return false;
        }
    }

    private static MethodInfo? FindNestedPrefix(string nestedTypeName)
    {
        var outer = AccessTools.TypeByName(PatchesTypeName);
        if (outer == null)
        {
            LockSmith.Log?.LogWarning($"Compat ArcaneWard: {PatchesTypeName} not found.");
            return null;
        }

        var nested = outer.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
        var method = nested == null ? null : AccessTools.Method(nested, "Prefix");
        if (method == null)
            LockSmith.Log?.LogWarning($"Compat ArcaneWard: {nestedTypeName}.Prefix not found.");
        return method;
    }

    private static void SwapPrefix(Harmony harmony, MethodInfo? original, MethodInfo? awPatch, string ourName)
    {
        if (!TryRemoveAw(harmony, original, awPatch, ourName))
            return;

        var ours = AccessTools.Method(typeof(ArcaneWardModule), ourName);
        if (ours == null || original == null)
            return;

        harmony.Patch(original, prefix: new HarmonyMethod(ours) { priority = HarmonyLib.Priority.First });
    }

    private static bool TryRemoveAw(Harmony harmony, MethodInfo? original, MethodInfo? awPatch, string ourName)
    {
        if (original == null || awPatch == null)
        {
            LockSmith.Log?.LogWarning($"Compat ArcaneWard: cannot gate {ourName} (missing method).");
            return false;
        }

        var present = ListsAw(original, awPatch);
        try
        {
            if (present)
                harmony.Unpatch(original, awPatch);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"Compat ArcaneWard unpatch {ourName}: {ex.Message}");
        }

        var still = ListsAw(original, awPatch);
        LockSmith.Log?.LogInfo(
            $"Compat ArcaneWard {ourName}: Arcane patch {(present ? "found" : "absent")}, still attached={still}.");
        return true;
    }

    private static bool ListsAw(MethodBase original, MethodInfo awPatch)
    {
        var info = Harmony.GetPatchInfo(original);
        if (info == null)
            return false;

        return ListsPatch(info.Prefixes, awPatch);
    }

    private static bool ListsPatch(IEnumerable? patches, MethodInfo awPatch)
    {
        if (patches == null)
            return false;

        foreach (var entry in patches)
        {
            if (entry is not Patch info)
                continue;

            if (SameMethod(info.PatchMethod, awPatch))
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
        if (_typesOk || _instancesField != null)
            return;

        try
        {
            var componentType = AccessTools.TypeByName(ComponentTypeName);
            var protectionType = AccessTools.TypeByName(ProtectionTypeName);
            if (componentType == null || protectionType == null)
                return;

            _instancesField = AccessTools.Field(componentType, "_instances");
            _checkFlag = AccessTools.Method(
                componentType,
                "CheckFlag",
                new[] { typeof(Vector3), typeof(bool), protectionType, typeof(bool) });
            _isEnabled = AccessTools.Property(componentType, "IsEnabled");
            _radius = AccessTools.Property(componentType, "Radius");
            _isPermitted = AccessTools.Method(componentType, "IsPermitted", new[] { typeof(long) });

            try
            {
                _protectionDoor = Enum.Parse(protectionType, "Door");
                _protectionContainer = Enum.Parse(protectionType, "Container");
            }
            catch
            {
                _protectionDoor = null;
                _protectionContainer = null;
            }

            _typesOk = _instancesField != null
                       && _checkFlag != null
                       && _isEnabled != null
                       && _radius != null
                       && _protectionDoor != null;
        }
        catch (Exception ex)
        {
            _typesOk = false;
            LockSmith.Log?.LogDebug($"Compat ArcaneWard resolve: {ex.Message}");
        }
    }
}

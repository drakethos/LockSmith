using System;
using System.Collections;
using System.Reflection;
using DrakeModsLibs.Compat;
using HarmonyLib;
using LockSmith.Access;
using LockSmith.Patches;
using UnityEngine;

namespace LockSmith.Compat.ProtectiveWards;

/// <summary>
/// Soft ProtectiveWards stack. Absent → module not registered.
/// Gates Door/Container Interact only (WIL parity). ItemStand and other PW
/// Interact surfaces are left to ProtectiveWards / RenameIt.
/// </summary>
internal sealed class ProtectiveWardsModule : IWardCompatModule
{
    public const string ModuleId = "ProtectiveWards";
    public const string PluginGuid = "shudnal.ProtectiveWards";

    private const string PwTypeName = "ProtectiveWards.ProtectiveWards";
    private const string FullProtectionTypeName = "ProtectiveWards.FullProtection";

    private MethodInfo? _insideEnabledPlayersArea;
    private MethodInfo? _insideEnabledPlayersAreaOut;
    private MethodInfo? _hasAccessPlayer;
    private MethodInfo? _hasAccessPlayerId;
    private MethodInfo? _findProtectedWard;
    private FieldInfo? _connectedAccessModeField;
    private Type? _connectedAccessModeType;
    private bool _typesOk;
    private bool _patchesApplied;

    private static MethodInfo? _pwDoorBlock;
    private static MethodInfo? _pwContainerBlock;
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

        _pwDoorBlock = FindFullProtectionNestedPrefix("Door_Interact_PreventUnauthorizedAccess");
        _pwContainerBlock = FindFullProtectionNestedPrefix("Container_Interact_PreventUnauthorizedAccess");

        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(Door), GameHookTargets.DoorInteract, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _pwDoorBlock,
            nameof(DoorInteractGate));
        SwapPrefix(
            harmony,
            AccessTools.Method(typeof(Container), GameHookTargets.ContainerInteract, new[] { typeof(Humanoid), typeof(bool), typeof(bool) }),
            _pwContainerBlock,
            nameof(ContainerInteractGate));
    }

    public bool IsInsideEnabledWard(Vector3 position)
    {
        if (!IsActive)
            return false;

        try
        {
            if (_insideEnabledPlayersArea != null)
                return _insideEnabledPlayersArea.Invoke(null, new object[] { position, false }) is true;

            PrivateArea? area = null;
            return InvokeInsideOut(position, ref area);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ProtectiveWards IsInsideWard: {ex.Message}");
            return false;
        }
    }

    public WardCoverageKind QueryLocalAccess(Vector3 position, bool flash)
    {
        if (!IsActive || !TryGetCoveringWard(position, out var area) || !area)
            return WardCoverageKind.Unrelated;

        try
        {
            var player = Player.m_localPlayer;
            if (!player)
                return WardCoverageKind.Denied;

            if (_hasAccessPlayer != null
                && _hasAccessPlayer.Invoke(null, new object[] { area, player }) is bool ok)
                return ok ? WardCoverageKind.Allowed : WardCoverageKind.Denied;

            // PW's CheckAccess Prefix is still installed — safe local fallback.
            return PrivateArea.CheckAccess(position, 0f, flash, wardCheck: false)
                ? WardCoverageKind.Allowed
                : WardCoverageKind.Denied;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ProtectiveWards CheckAccess: {ex.Message}");
            return WardCoverageKind.Denied;
        }
    }

    public WardCoverageKind QueryPlayerAccess(long playerId, Vector3 position, bool flash)
    {
        _ = flash;
        if (!IsActive || !TryGetCoveringWard(position, out var area) || !area)
            return WardCoverageKind.Unrelated;

        try
        {
            if (_hasAccessPlayerId != null)
            {
                var mode = ResolveConnectedAccessMode();
                if (mode != null
                    && _hasAccessPlayerId.Invoke(null, new object[] { area, playerId, mode }) is bool ok)
                    return ok ? WardCoverageKind.Allowed : WardCoverageKind.Denied;
            }

            // Direct permitted / creator via vanilla when connected-mode invoke failed.
            if (area.IsPermitted(playerId))
                return WardCoverageKind.Allowed;

            var piece = area.GetComponent<Piece>();
            if (piece != null && piece.GetCreator() == playerId)
                return WardCoverageKind.Allowed;

            return WardCoverageKind.Denied;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ProtectiveWards player access: {ex.Message}");
            return WardCoverageKind.Denied;
        }
    }

    /// <summary>
    /// Runs in place of PW's door interact prefix. Public / guest pieces skip PW.
    /// Propagates <c>__state</c> so PW's Finalizer still clears CheckAccess bypass.
    /// </summary>
    private static bool DoorInteractGate(
        Door __instance,
        Humanoid character,
        bool hold,
        bool alt,
        ref bool __result,
        ref bool __state)
    {
        _ = hold;
        _ = alt;

        if (SafeAllowsDoor(__instance))
        {
            if (!_loggedDoorAllow)
            {
                _loggedDoorAllow = true;
                LockSmith.Log?.LogInfo("Compat ProtectiveWards: LockSmith door bypassed PW interact block.");
            }

            return true;
        }

        return CallPwInteractPrefix(_pwDoorBlock, __instance, character, ref __result, ref __state);
    }

    private static bool ContainerInteractGate(
        Container __instance,
        Humanoid character,
        bool hold,
        bool alt,
        ref bool __result,
        ref bool __state)
    {
        _ = hold;
        _ = alt;

        if (SafeAllowsContainer(__instance))
        {
            if (!_loggedContainerAllow)
            {
                _loggedContainerAllow = true;
                LockSmith.Log?.LogInfo("Compat ProtectiveWards: LockSmith chest bypassed PW interact block.");
            }

            return true;
        }

        return CallPwInteractPrefix(_pwContainerBlock, __instance, character, ref __result, ref __state);
    }

    private static bool CallPwInteractPrefix(
        MethodInfo? pw,
        UnityEngine.Object piece,
        Humanoid character,
        ref bool __result,
        ref bool __state)
    {
        if (pw == null || !piece)
            return true;

        try
        {
            var args = new object[] { piece, character, __result, __state };
            var cont = pw.Invoke(null, args) is not false;
            __result = args[2] is true;
            __state = args[3] is true;
            return cont;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ProtectiveWards invoke {pw.DeclaringType?.Name}: {ex.Message}");
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
        LockSmith.Log?.LogWarning($"Compat ProtectiveWards allow-check failed: {ex.Message}");
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

    private bool TryGetCoveringWard(Vector3 position, out PrivateArea? area)
    {
        area = null;
        try
        {
            if (_findProtectedWard != null
                && _findProtectedWard.Invoke(null, new object[] { position }) is PrivateArea found
                && found)
            {
                area = found;
                return true;
            }

            return InvokeInsideOut(position, ref area) && area;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ProtectiveWards covering ward: {ex.Message}");
            return false;
        }
    }

    private bool InvokeInsideOut(Vector3 position, ref PrivateArea? area)
    {
        if (_insideEnabledPlayersAreaOut == null)
            return false;

        var args = new object[] { position, area!, false };
        var ok = _insideEnabledPlayersAreaOut.Invoke(null, args) is true;
        area = args[1] as PrivateArea;
        return ok;
    }

    private object? ResolveConnectedAccessMode()
    {
        if (_connectedAccessModeField == null || _connectedAccessModeType == null)
            return null;

        try
        {
            var entry = _connectedAccessModeField.GetValue(null);
            if (entry == null)
                return Enum.ToObject(_connectedAccessModeType, 0);

            var valueProp = entry.GetType().GetProperty("Value", BindingFlags.Instance | BindingFlags.Public);
            return valueProp?.GetValue(entry) ?? Enum.ToObject(_connectedAccessModeType, 0);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"Compat ProtectiveWards connected mode: {ex.Message}");
            return Enum.ToObject(_connectedAccessModeType, 0);
        }
    }

    private static MethodInfo? FindFullProtectionNestedPrefix(string nestedTypeName)
    {
        var outer = AccessTools.TypeByName(FullProtectionTypeName);
        if (outer == null)
        {
            LockSmith.Log?.LogWarning($"Compat ProtectiveWards: {FullProtectionTypeName} not found.");
            return null;
        }

        var nested = outer.GetNestedType(nestedTypeName, BindingFlags.Public | BindingFlags.NonPublic);
        var method = nested == null ? null : AccessTools.Method(nested, "Prefix");
        if (method == null)
            LockSmith.Log?.LogWarning($"Compat ProtectiveWards: {nestedTypeName}.Prefix not found.");
        return method;
    }

    private static void SwapPrefix(Harmony harmony, MethodInfo? original, MethodInfo? pwPatch, string ourName)
    {
        if (!TryRemovePw(harmony, original, pwPatch, ourName))
            return;

        var ours = AccessTools.Method(typeof(ProtectiveWardsModule), ourName);
        if (ours == null || original == null)
            return;

        harmony.Patch(original, prefix: new HarmonyMethod(ours) { priority = HarmonyLib.Priority.First });
    }

    /// <summary>
    /// Drop PW's Prefix from the game method. Finalizer stays so bypass depth is cleared.
    /// </summary>
    private static bool TryRemovePw(Harmony harmony, MethodInfo? original, MethodInfo? pwPatch, string ourName)
    {
        if (original == null || pwPatch == null)
        {
            LockSmith.Log?.LogWarning($"Compat ProtectiveWards: cannot gate {ourName} (missing method).");
            return false;
        }

        var present = ListsPw(original, pwPatch);
        try
        {
            if (present)
                harmony.Unpatch(original, pwPatch);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"Compat ProtectiveWards unpatch {ourName}: {ex.Message}");
        }

        var still = ListsPw(original, pwPatch);
        LockSmith.Log?.LogInfo(
            $"Compat ProtectiveWards {ourName}: PW patch {(present ? "found" : "absent")}, still attached={still}.");
        return true;
    }

    private static bool ListsPw(MethodBase original, MethodInfo pwPatch)
    {
        var info = Harmony.GetPatchInfo(original);
        if (info == null)
            return false;

        return ListsPatch(info.Prefixes, pwPatch);
    }

    private static bool ListsPatch(IEnumerable? patches, MethodInfo pwPatch)
    {
        if (patches == null)
            return false;

        foreach (var entry in patches)
        {
            if (entry is not Patch info)
                continue;

            if (SameMethod(info.PatchMethod, pwPatch))
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
        if (_typesOk || _insideEnabledPlayersArea != null || _insideEnabledPlayersAreaOut != null)
            return;

        try
        {
            var pwType = AccessTools.TypeByName(PwTypeName);
            if (pwType == null)
                return;

            _insideEnabledPlayersArea = AccessTools.Method(
                pwType,
                "InsideEnabledPlayersArea",
                new[] { typeof(Vector3), typeof(bool) });
            _insideEnabledPlayersAreaOut = AccessTools.Method(
                pwType,
                "InsideEnabledPlayersArea",
                new[] { typeof(Vector3), typeof(PrivateArea).MakeByRefType(), typeof(bool) });
            _hasAccessPlayer = AccessTools.Method(
                pwType,
                "HasAccessToWardOrConnectedWard",
                new[] { typeof(PrivateArea), typeof(Player) });
            _findProtectedWard = AccessTools.Method(pwType, "FindProtectedWard", new[] { typeof(Vector3) });

            _connectedAccessModeType = AccessTools.TypeByName("ProtectiveWards.WardConnectedAccessMode")
                                       ?? pwType.GetNestedType("WardConnectedAccessMode", BindingFlags.Public | BindingFlags.NonPublic);
            if (_connectedAccessModeType != null)
            {
                _hasAccessPlayerId = AccessTools.Method(
                    pwType,
                    "HasAccessToWardOrConnectedWard",
                    new[] { typeof(PrivateArea), typeof(long), _connectedAccessModeType });
                _connectedAccessModeField = AccessTools.Field(pwType, "wardAccessConnectedAccessMode");
            }

            _typesOk = (_insideEnabledPlayersArea != null || _insideEnabledPlayersAreaOut != null)
                       && (_hasAccessPlayer != null || _hasAccessPlayerId != null || _findProtectedWard != null);
        }
        catch (Exception ex)
        {
            _typesOk = false;
            LockSmith.Log?.LogDebug($"Compat ProtectiveWards resolve: {ex.Message}");
        }
    }
}

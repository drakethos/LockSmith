using System;
using System.Collections;
using System.Collections.Generic;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace LockSmith.Access;

/// <summary>
/// Phase 4: discover ward-locked chest/door Piece prefabs (vanilla + mods) and register
/// always-public Hammer clones. No ZDO / key toggle for this path.
/// </summary>
public static class PublicPieceRegistration
{
    public const string PrefabSuffix = "_public";
    public const string HammerCategory = "Public";

    private static readonly HashSet<string> RegisteredPublicPrefabs =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static readonly HashSet<string> AttemptedDonors =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private static bool _hooksInstalled;
    private static bool _delayedScanStarted;
    private static bool _categoryEnsured;

    /// <summary>True when this prefab/instance is a LockSmith public hammer clone.</summary>
    public static bool IsPublicPrefab(string? prefabName)
    {
        if (string.IsNullOrEmpty(prefabName))
            return false;

        var name = Utils.GetPrefabName(prefabName);
        if (RegisteredPublicPrefabs.Contains(name))
            return true;

        return name.EndsWith(PrefabSuffix, StringComparison.OrdinalIgnoreCase);
    }

    public static bool IsPublicPrefab(GameObject? go)
    {
        if (!go)
            return false;

        return IsPublicPrefab(go.name);
    }

    public static bool IsPublicPrefab(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return false;

        // ZDO prefab hash → name via ZNetScene when available.
        if (ZNetScene.instance != null)
        {
            var prefab = ZNetScene.instance.GetPrefab(zdo.GetPrefab());
            if (prefab)
                return IsPublicPrefab(prefab.name);
        }

        return IsPublicPrefab(nview.gameObject != null ? nview.gameObject.name : null);
    }

    public static bool IsPublicPiece(Container? container)
    {
        if (container == null)
            return false;

        var nview = PieceAccessState.GetNetView(container);
        if (IsPublicPrefab(nview))
            return true;

        return IsPublicPrefab(container.gameObject);
    }

    public static bool IsPublicPiece(Door? door)
    {
        if (door == null)
            return false;

        var nview = PieceAccessState.GetNetView(door);
        if (IsPublicPrefab(nview))
            return true;

        return IsPublicPrefab(door.gameObject);
    }

    /// <summary>Call once from vanilla-prefabs hook. Safe to call again; dedupes donors.</summary>
    public static void TryRegisterAll(string reason)
    {
        if (!LockSmithConfig.EnablePieceMode)
            return;

        try
        {
            EnsureHooks();
            var registered = RegisterPass(reason);
            if (registered > 0)
                LockSmith.Log?.LogInfo($"Public pieces: registered {registered} clone(s) ({reason}).");
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Public piece registration failed ({reason}): {ex}");
        }
    }

    private static void EnsureHooks()
    {
        if (_hooksInstalled)
            return;

        _hooksInstalled = true;
        PieceManager.OnPiecesRegistered += OnPiecesRegistered;

        if (!_delayedScanStarted && LockSmith.Instance != null)
        {
            _delayedScanStarted = true;
            LockSmith.Instance.StartCoroutine(DelayedRescan());
        }
    }

    private static void OnPiecesRegistered()
    {
        TryRegisterAll("OnPiecesRegistered");
    }

    private static IEnumerator DelayedRescan()
    {
        // Late piece mods often finish after the first vanilla pass.
        yield return new WaitForSeconds(2f);
        TryRegisterAll("delayed");
        yield return new WaitForSeconds(5f);
        TryRegisterAll("delayed-late");
    }

    private static int RegisterPass(string reason)
    {
        var deny = ParseNameSet(LockSmithConfig.PublicPieceDenyList);
        var allow = ParseNameSet(LockSmithConfig.PublicPieceAllowList);
        var donors = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var name in EnumerateCandidatePrefabNames())
            donors.Add(name);

        foreach (var name in allow)
            donors.Add(name);

        var count = 0;
        foreach (var donorName in donors)
        {
            if (string.IsNullOrWhiteSpace(donorName))
                continue;
            if (deny.Contains(donorName))
                continue;
            if (AttemptedDonors.Contains(donorName))
                continue;

            var result = TryRegisterClone(donorName, forceAllow: allow.Contains(donorName));
            if (result == RegisterResult.Missing)
                continue; // retry on deferred passes

            AttemptedDonors.Add(donorName);
            if (result == RegisterResult.Registered)
                count++;
        }

        return count;
    }

    private enum RegisterResult
    {
        Missing,
        Skipped,
        Registered
    }

    private static RegisterResult TryRegisterClone(string donorName, bool forceAllow)
    {
        var publicName = donorName + PrefabSuffix;
        if (RegisteredPublicPrefabs.Contains(publicName) || PieceManager.Instance.GetPiece(publicName) != null)
        {
            RegisteredPublicPrefabs.Add(publicName);
            return RegisterResult.Skipped;
        }

        GameObject? donor = null;
        try
        {
            donor = PrefabManager.Instance.GetPrefab(donorName);
        }
        catch
        {
            donor = null;
        }

        if (!donor)
            donor = FindLoadedPrefab(donorName);

        if (!donor)
        {
            if (forceAllow)
                LockSmith.Log?.LogWarning($"Public pieces: allow-list donor '{donorName}' not found yet.");
            return RegisterResult.Missing;
        }

        if (!IsEligibleDonor(donor, forceAllow, out var skipReason))
        {
            LockSmith.Log?.LogDebug($"Public pieces: skip '{donorName}' ({skipReason}).");
            return RegisterResult.Skipped;
        }

        try
        {
            var piece = donor.GetComponent<Piece>();
            if (piece == null)
                piece = donor.GetComponentInChildren<Piece>(true);
            if (piece == null)
                return RegisterResult.Skipped;

            var displayName = BuildDisplayName(piece);
            var config = new PieceConfig
            {
                PieceTable = PieceTables.Hammer,
                Category = HammerCategory,
                Enabled = true,
                Name = displayName,
                AllowedInDungeons = piece.m_allowedInDungeons
            };

            CopyRequirements(piece, config);
            CopyCraftingStation(piece, config);

            EnsurePublicCategory();

            var custom = new CustomPiece(publicName, donorName, config);
            if (custom.PiecePrefab == null || custom.Piece == null)
            {
                LockSmith.Log?.LogWarning($"Public pieces: CustomPiece failed for '{donorName}'.");
                return RegisterResult.Skipped;
            }

            ApplyAlwaysPublic(custom.PiecePrefab);
            custom.Piece.m_name = displayName;

            if (!PieceManager.Instance.AddPiece(custom))
            {
                LockSmith.Log?.LogWarning($"Public pieces: AddPiece rejected '{publicName}'.");
                return RegisterResult.Skipped;
            }

            RegisteredPublicPrefabs.Add(publicName);
            return RegisterResult.Registered;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"Public pieces: failed cloning '{donorName}': {ex.Message}");
            return RegisterResult.Skipped;
        }
    }

    private static string BuildDisplayName(Piece piece)
    {
        var raw = string.IsNullOrWhiteSpace(piece.m_name) ? piece.name : piece.m_name;
        var localized = raw;
        try
        {
            if (Localization.instance != null)
                localized = Localization.instance.Localize(raw);
        }
        catch
        {
            localized = raw;
        }

        if (string.IsNullOrWhiteSpace(localized))
            localized = piece.name;

        if (!LockSmithConfig.PublicPieceNameSuffix)
            return localized;

        var suffix = LockSmithLocalization.T(LockSmithLocalization.PublicNameSuffixToken);
        if (string.IsNullOrEmpty(suffix))
            suffix = " (public)";

        if (localized.EndsWith(suffix, StringComparison.OrdinalIgnoreCase))
            return localized;

        return localized + suffix;
    }

    private static void CopyRequirements(Piece piece, PieceConfig config)
    {
        if (piece.m_resources == null)
            return;

        foreach (var req in piece.m_resources)
        {
            if (req == null || req.m_resItem == null)
                continue;

            var itemGo = req.m_resItem.gameObject;
            if (!itemGo)
                continue;

            var itemName = Utils.GetPrefabName(itemGo);
            if (string.IsNullOrEmpty(itemName))
                continue;

            config.AddRequirement(new RequirementConfig
            {
                Item = itemName,
                Amount = Math.Max(1, req.m_amount),
                AmountPerLevel = req.m_amountPerLevel,
                Recover = req.m_recover
            });
        }
    }

    private static void CopyCraftingStation(Piece piece, PieceConfig config)
    {
        if (piece.m_craftingStation == null || !piece.m_craftingStation)
            return;

        var stationGo = piece.m_craftingStation.gameObject;
        if (!stationGo)
            return;

        var name = Utils.GetPrefabName(stationGo);
        if (!string.IsNullOrEmpty(name))
            config.CraftingStation = name;
    }

    private static void EnsurePublicCategory()
    {
        if (_categoryEnsured)
            return;

        try
        {
            // Valheim 1.0 / Jotunn: register the tab before AddPiece so the new-style hammer UI shows it.
            PieceManager.Instance.AddPieceCategory(HammerCategory);
            _categoryEnsured = true;
            LockSmith.Log?.LogInfo($"Public pieces: hammer category '{HammerCategory}' registered.");
        }
        catch (Exception ex)
        {
            // Category string on PieceConfig still works in many Jotunn builds; keep going.
            LockSmith.Log?.LogWarning($"Public pieces: AddPieceCategory('{HammerCategory}') failed: {ex.Message}");
            _categoryEnsured = true;
        }
    }

    private static void ApplyAlwaysPublic(GameObject prefab)
    {
        foreach (var container in prefab.GetComponentsInChildren<Container>(true))
        {
            if (container != null)
                container.m_checkGuardStone = false;
        }

        foreach (var door in prefab.GetComponentsInChildren<Door>(true))
        {
            if (door != null)
                door.m_checkGuardStone = false;
        }
    }

    private static bool IsEligibleDonor(GameObject donor, bool forceAllow, out string reason)
    {
        reason = "";
        var name = Utils.GetPrefabName(donor);
        if (IsPublicPrefab(name))
        {
            reason = "already public clone";
            return false;
        }

        var piece = donor.GetComponent<Piece>() ?? donor.GetComponentInChildren<Piece>(true);
        if (piece == null)
        {
            reason = "no Piece";
            return false;
        }

        var containers = donor.GetComponentsInChildren<Container>(true);
        var doors = donor.GetComponentsInChildren<Door>(true);
        var hasContainer = containers != null && containers.Length > 0;
        var hasDoor = doors != null && doors.Length > 0;
        if (!hasContainer && !hasDoor)
        {
            reason = "no Container/Door";
            return false;
        }

        if (hasContainer && !LockSmithConfig.EnableChests && !hasDoor)
        {
            reason = "chests disabled";
            return false;
        }

        if (hasDoor && !LockSmithConfig.EnableDoors && !hasContainer)
        {
            reason = "doors disabled";
            return false;
        }

        if (hasContainer && !LockSmithConfig.EnableChests)
            hasContainer = false;
        if (hasDoor && !LockSmithConfig.EnableDoors)
            hasDoor = false;
        if (!hasContainer && !hasDoor)
        {
            reason = "feature flags";
            return false;
        }

        if (hasContainer)
        {
            foreach (var c in containers!)
            {
                if (c == null)
                    continue;
                if (c.m_privacy == Container.PrivacySetting.Private
                    || c.m_privacy == Container.PrivacySetting.Group)
                {
                    reason = "private-family chest";
                    return false;
                }
            }
        }

        if (IsDonorAlreadyPublic(containers, doors))
        {
            reason = "already public (m_checkGuardStone false)";
            return false;
        }

        if (!forceAllow)
        {
            // Prefer buildable pieces; skip prop-like prefabs with no recipe.
            var hasRecipe = piece.m_resources != null && piece.m_resources.Length > 0;
            if (!hasRecipe)
            {
                reason = "no build requirements";
                return false;
            }
        }

        return true;
    }

    /// <summary>True when every Container/Door on the donor is already ward-open.</summary>
    private static bool IsDonorAlreadyPublic(Container[]? containers, Door[]? doors)
    {
        var saw = false;
        if (containers != null)
        {
            foreach (var c in containers)
            {
                if (c == null)
                    continue;
                saw = true;
                if (c.m_checkGuardStone)
                    return false;
            }
        }

        if (doors != null)
        {
            foreach (var d in doors)
            {
                if (d == null)
                    continue;
                saw = true;
                if (d.m_checkGuardStone)
                    return false;
            }
        }

        return saw;
    }

    private static IEnumerable<string> EnumerateCandidatePrefabNames()
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        // Vanilla baselines always considered (eligibility still applies).
        foreach (var name in DefaultDonors())
        {
            if (seen.Add(name))
                yield return name;
        }

        GameObject[] loaded;
        try
        {
            loaded = Resources.FindObjectsOfTypeAll<GameObject>();
        }
        catch
        {
            yield break;
        }

        if (loaded == null)
            yield break;

        foreach (var go in loaded)
        {
            if (!go)
                continue;

            // Skip scene instances; keep prefab assets.
            if (go.scene.IsValid())
                continue;

            var prefabName = Utils.GetPrefabName(go);
            if (string.IsNullOrEmpty(prefabName) || !seen.Add(prefabName))
                continue;

            if (IsPublicPrefab(prefabName))
                continue;

            var hasPiece = go.GetComponent<Piece>() != null || go.GetComponentInChildren<Piece>(true) != null;
            if (!hasPiece)
                continue;

            var hasTarget = go.GetComponentInChildren<Container>(true) != null
                            || go.GetComponentInChildren<Door>(true) != null;
            if (!hasTarget)
                continue;

            yield return prefabName;
        }
    }

    private static IEnumerable<string> DefaultDonors()
    {
        yield return "piece_chest_wood";
        yield return "piece_chest";
        yield return "wood_door";
        yield return "wood_gate";
        yield return "darkwood_gate";
        yield return "dvergrtown_wood_door";
    }

    private static GameObject? FindLoadedPrefab(string donorName)
    {
        GameObject[] loaded;
        try
        {
            loaded = Resources.FindObjectsOfTypeAll<GameObject>();
        }
        catch
        {
            return null;
        }

        if (loaded == null)
            return null;

        foreach (var go in loaded)
        {
            if (!go || go.scene.IsValid())
                continue;
            if (string.Equals(Utils.GetPrefabName(go), donorName, StringComparison.OrdinalIgnoreCase))
                return go;
        }

        return null;
    }

    private static HashSet<string> ParseNameSet(string? raw)
    {
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(raw))
            return set;

        foreach (var part in raw!.Split(new[] { ',', ';', '\n', '\r' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var name = part.Trim();
            if (name.Length > 0)
                set.Add(name);
        }

        return set;
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using BepInEx;
using DrakeModsLibs.API;
using DrakeModsLibs.Data;
using Jotunn.Configs;
using Jotunn.Entities;
using Jotunn.Managers;
using Jotunn.Utils;
using LockSmith.Access;
using UnityEngine;
using Paths = BepInEx.Paths;

namespace LockSmith;

/// <summary>
/// Official key = ArtItem <c>masterkey</c> from <c>Assets/Items/keys/keys.bundle</c> (<c>MasterKey</c>).
/// Grip flipped so the handle is in-hand; shaft blackmetal, skull bone.
/// </summary>
public static class ContentRegistration
{
    public const string OfficialKeyId = "masterkey";
    public const string BundleArtPrefab = "MasterKey";

    private static string? _registeredKeyPrefab;

    public static string? RegisteredKeyPrefab => _registeredKeyPrefab;

    public static IReadOnlyList<string> RegisteredKeyPrefabs =>
        string.IsNullOrEmpty(_registeredKeyPrefab)
            ? Array.Empty<string>()
            : new[] { _registeredKeyPrefab! };

    public static bool IsKeyPrefab(string? name)
    {
        if (string.IsNullOrEmpty(name))
            return false;

        if (!string.IsNullOrEmpty(_registeredKeyPrefab))
        {
            var key = _registeredKeyPrefab!;
            if (name!.Equals(key, StringComparison.OrdinalIgnoreCase)
                || name.StartsWith(key + "(", StringComparison.OrdinalIgnoreCase))
                return true;
        }

        return name!.Equals(OfficialKeyId, StringComparison.OrdinalIgnoreCase)
               || name.StartsWith(OfficialKeyId + "(", StringComparison.OrdinalIgnoreCase)
               || name.Equals("LockSmithKey", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("LockSmithKey(", StringComparison.OrdinalIgnoreCase)
               || name.Equals("MasterKey", StringComparison.OrdinalIgnoreCase)
               || name.StartsWith("MasterKey(", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// ArtItem customize — recipe from LockSmith <c>03 Key</c> config.
    /// Name/description tokens are stamped in <see cref="FinalizeOfficialKeyFromKeysPack"/>.
    /// </summary>
    public static void CustomizeMasterKeyArtItem(DrakeModsLibs.Art.ArtItemContext item)
    {
        if (item == null)
            return;

        if (!item.Id.Equals(OfficialKeyId, StringComparison.OrdinalIgnoreCase))
        {
            LockSmith.Log?.LogWarning($"[ArtForge] Ignoring unexpected art item '{item.Id}'.");
            return;
        }

        // Prefer LockSmith KeyName for the interim ArtForge token; Sanitize overwrites to KeyNameToken.
        item.DisplayName = string.IsNullOrWhiteSpace(LockSmithConfig.KeyName)
            ? "Locksmith Key"
            : LockSmithConfig.KeyName.Trim();
        item.Description = "$" + LockSmithLocalization.KeyDescToken;
        item.CraftingStation = string.IsNullOrWhiteSpace(LockSmithConfig.KeyCraftingStation)
            ? null
            : LockSmithConfig.KeyCraftingStation.Trim();

        var mats = ParseMaterials(LockSmithConfig.KeyMaterials);
        if (mats.Count > 0)
        {
            item.RequirementItem = mats[0].PrefabName;
            item.RequirementAmount = mats[0].Amount;
        }
    }

    /// <summary>
    /// Confirm ArtItemLoader registered <c>masterkey</c>, then flip grip + bone skull / iron shaft.
    /// </summary>
    public static void FinalizeOfficialKeyFromKeysPack()
    {
        _registeredKeyPrefab = null;

        try
        {
            var prefab = PrefabManager.Instance?.GetPrefab(OfficialKeyId);
            if (!prefab)
            {
                LockSmith.Log?.LogError(
                    $"Official key '{OfficialKeyId}' missing after ArtItemLoader. " +
                    "Need Assets/Items/keys/masterkey.json + keys.bundle with artPrefab MasterKey.");
                return;
            }

            var icon = LoadMasterKeyIcon();
            SanitizeOfficialKey(prefab, icon);
            FixMasterKeyHoldAndLook(prefab);
            EnsureRootZNetView(prefab);
            StampRenameHandOffOnPrefab(prefab);

            _registeredKeyPrefab = OfficialKeyId;
            LockSmith.Log?.LogInfo(
                $"Official LockSmith key ready: '{_registeredKeyPrefab}' " +
                $"(keys.bundle {BundleArtPrefab}, handle in hand, bone skull).");
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Failed to finalize official key from keys pack: {ex}");
        }
    }

    /// <summary>Flag the whole key type once on the prefab ItemData (inherited by new keys).</summary>
    static void StampRenameHandOffOnPrefab(GameObject prefab)
    {
        if (!prefab)
            return;

        var drop = prefab.GetComponent<ItemDrop>();
        if (!drop || drop.m_itemData == null)
            return;

        ChestAccessService.EnsureRenameHandOff(drop.m_itemData);
    }

    /// <summary>
    /// ArtItemLoader parked the bit in the hand — re-orient so the hex handle is gripped.
    /// Bone mat only on skull accent slots / keyskull — never replace the whole key mesh.
    /// </summary>
    private static void FixMasterKeyHoldAndLook(GameObject prefab)
    {
        if (!prefab)
            return;

        var art = FindArtVisual(prefab);
        if (art)
        {
            // AssetForge packs world-drop physics on MasterKey. Held art must be visual-only
            // or equip parents a live Rigidbody+gravity under the hand and the key hits the floor.
            StripArtWorldPhysics(art);
            ReorientHeldKey(art, handleAtMax: true);
        }
        else
            LockSmith.Log?.LogWarning("masterkey art visual not found; grip flip skipped.");

        ApplyBoneToSkullOnly(prefab);
    }

    /// <summary>
    /// AssetForge MasterKey is a full world-drop prefab (physics + item_particle VFX).
    /// Art under the hand / donor ItemDrop must be mesh-only or you get floor falls and
    /// floating black particle quads.
    /// </summary>
    private static void StripArtWorldPhysics(GameObject visual)
    {
        if (!visual)
            return;

        var stripped = 0;
        foreach (var rb in visual.GetComponentsInChildren<Rigidbody>(true))
        {
            if (!rb)
                continue;
            UnityEngine.Object.DestroyImmediate(rb);
            stripped++;
        }

        foreach (var col in visual.GetComponentsInChildren<Collider>(true))
        {
            if (!col)
                continue;
            UnityEngine.Object.DestroyImmediate(col);
            stripped++;
        }

        // ParticleSystem is Component, not MonoBehaviour — black billboard quads if left on.
        foreach (var ps in visual.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!ps)
                continue;
            UnityEngine.Object.DestroyImmediate(ps);
            stripped++;
        }

        foreach (var psr in visual.GetComponentsInChildren<ParticleSystemRenderer>(true))
        {
            if (!psr)
                continue;
            UnityEngine.Object.DestroyImmediate(psr);
            stripped++;
        }

        foreach (var behaviour in visual.GetComponentsInChildren<MonoBehaviour>(true))
        {
            if (!behaviour)
                continue;
            var n = behaviour.GetType().Name;
            if (n is not ("Floating" or "ZSyncTransform" or "ItemDrop"))
                continue;
            UnityEngine.Object.DestroyImmediate(behaviour);
            stripped++;
        }

        if (stripped > 0)
            LockSmith.Log?.LogInfo($"masterkey art: stripped {stripped} drop/VFX component(s).");
    }

    private static GameObject? FindArtVisual(GameObject prefab)
    {
        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (!t || t == prefab.transform)
                continue;
            if (t.name.Equals("art", StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
        {
            if (!t || t == prefab.transform)
                continue;
            if (t.name.Equals(BundleArtPrefab, StringComparison.OrdinalIgnoreCase) ||
                t.name.Equals("MasterKey", StringComparison.OrdinalIgnoreCase))
                return t.gameObject;
        }

        return null;
    }

    private static void ReorientHeldKey(GameObject visual, bool handleAtMax)
    {
        if (!visual)
            return;

        visual.transform.localRotation = Quaternion.identity;
        visual.transform.localPosition = Vector3.zero;

        if (!TryGetArtLocalBounds(visual, out var bounds))
            return;

        var size = bounds.size;
        var axis = 0;
        if (size.y > size[axis])
            axis = 1;
        if (size.z > size[axis])
            axis = 2;

        // Yaw 180 around the hand so the skull face points outward (away from the thigh),
        // not inward at the body. Shaft-roll alone does not fix that.
        visual.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);

        var handleLocal = bounds.center;
        handleLocal[axis] = handleAtMax ? bounds.max[axis] : bounds.min[axis];
        var scaled = Vector3.Scale(handleLocal, visual.transform.localScale);
        visual.transform.localPosition = -(visual.transform.localRotation * scaled);

        LockSmith.Log?.LogInfo(
            $"masterkey grip: handle={(handleAtMax ? "max" : "min")} axis={axis} " +
            $"yaw180 faceOut bounds={size}.");
    }

    private static bool TryGetArtLocalBounds(GameObject visual, out Bounds bounds)
    {
        bounds = default;
        var first = true;
        foreach (var filter in visual.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter || !filter.sharedMesh)
                continue;

            var b = filter.sharedMesh.bounds;
            var corners = new[]
            {
                new Vector3(b.min.x, b.min.y, b.min.z),
                new Vector3(b.min.x, b.min.y, b.max.z),
                new Vector3(b.min.x, b.max.y, b.min.z),
                new Vector3(b.min.x, b.max.y, b.max.z),
                new Vector3(b.max.x, b.min.y, b.min.z),
                new Vector3(b.max.x, b.min.y, b.max.z),
                new Vector3(b.max.x, b.max.y, b.min.z),
                new Vector3(b.max.x, b.max.y, b.max.z),
            };

            foreach (var c in corners)
            {
                var world = filter.transform.TransformPoint(c);
                var local = visual.transform.InverseTransformPoint(world);
                if (first)
                {
                    bounds = new Bounds(local, Vector3.zero);
                    first = false;
                }
                else
                {
                    bounds.Encapsulate(local);
                }
            }
        }

        return !first;
    }

    /// <summary>
    /// Bone/trophy material on skull pieces only. Leave shaft materials from the pack alone.
    /// </summary>
    private static void ApplyBoneToSkullOnly(GameObject prefab)
    {
        if (!prefab)
            return;

        var bone = FindSkullMaterial();
        if (!bone)
        {
            LockSmith.Log?.LogWarning("No trophy bone material found; skull keeps pack mats.");
            return;
        }

        var boneSlots = 0;
        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer)
                continue;
            var typeName = renderer.GetType().Name;
            if (typeName != "MeshRenderer" && typeName != "SkinnedMeshRenderer")
                continue;

            // Dedicated keyskull / skull child — full bone.
            if (IsSkullRenderer(renderer))
            {
                renderer.enabled = true;
                renderer.sharedMaterial = bone;
                boneSlots++;
                continue;
            }

            // Multi-slot key mesh: only replace accent slots (e.g. silver_necklace), never mainkey/iron.
            var mats = renderer.sharedMaterials;
            if (mats == null || mats.Length < 2)
                continue;

            var changed = false;
            for (var i = 0; i < mats.Length; i++)
            {
                var mat = mats[i];
                var n = mat ? (mat.name ?? "") : "";
                if (n.IndexOf("mainkey", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("iron", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("blackmetal", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("black", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (n.IndexOf("necklace", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("silver", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("skull", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("bone", StringComparison.OrdinalIgnoreCase) >= 0 ||
                    n.IndexOf("gem", StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    mats[i] = bone;
                    boneSlots++;
                    changed = true;
                }
            }

            if (changed)
            {
                renderer.enabled = true;
                renderer.sharedMaterials = mats;
            }
        }

        LockSmith.Log?.LogInfo($"masterkey bone-on-skull only: slots={boneSlots} mat='{bone.name}'.");
    }

    /// <summary>
    /// Pack MasterKey may still reference NurbsPath (.resS = black spike). Disable that node,
    /// keep/attach embedded keyskull, and prefer the shaft already under attach/key.
    /// </summary>
    private static void FixKeysPackMasterKeyVisual(GameObject prefab)
    {
        if (!prefab)
            return;

        var killed = 0;
        foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
        {
            if (!filter)
                continue;

            var mesh = filter.sharedMesh;
            var meshName = mesh != null ? mesh.name ?? "" : "";
            // GO may still be named NurbsPath even when the mesh was replaced with embedded "key backup".
            var broken = mesh == null
                         || mesh.vertexCount <= 0
                         || meshName.IndexOf("NurbsPath", StringComparison.OrdinalIgnoreCase) >= 0;

            if (!broken)
                continue;

            foreach (var r in filter.GetComponentsInChildren<Renderer>(true))
            {
                if (r)
                    r.enabled = false;
            }

            filter.gameObject.SetActive(false);
            killed++;
        }

        if (killed > 0)
            LockSmith.Log?.LogInfo($"Disabled {killed} broken spike mesh node(s) on '{prefab.name}'.");
        else
            LockSmith.Log?.LogInfo($"MasterKey shaft mesh looks embedded — leaving it enabled.");

        // keyskull is already under MasterKey/attach in a good pack — only attach if missing.
        AttachKeySkullFromKeysBundle(prefab);
    }

    private static void AttachKeySkullFromKeysBundle(GameObject keyPrefab)
    {
        if (!keyPrefab)
            return;

        foreach (var t in keyPrefab.GetComponentsInChildren<Transform>(true))
        {
            if (t && t.name.Equals("keyskull", StringComparison.OrdinalIgnoreCase) && t != keyPrefab.transform)
            {
                var filter = t.GetComponent<MeshFilter>();
                if (filter && filter.sharedMesh && filter.sharedMesh.vertexCount > 0)
                {
                    LockSmith.Log?.LogInfo(
                        $"keys.bundle keyskull already under '{keyPrefab.name}' (mesh '{filter.sharedMesh.name}', verts={filter.sharedMesh.vertexCount}).");
                    return;
                }
            }
        }

        var bundle = TryLoadKeysBundle();
        if (bundle == null)
        {
            LockSmith.Log?.LogWarning("keys.bundle not loadable; falling back to trophy skull mesh.");
            AttachSkullDecoration(keyPrefab);
            return;
        }

        var source = bundle.LoadAsset<GameObject>("keyskull")
                     ?? bundle.LoadAsset<GameObject>("assets/drake/locksmit/keyskull.prefab");
        if (!source)
        {
            // Nested GOs are not always LoadAsset-able — scan all.
            foreach (var go in bundle.LoadAllAssets<GameObject>())
            {
                if (go != null && go.name.Equals("keyskull", StringComparison.OrdinalIgnoreCase))
                {
                    source = go;
                    break;
                }
            }
        }

        if (!source)
        {
            LockSmith.Log?.LogWarning("keys.bundle has no 'keyskull' GameObject; using trophy skull.");
            AttachSkullDecoration(keyPrefab);
            return;
        }

        Mesh? mesh = null;
        Material[]? mats = null;
        var filterSrc = source.GetComponentInChildren<MeshFilter>(true);
        if (filterSrc && filterSrc.sharedMesh)
        {
            mesh = filterSrc.sharedMesh;
            var mr = filterSrc.GetComponent<MeshRenderer>();
            if (mr)
                mats = mr.sharedMaterials;
        }

        if (!mesh || mesh.vertexCount <= 0)
        {
            LockSmith.Log?.LogWarning("keyskull has no usable mesh; using trophy skull.");
            AttachSkullDecoration(keyPrefab);
            return;
        }

        var parent = keyPrefab.transform.Find("art")
                     ?? keyPrefab.transform.Find("attach")
                     ?? keyPrefab.transform;
        var skull = new GameObject("keyskull");
        skull.transform.SetParent(parent, false);
        skull.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        skull.transform.localRotation = Quaternion.Euler(-10f, 180f, 0f);
        skull.transform.localScale = Vector3.one * 0.35f;

        var mf = skull.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var renderer = skull.AddComponent<MeshRenderer>();
        if (mats != null && mats.Length > 0)
            renderer.sharedMaterials = mats;

        LockSmith.Log?.LogInfo(
            $"Attached keys.bundle keyskull mesh '{mesh.name}' (verts={mesh.vertexCount}) onto '{keyPrefab.name}'.");
    }

    private static AssetBundle? TryLoadKeysBundle()
    {
        try
        {
            var plugin = LockSmith.Instance;
            if (plugin == null)
                return null;

            var pluginDir = Path.GetDirectoryName(plugin.Info.Location);
            if (string.IsNullOrEmpty(pluginDir))
                return null;

            var bundleFile = Path.Combine(pluginDir, "Assets", "Items", "keys", "keys.bundle");
            if (!File.Exists(bundleFile))
                return null;

            var pluginsRoot = Paths.PluginPath;
            if (string.IsNullOrEmpty(pluginsRoot))
                return null;

            var root = pluginsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            if (!bundleFile.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;

            var relative = bundleFile.Substring(root.Length).Replace('\\', '/');
            return AssetUtils.LoadAssetBundle(relative);
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"keys.bundle load failed: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Add a mesh-only skull visual. Never Instantiate trophy GameObjects (they carry ZNetView).
    /// </summary>
    private static void AttachSkullDecoration(GameObject keyPrefab)
    {
        if (!keyPrefab)
            return;

        // Remove any prior skull child (mesh-only rebuild).
        var stale = new List<GameObject>();
        foreach (var t in keyPrefab.GetComponentsInChildren<Transform>(true))
        {
            if (!t || t == keyPrefab.transform)
                continue;
            if (t.name.Equals("keyskull", StringComparison.OrdinalIgnoreCase))
                stale.Add(t.gameObject);
        }

        foreach (var go in stale)
        {
            if (go)
                UnityEngine.Object.DestroyImmediate(go);
        }

        var source = FindSkullMeshSource();
        if (!source)
        {
            LockSmith.Log?.LogWarning("No skull mesh source found; key will be CryptKey-only.");
            return;
        }

        Mesh? mesh = null;
        Material[]? mats = null;

        var filter = source.GetComponent<MeshFilter>();
        if (filter && filter.sharedMesh)
        {
            mesh = filter.sharedMesh;
            var mr = source.GetComponent<MeshRenderer>();
            if (mr)
                mats = mr.sharedMaterials;
        }
        else if (source is SkinnedMeshRenderer skinned && skinned.sharedMesh)
        {
            mesh = skinned.sharedMesh;
            mats = skinned.sharedMaterials;
        }

        if (!mesh)
        {
            LockSmith.Log?.LogWarning($"Skull source '{source.name}' has no mesh; skipping decoration.");
            return;
        }

        var skull = new GameObject("keyskull");
        skull.SetActive(true);
        skull.transform.SetParent(keyPrefab.transform, false);
        skull.transform.localPosition = new Vector3(0f, 0.08f, 0f);
        skull.transform.localRotation = Quaternion.Euler(-10f, 180f, 0f);
        skull.transform.localScale = Vector3.one * 0.18f;

        var mf = skull.AddComponent<MeshFilter>();
        mf.sharedMesh = mesh;
        var renderer = skull.AddComponent<MeshRenderer>();
        if (mats != null && mats.Length > 0)
            renderer.sharedMaterials = mats;
        else
        {
            var bone = FindSkullMaterial();
            if (bone)
                renderer.sharedMaterial = bone;
        }

        LockSmith.Log?.LogInfo(
            $"Attached mesh-only skull from '{source.name}' (mesh '{mesh.name}') onto '{keyPrefab.name}'.");
    }

    /// <summary>
    /// World spawn requires a live root ZNetView to claim ZNetView.m_initZDO.
    /// Without it Valheim Instantiates forever: "ZDO … not used when creating object".
    /// </summary>
    private static void EnsureRootZNetView(GameObject prefab)
    {
        if (!prefab)
            return;

        prefab.SetActive(true);

        var donor = PrefabManager.Instance?.GetPrefab("LeatherScraps")
                    ?? PrefabManager.Instance?.GetPrefab("CryptKey");
        ZNetView? donorView = null;
        if (donor)
            donorView = donor.GetComponent<ZNetView>() ?? donor.GetComponentInChildren<ZNetView>(true);

        // Collapse to a single root view — nested views steal/skip init ZDO.
        var existing = prefab.GetComponentsInChildren<ZNetView>(true);
        ZNetView? keep = prefab.GetComponent<ZNetView>();
        foreach (var view in existing)
        {
            if (!view)
                continue;
            if (keep && view == keep)
                continue;
            if (!keep && view.gameObject == prefab)
            {
                keep = view;
                continue;
            }

            UnityEngine.Object.DestroyImmediate(view);
        }

        if (!keep)
        {
            keep = prefab.AddComponent<ZNetView>();
            LockSmith.Log?.LogWarning($"Added missing root ZNetView on '{prefab.name}'.");
        }

        keep.enabled = true;
        if (donorView)
        {
            keep.m_persistent = donorView.m_persistent;
            keep.m_distant = donorView.m_distant;
            keep.m_type = donorView.m_type;
            keep.m_syncInitialScale = donorView.m_syncInitialScale;
        }
        else
        {
            keep.m_persistent = true;
            keep.m_distant = false;
        }

        var viewsLeft = prefab.GetComponentsInChildren<ZNetView>(true).Length;
        LockSmith.Log?.LogInfo($"'{prefab.name}' ZNetView check: root={(bool)keep}, total={viewsLeft}.");
    }

    private static Renderer? FindSkullMeshSource()
    {
        foreach (var prefabName in new[] { "TrophySkeleton", "TrophyDraugr", "Skeleton" })
        {
            var go = PrefabManager.Instance?.GetPrefab(prefabName);
            if (!go)
                continue;

            Renderer? best = null;
            var bestScore = -1;
            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer)
                    continue;
                var typeName = renderer.GetType().Name;
                if (typeName != "MeshRenderer" && typeName != "SkinnedMeshRenderer")
                    continue;

                var n = renderer.name ?? "";
                var score = 0;
                if (n.IndexOf("skull", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 10;
                if (n.IndexOf("head", StringComparison.OrdinalIgnoreCase) >= 0)
                    score += 5;
                if (renderer is MeshRenderer)
                    score += 2;

                var mat = renderer.sharedMaterial;
                if (mat)
                {
                    var mn = mat.name ?? "";
                    if (mn.IndexOf("skull", StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 8;
                    if (mn.IndexOf("bone", StringComparison.OrdinalIgnoreCase) >= 0)
                        score += 4;
                }

                if (score > bestScore)
                {
                    bestScore = score;
                    best = renderer;
                }
            }

            if (best)
                return best;
        }

        return null;
    }

    private static void SanitizeOfficialKey(GameObject? prefab, Sprite? icon)
    {
        if (!prefab)
            return;

        var drop = prefab.GetComponent<ItemDrop>();
        if (drop?.m_itemData?.m_shared == null)
            return;

        var shared = drop.m_itemData.m_shared;
        shared.m_name = "$" + LockSmithLocalization.KeyNameToken;
        shared.m_description = "$" + LockSmithLocalization.KeyDescToken;
        shared.m_itemType = ItemDrop.ItemData.ItemType.Tool;
        shared.m_maxStackSize = 1;
        shared.m_variants = 1;
        shared.m_attack = new Attack();
        shared.m_secondaryAttack = new Attack();
        shared.m_useDurability = false;

        if (icon)
            shared.m_icons = new[] { icon };

        if (drop.m_itemData.m_dropPrefab == null)
            drop.m_itemData.m_dropPrefab = prefab;
    }

    /// <summary>
    /// Blackmetal on the shaft; vanilla skull/bone material on keyskull.
    /// Strip lights/particles and kill leftover emissive glow.
    /// </summary>
    private static void ApplyBlackIronNoGlow(GameObject? prefab)
    {
        if (!prefab)
            return;

        var lightsOff = 0;
        foreach (var light in prefab.GetComponentsInChildren<Light>(true))
        {
            if (!light)
                continue;
            // Do NOT SetActive(false) on the GameObject — that can hide a ZNetView parent/child.
            light.enabled = false;
            light.intensity = 0f;
            lightsOff++;
        }

        var fxOff = 0;
        foreach (var ps in prefab.GetComponentsInChildren<ParticleSystem>(true))
        {
            if (!ps)
                continue;
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            var emission = ps.emission;
            emission.enabled = false;
            fxOff++;
        }

        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer)
                continue;
            var typeName = renderer.GetType().Name;
            if (typeName is "ParticleSystemRenderer" or "TrailRenderer" or "LineRenderer")
            {
                renderer.enabled = false;
                fxOff++;
            }
        }

        var black = FindBlackIronMaterial();
        var skull = FindSkullMaterial();
        var blackSlots = 0;
        var skullSlots = 0;

        foreach (var renderer in prefab.GetComponentsInChildren<Renderer>(true))
        {
            if (!renderer)
                continue;
            var typeName = renderer.GetType().Name;
            if (typeName != "MeshRenderer" && typeName != "SkinnedMeshRenderer")
                continue;

            renderer.enabled = true;
            if (IsSkullRenderer(renderer))
            {
                if (skull)
                {
                    renderer.sharedMaterial = skull;
                    skullSlots++;
                }
                else if (black)
                {
                    renderer.sharedMaterial = black;
                    blackSlots++;
                }
            }
            else if (black)
            {
                renderer.sharedMaterial = black;
                blackSlots++;
            }
            else
            {
                // Keep bundle mat but strip emissive so the bit doesn't stay cyan.
                KillEmission(renderer);
            }
        }

        if (!black)
            LockSmith.Log?.LogWarning("Blackmetal material not found; shaft may keep bundle mats.");
        if (!skull)
            LockSmith.Log?.LogWarning("Skull/bone material not found; skull may stay blackmetal.");

        LockSmith.Log?.LogInfo(
            $"Key look: blackmetal={blackSlots}, skullMat={skullSlots} ('{skull?.name}'), " +
            $"lightsOff={lightsOff}, fxOff={fxOff}.");
    }

    private static bool IsSkullRenderer(Renderer renderer)
    {
        Transform? t = renderer.transform;
        while (t)
        {
            var n = t.name ?? "";
            if (n.IndexOf("skull", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.IndexOf("keyskull", StringComparison.OrdinalIgnoreCase) >= 0 ||
                n.Equals("skullKey2", StringComparison.OrdinalIgnoreCase))
                return true;
            t = t.parent;
        }

        return false;
    }

    private static void KillEmission(Renderer renderer)
    {
        // Clone so we don't mutate shared Valheim mats when zeroing emission leftovers.
        var mats = renderer.materials;
        for (var i = 0; i < mats.Length; i++)
        {
            var mat = mats[i];
            if (!mat)
                continue;
            if (mat.HasProperty("_EmissionColor"))
                mat.SetColor("_EmissionColor", Color.black);
            if (mat.HasProperty("_EmissionColorUI"))
                mat.SetColor("_EmissionColorUI", Color.black);
            if (mat.HasProperty("_EmissionIntensity"))
                mat.SetFloat("_EmissionIntensity", 0f);
            if (mat.IsKeywordEnabled("_EMISSION"))
                mat.DisableKeyword("_EMISSION");
        }

        renderer.materials = mats;
    }

    private static Material? FindBlackIronMaterial()
    {
        return FindMeshMaterialFromPrefabs(
            new[]
            {
                "AxeBlackMetal", "PickaxeBlackMetal", "SwordBlackMetal", "BlackMetal",
                "AtgeirBlackmetal", "KnifeBlackMetal",
            });
    }

    private static Material? FindSkullMaterial()
    {
        // Trophy bone only — never living "Skeleton" character mats (those spike static meshes).
        var fromTrophy = FindMeshMaterialFromPrefabs(
            new[] { "TrophySkeleton", "BoneFragments" },
            preferNameContains: new[] { "bone", "skull", "trophy" });
        if (fromTrophy)
            return fromTrophy;

        foreach (var mat in Resources.FindObjectsOfTypeAll<Material>())
        {
            if (!mat)
                continue;
            var n = mat.name ?? "";
            if (n.IndexOf("bone", StringComparison.OrdinalIgnoreCase) < 0 &&
                n.IndexOf("skull", StringComparison.OrdinalIgnoreCase) < 0)
                continue;
            if (n.Equals("Skeleton", StringComparison.OrdinalIgnoreCase))
                continue;
            if (n.IndexOf("particle", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            if (n.IndexOf("vfx", StringComparison.OrdinalIgnoreCase) >= 0)
                continue;
            return mat;
        }

        return null;
    }

    private static Material? FindMeshMaterialFromPrefabs(string[] prefabNames, string[]? preferNameContains = null)
    {
        Material? fallback = null;
        foreach (var prefabName in prefabNames)
        {
            var go = PrefabManager.Instance?.GetPrefab(prefabName);
            if (!go)
                continue;

            foreach (var renderer in go.GetComponentsInChildren<Renderer>(true))
            {
                if (!renderer)
                    continue;
                var typeName = renderer.GetType().Name;
                if (typeName != "MeshRenderer" && typeName != "SkinnedMeshRenderer")
                    continue;
                var mat = renderer.sharedMaterial;
                if (!mat)
                    continue;
                var n = mat.name ?? "";
                if (n.IndexOf("particle", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;
                if (n.IndexOf("vfx", StringComparison.OrdinalIgnoreCase) >= 0)
                    continue;

                if (preferNameContains != null)
                {
                    var hit = false;
                    foreach (var needle in preferNameContains)
                    {
                        if (n.IndexOf(needle, StringComparison.OrdinalIgnoreCase) >= 0)
                        {
                            hit = true;
                            break;
                        }
                    }

                    if (hit)
                        return mat;
                    fallback ??= mat;
                    continue;
                }

                return mat;
            }
        }

        return fallback;
    }

    private static Sprite? LoadMasterKeyIcon()
    {
        try
        {
            var plugin = LockSmith.Instance;
            if (plugin == null)
                return null;

            var pluginDir = Path.GetDirectoryName(plugin.Info.Location);
            if (string.IsNullOrEmpty(pluginDir))
                return null;

            var iconFile = Path.Combine(pluginDir, "Assets", "masterkey_icon.png");
            if (!File.Exists(iconFile))
                return null;

            var pluginsRoot = Paths.PluginPath;
            if (string.IsNullOrEmpty(pluginsRoot))
                return null;

            var root = pluginsRoot.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar)
                       + Path.DirectorySeparatorChar;
            if (!iconFile.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                return null;

            var relative = iconFile.Substring(root.Length).Replace('\\', '/');
            var sprite = AssetUtils.LoadSpriteFromFile(relative);
            if (sprite)
                LockSmith.Log?.LogInfo($"Loaded master key icon from {relative}");
            return sprite ? sprite : null;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogWarning($"Failed to load master key icon: {ex.Message}");
            return null;
        }
    }

    internal static List<(string PrefabName, int Amount)> ParseMaterials(string? raw)
    {
        var list = new List<(string PrefabName, int Amount)>();
        if (string.IsNullOrWhiteSpace(raw))
            return list;

        foreach (var segment in raw!.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
        {
            var s = segment.Trim();
            var idx = s.LastIndexOf(':');
            if (idx <= 0 || idx >= s.Length - 1)
            {
                LockSmith.Log?.LogWarning($"Ignoring invalid key material segment: \"{s}\"");
                continue;
            }

            var namePart = s.Substring(0, idx).Trim();
            var amtPart = s.Substring(idx + 1).Trim();
            if (!int.TryParse(amtPart, NumberStyles.Integer, CultureInfo.InvariantCulture, out var amount) ||
                amount <= 0)
            {
                LockSmith.Log?.LogWarning($"Ignoring invalid key material amount: \"{s}\"");
                continue;
            }

            list.Add((namePart, amount));
        }

        return list;
    }
}

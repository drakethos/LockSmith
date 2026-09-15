using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Persists public/private on the piece ZDO and mirrors it onto the live
/// <see cref="Container.m_checkGuardStone"/> / <see cref="Door.m_checkGuardStone"/> fields
/// (the vanilla ward gate on that instance).
/// </summary>
public static class PieceAccessState
{
    private static readonly int PublicFlagHash = GameHookTargets.ZdoPublicFlag.GetStableHashCode();
    private static readonly int ManagedFlagHash = GameHookTargets.ZdoManagedFlag.GetStableHashCode();
    private static readonly int GroupModeHash = GameHookTargets.ZdoGroupMode.GetStableHashCode();

    public static bool IsPublic(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        return zdo != null && zdo.GetInt(PublicFlagHash, 0) == 1;
    }

    /// <summary>
    /// LockSmith-designated piece. Explicit flag, or legacy inference from any prior LockSmith write.
    /// When <see cref="LockSmithConfig.EnableDesignate"/> is off, every piece is treated as usable.
    /// </summary>
    public static bool IsManaged(ZNetView? nview)
    {
        if (!LockSmithConfig.EnableDesignate)
            return true;

        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return false;

        if (zdo.GetInt(ManagedFlagHash, 0) == 1)
            return true;

        // Pre-0.3.5 worlds: any LockSmith state means the piece was already claimed.
        if (zdo.GetInt(PublicFlagHash, 0) == 1)
            return true;
        if (zdo.GetInt(GroupModeHash, 0) == 1)
            return true;
        if (PieceGuestAccess.IsOptInReady(nview))
            return true;
        return PieceGuestAccess.GetGuests(nview).Count > 0;
    }

    /// <summary>True when designate UX should show [Not LockSmith] / Enable LockSmith.</summary>
    public static bool NeedsDesignate(ZNetView? nview) =>
        LockSmithConfig.EnableDesignate && !IsManaged(nview);

    /// <summary>Persist designation. Returns true when this call newly set the flag.</summary>
    public static bool MarkManaged(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return false;

        if (zdo.GetInt(ManagedFlagHash, 0) == 1)
            return false;

        zdo.Set(ManagedFlagHash, 1, false);
        return true;
    }

    /// <summary>Strip all LockSmith ZDO state and restore vanilla ward/privacy behavior on this instance.</summary>
    public static void ClearLockSmithData(ZNetView nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        zdo.Set(ManagedFlagHash, 0, false);
        zdo.Set(PublicFlagHash, 0, false);
        zdo.Set(GroupModeHash, 0, false);
        PieceGuestAccess.ClearGuests(nview);
        PieceGuestAccess.SetOptInReady(nview, false);
        ApplyGuardStoneToNetView(nview, isPublic: false);

        foreach (var container in nview.GetComponentsInChildren<Container>(true))
        {
            if (GroupAccessState.IsPrivateFamilyChest(container))
                GroupAccessState.SyncPrivacyFromZdo(container);
        }
    }

    public static void SetPublic(ZNetView nview, bool isPublic)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        MarkManaged(nview);
        zdo.Set(PublicFlagHash, isPublic ? 1 : 0, false);
        ApplyGuardStoneToNetView(nview, isPublic);
    }

    /// <summary>
    /// Re-apply ZDO → component. Call on Awake and before interact so peers stay in sync
    /// after the ZDO replicates.
    /// </summary>
    public static void SyncGuardStoneFromZdo(Container container)
    {
        if (container == null)
            return;

        var nview = GetNetView(container);
        if (nview == null || !nview.IsValid())
            return;

        ApplyGuardStone(container, IsPublic(nview));
    }

    public static void SyncGuardStoneFromZdo(Door door)
    {
        if (door == null)
            return;

        var nview = GetNetView(door);
        if (nview == null || !nview.IsValid())
            return;

        ApplyGuardStone(door, IsPublic(nview));
    }

    public static void SyncGuardStoneFromZdo(ZNetView nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        ApplyGuardStoneToNetView(nview, IsPublic(nview));
    }

    private static void ApplyGuardStoneToNetView(ZNetView nview, bool isPublic)
    {
        foreach (var container in nview.GetComponentsInChildren<Container>(true))
            ApplyGuardStone(container, isPublic);

        foreach (var door in nview.GetComponentsInChildren<Door>(true))
            ApplyGuardStone(door, isPublic);
    }

    /// <summary>
    /// Vanilla: ward is checked when <c>m_checkGuardStone</c> is true.
    /// Public LockSmith pieces turn that off on this instance only (not the prefab).
    /// </summary>
    public static void ApplyGuardStone(Container container, bool isPublic)
    {
        if (container == null)
            return;

        container.m_checkGuardStone = !isPublic;
    }

    public static void ApplyGuardStone(Door door, bool isPublic)
    {
        if (door == null)
            return;

        door.m_checkGuardStone = !isPublic;
    }

    public static bool IsEligibleChest(Container? container)
    {
        if (container == null)
            return false;

        if (container.m_privacy == Container.PrivacySetting.Private)
            return false;

        var piece = container.GetComponentInParent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer())
            return false;

        return container.GetComponentInParent<ZNetView>() != null;
    }

    public static bool IsEligibleDoor(Door? door)
    {
        if (door == null)
            return false;

        var piece = door.GetComponentInParent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer())
            return false;

        return GetNetView(door) != null;
    }

    public static ZNetView? GetNetView(Container container) =>
        container.GetComponentInParent<ZNetView>();

    public static ZNetView? GetNetView(Door door)
    {
        if (door == null)
            return null;

        // Do not touch Door.m_nview — it is private at runtime (publicized only at compile).
        var own = door.GetComponent<ZNetView>();
        if (own != null && own.IsValid())
            return own;

        return door.GetComponentInParent<ZNetView>();
    }

    public static Vector3 GetPosition(Container container) =>
        container.transform.position;

    public static Vector3 GetPosition(Door door) =>
        door.transform.position;
}

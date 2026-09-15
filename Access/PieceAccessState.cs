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

    public static bool IsPublic(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        return zdo != null && zdo.GetInt(PublicFlagHash, 0) == 1;
    }

    public static void SetPublic(ZNetView nview, bool isPublic)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

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

using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Phase 3: team mode overlay on private-family chests.
/// Guest ids live in <see cref="PieceGuestAccess"/>; mode in ZDO <c>locksmith_group_mode</c>.
/// </summary>
public static class GroupAccessState
{
    private static readonly int TeamModeHash = GameHookTargets.ZdoGroupMode.GetStableHashCode();

    public static bool IsPrivateFamilyChest(Container? container)
    {
        if (container == null)
            return false;

        if (container.m_privacy != Container.PrivacySetting.Private
            && container.m_privacy != Container.PrivacySetting.Group)
            return false;

        var piece = container.GetComponentInParent<Piece>();
        if (piece == null || !piece.IsPlacedByPlayer())
            return false;

        return GetNetView(container) != null;
    }

    public static ZNetView? GetNetView(Container container)
    {
        if (container == null)
            return null;

        var own = container.GetComponent<ZNetView>();
        if (own != null && own.IsValid())
            return own;

        return container.GetComponentInParent<ZNetView>();
    }

    public static Vector3 GetPosition(Container container) =>
        container.transform.position;

    public static long GetCreatorId(Container container)
    {
        var piece = container.GetComponentInParent<Piece>();
        return piece != null ? piece.GetCreator() : 0L;
    }

    public static bool IsCreator(Container container, long playerId)
    {
        var creator = GetCreatorId(container);
        return creator != 0L && creator == playerId;
    }

    public static bool IsTeamMode(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        return zdo != null && zdo.GetInt(TeamModeHash, 0) == 1;
    }

    public static void SetTeamMode(ZNetView nview, bool team)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        zdo.Set(TeamModeHash, team ? 1 : 0, false);
        if (!team)
        {
            PieceGuestAccess.ClearGuests(nview);
            PieceGuestAccess.SetOptInReady(nview, false);
        }

        ApplyPrivacyToInstance(nview, team);
    }

    public static bool HasTeamAccess(Container container, long playerId)
    {
        if (container == null || playerId == 0L)
            return false;

        if (IsCreator(container, playerId))
            return true;

        var nview = GetNetView(container);
        return IsTeamMode(nview) && PieceGuestAccess.IsGuest(nview, playerId);
    }

    public static void SyncPrivacyFromZdo(Container container)
    {
        if (container == null || !IsPrivateFamilyChest(container))
            return;

        var nview = GetNetView(container);
        if (nview == null || !nview.IsValid())
            return;

        ApplyPrivacyToInstance(nview, IsTeamMode(nview));
    }

    private static void ApplyPrivacyToInstance(ZNetView nview, bool team)
    {
        foreach (var container in nview.GetComponentsInChildren<Container>(true))
        {
            if (container == null)
                continue;

            if (container.m_privacy != Container.PrivacySetting.Private
                && container.m_privacy != Container.PrivacySetting.Group)
                continue;

            container.m_privacy = team
                ? Container.PrivacySetting.Group
                : Container.PrivacySetting.Private;
        }
    }
}

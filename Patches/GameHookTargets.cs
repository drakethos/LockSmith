namespace LockSmith;

/// <summary>
/// Named game API surfaces this mod patches. When Valheim updates break hooks, start here.
/// </summary>
public static class GameHookTargets
{
    public const string ContainerType = "Container";
    public const string DoorType = "Door";

    public const string ContainerAwake = "Awake";
    public const string ContainerInteract = "Interact";
    public const string ContainerGetHoverText = "GetHoverText";
    public const string ContainerTakeAll = "TakeAll";
    public const string ContainerUseItem = "UseItem";
    public const string ContainerCheckAccess = "CheckAccess";

    public const string DoorAwake = "Awake";
    public const string DoorInteract = "Interact";
    public const string DoorGetHoverText = "GetHoverText";

    public const string HumanoidRightItemField = "m_rightItem";
    public const string HumanoidLeftItemField = "m_leftItem";

    /// <summary>ZNetView RPC registered on each Container. Must stay unique.</summary>
    public const string RpcSetChestPublic = "RPC_LockSmithSetChestPublic";

    /// <summary>ZNetView RPC registered on each Door. Must stay unique.</summary>
    public const string RpcSetDoorPublic = "RPC_LockSmithSetDoorPublic";

    /// <summary>ZNetView RPC: set Personal(0) / Team(1) on a private-family chest.</summary>
    public const string RpcSetGroupMode = "RPC_LockSmithSetGroupMode";

    /// <summary>ZNetView RPC: creator/ward toggles opt-in ready (0/1).</summary>
    public const string RpcSetOptInReady = "RPC_LockSmithSetOptInReady";

    /// <summary>ZNetView RPC: player opts in with id + display name.</summary>
    public const string RpcOptInSelf = "RPC_LockSmithOptInSelf";

    /// <summary>ZNetView RPC: player removes themselves from the guest list.</summary>
    public const string RpcOptOutSelf = "RPC_LockSmithOptOutSelf";

    /// <summary>ZNetView RPC: strip all LockSmith state from a piece.</summary>
    public const string RpcClearLockSmith = "RPC_LockSmithClear";

    /// <summary>ZDO int: 0 private (default), 1 public (ward chests/doors).</summary>
    public const string ZdoPublicFlag = "locksmith_public";

    /// <summary>
    /// ZDO int: 0 = normal piece, 1 = LockSmith-designated (key was used once).
    /// Designated pieces allow permitted players to Alt+E public/private without holding the key.
    /// </summary>
    public const string ZdoManagedFlag = "locksmith_managed";

    /// <summary>ZDO int: 0 personal (default), 1 team mode (private-family chests).</summary>
    public const string ZdoGroupMode = "locksmith_group_mode";

    /// <summary>ZDO string: comma-separated player ids (team guests / piece guests).</summary>
    public const string ZdoGroupMembers = "locksmith_group_members";

    /// <summary>ZDO int: 0 closed, 1 opt-in ready (ward-style join).</summary>
    public const string ZdoOptInReady = "locksmith_optin";
}

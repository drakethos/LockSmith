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

    public const string DoorAwake = "Awake";
    public const string DoorInteract = "Interact";
    public const string DoorGetHoverText = "GetHoverText";

    public const string HumanoidRightItemField = "m_rightItem";
    public const string HumanoidLeftItemField = "m_leftItem";

    /// <summary>ZNetView RPC registered on each Container. Must stay unique.</summary>
    public const string RpcSetChestPublic = "RPC_LockSmithSetChestPublic";

    /// <summary>ZNetView RPC registered on each Door. Must stay unique.</summary>
    public const string RpcSetDoorPublic = "RPC_LockSmithSetDoorPublic";

    /// <summary>ZDO int: 0 private (default), 1 public.</summary>
    public const string ZdoPublicFlag = "locksmith_public";
}

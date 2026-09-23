namespace LockSmith.Compat;

/// <summary>
/// Result of asking a ward-compat module about a world position.
/// </summary>
public enum WardCoverageKind
{
    /// <summary>This module has no ward (or relevant rule) covering the point.</summary>
    Unrelated = 0,

    /// <summary>Covered / rules apply and the player is allowed.</summary>
    Allowed = 1,

    /// <summary>Covered / rules apply and the player is denied.</summary>
    Denied = 2,
}

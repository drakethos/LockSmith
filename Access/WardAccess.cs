using LockSmith.Compat;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>
/// Ward helpers for LockSmith gameplay. Delegates optional stacks to
/// <see cref="CompatibilityManager"/> — do not branch on foreign mods here.
/// </summary>
public static class WardAccess
{
    public static bool HasLocalWardAccess(Vector3 position) =>
        CompatibilityManager.HasLocalWardAccess(position, flash: false);

    /// <summary>
    /// True when <paramref name="position"/> is inside at least one enabled ward
    /// (vanilla and/or registered compat modules such as WardIsLove).
    /// </summary>
    public static bool IsInsideEnabledWard(Vector3 position) =>
        CompatibilityManager.IsInsideEnabledWard(position);

    /// <summary>
    /// Whether LockSmith may manage/toggle this piece under <see cref="LockSmithConfig.RequireActiveWard"/>.
    /// Private-family chests are always allowed; doors pass <paramref name="isPrivateFamilyChest"/> as false.
    /// </summary>
    public static bool AllowsToolOnPiece(Vector3 position, bool isPrivateFamilyChest)
    {
        if (!LockSmithConfig.RequireActiveWard)
            return true;

        if (isPrivateFamilyChest)
            return true;

        return IsInsideEnabledWard(position);
    }

    public static bool HasWardAccessForPlayer(Vector3 position, long playerId) =>
        CompatibilityManager.HasWardAccessForPlayer(position, playerId, flash: false);
}

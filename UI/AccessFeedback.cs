using System;
using DrakeModsLibs.UI;
using UnityEngine;

namespace LockSmith.UI;

/// <summary>
/// LockSmith's action feedback. Every message goes to the shared card (<see cref="DrakeMessage"/>) instead of Valheim's yellow
/// centre text, so the look is set once in DrakeModsLibs.
/// </summary>
public static class AccessFeedback
{
    public static void Show(Humanoid? user, string localizationToken)
    {
        if (user == null || string.IsNullOrEmpty(localizationToken))
            return;

        ShowRaw(user, LockSmithLocalization.T(localizationToken));
    }

    public static void ShowRaw(Humanoid? user, string message)
    {
        if (user == null || string.IsNullOrEmpty(message))
            return;

        try
        {
            DrakeMessage.Show(message);
        }
        catch (Exception ex)
        {
            // Never let feedback tear down Interact prefixes.
            LockSmith.Log?.LogDebug($"AccessFeedback failed: {ex.Message}");
        }
    }
}

namespace LockSmith.UI;

/// <summary>Light Valheim-native feedback (center message). No custom panels.</summary>
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

        user.Message(MessageHud.MessageType.Center, message);
    }
}

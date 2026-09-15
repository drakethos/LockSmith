namespace LockSmith.UI;

/// <summary>Light Valheim-native feedback (center message). No custom panels.</summary>
public static class AccessFeedback
{
    public static void Show(Humanoid? user, string localizationToken)
    {
        if (user == null || string.IsNullOrEmpty(localizationToken))
            return;

        var text = LockSmithLocalization.T(localizationToken);
        user.Message(MessageHud.MessageType.Center, text);
    }
}

using System;
using System.Reflection;
using HarmonyLib;

namespace LockSmith.UI;

/// <summary>Light Valheim-native feedback (center message). No custom panels.</summary>
public static class AccessFeedback
{
    static MethodInfo? _showMessage;
    static bool _resolved;

    public static void Show(Humanoid? user, string localizationToken)
    {
        if (user == null || string.IsNullOrEmpty(localizationToken))
            return;

        ShowRaw(user, LockSmithLocalization.T(localizationToken));
    }

    public static void ShowRaw(Humanoid? user, string message)
    {
        if (string.IsNullOrEmpty(message))
            return;

        try
        {
            EnsureShowMessage();
            if (_showMessage != null && MessageHud.instance != null)
            {
                // Valheim 1.0+: ShowMessage(type, msg, amount, icon, log, …)
                var p = _showMessage.GetParameters();
                var args = new object?[p.Length];
                args[0] = MessageHud.MessageType.Center;
                args[1] = message;
                for (var i = 2; i < p.Length; i++)
                {
                    if (p[i].ParameterType == typeof(int))
                        args[i] = 0;
                    else if (p[i].ParameterType == typeof(bool))
                        args[i] = false;
                    else
                        args[i] = null;
                }

                _showMessage.Invoke(MessageHud.instance, args);
                return;
            }
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"AccessFeedback ShowMessage failed: {ex.Message}");
        }

        if (user != null)
        {
            try
            {
                // Fallback — may missing-method on some game builds.
                user.Message(MessageHud.MessageType.Center, message);
            }
            catch (Exception)
            {
                /* ignore */
            }
        }
    }

    static void EnsureShowMessage()
    {
        if (_resolved)
            return;
        _resolved = true;
        _showMessage = AccessTools.Method(typeof(MessageHud), nameof(MessageHud.ShowMessage));
    }
}

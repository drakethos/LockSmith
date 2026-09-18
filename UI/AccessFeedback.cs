using System;
using System.Reflection;
using HarmonyLib;
using UnityEngine;

namespace LockSmith.UI;

/// <summary>
/// Light Valheim-native feedback (center message). No custom panels.
/// Resolves <see cref="Character.Message"/> / <see cref="MessageHud.ShowMessage"/> from the
/// <b>runtime</b> game assembly — never from publicized compile refs (those still expose the
/// pre-1.0 4-arg <c>Message</c> and Invoke throws <see cref="MissingMethodException"/>).
/// </summary>
public static class AccessFeedback
{
    static MethodInfo? _showMessage;
    static MethodInfo? _characterMessage;
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
            EnsureResolved();

            if (_showMessage != null && MessageHud.instance != null)
            {
                if (TryInvokeHud(_showMessage, MessageHud.instance, message))
                    return;
            }

            if (user != null && _characterMessage != null)
                TryInvokeCharacter(_characterMessage, user, message);
        }
        catch (Exception ex)
        {
            // Never let feedback tear down Interact prefixes.
            LockSmith.Log?.LogDebug($"AccessFeedback failed: {Unwrap(ex).Message}");
        }
    }

    static bool TryInvokeHud(MethodInfo method, object hud, string message)
    {
        try
        {
            var p = method.GetParameters();
            var args = new object?[p.Length];
            args[0] = MessageHud.MessageType.Center;
            args[1] = message;
            for (var i = 2; i < p.Length; i++)
            {
                var t = p[i].ParameterType;
                if (t == typeof(int))
                    args[i] = 0;
                else if (t == typeof(bool))
                    args[i] = false;
                else
                    args[i] = null;
            }

            method.Invoke(hud, args);
            return true;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"AccessFeedback MessageHud invoke failed: {Unwrap(ex).Message}");
            return false;
        }
    }

    static void TryInvokeCharacter(MethodInfo method, Humanoid user, string message)
    {
        try
        {
            var p = method.GetParameters();
            // Valheim 1.0+: (type, msg, amount, icon, log). Older: (type, msg, amount, icon).
            if (p.Length >= 5)
            {
                method.Invoke(
                    user,
                    new object?[] { MessageHud.MessageType.Center, message, 0, null, false });
                return;
            }

            if (p.Length >= 4)
            {
                method.Invoke(
                    user,
                    new object?[] { MessageHud.MessageType.Center, message, 0, null });
            }
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"AccessFeedback Character.Message invoke failed: {Unwrap(ex).Message}");
        }
    }

    static void EnsureResolved()
    {
        if (_resolved)
            return;

        _resolved = true;

        var messageHudType = RuntimeType("MessageHud") ?? typeof(MessageHud);
        var characterType = RuntimeType("Character") ?? typeof(Character);
        var messageType = RuntimeType("MessageHud+MessageType")
                          ?? RuntimeType("MessageHud.MessageType")
                          ?? typeof(MessageHud.MessageType);
        var spriteType = RuntimeType("UnityEngine.Sprite") ?? typeof(Sprite);

        // Prefer HUD — avoids Character.Message signature churn.
        _showMessage = AccessTools.Method(messageHudType, "ShowMessage")
                       ?? FindMethodByName(messageHudType, "ShowMessage");

        // Prefer 5-arg Valheim 1.0+ only when resolved from the runtime assembly.
        _characterMessage = AccessTools.Method(
                                characterType,
                                "Message",
                                new[] { messageType, typeof(string), typeof(int), spriteType, typeof(bool) })
                            ?? AccessTools.Method(
                                characterType,
                                "Message",
                                new[] { messageType, typeof(string), typeof(int), spriteType })
                            ?? FindMethodByName(characterType, "Message");

        // Refuse publicized-only 4-arg ghosts: if Parameter count is 4 but runtime Character
        // does not declare that method, Invoke would MissingMethod — drop it.
        if (_characterMessage != null && !IsCallableOnRuntimeCharacter(_characterMessage))
        {
            LockSmith.Log?.LogDebug(
                "AccessFeedback: dropping Character.Message MethodInfo that is not callable at runtime.");
            _characterMessage = null;
        }

        if (_showMessage == null && _characterMessage == null)
            LockSmith.Log?.LogWarning("AccessFeedback: no MessageHud/Character.Message overload resolved.");
    }

    /// <summary>Type from loaded <c>assembly_valheim</c>, not the publicized compile reference.</summary>
    static Type? RuntimeType(string fullName)
    {
        foreach (var asm in AppDomain.CurrentDomain.GetAssemblies())
        {
            try
            {
                var name = asm.GetName().Name;
                if (name != "assembly_valheim" && name != "UnityEngine.CoreModule")
                    continue;
                var t = asm.GetType(fullName, throwOnError: false);
                if (t != null)
                    return t;
            }
            catch
            {
                /* skip dynamic/unloadable */
            }
        }

        return AccessTools.TypeByName(fullName);
    }

    static MethodInfo? FindMethodByName(Type type, string name)
    {
        MethodInfo? best = null;
        foreach (var m in type.GetMethods(BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic))
        {
            if (m == null || m.Name != name)
                continue;
            var n = m.GetParameters().Length;
            if (best == null || n > best.GetParameters().Length)
                best = m;
        }

        return best;
    }

    static bool IsCallableOnRuntimeCharacter(MethodInfo method)
    {
        try
        {
            var runtime = RuntimeType("Character");
            if (runtime == null)
                return true;

            // MethodInfo declaring type must be the runtime Character (or its base).
            var decl = method.DeclaringType;
            if (decl == null)
                return false;
            if (decl == runtime || runtime.IsSubclassOf(decl) || decl.IsAssignableFrom(runtime))
            {
                // Confirm an identical signature exists on the runtime type.
                var ps = method.GetParameters();
                var types = new Type[ps.Length];
                for (var i = 0; i < ps.Length; i++)
                    types[i] = ps[i].ParameterType;
                return runtime.GetMethod(method.Name, types) != null
                       || AccessTools.Method(runtime, method.Name, types) != null;
            }

            return false;
        }
        catch
        {
            return false;
        }
    }

    static Exception Unwrap(Exception ex)
    {
        while (ex is TargetInvocationException { InnerException: { } inner })
            ex = inner;
        return ex;
    }
}

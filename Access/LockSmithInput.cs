using UnityEngine;

namespace LockSmith.Access;

/// <summary>Local modifier chords for LockSmith key actions.</summary>
public static class LockSmithInput
{
    public static bool IsModifierHeld(LockSmithModifier modifier)
    {
        switch (modifier)
        {
            case LockSmithModifier.Alt:
                return Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            case LockSmithModifier.Shift:
                return Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
            case LockSmithModifier.Control:
                return Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
            default:
                return false;
        }
    }

    public static bool IsClearModifierHeld() =>
        IsModifierHeld(LockSmithConfig.ClearModifier);

    public static bool IsSetupModifierHeld() =>
        IsModifierHeld(LockSmithConfig.SetupModifier);

    public static string ModifierLabel(LockSmithModifier modifier)
    {
        switch (modifier)
        {
            case LockSmithModifier.Alt:
                return "Alt";
            case LockSmithModifier.Shift:
                return "Shift";
            case LockSmithModifier.Control:
                return "Ctrl";
            default:
                return modifier.ToString();
        }
    }

    public static string FormatModifierUse(LockSmithModifier modifier)
    {
        var mod = ModifierLabel(modifier);
        return "[<color=yellow><b>" + mod + "</b></color>+<color=yellow><b>$KEY_Use</b></color>]";
    }
}

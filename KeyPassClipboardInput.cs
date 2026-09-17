using LockSmith.Access;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith;

/// <summary>
/// World Ctrl+C / Ctrl+V while holding the Locksmith key.
/// Copy = piece guests → key; Paste = key guests → piece (merge).
/// </summary>
public static class KeyPassClipboardInput
{
    public static void Tick(Player player)
    {
        if (!LockSmithConfig.EnableKeyPasses || !LockSmithConfig.EnableKeyMode)
            return;
        if (!player || player != Player.m_localPlayer)
            return;
        if (KeyPassMenu.IsOpen)
            return;
        if (IsTextUiBlocking())
            return;

        var key = ChestAccessService.GetEquippedLocksmithKey(player);
        if (key == null)
            return;

        var ctrl = Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl);
        if (!ctrl)
            return;

        try
        {
            if (Input.GetKeyDown(KeyCode.C))
            {
                KeyPassService.TryPullFromLookTarget(key);
                return;
            }

            if (Input.GetKeyDown(KeyCode.V))
                KeyPassService.TryPasteOntoLookTarget(key);
        }
        catch (System.Exception ex)
        {
            LockSmith.Log?.LogError($"KeyPass clipboard input failed: {ex}");
        }
    }

    static bool IsTextUiBlocking()
    {
        try
        {
            if (Console.instance != null && Console.IsVisible())
                return true;
        }
        catch
        {
            /* older builds */
        }

        try
        {
            if (Chat.instance != null && Chat.instance.HasFocus())
                return true;
        }
        catch
        {
            /* older builds */
        }

        try
        {
            if (TextInput.IsVisible())
                return true;
        }
        catch
        {
            /* optional */
        }

        try
        {
            if (InventoryGui.IsVisible() && (Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            {
                // Allow clipboard while inventory is open only if no input field is focused —
                // Relabel prompt already blocks via KeyPassMenu.IsOpen.
            }
        }
        catch
        {
            /* ignore */
        }

        return false;
    }
}

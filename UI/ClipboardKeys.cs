using DrakeModsLibs.UI.Toolkit;
using UnityEngine;

namespace LockSmith.UI;

/// <summary>
/// Ctrl+C / Ctrl+V while looking at a chest or door you manage: copy the people on it, paste them onto another.
/// Stays out of the way while a window, the wheel, chat or the console is open.
/// </summary>
public static class ClipboardKeys
{
    public static void Tick()
    {
        if (!(Input.GetKey(KeyCode.LeftControl) || Input.GetKey(KeyCode.RightControl)))
            return;

        var copy = Input.GetKeyDown(KeyCode.C);
        var paste = Input.GetKeyDown(KeyCode.V);
        if (!copy && !paste)
            return;

        if (UkWindow.AnyOpen || LockWheel.IsOpen || PieceAccessMenu.IsOpen)
            return;
        if ((Chat.instance != null && Chat.instance.HasFocus()) || Console.IsVisible())
            return;

        var player = Player.m_localPlayer;
        if (player == null)
            return;

        var hover = player.GetHoverObject();
        if (!hover)
            return;

        var chest = hover.GetComponentInParent<Container>();
        var door = chest == null ? hover.GetComponentInParent<Door>() : null;
        if (chest == null && door == null)
            return;

        if (copy)
            PieceAccessMenu.QuickCopy(chest, door, player);
        else
            PieceAccessMenu.QuickPaste(chest, door, player);
    }
}

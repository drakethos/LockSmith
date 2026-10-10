using System;
using System.Collections.Generic;
using System.Linq;
using DrakeModsLibs.UI;
using DrakeModsLibs.UI.Toolkit;
using LockSmith.Access;
using UnityEngine;
using UnityEngine.UIElements;

namespace LockSmith.UI;

/// <summary>
/// Hold the Lock key (AltPlace + E) on a chest or door and a wheel opens around the crosshair: Join, Public, Ward, add the nearest
/// person, and the full menu. Aim the mouse at a slice and let go of E to do it; let go in the middle to cancel. A quick tap still opens
/// the full Lock window, just a moment later than before (on release) so the two can be told apart.
/// </summary>
public static class LockWheel
{
    const float HoldSeconds = 0.28f;
    const float MaxDistance = 6f;
    const float DeadZonePixels = 70f;
    const float RingRadius = 150f;
    const float SliceWidth = 150f;
    const float SliceHeight = 52f;
    const int MaxSlices = 6;

    sealed class Slice
    {
        public string Label = "";
        public bool Enabled = true;
        public Action Run = () => { };
    }

    static UkScreen? _screen;
    static VisualElement? _layer;
    static VisualElement? _wheel;
    static Label? _centerTitle;
    static Label? _centerSub;
    static readonly List<VisualElement> SliceViews = new();
    static List<Slice> _slices = new();

    static Container? _chest;
    static Door? _door;
    static bool _pending;
    static bool _shown;
    static bool _weBlocked;
    static float _pressedAt;
    static int _selected = -1;

    public static bool IsOpen => _shown;

    /// <summary>
    /// AltPlace + E on a piece. With the wheel off this opens the window right away. With it on, the press is held back until we know
    /// whether it is a tap (window) or a hold (wheel). Returns false when there is nothing to do here, so vanilla runs.
    /// </summary>
    public static bool TryBegin(Container container, Humanoid user) => Begin(container, null, user);

    public static bool TryBegin(Door door, Humanoid user) => Begin(null, door, user);

    static bool Begin(Container? container, Door? door, Humanoid user)
    {
        if (!LockSmithConfig.UseWheel)
            return container != null
                ? PieceAccessMenu.TryOpenLockMenu(container, user)
                : PieceAccessMenu.TryOpenLockMenu(door!, user);

        // Already waiting on the same press (Interact can repeat while the key is down).
        if (_pending || _shown)
            return true;

        var hasMenu = container != null ? PieceAccessMenu.HasLockMenu(container) : PieceAccessMenu.HasLockMenu(door!);
        if (!hasMenu)
            return false;

        _chest = container;
        _door = door;
        _pending = true;
        _pressedAt = Time.unscaledTime;
        return true;
    }

    /// <summary>Called every frame from the plugin.</summary>
    /// <summary>Called every frame from the plugin. A fault here clears the wheel so it can never stay stuck.</summary>
    public static void Tick()
    {
        try
        {
            TickInner();
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Lock wheel tick failed: {ex}");
            try { Cancel(); } catch (Exception) { }
        }
    }

    static void TickInner()
    {
        if (!_pending && !_shown)
            return;

        var player = Player.m_localPlayer;
        if (player == null || !TargetValid(player))
        {
            Cancel();
            return;
        }

        var held = UseHeld();
        if (_pending)
        {
            if (!held)
            {
                // A tap: the full window, like before.
                _pending = false;
                OpenWindow(player);
                return;
            }

            if (Time.unscaledTime - _pressedAt >= HoldSeconds)
            {
                _pending = false;
                Show(player);
            }

            return;
        }

        UpdateSelection();
        if (!held)
        {
            var pick = _selected;
            var slices = _slices;
            Hide();
            if (pick >= 0 && pick < slices.Count && slices[pick].Enabled)
                slices[pick].Run();
        }
    }

    static bool UseHeld() => Input.GetKey(KeyCode.E) || ZInput.GetButton("Use");

    static bool TargetValid(Player player)
    {
        Vector3 pos;
        if (_chest != null)
            pos = _chest.transform.position;
        else if (_door != null)
            pos = _door.transform.position;
        else
            return false;
        return Vector3.Distance(player.transform.position, pos) <= MaxDistance;
    }

    static void OpenWindow(Player player)
    {
        if (_chest != null)
            PieceAccessMenu.TryOpenLockMenu(_chest, player);
        else if (_door != null)
            PieceAccessMenu.TryOpenLockMenu(_door, player);
        _chest = null;
        _door = null;
    }

    static void Cancel()
    {
        _pending = false;
        if (_shown)
            Hide();
        _chest = null;
        _door = null;
    }

    // ---- slices -----------------------------------------------------------------------------------

    static List<Slice> BuildSlices(Player player)
    {
        var slices = new List<Slice>();
        var actions = PieceAccessMenu.WheelActions(_chest, _door) ?? new List<DrakeMenuAction>();

        Slice FromAction(DrakeMenuAction action, string label) => new Slice
        {
            Label = label,
            Enabled = action.Enabled,
            Run = () =>
            {
                try
                {
                    action.OnClick();
                }
                catch (Exception ex)
                {
                    LockSmith.Log?.LogError($"Lock wheel action failed: {ex}");
                }
            },
        };

        foreach (var action in actions)
        {
            switch (LockMenuKinds.TagOf(action).Kind)
            {
                case LockMenuKind.Join:
                    slices.Add(FromAction(action, action.Label));
                    break;
                case LockMenuKind.Public:
                    slices.Add(FromAction(action, action.Label));
                    break;
            }
        }

        var ward = WardSlice(player);
        if (ward != null)
            slices.Add(ward);

        // People on the piece: choosing one opens the window on what that person can do.
        var shownPeople = 0;
        foreach (var action in actions)
        {
            var tag = LockMenuKinds.TagOf(action);
            if (tag.Kind != LockMenuKind.Remove || tag.PersonId == 0L || tag.SetFlags == null || shownPeople >= 2)
                continue;

            var id = tag.PersonId;
            var who = action.Label.StartsWith("Remove ", StringComparison.Ordinal) ? action.Label.Substring(7) : action.Label;
            slices.Add(new Slice
            {
                Label = who + "…",
                Run = () =>
                {
                    PieceAccessMenu.FocusPerson(id);
                    OpenWindowLater(player);
                },
            });
            shownPeople++;
        }

        var added = 0;
        foreach (var action in actions)
        {
            if (LockMenuKinds.TagOf(action).Kind == LockMenuKind.Nearby && action.Enabled && added < 2)
            {
                slices.Add(FromAction(action, action.Label));
                added++;
            }
        }

        // Keep room for the Menu slice, which is always there.
        while (slices.Count > MaxSlices - 1)
            slices.RemoveAt(slices.Count - 1);

        slices.Add(new Slice
        {
            Label = "Menu",
            Run = () => OpenWindowLater(player),
        });
        return slices;
    }

    /// <summary>Opens the window next frame, after the wheel has handed input back.</summary>
    static void OpenWindowLater(Player player, LockTab tab = LockTab.Piece)
    {
        var chest = _chest;
        var door = _door;
        _chest = null;
        _door = null;
        if (chest != null)
            PieceAccessMenu.TryOpenLockMenu(chest, player, tab);
        else if (door != null)
            PieceAccessMenu.TryOpenLockMenu(door, player, tab);
    }

    static Slice? WardSlice(Player player)
    {
        try
        {
            var me = player.GetPlayerID();
            var wards = WardService.WardsFor(player.transform.position, me);
            if (wards.Count == 0)
                return null;

            var area = wards[0];
            var nview = area.GetComponent<ZNetView>();
            var level = WardService.LevelOf(area, me);
            var canSwitch = level != WardLevel.None && (int)level >= WardService.SwitchMinimum(nview);
            var on = PrivateAreaAccess.IsEnabled(area);
            return new Slice
            {
                Label = "Ward…",
                Enabled = true,
                Run = () => OpenWindowLater(player, LockTab.Ward),
            };
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Lock wheel ward slice failed: {ex}");
            return null;
        }
    }

    // ---- drawing ----------------------------------------------------------------------------------

    static void EnsureBuilt()
    {
        if (_screen != null)
            return;

        _screen = UkScreen.Create("locksmith_wheel");
        _layer = new VisualElement();
        _layer.style.position = Position.Absolute;
        _layer.style.left = _layer.style.right = _layer.style.top = _layer.style.bottom = 0;
        _layer.style.alignItems = Align.Center;
        _layer.style.justifyContent = Justify.Center;
        _layer.pickingMode = PickingMode.Ignore;
        _layer.Show(false);
        _screen.Root.Add(_layer);

        var size = (RingRadius + SliceWidth / 2f + 20f) * 2f;
        _wheel = new VisualElement();
        _wheel.style.width = size;
        _wheel.style.height = size;
        _wheel.pickingMode = PickingMode.Ignore;
        _layer.Add(_wheel);
    }

    static void Show(Player player)
    {
        try
        {
            EnsureBuilt();
            _slices = BuildSlices(player);
            Rebuild();
            _screen!.RaiseAboveCanvases(20);
            _layer!.Show(true);
            // Frees the cursor and holds the camera while the wheel is up.
            if (!DrakeGuiInput.IsBlocked)
            {
                DrakeGuiInput.EnsureBlocked();
                _weBlocked = true;
            }

            _shown = true;
            _selected = -1;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Lock wheel failed to open: {ex}");
            Cancel();
            OpenWindowFallback(player);
        }
    }

    static void OpenWindowFallback(Player player)
    {
        // If the wheel can't draw, never leave the player with nothing: open the window.
        if (_chest != null)
            PieceAccessMenu.TryOpenLockMenu(_chest, player);
        else if (_door != null)
            PieceAccessMenu.TryOpenLockMenu(_door, player);
    }

    static void Hide()
    {
        _layer?.Show(false);
        if (_weBlocked)
        {
            DrakeGuiInput.EnsureUnblocked();
            _weBlocked = false;
        }

        _shown = false;
        _selected = -1;
    }

    static void Rebuild()
    {
        _wheel!.Clear();
        SliceViews.Clear();
        var size = (RingRadius + SliceWidth / 2f + 20f) * 2f;
        var cx = size / 2f;
        var cy = size / 2f;

        // Centre: who is on it.
        var hub = new VisualElement().Column().Fill(UkTheme.PanelDeep).Round(60).Border(UkTheme.Accent, 2f);
        hub.style.position = Position.Absolute;
        hub.style.width = 120;
        hub.style.height = 120;
        hub.style.left = cx - 60;
        hub.style.top = cy - 60;
        hub.style.alignItems = Align.Center;
        hub.style.justifyContent = Justify.Center;
        hub.pickingMode = PickingMode.Ignore;
        _centerTitle = UkControls.Text("", 15, UkTheme.Gold, heading: true, wrap: false);
        _centerSub = UkControls.Text("", 12, UkTheme.TextMuted, wrap: false);
        var summary = PeopleSummary();
        _centerTitle.text = summary.Item1;
        _centerSub.text = summary.Item2;
        hub.Add(_centerTitle);
        hub.Add(_centerSub);
        _wheel.Add(hub);

        var count = _slices.Count;
        for (var i = 0; i < count; i++)
        {
            var angle = i * (360f / count) * Mathf.Deg2Rad;
            var x = cx + RingRadius * Mathf.Sin(angle) - SliceWidth / 2f;
            var y = cy - RingRadius * Mathf.Cos(angle) - SliceHeight / 2f;

            var slice = new VisualElement().Fill(UkTheme.Raised).Round(SliceHeight / 2f).Border(UkTheme.Line);
            slice.style.position = Position.Absolute;
            slice.style.width = SliceWidth;
            slice.style.height = SliceHeight;
            slice.style.left = x;
            slice.style.top = y;
            slice.style.alignItems = Align.Center;
            slice.style.justifyContent = Justify.Center;
            slice.pickingMode = PickingMode.Ignore;
            var label = UkControls.Text(_slices[i].Label, 15, _slices[i].Enabled ? UkTheme.Text : UkTheme.TextMuted, wrap: false);
            slice.Add(label);
            _wheel.Add(slice);
            SliceViews.Add(slice);
        }
    }

    /// <summary>Hub text: "3 on it" and the first names.</summary>
    static (string, string) PeopleSummary()
    {
        try
        {
            var nview = _chest != null
                ? (GroupAccessState.IsPrivateFamilyChest(_chest) ? GroupAccessState.GetNetView(_chest) : PieceAccessState.GetNetView(_chest))
                : _door != null ? PieceAccessState.GetNetView(_door) : null;
            var guests = PieceGuestAccess.GetGuests(nview);
            if (guests.Count == 0)
                return ("Just you", "");
            var names = guests.Select(g => string.IsNullOrEmpty(g.DisplayName) ? "?" : g.DisplayName).ToList();
            var shown = string.Join(", ", names.Take(2));
            if (names.Count > 2)
                shown += " +" + (names.Count - 2);
            return (guests.Count + (guests.Count == 1 ? " person" : " people"), shown);
        }
        catch (Exception)
        {
            return ("", "");
        }
    }

    static void UpdateSelection()
    {
        if (_slices.Count == 0)
            return;

        var dx = Input.mousePosition.x - Screen.width / 2f;
        var dy = Input.mousePosition.y - Screen.height / 2f;
        var next = -1;
        if (dx * dx + dy * dy >= DeadZonePixels * DeadZonePixels)
        {
            var degrees = Mathf.Atan2(dx, dy) * Mathf.Rad2Deg;
            if (degrees < 0f)
                degrees += 360f;
            var step = 360f / _slices.Count;
            next = Mathf.RoundToInt(degrees / step) % _slices.Count;
        }

        if (next == _selected)
            return;
        _selected = next;
        for (var i = 0; i < SliceViews.Count; i++)
        {
            var view = SliceViews[i];
            var on = i == _selected && _slices[i].Enabled;
            view.style.backgroundColor = on ? UkTheme.Accent : UkTheme.Raised;
            view.style.borderTopColor = view.style.borderBottomColor = view.style.borderLeftColor = view.style.borderRightColor =
                on ? UkTheme.AccentHover : UkTheme.Line;
            if (view.childCount > 0 && view[0] is Label text)
                text.style.color = on ? UkTheme.AccentText : _slices[i].Enabled ? UkTheme.Text : UkTheme.TextMuted;
        }
    }
}

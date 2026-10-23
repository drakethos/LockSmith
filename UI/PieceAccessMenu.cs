using System;
using System.Collections.Generic;
using System.Text;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI;
using LockSmith.Access;
using UnityEngine;

namespace LockSmith.UI;

/// <summary>
/// The one Lock menu for a chest or door, opened with AltPlace + E.
/// Buttons are context-sensitive and only shown to players with access; strangers get no menu.
/// Every action calls the existing access services.
/// Re-renders while open so RPC confirms (or failures) show up without reopening.
/// </summary>
public static class PieceAccessMenu
{
    const float MaxDistance = 6f;
    const float RefreshSeconds = 0.25f;

    static readonly LockMenuView Menu = new LockMenuView();
    static readonly DrakeConfirmPanel Confirm = new DrakeConfirmPanel("locksmith_piece_confirm");

    static Container? _chest;
    static Door? _door;
    static string _lastSignature = string.Empty;
    static float _nextRefresh;

    // One-player-at-a-time lease on the open piece (see PieceMenuLock).
    static ZNetView? _leaseView;
    static long _leasePlayer;
    static float _nextRenew;
    static LockTab _tab = LockTab.Piece;
    static string? _extraId;
    static PrivateArea? _wardArea;

    public static bool IsOpen => Menu.IsOpen || Confirm.IsOpen;

    /// <summary>AltPlace + E. Returns false (vanilla runs) when this player has nothing to do here.</summary>
    public static bool TryOpenLockMenu(Container container, Humanoid user, LockTab startTab = LockTab.Piece) =>
        IsLockMenuTarget(container) && TryOpen(container, null, user, reportEmpty: false, startTab);

    public static bool TryOpenLockMenu(Door door, Humanoid user, LockTab startTab = LockTab.Piece) =>
        IsLockMenuTarget(door) && TryOpen(null, door, user, reportEmpty: false, startTab);

    /// <summary>
    /// Hover check: would AltPlace+E open a menu with buttons for the local player?
    /// Strangers get false, so they never see the hint. Inside a ward that is switched off the menu still opens (on the Ward tab).
    /// </summary>
    public static bool HasLockMenu(Container container) => LockMenuActionCount(container, null) > 0 || WardOffOpens(container, null);

    public static bool HasLockMenu(Door door) => LockMenuActionCount(null, door) > 0 || WardOffOpens(null, door);
    /// <summary>The piece sits in a ward that is off (so LockSmith won't manage it) and the player has a role in that ward.</summary>
    static bool WardIsOffHere(Target t, Player player) =>
        !t.PrivateFamily
        && !WardAccess.AllowsToolOnPiece(t.Pos, isPrivateFamilyChest: false)
        && WardService.WardsFor(player.transform.position, player.GetPlayerID()).Count > 0;


    /// <summary>
    /// The ward around a piece is off, so LockSmith won't manage the piece, but the player has a role in that ward. The menu still opens
    /// on the Ward tab so they can switch it back on. Without this, turning the ward off would hide the very menu that turns it on.
    /// </summary>
    static bool WardOffOpens(Container? chest, Door? door)
    {
        var local = Player.m_localPlayer;
        if (local == null)
            return false;
        if (!(chest != null ? IsLockMenuTarget(chest) : door != null && IsLockMenuTarget(door)))
            return false;
        if (!TryResolve(chest, door, out var t) || t.PrivateFamily)
            return false;
        if (WardAccess.AllowsToolOnPiece(t.Pos, isPrivateFamilyChest: false))
            return false;
        return WardService.WardsFor(local.transform.position, local.GetPlayerID()).Count > 0;
    }

    /// <summary>
    /// Hover hint for the local player: "[AltPlace+E] Join access" when joining is all they can do,
    /// "[AltPlace+E] Lock menu" when they have more, empty for strangers with nothing to do.
    /// </summary>
    public static string LockMenuHint(Container container) => Hint(LockMenuActions(container, null));

    public static string LockMenuHint(Door door) => Hint(LockMenuActions(null, door));

    static string Hint(List<DrakeMenuAction>? actions)
    {
        if (actions == null || actions.Count == 0)
            return string.Empty;
        if (actions.Count == 1 && actions[0].Label == L(LockSmithLocalization.HoverJoinAccessToken))
            return AccessHoverDisplay.JoinHint();
        return AccessHoverDisplay.MenuHint();
    }

    static int LockMenuActionCount(Container? chest, Door? door) => LockMenuActions(chest, door)?.Count ?? 0;

    /// <summary>The Piece-tab actions for the wheel (same tags as the window).</summary>
    internal static List<DrakeMenuAction>? WheelActions(Container? chest, Door? door) => LockMenuActions(chest, door);

    /// <summary>Open this person's permissions when the window next draws (the wheel sends you here).</summary>
    internal static void FocusPerson(long personId) => Menu.FocusPerson(personId);

    /// <summary>Ctrl+C on a piece you manage: copy the people on it. Returns false when LockSmith has nothing to do here.</summary>
    internal static bool QuickCopy(Container? chest, Door? door, Player player)
    {
        if (!CanQuick(chest, door, player, out var t))
            return false;

        var guests = PieceGuestAccess.GetGuests(t.Nview);
        if (guests.Count == 0)
        {
            AccessFeedback.ShowRaw(player, "Nobody on this " + t.Noun + " to copy.");
            return true;
        }

        GuestClipboard.Copy(guests);
        AccessFeedback.ShowRaw(player, "Copied " + GuestClipboard.Count + " people.");
        return true;
    }

    /// <summary>Ctrl+V on a piece you manage: add the copied people to it.</summary>
    internal static bool QuickPaste(Container? chest, Door? door, Player player)
    {
        if (!CanQuick(chest, door, player, out var t))
            return false;

        if (GuestClipboard.Count == 0)
        {
            AccessFeedback.ShowRaw(player, "Nothing copied yet. Press Ctrl+C on another " + t.Noun + " first.");
            return true;
        }

        if (PieceMenuLock.BlockIfHeldByOther(t.Nview, player))
            return true;

        var count = GuestClipboard.Count;
        PieceGuestAccess.RequestMergeGuests(t.Nview, GuestClipboard.People, player.GetPlayerID(),
            () => AccessFeedback.ShowRaw(player, "Pasted " + count + " people onto this " + t.Noun + "."));
        return true;
    }

    static bool CanQuick(Container? chest, Door? door, Player player, out Target t)
    {
        t = null!;
        var ok = chest != null ? IsLockMenuTarget(chest) : door != null && IsLockMenuTarget(door);
        if (!ok || !TryResolve(chest, door, out t))
            return false;
        return PieceGuestAccess.CanManageGuests(t.Nview, player.GetPlayerID());
    }

    static List<DrakeMenuAction>? LockMenuActions(Container? chest, Door? door)
    {
        if (chest != null ? !IsLockMenuTarget(chest) : door == null || !IsLockMenuTarget(door))
            return null;

        var local = Player.m_localPlayer;
        if (local == null || !TryResolve(chest, door, out var target))
            return null;
        if (!target.PrivateFamily && !WardAccess.AllowsToolOnPiece(target.Pos, isPrivateFamilyChest: false))
            return null;
        return BuildPieceActions(target, local);
    }

    static bool IsLockMenuTarget(Container container)
    {
        if (container == null || PublicPieceRegistration.IsPublicPiece(container))
            return false;
        if (GroupAccessState.IsPrivateFamilyChest(container))
            return LockSmithConfig.EnableGroupChests;
        return LockSmithConfig.EnableChests && PieceAccessState.IsEligibleChest(container);
    }

    static bool IsLockMenuTarget(Door door) =>
        door != null
        && LockSmithConfig.EnableDoors
        && !PublicPieceRegistration.IsPublicPiece(door)
        && PieceAccessState.IsEligibleDoor(door);

    /// <summary>Called every frame from the plugin: refresh state, close when the piece is gone or far away.</summary>
    public static void Tick()
    {
        if (_leaseView == null)
            return;

        // Closed some other way (e.g. Escape on the Yes/No): give the piece back.
        if (!Menu.IsOpen && !Confirm.IsOpen)
        {
            Close();
            return;
        }

        var holder = Player.m_localPlayer;
        if (holder != null && Time.time >= _nextRenew)
        {
            _nextRenew = Time.time + PieceMenuLock.RenewSeconds;
            PieceMenuLock.Renew(_leaseView, holder);
        }

        if (!Menu.IsOpen || Time.time < _nextRefresh)
            return;
        _nextRefresh = Time.time + RefreshSeconds;

        var local = Player.m_localPlayer;
        if (!TryResolve(out var target) || local == null
            || Vector3.Distance(local.transform.position, target.Pos) > MaxDistance)
        {
            Close();
            return;
        }

        Render(target, local, force: false);
    }

    public static void Close()
    {
        Confirm.Close();
        _tab = LockTab.Piece;
        _wardArea = null;
        Menu.CloseSilent();
        ReleaseLease();
        _chest = null;
        _door = null;
        _lastSignature = string.Empty;
    }

    static void ReleaseLease()
    {
        if (_leaseView == null)
            return;

        PieceMenuLock.Release(_leaseView, _leasePlayer);
        _leaseView = null;
        _leasePlayer = 0L;
    }

    static bool TryOpen(Container? container, Door? door, Humanoid user, bool reportEmpty, LockTab startTab = LockTab.Piece)
    {
        var player = user as Player;
        if (player == null)
            return false;

        if (_leaseView != null)
            Close();

        _chest = container;
        _door = door;
        if (!TryResolve(out var target))
            return false;

        // The ward around the piece is off. If the player has a role in it, still open, showing only the switch to turn it back on.
        var wardOff = WardIsOffHere(target, player);
        if (!target.PrivateFamily && !WardAccess.AllowsToolOnPiece(target.Pos, isPrivateFamilyChest: false) && !wardOff)
        {
            _chest = null;
            _door = null;
            return reportEmpty && Deny(user, LockSmithLocalization.MsgNeedActiveWardToken);
        }

        if (wardOff)
            startTab = LockTab.Ward;

        var actions = wardOff ? new List<DrakeMenuAction>() : BuildPieceActions(target, player);
        if (actions.Count == 0 && !wardOff)
        {
            _chest = null;
            _door = null;
            if (!reportEmpty)
                return false;

            return Deny(
                user,
                target.PrivateFamily
                    ? LockSmithLocalization.MsgGroupOwnerOnlyToken
                    : LockSmithLocalization.MsgDeniedToken);
        }

        // Like a vanilla chest: only one player in the menu at a time. The owner hands us the
        // piece on yes, so the actions below are local writes.
        var nview = target.Nview;
        PieceMenuLock.Request(
            nview,
            player,
            onGranted: () =>
            {
                _leaseView = nview;
                _leasePlayer = player.GetPlayerID();
                _nextRenew = Time.time + PieceMenuLock.RenewSeconds;
                _tab = startTab;
                _extraId = null;
                // Other mods' tabs lead the shared menu: open on the first one unless a specific tab was asked for.
                if (startTab == LockTab.Piece && !wardOff && _chest != null && PieceMenuTabs.For(_chest) is { Count: > 0 } leading)
                {
                    _tab = LockTab.Extra;
                    _extraId = leading[0].Id;
                }
                Reopen();
            },
            onDenied: () =>
            {
                _chest = null;
                _door = null;
            });
        return true;
    }

    static void Render(Target target, Player player, bool force)
    {
        var tabs = BuildTabs(target, player);
        var wardOff = WardIsOffHere(target, player);
        if (wardOff)
        {
            // Ward is off: only the switch to turn it back on. No Piece, Ward list or Groups.
            _tab = LockTab.Ward;
            tabs.ShowWard = true;
            tabs.ShowGroups = false;
            tabs.OnlyWard = true;
        }

        if (_tab == LockTab.Ward && (!tabs.ShowWard || _wardArea == null))
            _tab = LockTab.Piece;
        if (_tab == LockTab.Groups && !tabs.ShowGroups)
            _tab = LockTab.Piece;
        if (_tab == LockTab.Extra && !tabs.Extras.Exists(e => e.Id == _extraId))
            _tab = LockTab.Piece;
        tabs.Active = _tab;
        tabs.ActiveExtraId = _extraId;

        var sig = new StringBuilder(_tab.ToString());
        if (_tab == LockTab.Extra)
            sig.Append(_extraId);
        Action closed = () =>
        {
            ReleaseLease();
            _chest = null;
            _door = null;
            _lastSignature = string.Empty;
            _tab = LockTab.Piece;
            _extraId = null;
            _wardArea = null;
        };

        if (_tab == LockTab.Ward)
        {
            var model = LockSections.Ward(_wardArea!, player, Reopen);
            model.SwitchOnly = wardOff;
            if (wardOff && !model.CanSwitch)
                model.Note = "The ward is off. Ask someone on the ward to turn it back on.";
            sig.Append(model.On).Append(model.SwitchMin).Append(model.Note).Append(wardOff);
            foreach (var p in model.People)
                sig.Append('|').Append(p.Name).Append(p.Role).Append(p.CanRemove);
            if (!Changed(sig, force))
                return;
            Menu.OpenWard(wardOff ? "Ward is off" : "Ward", wardOff ? target.Name : LockSections.WardSubtitle(_wardArea!), model, tabs, closed);
            return;
        }

        if (_tab == LockTab.Extra && target.Chest != null && tabs.Extras.Find(e => e.Id == _extraId) is { } extra)
        {
            if (!Changed(sig, force))
                return;
            var canManage = PieceGuestAccess.CanManageGuests(target.Nview, player.GetPlayerID());
            Menu.OpenExtra(target.Name, extra.Title, extra, target.Chest, canManage, Reopen, tabs, closed);
            return;
        }

        if (_tab == LockTab.Groups)
        {
            var model = LockSections.Groups(target.Nview, player, target.Noun, Reopen);
            sig.Append(model.CanSaveGuests).Append(model.CanSaveNearby);
            foreach (var g in model.Groups)
                sig.Append('|').Append(g.Name).Append(g.Members.Count);
            if (!Changed(sig, force))
                return;
            Menu.OpenGroups(target.Name, "Saved groups", model, tabs, closed);
            return;
        }

        var actions = BuildPieceActions(target, player);
        var subtitle = BuildSubtitle(target, player);
        sig.Append(subtitle);
        foreach (var a in actions)
            sig.Append('|').Append(a.Label).Append(a.Enabled ? '1' : '0').Append(LockMenuKinds.TagOf(a).Flags);
        if (!Changed(sig, force))
            return;
        if (actions.Count == 0)
        {
            Close();
            return;
        }

        var joinOn = PieceGuestAccess.IsOptInReady(target.Nview);
        var publicOn = !target.PrivateFamily && PieceAccessState.IsPublic(target.Nview);
        Menu.OpenPiece(target.Name, subtitle, actions, joinOn, publicOn, tabs, closed);
    }

    static bool Changed(StringBuilder sig, bool force)
    {
        var signature = sig.ToString();
        if (!force && signature == _lastSignature)
            return false;
        _lastSignature = signature;
        return true;
    }

    /// <summary>Piece is always there. Ward/Groups only after Enable LockSmith on this piece (and when the player can use them).</summary>
    static LockTabs BuildTabs(Target t, Player player)
    {
        var tabs = new LockTabs { Select = SelectTab, SelectExtra = SelectExtra };
        if (t.Chest != null)
            tabs.Extras = PieceMenuTabs.For(t.Chest);
        try
        {
            // Keep the Lock tab lean until designation: Ward and Groups appear only once LockSmith is on.
            if (!PieceAccessState.IsManaged(t.Nview))
                return tabs;

            var playerId = player.GetPlayerID();
            var wards = WardService.WardsFor(player.transform.position, playerId);
            tabs.ShowWard = wards.Count > 0;
            if (tabs.ShowWard && (_wardArea == null || !wards.Contains(_wardArea)))
                _wardArea = wards[0];
            tabs.ShowGroups = PieceGuestAccess.CanManageGuests(t.Nview, playerId);
        }
        catch (Exception ex)
        {
            // Tabs are extras: the Piece tab still opens without them.
            LockSmith.Log?.LogError($"Lock window tabs failed: {ex}");
        }

        return tabs;
    }

    static void SelectExtra(PieceMenuTab tab)
    {
        _tab = LockTab.Extra;
        _extraId = tab.Id;
        _lastSignature = string.Empty;
        Reopen();
    }

    static void SelectTab(LockTab tab)
    {
        _tab = tab;
        _lastSignature = string.Empty;
        Reopen();
    }

    static List<DrakeMenuAction> BuildPieceActions(Target t, Player player)
    {
        LockMenuKinds.Forget();
        var actions = new List<DrakeMenuAction>();
        var playerId = player.GetPlayerID();
        var nview = t.Nview;
        var isGuest = PieceGuestAccess.IsGuest(nview, playerId);
        // Owner tools (Enable, Join, Personal/Team, Remove) are open to anyone with access once managed access is on.
        var ownerTools = LockSmithConfig.EnableManagedAccess;

        if (t.PrivateFamily)
        {
            var creator = GroupAccessState.IsCreator(t.Chest!, playerId);
            if (creator)
                GroupAccessState.EnsureTeamModeWhenPauseDisabled(nview);
            var team = GroupAccessState.IsTeamMode(nview);

            if (creator && ownerTools && GroupChestService.IsEligible(t.Chest))
            {
                if (LockSmithConfig.EnablePersonalPause)
                {
                    actions.Add(Action(
                        L(team ? LockSmithLocalization.HoverMakePersonalToken : LockSmithLocalization.HoverMakeTeamToken),
                        nview, GameHookTargets.RpcSetGroupMode,
                        () =>
                        {
                            GroupChestService.RequestSetTeamMode(nview, !team, playerId);
                            AccessFeedback.Show(player,
                                team ? LockSmithLocalization.MsgNowPersonalToken : LockSmithLocalization.MsgNowTeamToken);
                        }));
                }

                if (team && LockSmithConfig.EnableOptInAccess)
                    actions.Add(LockMenuKinds.Tag(JoinAction(nview, player), LockMenuKind.Join));

                if (team)
                    AddNearby(actions, nview, player);

                AddRemoveGuests(actions, t, player);
                AddCopyPaste(actions, t, player);
                actions.Add(ClearTagged(t, player));
            }
            else if (!creator && isGuest && team)
            {
                // A guest who was allowed to let others on can open Join and add people. Everyone can leave.
                if (GuestPerms.HasPerm(nview, playerId, GuestPerm.Add))
                {
                    if (LockSmithConfig.EnableOptInAccess)
                        actions.Add(LockMenuKinds.Tag(JoinAction(nview, player), LockMenuKind.Join));
                    AddNearby(actions, nview, player);
                }

                actions.Add(LeaveTagged(t, player));
            }
            else if (!creator && team && LockSmithConfig.EnableOptInAccess && PieceGuestAccess.IsOptInReady(nview))
            {
                actions.Add(JoinSelfAction(nview, player));
            }

            return actions;
        }

        var wardMember = WardAccess.HasLocalWardAccess(t.Pos);
        var managed = PieceAccessState.IsManaged(nview);
        var isPublic = PieceAccessState.IsPublic(nview);

        if (!managed)
        {
            if (ownerTools && wardMember)
            {
                actions.Add(Action(L(LockSmithLocalization.HoverDesignateToken), nview, PublicRpc(t), () =>
                {
                    RequestSetPublic(t, PieceAccessState.IsPublic(nview), playerId);
                    AccessFeedback.Show(player, LockSmithLocalization.MsgManagedToken);
                    Reopen(); // Ward / Groups tabs appear once LockSmith is on.
                }));
            }

            return actions;
        }

        // Not on the ward and not a guest: the only thing on offer is joining while Join is open.
        if (!wardMember && !isGuest)
        {
            if (!isPublic && PieceGuestService.GuestsEnabled && PieceGuestAccess.IsOptInReady(nview))
                actions.Add(JoinSelfAction(nview, player));
            return actions;
        }

        // A plain guest has access but can't change anything. A guest the owner allowed can let others on and/or switch public/private.
        var guestAdd = isGuest && !wardMember && GuestPerms.HasPerm(nview, playerId, GuestPerm.Add);
        var guestToggle = isGuest && !wardMember && GuestPerms.HasPerm(nview, playerId, GuestPerm.Toggle);

        if ((wardMember && ownerTools) || guestToggle)
        {
            actions.Add(Action(L(isPublic ? LockSmithLocalization.HoverMakePrivateToken : LockSmithLocalization.HoverMakePublicToken), nview, PublicRpc(t), () =>
            {
                RequestSetPublic(t, !isPublic, playerId);
                AccessFeedback.Show(player,
                    isPublic ? LockSmithLocalization.MsgNowPrivateToken : LockSmithLocalization.MsgNowPublicToken);
            }));
            LockMenuKinds.Tag(actions[actions.Count - 1], LockMenuKind.Public);
        }

        if (!isPublic && PieceGuestService.GuestsEnabled && ((ownerTools && wardMember) || guestAdd))
        {
            actions.Add(LockMenuKinds.Tag(JoinAction(nview, player), LockMenuKind.Join));
            AddNearby(actions, nview, player);

            // Removing people, their permissions and copy/paste stay with ward members and the owner.
            if (wardMember)
            {
                AddRemoveGuests(actions, t, player);
                AddCopyPaste(actions, t, player);
            }
        }

        if (isGuest && !wardMember)
            actions.Add(LeaveTagged(t, player));

        if (ownerTools && wardMember && PieceClearService.HasLockSmithData(nview))
            actions.Add(ClearTagged(t, player));

        return actions;
    }

    /// <summary>Leave access, shown in the footer with an inline "are you sure?" instead of a popup.</summary>
    static DrakeMenuAction LeaveTagged(Target t, Player player)
    {
        var nview = t.Nview;
        var playerId = player.GetPlayerID();
        var ask = PieceGuestAccess.IsOptInReady(nview)
            ? "Leave access? You can join again while Join is open."
            : "Leave access? You can't rejoin unless the owner opens Join.";
        return LockMenuKinds.Tag(LeaveAction(t, player), LockMenuKind.Leave, ask, "Leave", () =>
        {
            PieceGuestAccess.RequestOptOutSelf(nview, playerId);
            Close();
        });
    }

    /// <summary>Remove LockSmith, shown in the footer with an inline confirm.</summary>
    static DrakeMenuAction ClearTagged(Target t, Player player)
    {
        var nview = t.Nview;
        var guests = PieceGuestAccess.GetGuests(nview).Count;
        var ask = guests > 0
            ? "Back to a normal " + t.Noun + ". Its " + guests + " guest(s) lose access."
            : "Back to a normal " + t.Noun + ".";
        return LockMenuKinds.Tag(ClearAction(t, player), LockMenuKind.Clear, ask, "Remove", () =>
        {
            PieceClearService.RequestClear(nview, player.GetPlayerID());
            AccessFeedback.Show(player, LockSmithLocalization.MsgClearedToken);
            Close();
        });
    }

    /// <summary>Copy the people on this piece, paste them onto another one. Session clipboard on this computer; nothing is saved.</summary>
    static void AddCopyPaste(List<DrakeMenuAction> actions, Target t, Player player)
    {
        var nview = t.Nview;
        var playerId = player.GetPlayerID();
        var guests = PieceGuestAccess.GetGuests(nview);

        actions.Add(LockMenuKinds.Tag(new DrakeMenuAction("Copy people (" + guests.Count + ")", () =>
        {
            GuestClipboard.Copy(PieceGuestAccess.GetGuests(nview));
            AccessFeedback.ShowRaw(Player.m_localPlayer, "Copied " + GuestClipboard.Count + " people.");
            Reopen();
        }, enabled: guests.Count > 0), LockMenuKind.Copy));

        var held = GuestClipboard.Count;
        var paste = Action("Paste people (" + held + ")", nview, GameHookTargets.RpcMergeGuests, () =>
        {
            if (PieceMenuLock.BlockIfHeldByOther(nview, player))
                return;
            var count = GuestClipboard.Count;
            PieceGuestAccess.RequestMergeGuests(nview, GuestClipboard.People, playerId,
                () => AccessFeedback.ShowRaw(Player.m_localPlayer, "Pasted " + count + " people."));
        });
        if (held == 0)
            paste.Enabled = false;
        actions.Add(LockMenuKinds.Tag(paste, LockMenuKind.Paste));
    }

    static DrakeMenuAction JoinAction(ZNetView nview, Player player)
    {
        var ready = PieceGuestAccess.IsOptInReady(nview);
        return Action(L(ready ? LockSmithLocalization.HoverCloseOptInToken : LockSmithLocalization.HoverOpenOptInToken), nview, GameHookTargets.RpcSetOptInReady, () =>
        {
            if (!PieceGuestAccess.TryRequestJoinToggle(nview, player.GetPlayerID(), out var next))
                return;
            AccessFeedback.Show(player,
                next ? LockSmithLocalization.MsgOptInOpenedToken : LockSmithLocalization.MsgOptInClosedToken);
        });
    }

    /// <summary>Opt yourself in (Join is open). Joining always goes through this menu, never plain E.</summary>
    static DrakeMenuAction JoinSelfAction(ZNetView nview, Player player) =>
        Action(L(LockSmithLocalization.HoverJoinAccessToken), nview, GameHookTargets.RpcOptInSelf,
            () => PieceGuestAccess.RequestOptInSelf(nview, player.GetPlayerID(), player.GetPlayerName()));

    /// <summary>
    /// Optional (<see cref="LockSmithConfig.EnableAddNearby"/>): one "Add name" button per nearby player who isn't a guest yet.
    /// </summary>
    static void AddNearby(List<DrakeMenuAction> actions, ZNetView nview, Player player)
    {
        if (!LockSmithConfig.EnableAddNearby)
            return;

        var nearby = NearbyPlayers.FindAllNearby(player, p => !PieceGuestAccess.IsGuest(nview, p.GetPlayerID()));
        if (nearby.Count == 0)
        {
            actions.Add(LockMenuKinds.Tag(new DrakeMenuAction("Add nearby: no one", () => { }, enabled: false), LockMenuKind.Nearby));
            return;
        }

        foreach (var near in nearby)
            {
            AddNearbyPlayer(actions, nview, player, new PieceGuest(near.GetPlayerID(), near.GetPlayerName()));
            LockMenuKinds.Tag(actions[actions.Count - 1], LockMenuKind.Nearby);
            }
    }

    /// <summary>One "Remove name" button per guest. Each asks Yes/No first; the menu closes while it's up.</summary>
    static void AddRemoveGuests(List<DrakeMenuAction> actions, Target t, Player player)
    {
        var nview = t.Nview;
        foreach (var guest in PieceGuestAccess.GetGuests(nview))
        {
            var target = guest;
            actions.Add(LockMenuKinds.Tag(Action("Remove " + guest.DisplayName, nview, GameHookTargets.RpcRemoveGuest, () =>
            {
                Menu.CloseSilent();
                Confirm.Show(
                    "Remove " + target.DisplayName + "?",
                    target.DisplayName + " loses access to this " + t.Noun + ".",
                    onYes: () =>
                    {
                        PieceGuestAccess.RequestRemoveGuest(nview, target.PlayerId, player.GetPlayerID(),
                            () => AccessFeedback.ShowRaw(Player.m_localPlayer, "Removed " + target.DisplayName));
                        Close();
                    },
                    onNo: Reopen);
            }), LockMenuKind.Remove, "Remove " + target.DisplayName + "?", "Remove", () =>
            {
                PieceGuestAccess.RequestRemoveGuest(nview, target.PlayerId, player.GetPlayerID(),
                    () => AccessFeedback.ShowRaw(Player.m_localPlayer, "Removed " + target.DisplayName));
                Reopen();
            }, personId: target.PlayerId, flags: GuestPerms.FlagsOf(nview, target.PlayerId),
                setFlags: flags => GuestPerms.RequestOne(nview, target.PlayerId, flags, player.GetPlayerID())));
        }
    }

    static void AddNearbyPlayer(List<DrakeMenuAction> actions, ZNetView nview, Player player, PieceGuest guest)
    {
        actions.Add(Action("Add " + guest.DisplayName, nview, GameHookTargets.RpcMergeGuests, () =>
        {
            if (PieceMenuLock.BlockIfHeldByOther(nview, player))
                return;
            PieceGuestAccess.RequestMergeGuests(
                nview,
                new[] { guest },
                player.GetPlayerID(),
                () => AccessFeedback.ShowRaw(Player.m_localPlayer, "Added " + guest.DisplayName));
        }));
    }

    /// <summary>Always asks first; the text says whether you can come back.</summary>
    static DrakeMenuAction LeaveAction(Target t, Player player)
    {
        var nview = t.Nview;
        return Action(L(LockSmithLocalization.HoverLeaveAccessToken), nview, GameHookTargets.RpcOptOutSelf, () =>
        {
            var playerId = player.GetPlayerID();
            Menu.CloseSilent();
            Confirm.Show(
                "Leave access?",
                PieceGuestAccess.IsOptInReady(nview)
                    ? "You can join again while Join is open."
                    : "You can't rejoin unless the owner opens Join.",
                onYes: () =>
                {
                    PieceGuestAccess.RequestOptOutSelf(nview, playerId);
                    Close();
                },
                onNo: Reopen);
        });
    }

    static DrakeMenuAction ClearAction(Target t, Player player)
    {
        var nview = t.Nview;
        var guests = PieceGuestAccess.GetGuests(nview).Count;
        return Action(L(LockSmithLocalization.HoverClearLockSmithToken) + "…", nview, GameHookTargets.RpcClearLockSmith, () =>
        {
            Menu.CloseSilent();
            Confirm.Show(
                "Remove LockSmith?",
                guests > 0
                    ? "This goes back to a normal " + t.Noun + ". Its " + guests + " guest(s) lose access."
                    : "This goes back to a normal " + t.Noun + ".",
                onYes: () =>
                {
                    PieceClearService.RequestClear(nview, player.GetPlayerID());
                    AccessFeedback.Show(player, LockSmithLocalization.MsgClearedToken);
                    Close();
                },
                onNo: Reopen);
        });
    }

    /// <summary>Button that greys out while its request is still syncing (no double-sends).</summary>
    static DrakeMenuAction Action(string label, ZNetView nview, string rpc, Action onClick) =>
        new DrakeMenuAction(label, () =>
        {
            try
            {
                onClick();
            }
            catch (Exception ex)
            {
                LockSmith.Log?.LogError($"Lock menu '{label}' failed: {ex}");
            }

            _nextRefresh = 0f;
        }, enabled: !PieceRpc.HasPending(nview, rpc));

    static string BuildSubtitle(Target t, Player player)
    {
        var nview = t.Nview;
        var parts = new List<string>();

        if (t.PrivateFamily)
        {
            parts.Add(GroupAccessState.IsTeamMode(nview) ? "Shared" : "Personal");
        }
        else if (!PieceAccessState.IsManaged(nview))
        {
            parts.Add("Not LockSmith yet");
        }
        else
        {
            parts.Add(PieceAccessState.IsPublic(nview) ? "Public" : "Private");
        }

        if (PieceGuestAccess.IsOptInReady(nview))
            parts.Add("Join open");

        PieceGuestAccess.TryRefreshGuestNames(nview);
        var guests = PieceGuestAccess.GetGuests(nview);
        if (guests.Count > 0)
        {
            var names = new StringBuilder();
            foreach (var g in guests)
            {
                if (names.Length > 0)
                    names.Append(", ");
                names.Append(string.IsNullOrEmpty(g.DisplayName) ? "Unknown" : g.DisplayName);
            }

            parts.Add("Guests: " + names);
        }

        if (PieceGuestAccess.IsGuest(nview, player.GetPlayerID()))
            parts.Add("You're a guest");

        return string.Join(" · ", parts.ToArray());
    }

    static void Reopen()
    {
        var local = Player.m_localPlayer;
        if (local != null && TryResolve(out var target))
            Render(target, local, force: true);
    }

    static void RequestSetPublic(Target t, bool isPublic, long playerId)
    {
        if (t.Door != null)
            DoorAccessService.RequestSetPublic(t.Nview, isPublic, playerId);
        else
            ChestAccessService.RequestSetPublic(t.Nview, isPublic, playerId);
    }

    static string PublicRpc(Target t) =>
        t.Door != null ? GameHookTargets.RpcSetDoorPublic : GameHookTargets.RpcSetChestPublic;

    static string L(string token) => LockSmithLocalization.T(token);

    static bool Deny(Humanoid user, string token)
    {
        AccessFeedback.Show(user, token);
        return true;
    }

    sealed class Target
    {
        public ZNetView Nview = null!;
        public Vector3 Pos;
        public Container? Chest;
        public Door? Door;
        public bool PrivateFamily;
        public string Name = string.Empty;
        public string Noun = "chest";
    }

    static bool TryResolve(out Target target) => TryResolve(_chest, _door, out target);

    static bool TryResolve(Container? chest, Door? door, out Target target)
    {
        target = null!;
        if (chest)
        {
            var privateFamily = GroupAccessState.IsPrivateFamilyChest(chest);
            var nview = privateFamily ? GroupAccessState.GetNetView(chest!) : PieceAccessState.GetNetView(chest!);
            if (nview == null || !nview.IsValid())
                return false;

            target = new Target
            {
                Nview = nview,
                Pos = privateFamily ? GroupAccessState.GetPosition(chest!) : PieceAccessState.GetPosition(chest!),
                Chest = chest,
                PrivateFamily = privateFamily,
                Name = AccessHoverDisplay.LocalizedPieceName(chest!),
                Noun = "chest"
            };
            return true;
        }

        if (door)
        {
            var nview = PieceAccessState.GetNetView(door!);
            if (nview == null || !nview.IsValid())
                return false;

            target = new Target
            {
                Nview = nview,
                Pos = PieceAccessState.GetPosition(door!),
                Door = door,
                Name = AccessHoverDisplay.LocalizedPieceName(door!),
                Noun = "door"
            };
            return true;
        }

        return false;
    }
}

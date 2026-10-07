using System;
using System.Collections.Generic;
using System.Text;
using DrakeModsLibs.UI;
using LockSmith.Access;
using UnityEngine;

namespace LockSmith.UI;

/// <summary>
/// The one Lock menu for a chest or door: AltPlace + E (no key needed), or E while holding the key.
/// Buttons are context-sensitive and only shown to players with access; strangers get no menu.
/// Every action calls the existing access services.
/// Re-renders while open so RPC confirms (or failures) show up without reopening.
/// </summary>
public static class PieceAccessMenu
{
    const float MaxDistance = 6f;
    const float RefreshSeconds = 0.25f;

    static readonly DrakeWoodActionMenu Menu = new DrakeWoodActionMenu("locksmith_piece_menu");
    static readonly DrakeConfirmPanel Confirm = new DrakeConfirmPanel("locksmith_piece_confirm");

    static Container? _chest;
    static Door? _door;
    static bool _withKey;
    static string _lastSignature = string.Empty;
    static float _nextRefresh;

    // One-player-at-a-time lease on the open piece (see PieceMenuLock).
    static ZNetView? _leaseView;
    static long _leasePlayer;
    static float _nextRenew;

    public static bool IsOpen => Menu.IsOpen || Confirm.IsOpen;

    /// <summary>Key in hand. Always consumes the interact (shows a reason when there is nothing to do).</summary>
    public static bool TryOpenWithKey(Container container, Humanoid user)
    {
        if (!LockSmithConfig.EnableManagedAccess)
            return Deny(user, LockSmithLocalization.MsgDisabledToken);
        if (PublicPieceRegistration.IsPublicPiece(container))
            return Deny(user, LockSmithLocalization.MsgPublicPrefabToken);

        if (GroupAccessState.IsPrivateFamilyChest(container))
        {
            if (!GroupChestService.IsEligible(container))
                return Deny(user, LockSmithLocalization.MsgDisabledToken);
        }
        else
        {
            if (!LockSmithConfig.EnableChests)
                return Deny(user, LockSmithLocalization.MsgDisabledToken);
            if (!PieceAccessState.IsEligibleChest(container))
                return Deny(user, LockSmithLocalization.MsgWrongTargetToken);
        }

        return TryOpen(container, null, user, withKey: true, reportEmpty: true);
    }

    public static bool TryOpenWithKey(Door door, Humanoid user)
    {
        if (!LockSmithConfig.EnableDoors || !LockSmithConfig.EnableManagedAccess)
            return Deny(user, LockSmithLocalization.MsgDisabledToken);
        if (PublicPieceRegistration.IsPublicPiece(door))
            return Deny(user, LockSmithLocalization.MsgPublicPrefabToken);
        if (!PieceAccessState.IsEligibleDoor(door))
            return Deny(user, LockSmithLocalization.MsgWrongTargetToken);

        return TryOpen(null, door, user, withKey: true, reportEmpty: true);
    }

    /// <summary>No key, AltPlace + E. Returns false (vanilla runs) when this player has nothing to do here.</summary>
    public static bool TryOpenNoKey(Container container, Humanoid user) =>
        IsNoKeyTarget(container) && TryOpen(container, null, user, withKey: false, reportEmpty: false);

    public static bool TryOpenNoKey(Door door, Humanoid user) =>
        IsNoKeyTarget(door) && TryOpen(null, door, user, withKey: false, reportEmpty: false);

    /// <summary>
    /// Hover check: would AltPlace+E (no key) open a menu with buttons for the local player?
    /// Strangers get false, so they never see the hint.
    /// </summary>
    public static bool HasNoKeyMenu(Container container) => NoKeyActionCount(container, null) > 0;

    public static bool HasNoKeyMenu(Door door) => NoKeyActionCount(null, door) > 0;

    /// <summary>
    /// Hover hint for the local player: "[AltPlace+E] Join access" when joining is all they can do,
    /// "[AltPlace+E] Lock menu" when they have more, empty for strangers with nothing to do.
    /// </summary>
    public static string NoKeyHint(Container container) => Hint(NoKeyActions(container, null));

    public static string NoKeyHint(Door door) => Hint(NoKeyActions(null, door));

    static string Hint(List<DrakeMenuAction>? actions)
    {
        if (actions == null || actions.Count == 0)
            return string.Empty;
        if (actions.Count == 1 && actions[0].Label == L(LockSmithLocalization.HoverJoinAccessToken))
            return AccessHoverDisplay.JoinHint();
        return AccessHoverDisplay.MenuHint(withKey: false);
    }

    static int NoKeyActionCount(Container? chest, Door? door) => NoKeyActions(chest, door)?.Count ?? 0;

    static List<DrakeMenuAction>? NoKeyActions(Container? chest, Door? door)
    {
        if (chest != null ? !IsNoKeyTarget(chest) : door == null || !IsNoKeyTarget(door))
            return null;

        var local = Player.m_localPlayer;
        if (local == null || !TryResolve(chest, door, out var target))
            return null;
        if (!target.PrivateFamily && !WardAccess.AllowsToolOnPiece(target.Pos, isPrivateFamilyChest: false))
            return null;
        return BuildActions(target, local, withKey: false);
    }

    static bool IsNoKeyTarget(Container container)
    {
        if (container == null || PublicPieceRegistration.IsPublicPiece(container))
            return false;
        if (GroupAccessState.IsPrivateFamilyChest(container))
            return LockSmithConfig.EnableGroupChests;
        return LockSmithConfig.EnableChests && PieceAccessState.IsEligibleChest(container);
    }

    static bool IsNoKeyTarget(Door door) =>
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
            || Vector3.Distance(local.transform.position, target.Pos) > MaxDistance
            || (_withKey && !ChestAccessService.IsHoldingLocksmithKey(local)))
        {
            Close();
            return;
        }

        Render(target, local, force: false);
    }

    public static void Close()
    {
        Confirm.Close();
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

    static bool TryOpen(Container? container, Door? door, Humanoid user, bool withKey, bool reportEmpty)
    {
        var player = user as Player;
        if (player == null)
            return false;

        if (_leaseView != null)
            Close();

        _chest = container;
        _door = door;
        _withKey = withKey;
        if (!TryResolve(out var target))
            return false;

        if (!target.PrivateFamily && !WardAccess.AllowsToolOnPiece(target.Pos, isPrivateFamilyChest: false))
        {
            _chest = null;
            _door = null;
            return reportEmpty && Deny(user, LockSmithLocalization.MsgNeedActiveWardToken);
        }

        var actions = BuildActions(target, player, _withKey);
        if (actions.Count == 0)
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
        var actions = BuildActions(target, player, _withKey);
        var subtitle = BuildSubtitle(target, player);

        var sig = new StringBuilder(subtitle);
        foreach (var a in actions)
            sig.Append('|').Append(a.Label).Append(a.Enabled ? '1' : '0');
        var signature = sig.ToString();
        if (!force && signature == _lastSignature)
            return;
        _lastSignature = signature;

        if (actions.Count == 0)
        {
            Close();
            return;
        }

        Menu.Open(
            target.Name,
            subtitle,
            actions,
            cancelLabel: "Close",
            onClosed: () =>
            {
                ReleaseLease();
                _chest = null;
                _door = null;
                _lastSignature = string.Empty;
            },
            showCancel: true);
    }

    /// <summary>
    /// Owner tools (Enable, Join, Personal/Team, Paste, Remove) need no key unless the server
    /// turns on <see cref="LockSmithConfig.RequireKeyForSetup"/>.
    /// </summary>
    static bool OwnerTools(bool withKey) =>
        LockSmithConfig.EnableManagedAccess && (withKey || !LockSmithConfig.RequireKeyForSetup);

    static List<DrakeMenuAction> BuildActions(Target t, Player player, bool withKey)
    {
        var actions = new List<DrakeMenuAction>();
        var playerId = player.GetPlayerID();
        var nview = t.Nview;
        var isGuest = PieceGuestAccess.IsGuest(nview, playerId);
        var ownerTools = OwnerTools(withKey);

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
                    actions.Add(JoinAction(nview, player));

                if (team)
                    AddNearby(actions, nview, player);

                AddCopyPaste(actions, t, player, withKey, canManage: true);
                actions.Add(ClearAction(t, player));
            }
            else if (!creator && isGuest && team)
            {
                // Guests only: Leave. Copy/Paste are owner tools.
                actions.Add(LeaveAction(t, player));
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

        var canToggleAsMember = wardMember
                                && (ownerTools
                                    || (LockSmithConfig.EnableManagedAccess && LockSmithConfig.EnableGuestPublicToggle));
        var canToggleAsGuest = isGuest
                               && LockSmithConfig.EnableGuestPublicToggle
                               && LockSmithConfig.EnablePieceGuests;
        if (canToggleAsMember || canToggleAsGuest)
        {
            actions.Add(Action(L(isPublic ? LockSmithLocalization.HoverMakePrivateToken : LockSmithLocalization.HoverMakePublicToken), nview, PublicRpc(t), () =>
            {
                RequestSetPublic(t, !isPublic, playerId);
                AccessFeedback.Show(player,
                    isPublic ? LockSmithLocalization.MsgNowPrivateToken : LockSmithLocalization.MsgNowPublicToken);
            }));
        }

        if (ownerTools && wardMember && !isPublic && PieceGuestService.GuestsEnabled)
        {
            actions.Add(JoinAction(nview, player));
            AddNearby(actions, nview, player);
        }

        // Copy/Paste are for ward members only; a guest just gets public/private and Leave.
        if (wardMember)
            AddCopyPaste(actions, t, player, withKey, canManage: ownerTools);

        if (isGuest && !wardMember)
            actions.Add(LeaveAction(t, player));

        if (ownerTools && wardMember && PieceClearService.HasLockSmithData(nview))
            actions.Add(ClearAction(t, player));

        return actions;
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

    /// <summary>Optional (<see cref="LockSmithConfig.EnableAddNearby"/>): add the closest player who isn't a guest yet.</summary>
    static void AddNearby(List<DrakeMenuAction> actions, ZNetView nview, Player player)
    {
        if (!LockSmithConfig.EnableAddNearby)
            return;

        var nearest = KeyPassService.FindNearestPlayer(player, p => !PieceGuestAccess.IsGuest(nview, p.GetPlayerID()));
        if (nearest == null)
        {
            actions.Add(new DrakeMenuAction("Add nearby: no one", () => { }, enabled: false));
            return;
        }

        var guest = new PieceGuest(nearest.GetPlayerID(), nearest.GetPlayerName());
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

    /// <summary>With the key in hand the names go on the key; otherwise a session clipboard holds them.</summary>
    static void AddCopyPaste(List<DrakeMenuAction> actions, Target t, Player player, bool withKey, bool canManage)
    {
        if (!LockSmithConfig.EnableKeyPasses)
            return;

        var key = withKey ? ChestAccessService.GetEquippedLocksmithKey(player) : null;
        var hasGuests = PieceGuestAccess.GetGuests(t.Nview).Count > 0;

        if (key != null)
        {
            if (hasGuests)
                actions.Add(new DrakeMenuAction("Copy guests to key", () => KeyPassService.TryPullFromPiece(key, t.Nview)));

            if (canManage && KeyPassService.HasPassPayload(key))
            {
                actions.Add(Action("Paste guests from key", t.Nview, GameHookTargets.RpcMergeGuests,
                    () => KeyPassService.TryPasteOntoPiece(key, t.Nview)));
            }

            return;
        }

        if (hasGuests)
            actions.Add(new DrakeMenuAction("Copy guests", () => KeyPassService.CopyToSession(t.Nview)));

        if (canManage && KeyPassService.HasSessionNames)
        {
            actions.Add(Action("Paste guests (" + KeyPassService.SessionNameCount + ")", t.Nview,
                GameHookTargets.RpcMergeGuests, () => KeyPassService.PasteFromSession(t.Nview)));
        }
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
            parts.Add(GroupAccessState.IsTeamMode(nview) ? "Team" : "Personal");
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

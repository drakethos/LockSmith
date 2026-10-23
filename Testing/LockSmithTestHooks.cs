using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using LockSmith.Access;
using UnityEngine;

namespace LockSmith.Testing;

/// <summary>DrakesTestLab finds methods marked with an attribute named <c>TestHookAttribute</c>. No reference to DrakesTestLab.</summary>
[AttributeUsage(AttributeTargets.Method)]
internal sealed class TestHookAttribute : Attribute
{
    public TestHookAttribute(string name) => Name = name;
    public string Name { get; }
}

/// <summary>
/// Test hooks for automated LockSmith tests. Actions go through the same request calls the Lock menu makes (the permission checks run
/// where the menu's do), as the current player. Whether a chest or door actually opens is tested with TestLab's interact step.
/// Target = a tagged chest / door (args "target"), else the nearest one.
/// </summary>
internal static class LockSmithTestHooks
{
    static Player P => Player.m_localPlayer ?? throw new InvalidOperationException("no local player");

    static bool B(Dictionary<string, object?> a, string key, bool fallback = false)
    {
        if (!a.TryGetValue(key, out var v) || v == null)
            return fallback;
        if (v is bool b)
            return b;
        var s = Convert.ToString(v, CultureInfo.InvariantCulture) ?? "";
        return s == "1" || s.Equals("true", StringComparison.OrdinalIgnoreCase);
    }

    static string S(Dictionary<string, object?> a, string key) =>
        a.TryGetValue(key, out var v) && v != null ? Convert.ToString(v, CultureInfo.InvariantCulture) ?? "" : "";

    /// <summary>The piece's chest or door and its ZNetView.</summary>
    static (Container? chest, Door? door, ZNetView view) Piece(GameObject? target)
    {
        GameObject? go = target;
        if (go == null)
        {
            var best = 20f;
            foreach (var c in UnityEngine.Object.FindObjectsOfType<Container>())
            {
                var d = Vector3.Distance(c.transform.position, P.transform.position);
                if (c.GetComponentInParent<global::Piece>() != null && d < best) { best = d; go = c.gameObject; }
            }

            foreach (var dr in UnityEngine.Object.FindObjectsOfType<Door>())
            {
                var d = Vector3.Distance(dr.transform.position, P.transform.position);
                if (d < best) { best = d; go = dr.gameObject; }
            }
        }

        if (go == null)
            throw new InvalidOperationException("no chest or door nearby");
        var chest = go.GetComponentInChildren<Container>() ?? go.GetComponentInParent<Container>();
        var door = chest == null ? go.GetComponentInChildren<Door>() ?? go.GetComponentInParent<Door>() : null;
        var view = go.GetComponentInParent<ZNetView>() ?? go.GetComponentInChildren<ZNetView>()
                   ?? throw new InvalidOperationException("the piece has no network view");
        return (chest, door, view);
    }

    static long Me => P.GetPlayerID();

    /// <summary>Open the Lock menu on the piece, as AltPlace+E does. True when it opened.</summary>
    [TestHook("lock.openMenu")]
    static bool OpenMenu(GameObject? target = null)
    {
        var (chest, door, _) = Piece(target);
        return chest != null ? global::LockSmith.UI.PieceAccessMenu.TryOpenLockMenu(chest, P) : global::LockSmith.UI.PieceAccessMenu.TryOpenLockMenu(door!, P);
    }

    [TestHook("lock.menuOpen")]
    static bool MenuOpen() => global::LockSmith.UI.PieceAccessMenu.IsOpen;

    [TestHook("lock.closeMenu")]
    static bool CloseMenu()
    {
        global::LockSmith.UI.PieceAccessMenu.Close();
        return true;
    }

    [TestHook("lock.isEligible")]
    static bool IsEligible(GameObject? target = null)
    {
        var (chest, door, _) = Piece(target);
        return chest != null ? PieceAccessState.IsEligibleChest(chest) : PieceAccessState.IsEligibleDoor(door);
    }

    [TestHook("lock.isPublic")]
    static bool IsPublic(GameObject? target = null) => PieceAccessState.IsPublic(Piece(target).view);

    [TestHook("lock.isManaged")]
    static bool IsManaged(GameObject? target = null) => PieceAccessState.IsManaged(Piece(target).view);

    /// <summary>Make public / private, as the Lock menu does (on = true makes it public). Returns the state afterwards.</summary>
    [TestHook("lock.setPublic")]
    static bool SetPublic(Dictionary<string, object?> args, GameObject? target = null)
    {
        var (chest, _, view) = Piece(target);
        var on = B(args, "on", true);
        if (chest != null)
            ChestAccessService.RequestSetPublic(view, on, Me);
        else
            DoorAccessService.RequestSetPublic(view, on, Me);
        return PieceAccessState.IsPublic(view);
    }

    /// <summary>Open / close Join (owner side, Lock menu). Returns whether Join is open afterwards.</summary>
    [TestHook("lock.openJoin")]
    static bool OpenJoin(Dictionary<string, object?> args, GameObject? target = null)
    {
        var view = Piece(target).view;
        PieceGuestAccess.RequestSetOptIn(view, B(args, "on", true), Me);
        return PieceGuestAccess.IsOptInReady(view);
    }

    [TestHook("lock.isJoinOpen")]
    static bool IsJoinOpen(GameObject? target = null) => PieceGuestAccess.IsOptInReady(Piece(target).view);

    /// <summary>Join as the current player (what AltPlace+E → Join access does). Returns whether they are a guest afterwards.</summary>
    [TestHook("lock.join")]
    static bool Join(GameObject? target = null)
    {
        var view = Piece(target).view;
        PieceGuestAccess.RequestOptInSelf(view, Me, P.GetPlayerName());
        return PieceGuestAccess.IsGuest(view, Me);
    }

    /// <summary>Leave the guest list as the current player.</summary>
    [TestHook("lock.leave")]
    static bool Leave(GameObject? target = null)
    {
        var view = Piece(target).view;
        PieceGuestAccess.RequestOptOutSelf(view, Me);
        return !PieceGuestAccess.IsGuest(view, Me);
    }

    /// <summary>Is the current player (or "who" = a name on the list) a guest?</summary>
    [TestHook("lock.isGuest")]
    static bool IsGuest(Dictionary<string, object?> args, GameObject? target = null)
    {
        var view = Piece(target).view;
        var who = S(args, "who");
        return who.Length == 0
            ? PieceGuestAccess.IsGuest(view, Me)
            : PieceGuestAccess.GetGuests(view).Any(g => g.DisplayName.Equals(who, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>Guest names, comma separated.</summary>
    [TestHook("lock.guests")]
    static string Guests(GameObject? target = null) =>
        string.Join(", ", PieceGuestAccess.GetGuests(Piece(target).view).Select(g => g.DisplayName));

    /// <summary>Remove a guest by name, as the current player (owner / manager). Returns whether they are gone afterwards.</summary>
    [TestHook("lock.removeGuest")]
    static bool RemoveGuest(Dictionary<string, object?> args, GameObject? target = null)
    {
        var view = Piece(target).view;
        var who = S(args, "who");
        var guest = PieceGuestAccess.GetGuests(view).FirstOrDefault(g => g.DisplayName.Equals(who, StringComparison.OrdinalIgnoreCase));
        if (guest.PlayerId == 0)
            throw new InvalidOperationException($"'{who}' is not a guest");
        PieceGuestAccess.RequestRemoveGuest(view, guest.PlayerId, Me);
        return !PieceGuestAccess.GetGuests(view).Any(g => g.PlayerId == guest.PlayerId);
    }

    /// <summary>Give a guest permissions, as the owner does in the Lock menu: who (name on the list), perms = "toggle", "add", "toggle,add" or "".
    /// Returns the guest's permissions afterwards.</summary>
    [TestHook("lock.grantPerm")]
    static string GrantPerm(Dictionary<string, object?> args, GameObject? target = null)
    {
        var view = Piece(target).view;
        var who = S(args, "who");
        var guest = PieceGuestAccess.GetGuests(view).FirstOrDefault(g => g.DisplayName.Equals(who, StringComparison.OrdinalIgnoreCase));
        if (guest.PlayerId == 0)
            throw new InvalidOperationException($"'{who}' is not a guest");
        var flags = GuestPerm.None;
        foreach (var part in S(args, "perms").Split(',', ';', '+'))
        {
            if (Enum.TryParse<GuestPerm>(part.Trim(), true, out var f))
                flags |= f;
        }

        GuestPerms.RequestOne(view, guest.PlayerId, (int)flags, Me);
        return ((GuestPerm)GuestPerms.FlagsOf(view, guest.PlayerId)).ToString();
    }

    /// <summary>May the current player manage the guest list?</summary>
    [TestHook("lock.canManageGuests")]
    static bool CanManageGuests(GameObject? target = null) => PieceGuestAccess.CanManageGuests(Piece(target).view, Me);

    /// <summary>Team / personal on a private-family chest (Make team = true). Returns team mode afterwards.</summary>
    [TestHook("lock.setTeam")]
    static bool SetTeam(Dictionary<string, object?> args, GameObject? target = null)
    {
        var view = Piece(target).view;
        GroupChestService.RequestSetTeamMode(view, B(args, "on", true), Me);
        return GroupAccessState.IsTeamMode(view);
    }

    [TestHook("lock.isTeam")]
    static bool IsTeam(GameObject? target = null) => GroupAccessState.IsTeamMode(Piece(target).view);

    /// <summary>Would the current player be let through (the same ward / guest / public checks the access patches use)?</summary>
    [TestHook("lock.wouldBypassWard")]
    static bool WouldBypassWard(GameObject? target = null)
    {
        var (chest, door, _) = Piece(target);
        return chest != null
            ? ChestAccessService.ShouldBypassWardCheck(chest) || PieceGuestService.ShouldBypassWardForGuest(chest)
            : DoorAccessService.ShouldBypassWardCheck(door!) || PieceGuestService.ShouldBypassWardForGuest(door!);
    }
}

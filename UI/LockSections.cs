using System;
using System.Collections.Generic;
using System.Linq;
using LockSmith.Access;
using UnityEngine;

namespace LockSmith.UI;

/// <summary>
/// Builds what the Ward and Groups tabs show. Ward rules are checked again on the ward owner's side (<see cref="WardService"/>);
/// these models only offer what the viewer is allowed to do.
/// </summary>
public static class LockSections
{
    public static string WardSubtitle(PrivateArea area)
    {
        var people = WardService.Members(area).Count + 1;
        var state = PrivateAreaAccess.IsEnabled(area) ? "On" : "Off";
        return state + " · " + people + (people == 1 ? " person" : " people");
    }

    public static LockWardModel Ward(PrivateArea area, Player player, Action reopen)
    {
        var nview = area.GetComponent<ZNetView>();
        var me = player.GetPlayerID();
        var myLevel = WardService.LevelOf(area, me);
        var iAmOwner = myLevel == WardLevel.Owner;
        var minimum = WardService.SwitchMinimum(nview);
        var on = PrivateAreaAccess.IsEnabled(area);

        var model = new LockWardModel
        {
            OwnerName = OwnerName(area),
            On = on,
            CanSwitch = myLevel != WardLevel.None && (int)myLevel >= minimum,
            SwitchMin = minimum,
            CanSetSwitchMin = iAmOwner,
            OnSwitch = value =>
            {
                WardService.RequestSetEnabled(nview, value, me);
                reopen();
            },
            SetSwitchMin = level =>
            {
                WardService.RequestSetSwitch(nview, level, me);
                reopen();
            },
        };

        if (!on && WardService.GuestTurnedOff(nview))
            model.Note = "A guest turned the ward off. Join stays closed until a member or the owner turns it on.";

        foreach (var member in WardService.Members(area))
        {
            var target = member;
            var person = new LockWardPerson
            {
                Name = target.Name,
                Role = target.Level == WardLevel.Guest ? "Guest" : "Member",
                CanRemove = iAmOwner || (WardService.IsRemover(nview, me) && myLevel > target.Level),
                Remove = () => WardService.RequestRemove(nview, target.Id, me, reopen),
            };

            if (iAmOwner)
            {
                var toGuest = target.Level == WardLevel.Team;
                person.RoleLabel = toGuest ? "Make guest" : "Make member";
                person.ToggleRole = () =>
                {
                    WardService.RequestSetRole(nview, target.Id, toGuest ? WardLevel.Guest : WardLevel.Team, me);
                    reopen();
                };

                var granted = WardService.IsRemover(nview, target.Id);
                person.GrantLabel = granted ? "Rights ✓" : "Can remove";
                person.ToggleGrant = () =>
                {
                    WardService.RequestSetRemover(nview, target.Id, !granted, me);
                    reopen();
                };
                if (granted)
                    person.Role += " · can remove";
            }

            model.People.Add(person);
        }

        return model;
    }

    public static LockGroupsModel Groups(ZNetView nview, Player player, string noun, Action reopen)
    {
        // Saved with player ids, so people can be added later even when they're offline.
        var guests = PieceGuestAccess.GetGuests(nview)
            .Where(g => g.PlayerId != 0L)
            .Select(g => new GroupMember(string.IsNullOrEmpty(g.DisplayName) ? "Unknown" : g.DisplayName, g.PlayerId))
            .ToList();
        var nearby = NearbyPlayers.FindAllNearby(player, _ => true)
            .Select(p => new GroupMember(p.GetPlayerName(), p.GetPlayerID()))
            .ToList();

        var model = new LockGroupsModel
        {
            Noun = noun,
            CanSaveGuests = guests.Count > 0,
            CanSaveNearby = nearby.Count > 0,
            FilePath = "config/LockSmith/groups.txt",
            SaveGuests = name => Save(name, guests, reopen),
            SaveNearby = name => Save(name, nearby, reopen),
        };

        foreach (var group in GroupStore.Load())
        {
            var g = group;
            model.Groups.Add(new LockGroupItem
            {
                Name = g.Name,
                Members = g.Members.Select(m => m.Name).ToList(),
                Add = () => AddGroup(nview, player, g, noun, reopen),
                Flags = g.Flags,
                SetFlags = flags =>
                {
                    GroupStore.SetFlags(g.Name, flags);
                    reopen();
                },
                CanAddGuests = guests.Count > 0,
                CanAddNearby = nearby.Count > 0,
                AddGuests = () => AddToGroup(g.Name, guests, reopen),
                AddNearby = () => AddToGroup(g.Name, nearby, reopen),
                Delete = () =>
                {
                    GroupStore.Delete(g.Name);
                    reopen();
                },
            });
        }

        return model;
    }

    /// <summary>Returns an error message to show under the name field, or null when it saved.</summary>
    static string? Save(string name, List<GroupMember> people, Action reopen)
    {
        if (string.IsNullOrWhiteSpace(name))
            return "Give the group a name first.";
        if (people.Count == 0)
            return "Nobody to save.";
        // Saving under a name that already exists adds to that group instead of replacing it.
        var added = GroupStore.AddMembers(name, people);
        if (added < 0)
            return "Could not write the groups file.";

        AccessFeedback.ShowRaw(Player.m_localPlayer,
            added == 0 ? name.Trim() + " already has everyone." : "Added " + added + " to " + name.Trim() + ".");
        reopen();
        return null;
    }

    /// <summary>Adds the given people to an existing group and reports what happened.</summary>
    static void AddToGroup(string group, List<GroupMember> people, Action reopen)
    {
        var added = GroupStore.AddMembers(group, people);
        AccessFeedback.ShowRaw(Player.m_localPlayer,
            added < 0 ? "Could not write the groups file."
            : added == 0 ? group + " already has everyone."
            : "Added " + added + " to " + group + ".");
        reopen();
    }

    /// <summary>
    /// Adds everyone in the group to this piece, online or not. A person saved with an id is added straight away; a bare name from a
    /// hand-edited file can only be matched while that player is nearby.
    /// </summary>
    static void AddGroup(ZNetView nview, Player player, PlayerGroup group, string noun, Action reopen)
    {
        var here = Player.GetAllPlayers();
        var people = new List<PieceGuest>();
        var needId = new List<string>();
        foreach (var member in group.Members)
        {
            if (member.Id != 0L)
            {
                people.Add(new PieceGuest(member.Id, member.Name));
                continue;
            }

            var match = here.FirstOrDefault(p => p && string.Equals(p.GetPlayerName(), member.Name, StringComparison.OrdinalIgnoreCase));
            if (match == null)
                needId.Add(member.Name);
            else
                people.Add(new PieceGuest(match.GetPlayerID(), match.GetPlayerName()));
        }

        var fresh = people.Count(p => !PieceGuestAccess.IsGuest(nview, p.PlayerId));
        if (people.Count > 0)
        {
            var me = player.GetPlayerID();
            PieceGuestAccess.RequestMergeGuests(nview, people, me, reopen);

            // The group's permissions go to everyone it adds. Anything they already had is kept.
            if (group.Flags != 0)
            {
                var grants = people.Select(p =>
                    new KeyValuePair<long, int>(p.PlayerId, GuestPerms.FlagsOf(nview, p.PlayerId) | group.Flags));
                GuestPerms.Request(nview, grants, me);
            }
        }

        var note = people.Count == 0
            ? "Nobody in " + group.Name + " could be added."
            : fresh == 0
                ? "Everyone in " + group.Name + " is already on this " + noun + "."
                : "Added " + fresh + " from " + group.Name + " to this " + noun + ".";
        if (needId.Count > 0)
            note += " No saved id for " + string.Join(", ", needId) + ": add them to the group once while they're nearby.";
        AccessFeedback.ShowRaw(Player.m_localPlayer, note);
        if (people.Count == 0)
            reopen();
    }

    static string OwnerName(PrivateArea area)
    {
        var creator = WardService.CreatorOf(area);
        var local = Player.m_localPlayer;
        if (local != null && local.GetPlayerID() == creator)
            return local.GetPlayerName();
        var name = area.GetComponent<ZNetView>()?.GetZDO()?.GetString("creatorName", string.Empty);
        return string.IsNullOrEmpty(name) ? "Ward owner" : name!;
    }
}

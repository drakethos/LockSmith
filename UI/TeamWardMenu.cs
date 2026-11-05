using System;
using DrakeModsLibs.UI.Toolkit;
using LockSmith.Compat.ServerTeams;
using UnityEngine.UIElements;

namespace LockSmith.UI;

/// <summary>
/// The Team Ward's body inside the one Lock menu. LockSmith treats the piece as team-managed: its own switches stay
/// off, and only a server admin can change what the ward protects.
/// </summary>
internal static class TeamWardMenu
{
    public static void FillTeam(VisualElement body, PrivateArea area)
    {
        if (!area)
        {
            body.Add(UkControls.Text("That ward is gone.", 14, UkTheme.TextMuted));
            return;
        }

        var admin = TeamWardBridge.LocalIsAdmin();
        body.Add(UkControls.Text("Team managed", 18, UkTheme.Text, heading: true));
        var name = UkControls.Text(TeamWardBridge.TeamName(area), 16, UkTheme.TextMuted);
        name.style.marginTop = 4;
        name.style.marginBottom = 10;
        body.Add(name);

        if (admin)
            AddTeamButtons(body, area);
        else
        {
            var locked = UkControls.Text("Only admins can change a Team Ward.", 14, UkTheme.TextMuted);
            locked.style.marginBottom = 10;
            body.Add(locked);
        }

        AddProtect(body, area, admin, "Build", "build", "Protect build", "Outsiders cannot build here.");
        AddProtect(body, area, admin, "Chest", "chests", "Protect chests", "Outsiders cannot open chests here.");
        AddProtect(body, area, admin, "Door", "doors", "Protect doors", "Outsiders cannot use doors here.");
    }

    public static void FillLock(VisualElement body)
    {
        body.Add(UkControls.Text("Team managed", 18, UkTheme.Text, heading: true));
        var note = UkControls.Text(
            "Only admins can change who this ward covers. Team members are covered by it and cannot reconfigure it.",
            14, UkTheme.TextMuted);
        note.style.marginTop = 6;
        note.style.marginBottom = 12;
        body.Add(note);

        AddLocked(body, "Ward on", "A Team Ward stays on.", on: true);
        AddLocked(body, "Public", "A Team Ward is not a public piece.", on: false);
        AddLocked(body, "Join", "People do not join a Team Ward. They are on the team or they are not.", on: false);

        var who = UkControls.Text("Who can change this ward: server admins.", 14, UkTheme.Text);
        who.style.marginTop = 8;
        body.Add(who);
    }

    static void AddTeamButtons(VisualElement body, PrivateArea area)
    {
        var teams = TeamWardBridge.Teams();
        if (teams.Count == 0)
            return;

        var current = TeamWardBridge.TeamId(area);
        var row = new VisualElement().Row();
        row.style.flexWrap = Wrap.Wrap;
        row.style.marginBottom = 10;
        foreach (var team in teams)
        {
            var captured = team;
            var selected = string.Equals(captured.id, current, StringComparison.OrdinalIgnoreCase);
            var button = UkControls.MakeButton(captured.name, () =>
            {
                TeamWardBridge.RunAdmin("ward team " + captured.id);
                PieceAccessMenu.RefreshTeamWard();
            }, selected ? UkButtonKind.Selected : UkButtonKind.Ghost);
            button.style.marginRight = 6;
            button.style.marginBottom = 6;
            row.Add(button);
        }

        var none = UkControls.MakeButton("None", () =>
        {
            TeamWardBridge.RunAdmin("ward team none");
            PieceAccessMenu.RefreshTeamWard();
        }, string.IsNullOrEmpty(current) ? UkButtonKind.Selected : UkButtonKind.Ghost);
        none.style.marginBottom = 6;
        row.Add(none);
        body.Add(row);
    }

    static void AddProtect(VisualElement body, PrivateArea area, bool admin, string kind, string command, string title, string hint)
    {
        var on = TeamWardBridge.Protects(area, kind);
        var sw = new UkSwitch(title, admin ? hint : "Only admins can change a Team Ward.");
        sw.SetValue(on, notify: false);
        sw.SetEnabled(admin);
        sw.Root.style.marginBottom = 8;
        sw.Changed = value =>
        {
            if (!TeamWardBridge.LocalIsAdmin())
            {
                sw.SetValue(TeamWardBridge.Protects(area, kind), notify: false);
                return;
            }

            TeamWardBridge.RunAdmin("ward " + command + (value ? " on" : " off"));
            PieceAccessMenu.RefreshTeamWard();
        };
        body.Add(sw.Root);
    }

    static void AddLocked(VisualElement body, string title, string hint, bool on)
    {
        var sw = new UkSwitch(title, hint);
        sw.SetValue(on, notify: false);
        sw.SetEnabled(false);
        sw.Root.style.marginBottom = 8;
        body.Add(sw.Root);
    }
}

using System;
using System.Collections.Generic;
using DrakeModsLibs.Integration;
using DrakeModsLibs.UI;
using DrakeModsLibs.UI.Toolkit;
using LockSmith.Access;
using UnityEngine;
using UnityEngine.UIElements;

namespace LockSmith.UI;

/// <summary>What a Piece-tab action is for, so the window can lay it out the same way the other mods do.</summary>
public enum LockMenuKind
{
    Button,
    Remove,
    Nearby,
    Join,
    Public,
    /// <summary>Server-team access switch (DrakesServerTeams). Tag Flags: 1 = on.</summary>
    Team,
    Leave,
    Clear,
    Copy,
    Paste,
}

/// <summary>Tag on an action: its role, plus the question and commit step when it needs an inline "are you sure?".</summary>
public sealed class LockMenuTag
{
    public LockMenuKind Kind;
    public string? Ask;
    public string? ConfirmText;
    public Action? Commit;

    /// <summary>For a person chip: who it is, what they may do now, and how to change it (null when the viewer can't).</summary>
    public long PersonId;
    public int Flags;
    public Action<int>? SetFlags;
}

public static class LockMenuKinds
{
    static readonly Dictionary<DrakeMenuAction, LockMenuTag> Tags = new();

    public static DrakeMenuAction Tag(DrakeMenuAction action, LockMenuKind kind, string? ask = null, string? confirmText = null, Action? commit = null,
        long personId = 0L, int flags = 0, Action<int>? setFlags = null)
    {
        Tags[action] = new LockMenuTag
        {
            Kind = kind, Ask = ask, ConfirmText = confirmText, Commit = commit,
            PersonId = personId, Flags = flags, SetFlags = setFlags,
        };
        return action;
    }

    public static void Forget() => Tags.Clear();

    public static LockMenuTag TagOf(DrakeMenuAction action) =>
        Tags.TryGetValue(action, out var tag) ? tag : new LockMenuTag { Kind = LockMenuKind.Button };
}

public enum LockTab
{
    Piece,
    Ward,
    Groups,
    /// <summary>A tab another mod added (see <see cref="DrakeModsLibs.Integration.PieceMenuTabs"/>).</summary>
    Extra,
}

/// <summary>
/// Which tabs exist and which one is lit. Smart groups: roots always; Ward/Groups only while on Lock
/// (and when ShowWard / ShowGroups are true — Ward membership / guest manage + LockSmith enabled).
/// </summary>
public sealed class LockTabs
{
    public bool ShowWard;
    public bool ShowGroups;
    /// <summary>Ward is off: show only the Ward tab, no tab row.</summary>
    public bool OnlyWard;
    public LockTab Active;
    public Action<LockTab> Select = _ => { };
    /// <summary>Tabs other mods registered for this chest, and which of them is lit when <see cref="Active"/> is Extra.</summary>
    public List<PieceMenuTab> Extras = new();
    public string? ActiveExtraId;
    public Action<PieceMenuTab> SelectExtra = _ => { };
}

public sealed class LockWardPerson
{
    public string Name = "";
    public string Role = "";
    public bool CanRemove;
    public Action Remove = () => { };
    public string? RoleLabel;
    public Action? ToggleRole;
    public string? GrantLabel;
    public Action? ToggleGrant;
}

public sealed class LockWardModel
{
    public string OwnerName = "";
    public bool On;
    /// <summary>Ward is off: show only the switch to turn it back on.</summary>
    public bool SwitchOnly;
    public bool CanSwitch;
    public Action<bool> OnSwitch = _ => { };
    public string? Note;
    public int SwitchMin;
    public bool CanSetSwitchMin;
    public Action<int> SetSwitchMin = _ => { };
    public List<LockWardPerson> People = new();
}

public sealed class LockGroupItem
{
    public string Name = "";
    public List<string> Members = new();
    public Action Add = () => { };
    public Action Delete = () => { };
    public bool CanAddGuests;
    public bool CanAddNearby;
    public Action AddGuests = () => { };
    public Action AddNearby = () => { };
    public int Flags;
    public Action<int> SetFlags = _ => { };
}

public sealed class LockGroupsModel
{
    public List<LockGroupItem> Groups = new();
    public bool CanSaveGuests;
    public bool CanSaveNearby;
    public string Noun = "piece";
    public Func<string, string?> SaveGuests = _ => null;
    public Func<string, string?> SaveNearby = _ => null;
    public string FilePath = "";
}

/// <summary>
/// The Lock window, built on the same shared shell as the other mods: header with a cog, a tab row, card sections, inline confirms,
/// switches and a footer. Piece, Ward and Groups are tabs in one window.
/// </summary>
public sealed class LockMenuView
{
    readonly UkWindow _window;
    readonly VisualElement _tabRow;
    readonly UkPopover _cog;
    Action? _onClosed;
    long _openPerson;
    long _permsShownFor;

    public LockMenuView()
    {
        _window = new UkWindow("locksmith_lock_menu", 520f);
        _window.Closed = () =>
        {
            var closed = _onClosed;
            _onClosed = null;
            closed?.Invoke();
        };

        _tabRow = new VisualElement().Row().Pad(10, 22).BorderBottom(UkTheme.LineSoft);
        _tabRow.Show(false);
        _window.Card.Insert(1, _tabRow);

        _cog = new UkPopover(_window.Card, 320f);
        BuildCogMenu();
        var cogButton = UkControls.MakeIconButton("", () => { }, "Settings (this device)", UkButtonKind.Secondary);
        cogButton.clicked += () => _cog.Toggle(cogButton);
        cogButton.Add(UkIcons.Gear(22f, UkTheme.TextMuted));
        _window.HeaderActions.Add(cogButton);
        _window.EscapeHandler = () =>
        {
            if (!_cog.IsOpen)
                return false;
            _cog.Close();
            return true;
        };
    }

    public bool IsOpen => _window.IsOpen;

    public void CloseSilent() => _window.CloseSilent();

    public void Close() => _window.Close();

    // ---- settings (this device only) ---------------------------------------------------------

    void BuildCogMenu()
    {
        _cog.Content.Add(Caption("Settings · this device only"));
        var nearby = new UkSwitch("Nearby list", "Show players within 5m on the Piece tab.");
        nearby.SetValue(LockSmithConfig.ShowNearbyList, notify: false);
        nearby.Changed = on => LockSmithConfig.SetShowNearbyList(on);
        _cog.Content.Add(nearby.Root);
        var wheel = new UkSwitch("Wheel", "Hold the Lock key on a chest or door for the wheel. A tap opens this window.");
        wheel.SetValue(LockSmithConfig.UseWheel, notify: false);
        wheel.Changed = on => LockSmithConfig.SetUseWheel(on);
        wheel.Root.style.marginTop = 8;
        _cog.Content.Add(wheel.Root);
        var note = UkControls.Text("Never synced. Server rules stay with the owner.", 13, UkTheme.TextMuted);
        note.style.marginTop = 8;
        _cog.Content.Add(note);
    }

    // ---- frame ----------------------------------------------------------------------------------

    void Frame(string title, string? subtitle, LockTabs tabs, Action? onClosed)
    {
        _onClosed = onClosed;
        _cog.Close();
        _window.SetHeader(title ?? "", subtitle);
        _window.HideTabs();

        _tabRow.Clear();
        var activeGroup = ActiveGroup(tabs);
        var visibleExtras = VisibleExtras(tabs, activeGroup);
        var inLock = activeGroup == PieceMenuTabs.LockSmithGroup;
        var showWard = inLock && tabs.ShowWard;
        var showGroups = inLock && tabs.ShowGroups;
        var count = 1 + (showWard ? 1 : 0) + (showGroups ? 1 : 0) + visibleExtras.Count;
        _tabRow.Show(count > 1 && !tabs.OnlyWard);
        _window.Card.style.width = WidthFor(visibleExtras, tabs.Extras.Count > 0, showWard, showGroups, tabs.OnlyWard);
        // Smart tabs: always show group roots; expand children only for the active group (teal child chrome).
        if (!tabs.OnlyWard)
        {
            foreach (var extra in visibleExtras)
            {
                var captured = extra;
                var isChild = !extra.IsRoot;
                var active = tabs.Active == LockTab.Extra && tabs.ActiveExtraId == extra.Id;
                var kind = isChild
                    ? (active ? UkButtonKind.ChildSelected : UkButtonKind.Child)
                    : (active ? UkButtonKind.Selected : UkButtonKind.Ghost);
                var button = UkControls.MakeButton(extra.Title, () => tabs.SelectExtra(captured), kind);
                button.style.height = 40;
                button.style.marginRight = 6;
                _tabRow.Add(button);
            }
        }
        if (!tabs.OnlyWard)
            AddTab(tabs, LockTab.Piece, tabs.Extras.Count > 0 ? "Lock" : "Piece", child: false);
        if (showWard)
            AddTab(tabs, LockTab.Ward, "Ward", child: true);
        if (showGroups)
            AddTab(tabs, LockTab.Groups, "Groups", child: true);

        _window.Body.Clear();
        _window.Footer.Clear();
    }

    const float BaseWidth = 520f;
    const float MaxWidth = 900f;

    /// <summary>Which smart-tab group is open: an extra's <see cref="PieceMenuTab.Group"/>, or LockSmith's own tabs.</summary>
    static string ActiveGroup(LockTabs tabs)
    {
        if (tabs.Active == LockTab.Extra && !string.IsNullOrEmpty(tabs.ActiveExtraId))
        {
            var extra = tabs.Extras.Find(e => e.Id == tabs.ActiveExtraId);
            if (extra != null)
                return extra.Group;
        }

        return PieceMenuTabs.LockSmithGroup;
    }

    /// <summary>Roots always; children only while their group is active.</summary>
    static List<PieceMenuTab> VisibleExtras(LockTabs tabs, string activeGroup)
    {
        var visible = new List<PieceMenuTab>();
        foreach (var extra in tabs.Extras)
        {
            if (extra.IsRoot || extra.Group == activeGroup)
                visible.Add(extra);
        }

        return visible;
    }

    /// <summary>
    /// Wide enough for the visible tab row in one line. Estimated from label length at the tab font
    /// (16 px heading, 18 px padding each side).
    /// </summary>
    static float WidthFor(List<PieceMenuTab> visibleExtras, bool hasExtras, bool showWard, bool showGroups, bool onlyWard)
    {
        if (onlyWard)
            return BaseWidth;
        var labels = new List<string>();
        labels.AddRange(visibleExtras.ConvertAll(e => e.Title));
        labels.Add(hasExtras ? "Lock" : "Piece");
        if (showWard)
            labels.Add("Ward");
        if (showGroups)
            labels.Add("Groups");
        var row = 44f;
        foreach (var label in labels)
            row += 40f + 11f * label.Length + 6f;
        return Mathf.Clamp(row, BaseWidth, MaxWidth);
    }

    void AddTab(LockTabs tabs, LockTab tab, string label, bool child)
    {
        var active = tabs.Active == tab;
        var kind = child
            ? (active ? UkButtonKind.ChildSelected : UkButtonKind.Child)
            : (active ? UkButtonKind.Selected : UkButtonKind.Ghost);
        var button = UkControls.MakeButton(label, () => tabs.Select(tab), kind);
        button.style.height = 40;
        button.style.marginRight = 6;
        _tabRow.Add(button);
    }

    void Finish(VisualElement body, VisualElement? footerLeft)
    {
        _window.Body.Add(body);
        _window.Footer.Clear();
        if (footerLeft != null)
            _window.Footer.Add(footerLeft);
        _window.Footer.Add(UkControls.Spacer());
        _window.Footer.Add(UkControls.MakeButton("Close", () => _window.Close(), UkButtonKind.Secondary));
        _window.ShowFooter(true);
        _window.Open();
    }

    // ---- Piece tab --------------------------------------------------------------------------------

    public void OpenPiece(string title, string? subtitle, IReadOnlyList<DrakeMenuAction> actions, bool joinOn, bool publicOn,
        LockTabs tabs, Action? onClosed)
    {
        Frame(title, subtitle, tabs, onClosed);

        var chips = new List<DrakeMenuAction>();
        var nearby = new List<DrakeMenuAction>();
        var buttons = new List<DrakeMenuAction>();
        DrakeMenuAction? join = null, pub = null, team = null, leave = null, clear = null, copy = null, paste = null;
        foreach (var action in actions)
        {
            switch (LockMenuKinds.TagOf(action).Kind)
            {
                case LockMenuKind.Remove: chips.Add(action); break;
                case LockMenuKind.Nearby: nearby.Add(action); break;
                case LockMenuKind.Join: join = action; break;
                case LockMenuKind.Public: pub = action; break;
                case LockMenuKind.Team: team = action; break;
                case LockMenuKind.Leave: leave = action; break;
                case LockMenuKind.Clear: clear = action; break;
                case LockMenuKind.Copy: copy = action; break;
                case LockMenuKind.Paste: paste = action; break;
                default: buttons.Add(action); break;
            }
        }

        var body = new VisualElement().Column();

        // Access: public + join, as switches.
        if (pub != null || join != null || team != null)
        {
            var access = SectionCard("Access");
            if (pub != null)
            {
                var sw = new UkSwitch("Public", "Anyone can open it. Nobody has to be on the list.");
                sw.SetValue(publicOn, notify: false);
                sw.SetEnabled(pub.Enabled);
                var action = pub;
                sw.Changed = _ => Run(action);
                access.Add(sw.Root);
            }

            if (join != null)
            {
                var sw = new UkSwitch("Join open", "Anyone nearby can join while this is on.");
                sw.SetValue(joinOn, notify: false);
                sw.SetEnabled(join.Enabled);
                var action = join;
                sw.Changed = _ => Run(action);
                sw.Root.style.marginTop = pub != null ? 8 : 0;
                access.Add(sw.Root);
            }

            if (team != null)
            {
                var name = Access.TeamAccess.LocalTeamName();
                var sw = new UkSwitch("Team",
                    name != null ? "Everyone on your server team (" + name + ") can use it." : "Everyone on the same server team as whoever built it can use it.");
                sw.SetValue(LockMenuKinds.TagOf(team).Flags == 1, notify: false);
                sw.SetEnabled(team.Enabled);
                var action = team;
                sw.Changed = _ => Run(action);
                sw.Root.style.marginTop = pub != null || join != null ? 8 : 0;
                access.Add(sw.Root);
            }

            body.Add(access);
        }

        // People on this piece.
        var people = SectionCard("On this " + (chips.Count == 1 ? "piece · 1 person" : "piece · " + chips.Count + " people"));
        if (chips.Count == 0)
        {
            people.Add(UkControls.Text("Nobody else yet. Open Join, or add someone nearby.", 14, UkTheme.TextMuted));
        }
        else
        {
            var row = new VisualElement().Row();
            row.style.flexWrap = Wrap.Wrap;
            var ask = new VisualElement().Column();
            ask.Show(false);
            var perms = new VisualElement().Column();
            perms.Show(false);
            foreach (var chip in chips)
                row.Add(PersonChip(chip, ask, perms));
            people.Add(row);
            people.Add(perms);
            people.Add(ask);

            // Keep a person's permissions open across refreshes (and open one straight away when the wheel sent us here).
            if (_openPerson != 0L)
            {
                var open = chips.Find(c => LockMenuKinds.TagOf(c).PersonId == _openPerson);
                if (open != null)
                    ShowPerms(perms, open);
                else
                    _openPerson = 0L;
            }
        }

        // Copy the people on this piece, paste them onto another.
        if (copy != null || paste != null)
        {
            var copyRow = new VisualElement().Row();
            copyRow.style.marginTop = 4;
            if (copy != null)
            {
                var action = copy;
                var button = UkControls.MakeButton(action.Label, () => Run(action), UkButtonKind.Secondary);
                button.style.height = 34;
                button.style.marginRight = 8;
                UkControls.SetButtonEnabled(button, action.Enabled);
                copyRow.Add(button);
            }

            if (paste != null)
            {
                var action = paste;
                var button = UkControls.MakeButton(action.Label, () => Run(action), UkButtonKind.Secondary);
                button.style.height = 34;
                UkControls.SetButtonEnabled(button, action.Enabled);
                copyRow.Add(button);
            }

            people.Add(copyRow);
        }

        body.Add(people);

        // Nearby.
        if (nearby.Count > 0 && LockSmithConfig.ShowNearbyList)
        {
            var near = SectionCard("Nearby");
            foreach (var action in nearby)
                near.Add(NearbyRow(action));
            body.Add(near);
        }

        // Anything else the player can do here.
        foreach (var button in buttons)
        {
            var b = button;
            var ui = UkControls.MakeButton(b.Label, () => Run(b), UkButtonKind.Primary);
            ui.style.height = 44;
            ui.style.marginBottom = 8;
            UkControls.SetButtonEnabled(ui, b.Enabled);
            body.Add(ui);
        }

        VisualElement? footerLeft = null;
        var danger = clear ?? leave;
        if (danger != null)
        {
            var tag = LockMenuKinds.TagOf(danger);
            var commit = tag.Commit ?? (() => Run(danger));
            footerLeft = new UkConfirm(danger.Label.TrimEnd('…', '.'), tag.Ask ?? "Are you sure?", tag.ConfirmText ?? "Yes", commit).Root;
        }

        Finish(body, footerLeft);
    }

    /// <summary>A person chip ("Mira ×"). × opens an inline "Remove Mira?" row under the chips.</summary>
    VisualElement PersonChip(DrakeMenuAction chip, VisualElement askHost, VisualElement permsHost)
    {
        var tag = LockMenuKinds.TagOf(chip);
        var name = NameOf(chip);
        var pill = new VisualElement().Row().Fill(UkTheme.Raised).Round(18).Border(UkTheme.Line).Pad(3, 4);
        pill.style.marginRight = 8;
        pill.style.marginBottom = 8;
        pill.Add(Avatar(name, 28, UkTheme.Accent));
        // The name is a button: click it for what this person can do.
        var label = UkControls.MakeButton(name, () => ShowPerms(permsHost, chip), UkButtonKind.Ghost);
        label.style.height = 28;
        label.style.marginLeft = 4;
        label.style.marginRight = 2;
        label.style.fontSize = 15;
        pill.Add(label);
        var x = UkControls.MakeIconButton("×", () => ShowAsk(askHost, tag.Ask ?? ("Remove " + name + "?"), tag.ConfirmText ?? "Remove", tag.Commit ?? (() => Run(chip))),
            "Remove " + name, UkButtonKind.Ghost);
        x.style.width = 28;
        x.style.height = 28;
        UkControls.SetButtonEnabled(x, chip.Enabled);
        pill.Add(x);
        return pill;
    }

    static string NameOf(DrakeMenuAction chip) =>
        chip.Label.StartsWith("Remove ", StringComparison.Ordinal) ? chip.Label.Substring(7) : chip.Label;

    /// <summary>Open this person's permissions the next time the Piece tab draws (the wheel uses this).</summary>
    public void FocusPerson(long personId) => _openPerson = personId;

    /// <summary>
    /// A person's card: guest by default, with two things you can allow. Changes apply straight away.
    /// Clicking the same name again, or Done, closes it.
    /// </summary>
    void ShowPerms(VisualElement host, DrakeMenuAction chip)
    {
        var tag = LockMenuKinds.TagOf(chip);
        var name = NameOf(chip);
        if (_openPerson == tag.PersonId && host.style.display == DisplayStyle.Flex && host.childCount > 0 && _permsShownFor == tag.PersonId)
        {
            host.Clear();
            host.Show(false);
            _openPerson = 0L;
            _permsShownFor = 0L;
            return;
        }

        _openPerson = tag.PersonId;
        _permsShownFor = tag.PersonId;
        host.Clear();
        host.Show(true);

        var current = tag.Flags;
        var card = new VisualElement().Column().Fill(UkTheme.PanelDeep).Round(UkTheme.Radius).Border(UkTheme.Line).Pad(12);
        card.style.marginTop = 2;
        card.style.marginBottom = 8;
        card.Add(UkControls.Text(name + " · what they can do", 14, UkTheme.Gold, heading: true, wrap: false));
        var plain = UkControls.Text("A guest can open and use it. Allow more below.", 13, UkTheme.TextMuted);
        plain.style.marginTop = 4;
        plain.style.marginBottom = 8;
        card.Add(plain);

        var add = new UkSwitch("Let others on", "Add people, open Join, add nearby players.");
        add.SetValue((current & (int)GuestPerm.Add) != 0, notify: false);
        add.Changed = on =>
        {
            current = on ? current | (int)GuestPerm.Add : current & ~(int)GuestPerm.Add;
            tag.SetFlags?.Invoke(current);
        };
        card.Add(add.Root);

        var toggle = new UkSwitch("Switch public / private", "Open it to everyone or lock it again.");
        toggle.SetValue((current & (int)GuestPerm.Toggle) != 0, notify: false);
        toggle.Changed = on =>
        {
            current = on ? current | (int)GuestPerm.Toggle : current & ~(int)GuestPerm.Toggle;
            tag.SetFlags?.Invoke(current);
        };
        toggle.Root.style.marginTop = 8;
        card.Add(toggle.Root);

        var done = UkControls.MakeButton("Done", () =>
        {
            host.Clear();
            host.Show(false);
            _openPerson = 0L;
            _permsShownFor = 0L;
        }, UkButtonKind.Secondary);
        done.style.height = 34;
        done.style.marginTop = 10;
        done.style.alignSelf = Align.FlexStart;
        card.Add(done);
        host.Add(card);
    }

    VisualElement NearbyRow(DrakeMenuAction action)
    {
        var tag = LockMenuKinds.TagOf(action);
        var row = new VisualElement().Row().Pad(4, 0);
        var name = action.Label.StartsWith("Add ", StringComparison.Ordinal) ? action.Label.Substring(4) : action.Label;
        if (!action.Enabled)
        {
            row.Add(UkControls.Text("Nobody nearby right now.", 14, UkTheme.TextMuted));
            return row;
        }

        row.Add(Avatar(name, 32, UkTheme.Line));
        var label = UkControls.Text(name, 15, UkTheme.Text, wrap: false);
        label.style.marginLeft = 10;
        label.style.flexGrow = 1;
        row.Add(label);
        var add = UkControls.MakeButton("Add", () => Run(action), UkButtonKind.Primary);
        add.style.height = 34;
        row.Add(add);
        return row;
    }

    // ---- Ward tab ---------------------------------------------------------------------------------

    public void OpenWard(string title, string? subtitle, LockWardModel model, LockTabs tabs, Action? onClosed)
    {
        Frame(title, subtitle, tabs, onClosed);
        var body = new VisualElement().Column();

        var state = SectionCard("Ward");
        var sw = new UkSwitch("Ward on", model.On ? "The ward is protecting this area." : "Anyone can build and open things here.");
        sw.SetValue(model.On, notify: false);
        sw.SetEnabled(model.CanSwitch, model.CanSwitch ? null : "Your role can't switch this ward.");
        sw.Changed = on => model.OnSwitch(on);
        state.Add(sw.Root);
        if (!string.IsNullOrEmpty(model.Note))
        {
            var note = new VisualElement().Column().Fill(UkTheme.DangerBg).Round(UkTheme.Radius).Border(UkTheme.DangerLine).Pad(10, 12);
            note.style.marginTop = 10;
            note.Add(UkControls.Text(model.Note!, 14, UkTheme.DangerText));
            state.Add(note);
        }

        body.Add(state);

        if (model.SwitchOnly)
        {
            Finish(body, null);
            return;
        }

        var who = SectionCard("Who can switch it");
        var seg = new UkSegmented("Owner only", "Owner and members", "Everyone on the ward");
        seg.SetIndex(Mathf.Clamp(3 - model.SwitchMin, 0, 2));
        seg.Changed = i => { if (model.CanSetSwitchMin) model.SetSwitchMin(3 - i); };
        seg.Root.SetEnabled(model.CanSetSwitchMin);
        who.Add(seg.Root);
        body.Add(who);

        var people = SectionCard("On this ward · " + (model.People.Count + 1));
        var ask = new VisualElement().Column();
        ask.Show(false);
        people.Add(WardRow(model.OwnerName, "Owner", null, ask, UkTheme.Gold));
        foreach (var person in model.People)
            people.Add(WardRow(person.Name, person.Role, person, ask, UkTheme.Accent));
        people.Add(ask);
        body.Add(people);

        Finish(body, null);
    }

    VisualElement WardRow(string name, string role, LockWardPerson? person, VisualElement askHost, Color ring)
    {
        var row = new VisualElement().Row().Pad(5, 0);
        row.Add(Avatar(name, 32, ring));
        var texts = new VisualElement().Column().Grow();
        texts.style.marginLeft = 10;
        texts.Add(UkControls.Text(string.IsNullOrEmpty(name) ? "Unknown" : name, 15, UkTheme.Text, wrap: false));
        texts.Add(UkControls.Text(role, 12, UkTheme.TextMuted, wrap: false));
        row.Add(texts);

        if (person != null)
        {
            if (person.ToggleRole != null)
                row.Add(SmallButton(person.RoleLabel ?? "Role", person.ToggleRole));
            if (person.ToggleGrant != null)
                row.Add(SmallButton(person.GrantLabel ?? "Rights", person.ToggleGrant));
            var x = UkControls.MakeIconButton("×", () => ShowAsk(askHost, "Take " + name + " off the ward?", "Remove", person.Remove),
                "Remove " + name, UkButtonKind.Ghost);
            x.style.width = 32;
            x.style.height = 32;
            x.style.marginLeft = 6;
            UkControls.SetButtonEnabled(x, person.CanRemove);
            row.Add(x);
        }

        return row;
    }

    // ---- Groups tab -------------------------------------------------------------------------------

    /// <summary>A tab another mod added: it fills the body, the window supplies the frame, tabs and Close.</summary>
    public void OpenExtra(string title, string? subtitle, PieceMenuTab tab, Container piece, bool canManage, Action rebuild, LockTabs tabs, Action? onClosed)
    {
        Frame(title, subtitle, tabs, onClosed);
        var body = new VisualElement().Column();
        try
        {
            tab.Build(new PieceMenuTabContext(piece, body, canManage, rebuild));
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Lock menu tab '{tab.Id}' failed: {ex}");
            body.Add(UkControls.Text("This tab could not be drawn.", 14, UkTheme.TextMuted));
        }

        Finish(body, null);
    }

    public void OpenGroups(string title, string? subtitle, LockGroupsModel model, LockTabs tabs, Action? onClosed)
    {
        Frame(title, subtitle, tabs, onClosed);
        var body = new VisualElement().Column();

        if (model.Groups.Count == 0)
        {
            var empty = SectionCard("Your groups");
            empty.Add(UkControls.Text("No groups yet. Save the people here as a group below.", 14, UkTheme.TextMuted));
            body.Add(empty);
        }

        foreach (var group in model.Groups)
        {
            var card = SectionCard(group.Name + " · " + group.Members.Count);
            card.Add(UkControls.Text(string.Join(", ", group.Members), 14, UkTheme.TextBody));

            // What these people get when the group is added to a piece.
            var current = group.Flags;
            var allow = new VisualElement().Column();
            allow.style.marginTop = 10;
            allow.Add(Caption("When added to a piece"));
            var allowAdd = new UkSwitch("Let others on", "Add people, open Join, add nearby players.");
            allowAdd.SetValue((current & (int)GuestPerm.Add) != 0, notify: false);
            allowAdd.Changed = on =>
            {
                current = on ? current | (int)GuestPerm.Add : current & ~(int)GuestPerm.Add;
                group.SetFlags(current);
            };
            allow.Add(allowAdd.Root);
            var allowToggle = new UkSwitch("Switch public / private", "Open it to everyone or lock it again.");
            allowToggle.SetValue((current & (int)GuestPerm.Toggle) != 0, notify: false);
            allowToggle.Changed = on =>
            {
                current = on ? current | (int)GuestPerm.Toggle : current & ~(int)GuestPerm.Toggle;
                group.SetFlags(current);
            };
            allowToggle.Root.style.marginTop = 8;
            allow.Add(allowToggle.Root);
            card.Add(allow);

            // The row wraps, so a button never gets squeezed into a column of letters.
            var actions = new VisualElement().Row();
            actions.style.flexWrap = Wrap.Wrap;
            actions.style.marginTop = 12;
            Action<VisualElement> place = element =>
            {
                element.style.flexShrink = 0;
                element.style.marginRight = 8;
                element.style.marginBottom = 8;
                actions.Add(element);
            };

            var add = UkControls.MakeButton("Add to this " + model.Noun, group.Add, UkButtonKind.Primary);
            add.style.height = 36;
            place(add);
            var addGuests = UkControls.MakeButton("+ Guests here", group.AddGuests, UkButtonKind.Secondary);
            addGuests.style.height = 36;
            UkControls.SetButtonEnabled(addGuests, group.CanAddGuests);
            place(addGuests);
            var addNearby = UkControls.MakeButton("+ Nearby", group.AddNearby, UkButtonKind.Secondary);
            addNearby.style.height = 36;
            UkControls.SetButtonEnabled(addNearby, group.CanAddNearby);
            place(addNearby);
            place(new UkConfirm("Delete", "Delete " + group.Name + "?", "Delete", group.Delete).Root);
            card.Add(actions);
            body.Add(card);
        }

        var save = SectionCard("Save a group");
        var field = new UkField("Group name", multiline: false, tips: _window.Tips);
        field.SetMax(32);
        save.Add(field.Root);
        var error = UkControls.Text("", 13, UkTheme.DangerText);
        error.Show(false);
        error.style.marginTop = 6;
        var row = new VisualElement().Row();
        row.style.marginTop = 10;
        var guests = UkControls.MakeButton("Save current guests", () => SaveVia(field, error, model.SaveGuests), UkButtonKind.Secondary);
        guests.style.height = 36;
        guests.style.marginRight = 8;
        UkControls.SetButtonEnabled(guests, model.CanSaveGuests);
        var nearby = UkControls.MakeButton("Save people nearby", () => SaveVia(field, error, model.SaveNearby), UkButtonKind.Secondary);
        nearby.style.height = 36;
        UkControls.SetButtonEnabled(nearby, model.CanSaveNearby);
        row.Add(guests);
        row.Add(nearby);
        save.Add(row);
        save.Add(error);
        var path = UkControls.Text("Saved on this computer: " + model.FilePath, 12, UkTheme.TextMuted);
        path.style.marginTop = 10;
        save.Add(path);
        body.Add(save);

        Finish(body, null);
    }

    static void SaveVia(UkField field, Label error, Func<string, string?> save)
    {
        var message = save(field.Value);
        error.text = message ?? "";
        error.Show(!string.IsNullOrEmpty(message));
        if (message == null)
            field.SetValue("");
    }

    // ---- pieces -----------------------------------------------------------------------------------

    static VisualElement SectionCard(string caption)
    {
        var card = new VisualElement().Column().Fill(UkTheme.Raised).Round(UkTheme.Radius + 2).Pad(14);
        card.style.marginBottom = 12;
        card.Add(Caption(caption));
        return card;
    }

    static Label Caption(string text)
    {
        var label = UkControls.Text(text.ToUpperInvariant(), 12, UkTheme.TextMuted, wrap: false);
        label.style.marginBottom = 8;
        return label;
    }

    static VisualElement Avatar(string name, float size, Color ring)
    {
        var circle = new VisualElement().Fill(UkTheme.Active).Round(size / 2f).Border(ring, 2f).Size(size, size).Fixed();
        circle.style.alignItems = Align.Center;
        circle.style.justifyContent = Justify.Center;
        var initials = string.IsNullOrWhiteSpace(name) ? "?" : name.Trim().Substring(0, Math.Min(2, name.Trim().Length)).ToUpperInvariant();
        circle.Add(UkControls.Text(initials, Mathf.RoundToInt(size * 0.4f), UkTheme.Text, heading: true, wrap: false));
        return circle;
    }

    static Button SmallButton(string text, Action onClick)
    {
        var button = UkControls.MakeButton(text, onClick, UkButtonKind.Secondary);
        button.style.height = 32;
        button.style.marginLeft = 6;
        button.style.fontSize = 13;
        return button;
    }

    /// <summary>The inline "Remove Mira?  [Remove] [Keep]" row. Replaces popups, like the other mods' inline confirms.</summary>
    static void ShowAsk(VisualElement host, string question, string confirmText, Action commit)
    {
        host.Clear();
        host.Show(true);
        var row = new VisualElement().Row().Fill(UkTheme.DangerBg).Round(UkTheme.Radius).Border(UkTheme.DangerLine).Pad(8, 12);
        row.style.marginTop = 4;
        var text = UkControls.Text(question, 14, UkTheme.DangerText);
        text.style.flexGrow = 1;
        row.Add(text);
        var keep = UkControls.MakeButton("Keep", () => { host.Clear(); host.Show(false); }, UkButtonKind.Secondary);
        keep.style.height = 32;
        keep.style.marginRight = 6;
        row.Add(keep);
        row.Add(UkControls.MakeButton(confirmText, () => { host.Clear(); host.Show(false); commit(); }, UkButtonKind.DangerFilled));
        host.Add(row);
    }

    static void Run(DrakeMenuAction action)
    {
        if (!action.Enabled)
            return;
        try
        {
            action.OnClick();
        }
        catch (Exception)
        {
            // The action logs its own failures; never throw into the UI click path.
        }
    }
}

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;

namespace LockSmith.Access;

/// <summary>One person in a group: their name and player id. The id is what lets you add them when they aren't online.</summary>
public sealed class GroupMember
{
    public GroupMember(string name, long id)
    {
        Name = name;
        Id = id;
    }

    public string Name { get; set; }

    /// <summary>0 when the file only has a name (typed by hand). Such a person can only be added while they're nearby.</summary>
    public long Id { get; set; }
}

/// <summary>A named list of people. Saved as plain text on this computer.</summary>
public sealed class PlayerGroup
{
    public PlayerGroup(string name, List<GroupMember> members)
    {
        Name = name;
        Members = members;
    }

    public string Name { get; }
    public List<GroupMember> Members { get; }

    /// <summary>What this group's people get when the group is added to a piece: <see cref="GuestPerm"/> bits (0 = plain guests).</summary>
    public int Flags { get; set; }
}

/// <summary>
/// Your named groups, in <c>config\LockSmith\groups.txt</c>: one group per line, <c>Name: Mira=7231, Tovald=881</c> (name=player id).
/// A bare name with no id also works, but that person can only be added while they're nearby.
/// Hand-editable. Groups are per computer and never synced.
/// </summary>
public static class GroupStore
{
    static string FilePath =>
        Path.Combine(BepInEx.Paths.ConfigPath, "LockSmith", "groups.txt");

    public static string DisplayPath => FilePath;

    public static List<PlayerGroup> Load()
    {
        var groups = new List<PlayerGroup>();
        try
        {
            if (!File.Exists(FilePath))
                return groups;

            foreach (var raw in File.ReadAllLines(FilePath))
            {
                var line = raw.Trim();
                if (line.Length == 0 || line.StartsWith("#", StringComparison.Ordinal))
                    continue;

                var colon = line.IndexOf(':');
                if (colon <= 0)
                    continue;

                // "Squad (add, toggle)" = the group name, then what they're allowed to do.
                var head = line.Substring(0, colon).Trim();
                var flags = 0;
                var open = head.LastIndexOf('(');
                if (open > 0 && head.EndsWith(")", StringComparison.Ordinal))
                {
                    foreach (var word in head.Substring(open + 1, head.Length - open - 2).Split(','))
                    {
                        var w = word.Trim().ToLowerInvariant();
                        if (w == "add")
                            flags |= (int)GuestPerm.Add;
                        else if (w == "toggle")
                            flags |= (int)GuestPerm.Toggle;
                    }

                    head = head.Substring(0, open).Trim();
                }

                var name = head;
                var members = new List<GroupMember>();
                foreach (var part in line.Substring(colon + 1).Split(','))
                {
                    var entry = part.Trim();
                    if (entry.Length == 0)
                        continue;

                    var equals = entry.LastIndexOf('=');
                    long id = 0;
                    var memberName = entry;
                    if (equals > 0 && long.TryParse(entry.Substring(equals + 1).Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed))
                    {
                        id = parsed;
                        memberName = entry.Substring(0, equals).Trim();
                    }

                    if (memberName.Length > 0)
                        members.Add(new GroupMember(memberName, id));
                }

                if (name.Length > 0 && members.Count > 0)
                    groups.Add(new PlayerGroup(name, members) { Flags = flags });
            }
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Could not read groups file: {ex.Message}");
        }

        return groups;
    }

    /// <summary>Sets what a group's people may do once added to a piece (<see cref="GuestPerm"/> bits). Returns false if it couldn't be saved.</summary>
    public static bool SetFlags(string name, int flags)
    {
        var list = Load();
        var group = list.Find(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));
        if (group == null)
            return false;
        group.Flags = flags;
        return Write(list);
    }

    static string FlagText(int flags)
    {
        var words = new List<string>();
        if ((flags & (int)GuestPerm.Add) != 0)
            words.Add("add");
        if ((flags & (int)GuestPerm.Toggle) != 0)
            words.Add("toggle");
        return words.Count == 0 ? "" : " (" + string.Join(", ", words) + ")";
    }

    public static bool Delete(string name)
    {
        var list = Load();
        if (list.RemoveAll(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase)) == 0)
            return false;
        return Write(list);
    }

    /// <summary>
    /// Adds people to a group (name matched ignoring case); creates it when it doesn't exist yet. Someone already in the group (same id,
    /// or same name when there's no id) is skipped, and a bare name is upgraded with the id when we learn it.
    /// Returns how many people were new, or -1 when the file couldn't be written.
    /// </summary>
    public static int AddMembers(string name, IEnumerable<GroupMember> members)
    {
        if (string.IsNullOrWhiteSpace(name))
            return -1;

        var list = Load();
        var group = list.Find(g => string.Equals(g.Name, name.Trim(), StringComparison.OrdinalIgnoreCase));
        if (group == null)
        {
            group = new PlayerGroup(name.Trim(), new List<GroupMember>());
            list.Add(group);
        }

        var added = 0;
        foreach (var member in members)
        {
            var clean = member.Name?.Trim();
            if (string.IsNullOrEmpty(clean))
                continue;

            var existing = group.Members.Find(m =>
                (member.Id != 0 && m.Id == member.Id)
                || (m.Id == 0 && string.Equals(m.Name, clean, StringComparison.OrdinalIgnoreCase)));
            if (existing != null)
            {
                if (existing.Id == 0 && member.Id != 0)
                    existing.Id = member.Id;
                continue;
            }

            group.Members.Add(new GroupMember(clean!, member.Id));
            added++;
        }

        if (group.Members.Count == 0)
            return -1;

        return Write(list) ? added : -1;
    }

    static bool Write(List<PlayerGroup> groups)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(FilePath)!);
            var sb = new StringBuilder();
            sb.AppendLine("# LockSmith groups. One per line: Name: Person=PlayerId, Person=PlayerId");
            sb.AppendLine("# A bare name (no =id) only works while that person is nearby.");
            sb.AppendLine("# Optional after the name: (add, toggle) = what they may do once added. add = let others on, toggle = switch public/private.");
            foreach (var group in groups)
            {
                var parts = new List<string>();
                foreach (var m in group.Members)
                    parts.Add(m.Id != 0 ? m.Name + "=" + m.Id.ToString(CultureInfo.InvariantCulture) : m.Name);
                sb.AppendLine(group.Name + FlagText(group.Flags) + ": " + string.Join(", ", parts));
            }

            File.WriteAllText(FilePath, sb.ToString());
            return true;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogError($"Could not write groups file: {ex.Message}");
            return false;
        }
    }
}

using System.Collections.Generic;

namespace LockSmith.Access;

/// <summary>
/// Copy people from one piece, paste them onto another. Lives for the play session on this computer; nothing is saved or synced.
/// </summary>
public static class GuestClipboard
{
    static readonly List<PieceGuest> Held = new();

    public static int Count => Held.Count;

    public static IReadOnlyList<PieceGuest> People => Held;

    public static void Copy(IEnumerable<PieceGuest> people)
    {
        Held.Clear();
        foreach (var person in people)
            if (person.PlayerId != 0L)
                Held.Add(person);
    }
}

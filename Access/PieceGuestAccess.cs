using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith.Access;

/// <summary>One guest entry persisted on the piece ZDO (id + name snapshot).</summary>
public readonly struct PieceGuest
{
    public PieceGuest(long playerId, string displayName)
    {
        PlayerId = playerId;
        DisplayName = displayName ?? string.Empty;
    }

    public long PlayerId { get; }
    public string DisplayName { get; }
}

/// <summary>
/// Shared guest list + ward-style opt-in ready flag on a piece ZDO.
/// Guest format: <c>id|Name;id|Name</c> (legacy comma-separated ids still load).
/// </summary>
public static class PieceGuestAccess
{
    private static readonly int GuestsHash = GameHookTargets.ZdoGroupMembers.GetStableHashCode();
    private static readonly int OptInHash = GameHookTargets.ZdoOptInReady.GetStableHashCode();
    private static readonly HashSet<ZDOID> RegisteredViews = new HashSet<ZDOID>();

    public static bool IsOptInReady(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid())
            return false;

        var zdo = nview.GetZDO();
        return zdo != null && zdo.GetInt(OptInHash, 0) == 1;
    }

    public static void SetOptInReady(ZNetView nview, bool ready)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        zdo.Set(OptInHash, ready ? 1 : 0, false);
    }

    public static List<PieceGuest> GetGuests(ZNetView? nview)
    {
        var result = new List<PieceGuest>();
        if (nview == null || !nview.IsValid())
            return result;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return result;

        var raw = zdo.GetString(GuestsHash, string.Empty);
        if (string.IsNullOrEmpty(raw))
            return result;

        // New format uses ';'. Legacy used ',' between bare ids.
        var parts = raw.IndexOf('|') >= 0 || raw.IndexOf(';') >= 0
            ? raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries)
            : raw.Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);

        var seen = new HashSet<long>();
        foreach (var part in parts)
        {
            var trimmed = part.Trim();
            if (string.IsNullOrEmpty(trimmed))
                continue;

            long id;
            var name = string.Empty;
            var pipe = trimmed.IndexOf('|');
            if (pipe >= 0)
            {
                if (!long.TryParse(trimmed.Substring(0, pipe).Trim(), NumberStyles.Integer,
                        CultureInfo.InvariantCulture, out id))
                    continue;
                name = trimmed.Substring(pipe + 1).Trim();
            }
            else if (!long.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out id))
            {
                continue;
            }

            if (id == 0L || !seen.Add(id))
                continue;

            if (string.IsNullOrEmpty(name))
                name = ResolveLiveName(id) ?? string.Empty;

            // Prefer live name over placeholders / Unknown from older joins.
            var live = ResolveLiveName(id);
            if (!string.IsNullOrEmpty(live) && IsPlaceholderName(name, id))
                name = live!;

            if (string.IsNullOrEmpty(name))
                name = live ?? string.Empty;

            result.Add(new PieceGuest(id, name));
        }

        return result;
    }

    public static List<long> GetGuestIds(ZNetView? nview)
    {
        var ids = new List<long>();
        foreach (var g in GetGuests(nview))
            ids.Add(g.PlayerId);
        return ids;
    }

    public static void SetGuests(ZNetView nview, List<PieceGuest> guests)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        if (guests == null || guests.Count == 0)
        {
            zdo.Set(GuestsHash, string.Empty);
            return;
        }

        var sb = new StringBuilder();
        var seen = new HashSet<long>();
        foreach (var g in guests)
        {
            if (g.PlayerId == 0L || !seen.Add(g.PlayerId))
                continue;

            if (sb.Length > 0)
                sb.Append(';');
            sb.Append(g.PlayerId.ToString(CultureInfo.InvariantCulture));
            sb.Append('|');
            sb.Append(SanitizeName(g.DisplayName));
        }

        zdo.Set(GuestsHash, sb.ToString());
    }

    public static void ClearGuests(ZNetView nview) =>
        SetGuests(nview, new List<PieceGuest>());

    public static bool IsGuest(ZNetView? nview, long playerId)
    {
        if (playerId == 0L)
            return false;

        foreach (var g in GetGuests(nview))
        {
            if (g.PlayerId == playerId)
                return true;
        }

        return false;
    }

    public static bool AddGuest(ZNetView nview, long playerId, string? displayName)
    {
        if (nview == null || !nview.IsValid() || playerId == 0L)
            return false;

        var guests = GetGuests(nview);
        foreach (var g in guests)
        {
            if (g.PlayerId == playerId)
                return false;
        }

        var name = NormalizePlayerName(displayName);
        if (IsPlaceholderName(name, playerId))
            name = ResolveLiveName(playerId) ?? string.Empty;

        if (string.IsNullOrEmpty(name))
            name = string.Empty;

        guests.Add(new PieceGuest(playerId, name));
        SetGuests(nview, guests);
        return true;
    }

    /// <summary>Owner-only: fill missing/Unknown names when that player is online.</summary>
    public static void TryRefreshGuestNames(ZNetView? nview)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        var guests = GetGuests(nview);
        if (guests.Count == 0)
            return;

        var changed = false;
        for (var i = 0; i < guests.Count; i++)
        {
            var g = guests[i];
            var live = ResolveLiveName(g.PlayerId);
            if (string.IsNullOrEmpty(live))
                continue;

            if (!IsPlaceholderName(g.DisplayName, g.PlayerId)
                && string.Equals(g.DisplayName, live, StringComparison.Ordinal))
                continue;

            // Always upgrade placeholders/Unknown; also refresh if stored name was empty.
            if (!IsPlaceholderName(g.DisplayName, g.PlayerId)
                && !string.IsNullOrEmpty(g.DisplayName))
                continue;

            guests[i] = new PieceGuest(g.PlayerId, live!);
            changed = true;
        }

        if (changed)
            SetGuests(nview, guests);
    }

    public static bool RemoveGuest(ZNetView nview, long playerId)
    {
        if (nview == null || !nview.IsValid() || playerId == 0L)
            return false;

        var guests = GetGuests(nview);
        var removed = guests.RemoveAll(g => g.PlayerId == playerId) > 0;
        if (!removed)
            return false;

        SetGuests(nview, guests);
        return true;
    }

    public static string FormatGuestNames(ZNetView? nview, bool team = false)
    {
        var guests = GetGuests(nview);
        if (guests.Count == 0)
            return string.Empty;

        var sb = new StringBuilder();
        sb.Append(AccessHoverDisplay.AccessCountLabel(team, guests.Count));

        var names = new StringBuilder();
        foreach (var g in guests)
        {
            var live = ResolveLiveName(g.PlayerId);
            var label = !string.IsNullOrEmpty(live)
                ? live!
                : g.DisplayName;

            if (IsPlaceholderName(label, g.PlayerId))
                label = "Unknown";

            if (names.Length > 0)
                names.Append(", ");
            names.Append(label);
        }

        if (names.Length > 0)
            sb.Append('\n').Append(names);

        return sb.ToString();
    }

    public static string FormatGuestSummary(ZNetView? nview, bool revealNames, bool team = false)
    {
        var guests = GetGuests(nview);
        if (guests.Count == 0)
            return team ? AccessHoverDisplay.AccessCountLabel(true, 0) : string.Empty;

        if (revealNames)
            return FormatGuestNames(nview, team);

        return AccessHoverDisplay.AccessCountLabel(team, guests.Count);
    }

    public static void RegisterRpcs(ZNetView nview)
    {
        if (nview == null || !nview.IsValid())
            return;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return;

        if (!RegisteredViews.Add(zdo.m_uid))
            return;

        nview.Register<int, long>(GameHookTargets.RpcSetOptInReady, (long sender, int flag, long playerId) =>
        {
            ApplySetOptIn(nview, flag == 1, playerId);
        });

        nview.Register<long, string>(GameHookTargets.RpcOptInSelf, (long sender, long playerId, string playerName) =>
        {
            ApplyOptInSelf(nview, playerId, playerName);
        });

        nview.Register<long>(GameHookTargets.RpcOptOutSelf, (long sender, long playerId) =>
        {
            ApplyOptOutSelf(nview, playerId);
        });
    }

    public static void RequestSetOptIn(ZNetView nview, bool ready, long playerId)
    {
        if (nview.IsOwner())
            ApplySetOptIn(nview, ready, playerId);
        else
            nview.InvokeRPC(GameHookTargets.RpcSetOptInReady, ready ? 1 : 0, playerId);
    }

    public static void RequestOptInSelf(ZNetView nview, long playerId, string playerName)
    {
        if (nview.IsOwner())
            ApplyOptInSelf(nview, playerId, playerName);
        else
            nview.InvokeRPC(GameHookTargets.RpcOptInSelf, playerId, playerName ?? string.Empty);
    }

    public static void RequestOptOutSelf(ZNetView nview, long playerId)
    {
        if (nview.IsOwner())
            ApplyOptOutSelf(nview, playerId);
        else
            nview.InvokeRPC(GameHookTargets.RpcOptOutSelf, playerId);
    }

    public static void ApplySetOptIn(ZNetView nview, bool ready, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        if (!LockSmithConfig.EnableOptInAccess || playerId == 0L)
            return;

        if (!CanManageOptIn(nview, playerId))
        {
            LockSmith.Log?.LogWarning($"Rejected opt-in toggle for player {playerId} on {nview.name}.");
            return;
        }

        SetOptInReady(nview, ready);
        PieceAccessState.MarkManaged(nview);
        LockSmith.Log?.LogInfo(
            $"Set locksmith_optin={(ready ? 1 : 0)} on {nview.name} by {playerId} " +
            $"(readback={IsOptInReady(nview)}).");
    }

    public static void ApplyOptInSelf(ZNetView nview, long playerId, string playerName)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        if (!LockSmithConfig.EnableOptInAccess || playerId == 0L)
            return;

        if (!IsOptInReady(nview))
            return;

        var container = nview.GetComponentInChildren<Container>();
        if (container != null && GroupAccessState.IsPrivateFamilyChest(container)
            && !GroupAccessState.IsTeamMode(nview))
            return;

        if (AddGuest(nview, playerId, NormalizePlayerName(playerName)))
        {
            LockSmith.Log?.LogInfo(
                $"Opt-in: added guest {playerId} as '{NormalizePlayerName(playerName)}' on {nview.name} (guests={GetGuests(nview).Count}).");
        }
    }

    public static void ApplyOptOutSelf(ZNetView nview, long playerId)
    {
        if (nview == null || !nview.IsValid() || !nview.IsOwner())
            return;

        if (playerId == 0L)
            return;

        if (RemoveGuest(nview, playerId))
        {
            LockSmith.Log?.LogInfo(
                $"Opt-out: removed player {playerId} from {nview.name} (guests={GetGuests(nview).Count}).");
        }
    }

    private static bool CanManageOptIn(ZNetView nview, long playerId)
    {
        var container = nview.GetComponentInChildren<Container>();
        if (container != null)
        {
            if (GroupAccessState.IsPrivateFamilyChest(container))
                return GroupAccessState.IsCreator(container, playerId);

            if (PieceAccessState.IsEligibleChest(container))
                return WardAccess.HasWardAccessForPlayer(PieceAccessState.GetPosition(container), playerId);
        }

        var door = nview.GetComponentInChildren<Door>();
        if (door != null && PieceAccessState.IsEligibleDoor(door))
            return WardAccess.HasWardAccessForPlayer(PieceAccessState.GetPosition(door), playerId);

        return false;
    }

    /// <summary>
    /// No key: Join when opt-in ready (E), or Leave when already a guest (AltPlace / SetupModifier).
    /// Plain E while a guest falls through so Open still works.
    /// Leave uses a 5s double-confirm (can't rejoin without owner opening Join).
    /// </summary>
    public static bool TryHandleGuestSelfInteract(ZNetView? nview, Humanoid user, bool hold, bool alt)
    {
        if (hold || nview == null || !nview.IsValid())
            return false;

        var player = user as Player;
        if (player == null)
            return false;

        var playerId = player.GetPlayerID();
        if (playerId == 0L)
            return false;

        if (IsGuest(nview, playerId))
        {
            var leaveChord = alt || LockSmithInput.IsSetupModifierHeld();
            if (!leaveChord)
                return false;

            return TryConfirmOptOut(nview, user, playerId);
        }

        if (alt || LockSmithInput.IsSetupModifierHeld())
            return false;

        if (!LockSmithConfig.EnableOptInAccess || !IsOptInReady(nview))
            return false;

        var name = CapturePlayerName(player);
        RequestOptInSelf(nview, playerId, name);
        AccessFeedback.Show(user, LockSmithLocalization.MsgOptedInToken);
        return true;
    }

    private static ZDOID _pendingLeaveId = ZDOID.None;
    private static float _pendingLeaveUntil;
    private const float LeaveConfirmSeconds = 5f;

    /// <summary>Guest leave with key or AltPlace: double-tap confirm within 5s.</summary>
    public static bool TryConfirmOptOut(ZNetView nview, Humanoid user, long playerId)
    {
        if (nview == null || !nview.IsValid() || playerId == 0L)
            return false;

        var zdo = nview.GetZDO();
        if (zdo == null)
            return false;

        var id = zdo.m_uid;
        if (_pendingLeaveId == id && Time.time <= _pendingLeaveUntil)
        {
            _pendingLeaveId = ZDOID.None;
            _pendingLeaveUntil = 0f;
            RequestOptOutSelf(nview, playerId);
            AccessFeedback.Show(user, LockSmithLocalization.MsgOptedOutToken);
            return true;
        }

        _pendingLeaveId = id;
        _pendingLeaveUntil = Time.time + LeaveConfirmSeconds;
        AccessFeedback.Show(user, LockSmithLocalization.MsgLeaveConfirmToken);
        return true;
    }

    public static void AppendOptInHover(StringBuilder sb, ZNetView? nview, bool isCreator, Vector3 piecePos)
    {
        if (!LockSmithConfig.EnableOptInAccess || nview == null)
            return;

        var ready = IsOptInReady(nview);
        var reveal = AccessHoverDisplay.CanRevealGuestNames(piecePos, isCreator, nview);
        var summary = FormatGuestSummary(nview, reveal, team: false);
        if (!string.IsNullOrEmpty(summary))
            sb.Append('\n').Append(summary);

        if (ready)
            sb.Append('\n').Append(LockSmithLocalization.T(LockSmithLocalization.PieceOptInReadyToken));

        if (isCreator)
        {
            var setupUse = Localization.instance.Localize(
                LockSmithInput.FormatModifierUse(LockSmithConfig.SetupModifier));
            var action = LockSmithLocalization.T(
                ready
                    ? LockSmithLocalization.HoverCloseOptInToken
                    : LockSmithLocalization.HoverOpenOptInToken);
            sb.Append('\n').Append(setupUse).Append(' ').Append(action);
        }
        else if (ready)
        {
            var useKey = Localization.instance.Localize("[<color=yellow><b>$KEY_Use</b></color>]");
            sb.Append('\n').Append(useKey).Append(' ')
                .Append(LockSmithLocalization.T(LockSmithLocalization.HoverJoinAccessToken));
        }
    }

    private static bool IsPlaceholderName(string? name, long playerId)
    {
        if (string.IsNullOrWhiteSpace(name))
            return true;

        if (string.Equals(name, "Unknown", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name!.StartsWith("Player ", StringComparison.OrdinalIgnoreCase))
            return true;

        if (name == playerId.ToString(CultureInfo.InvariantCulture))
            return true;

        return false;
    }

    private static string CapturePlayerName(Player player)
    {
        if (player == null)
            return string.Empty;

        try
        {
            var raw = player.GetPlayerName();
            var normalized = NormalizePlayerName(raw);
            if (!string.IsNullOrEmpty(normalized))
                return normalized;
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"CapturePlayerName failed: {ex.Message}");
        }

        return ResolveLiveName(player.GetPlayerID()) ?? string.Empty;
    }

    private static string NormalizePlayerName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var n = name!.Trim();
        try
        {
            if (n.StartsWith("$", StringComparison.Ordinal) && Localization.instance != null)
                n = Localization.instance.Localize(n);
        }
        catch (Exception)
        {
            // keep raw
        }

        return SanitizeName(n);
    }

    private static string SanitizeName(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        return name!.Replace("|", string.Empty).Replace(";", string.Empty).Trim();
    }

    private static string? ResolveLiveName(long playerId)
    {
        try
        {
            var direct = Player.GetPlayer(playerId);
            if (direct != null)
            {
                var n = NormalizePlayerName(direct.GetPlayerName());
                if (!string.IsNullOrEmpty(n))
                    return n;
            }

            var all = Player.GetAllPlayers();
            if (all != null)
            {
                foreach (var p in all)
                {
                    if (p == null || p.GetPlayerID() != playerId)
                        continue;

                    var n = NormalizePlayerName(p.GetPlayerName());
                    if (!string.IsNullOrEmpty(n))
                        return n;
                }
            }
        }
        catch (Exception ex)
        {
            LockSmith.Log?.LogDebug($"ResolveLiveName failed: {ex.Message}");
        }

        return null;
    }
}

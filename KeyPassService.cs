using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using DrakeModsLibs.API;
using LockSmith.Access;
using LockSmith.UI;
using UnityEngine;

namespace LockSmith;

/// <summary>
/// Guest/team name clipboard on the Locksmith key (customData).
/// World: Ctrl+C copy from piece, Ctrl+V paste merge onto piece.
/// </summary>
public static class KeyPassService
{
    public const string PassGuestsKey = "locksmith_pass_guests";
    public const string PassKindKey = "locksmith_pass_kind";

    const float NearbyRange = 5f;

    public static bool HasPassPayload(ItemDrop.ItemData? item)
    {
        if (item?.m_customData == null)
            return false;
        return item.m_customData.TryGetValue(PassGuestsKey, out var raw) && !string.IsNullOrWhiteSpace(raw);
    }

    public static string? FormatMembershipSubtitle(ItemDrop.ItemData? item)
    {
        var guests = GetGuests(item);
        if (guests.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.Append(guests.Count == 1 ? "Pass: " : "Team: ");
        AppendGuestNames(sb, guests);
        return sb.ToString();
    }

    /// <summary>Pink tooltip line after item description: <c>Team : Alice, Bob</c>.</summary>
    public static string? FormatMembershipTooltipLine(ItemDrop.ItemData? item)
    {
        var guests = GetGuests(item);
        if (guests.Count == 0)
            return null;

        var sb = new StringBuilder();
        sb.Append(LockSmithLocalization.T(LockSmithLocalization.PieceTeamToken));
        sb.Append(" : ");
        AppendGuestNames(sb, guests);
        return AccessHoverDisplay.Colorize(sb.ToString());
    }

    static void AppendGuestNames(StringBuilder sb, List<PieceGuest> guests)
    {
        for (var i = 0; i < guests.Count; i++)
        {
            if (i > 0)
                sb.Append(", ");
            sb.Append(string.IsNullOrEmpty(guests[i].DisplayName)
                ? guests[i].PlayerId.ToString(CultureInfo.InvariantCulture)
                : guests[i].DisplayName);
        }
    }

    public static List<PieceGuest> GetGuests(ItemDrop.ItemData? item)
    {
        var result = new List<PieceGuest>();
        if (item?.m_customData == null)
            return result;
        if (!item.m_customData.TryGetValue(PassGuestsKey, out var raw) || string.IsNullOrWhiteSpace(raw))
            return result;

        foreach (var part in raw.Split(new[] { ';' }, StringSplitOptions.RemoveEmptyEntries))
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

            if (id == 0L)
                continue;
            result.Add(new PieceGuest(id, name));
        }

        return result;
    }

    public static void SetGuests(ItemDrop.ItemData item, IReadOnlyList<PieceGuest> guests)
    {
        if (item == null)
            return;
        if (item.m_customData == null)
            item.m_customData = new Dictionary<string, string>();

        if (guests == null || guests.Count == 0)
        {
            item.m_customData.Remove(PassGuestsKey);
            item.m_customData.Remove(PassKindKey);
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
            sb.Append((g.DisplayName ?? string.Empty).Replace(";", ",").Replace("|", "/"));
        }

        item.m_customData[PassGuestsKey] = sb.ToString();
        item.m_customData[PassKindKey] = seen.Count <= 1 ? "person" : "group";
    }

    public static void ClearPass(ItemDrop.ItemData item)
    {
        if (item?.m_customData == null)
            return;
        item.m_customData.Remove(PassGuestsKey);
        item.m_customData.Remove(PassKindKey);
    }

    public static string GetLabelBracket(ItemDrop.ItemData item)
    {
        var display = CustomizeLibsAPI.GetProperName(item);
        if (string.IsNullOrEmpty(display))
            display = CustomizeLibsAPI.GetDisplayNameForUi(item) ?? "";

        var open = display.LastIndexOf('[');
        var close = display.LastIndexOf(']');
        if (open >= 0 && close > open)
            return display.Substring(open + 1, close - open - 1).Trim();

        return string.Empty;
    }

    public static void SetLabelBracket(ItemDrop.ItemData item, string? bracket)
    {
        if (item == null)
            return;

        var baseName = LockSmithConfig.KeyName;
        if (string.IsNullOrWhiteSpace(baseName))
            baseName = "Master Key";

        var label = (bracket ?? string.Empty).Trim();
        if (string.IsNullOrEmpty(label))
            CustomizeLibsAPI.SetCustomName(item, baseName);
        else
            CustomizeLibsAPI.SetCustomName(item, baseName + " [" + label + "]");
    }

    public static bool HasLookTargetGuests()
    {
        return TryGetLookTargetNview(out var nview) && PieceGuestAccess.GetGuests(nview).Count > 0;
    }

    public static void TryPullFromLookTarget(ItemDrop.ItemData item)
    {
        if (!TryGetLookTargetNview(out var nview))
        {
            AccessFeedback.ShowRaw(Player.m_localPlayer, "Look at a LockSmith piece first.");
            return;
        }

        var fromPiece = PieceGuestAccess.GetGuests(nview);
        if (fromPiece.Count == 0)
        {
            AccessFeedback.ShowRaw(Player.m_localPlayer, "No guests on that piece.");
            return;
        }

        MergeIntoKey(item, fromPiece);
        var sub = FormatMembershipSubtitle(item) ?? "names";
        AccessFeedback.ShowRaw(Player.m_localPlayer, "Copied " + sub);
    }

    /// <summary>Ctrl+V — merge key guests onto the looked-at piece (always merge).</summary>
    public static void TryPasteOntoLookTarget(ItemDrop.ItemData item)
    {
        var local = Player.m_localPlayer;
        if (!local)
            return;

        if (!HasPassPayload(item))
        {
            AccessFeedback.ShowRaw(local, "Key has no names — Ctrl+C a piece first.");
            return;
        }

        if (!TryGetLookTargetNview(out var nview))
        {
            AccessFeedback.ShowRaw(local, "Look at a chest or door first.");
            return;
        }

        var lookPos = nview.transform.position;
        var lookContainer = nview.GetComponentInChildren<Container>();
        var privateFamily = lookContainer != null && GroupAccessState.IsPrivateFamilyChest(lookContainer);
        if (!WardAccess.AllowsToolOnPiece(lookPos, privateFamily))
        {
            AccessFeedback.Show(local, LockSmithLocalization.MsgNeedActiveWardToken);
            return;
        }

        var playerId = local.GetPlayerID();
        if (!PieceGuestAccess.CanManageGuests(nview, playerId))
        {
            AccessFeedback.ShowRaw(local, "No access to change that piece.");
            return;
        }

        var fromKey = GetGuests(item);
        if (fromKey.Count == 0)
        {
            AccessFeedback.ShowRaw(local, "Key has no names.");
            return;
        }

        PieceGuestAccess.RequestMergeGuests(nview, fromKey, playerId);
        PieceAccessState.MarkManaged(nview);
        var sub = FormatMembershipSubtitle(item) ?? "names";
        AccessFeedback.ShowRaw(local, "Pasted " + sub);
    }

    public static void TryGrabNearby(ItemDrop.ItemData item)
    {
        var local = Player.m_localPlayer;
        if (!local)
            return;

        Player? nearest = null;
        var best = NearbyRange;
        foreach (var p in Player.GetAllPlayers())
        {
            if (!p || p == local)
                continue;
            var d = Vector3.Distance(local.transform.position, p.transform.position);
            if (d > best)
                continue;
            best = d;
            nearest = p;
        }

        if (!nearest)
        {
            AccessFeedback.ShowRaw(local, "No player nearby.");
            return;
        }

        MergeIntoKey(item, new[] { new PieceGuest(nearest.GetPlayerID(), nearest.GetPlayerName()) });
        AccessFeedback.ShowRaw(local, "Added " + nearest.GetPlayerName() + " to key.");
    }

    public static bool TryCloneKey(ItemDrop.ItemData source, out string detail)
    {
        detail = "";
        var player = Player.m_localPlayer;
        if (!player || source == null)
            return false;

        if (!TryConsumeCloneCost(player))
        {
            detail = "Need " + LockSmithConfig.KeyMaterials + " to clone.";
            AccessFeedback.ShowRaw(player, detail);
            return false;
        }

        var prefabName = ContentRegistration.OfficialKeyId;
        var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
        if (!prefab)
        {
            detail = "Key prefab missing.";
            AccessFeedback.ShowRaw(player, detail);
            return false;
        }

        var inv = player.GetInventory();
        if (inv == null || !inv.AddItem(prefab, 1))
        {
            detail = "Inventory full.";
            AccessFeedback.ShowRaw(player, detail);
            return false;
        }

        ItemDrop.ItemData? clone = null;
        foreach (var it in inv.GetAllItems())
        {
            if (it == null || ReferenceEquals(it, source))
                continue;
            if (!ChestAccessService.IsLocksmithKey(it))
                continue;
            clone = it;
        }

        if (clone == null)
        {
            detail = "Clone failed.";
            return false;
        }

        if (source.m_customData != null)
        {
            if (clone.m_customData == null)
                clone.m_customData = new Dictionary<string, string>();
            foreach (var kv in source.m_customData)
                clone.m_customData[kv.Key] = kv.Value;
        }

        ChestAccessService.EnsureRenameHandOff(clone);

        var name = CustomizeLibsAPI.GetProperName(source);
        if (!string.IsNullOrEmpty(name))
            CustomizeLibsAPI.SetCustomName(clone, name);

        try
        {
            player.ShowPickupMessage(clone, 1);
        }
        catch (Exception)
        {
            /* Valheim 1.0 path may differ — center text still below */
        }

        var label = CustomizeLibsAPI.GetDisplayNameForUi(clone, localize: true);
        if (string.IsNullOrEmpty(label))
            label = LockSmithConfig.KeyName;
        detail = "Cloned: " + label;
        return true;
    }

    static void MergeIntoKey(ItemDrop.ItemData item, IReadOnlyList<PieceGuest> add)
    {
        var merged = GetGuests(item);
        var seen = new HashSet<long>();
        foreach (var g in merged)
            seen.Add(g.PlayerId);
        foreach (var g in add)
        {
            if (g.PlayerId == 0L || !seen.Add(g.PlayerId))
                continue;
            merged.Add(g);
        }

        SetGuests(item, merged);
        if (merged.Count == 1)
            SetLabelBracket(item, string.IsNullOrEmpty(merged[0].DisplayName) ? "Pass" : merged[0].DisplayName);
        else if (merged.Count > 1 && string.IsNullOrEmpty(GetLabelBracket(item)))
            SetLabelBracket(item, "Team");
    }

    static bool TryGetLookTargetNview(out ZNetView nview)
    {
        nview = null!;
        var local = Player.m_localPlayer;
        if (!local)
            return false;

        var hover = local.GetHoverObject();
        if (!hover)
            return false;

        nview = hover.GetComponentInParent<ZNetView>();
        return nview && nview.IsValid();
    }

    static bool TryConsumeCloneCost(Player player)
    {
        var mats = LockSmithConfig.KeyMaterials;
        if (string.IsNullOrWhiteSpace(mats))
            return true;

        var first = mats.Split(',')[0].Trim();
        var parts = first.Split(':');
        if (parts.Length < 1)
            return true;

        var prefabName = parts[0].Trim();
        var amount = 1;
        if (parts.Length > 1)
            int.TryParse(parts[1].Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out amount);
        if (amount < 1)
            amount = 1;

        var inv = player.GetInventory();
        if (inv == null)
            return false;

        var prefab = ObjectDB.instance != null ? ObjectDB.instance.GetItemPrefab(prefabName) : null;
        if (!prefab)
            return false;
        var drop = prefab.GetComponent<ItemDrop>();
        var sharedName = drop?.m_itemData?.m_shared?.m_name;
        if (string.IsNullOrEmpty(sharedName))
            return false;

        if (inv.CountItems(sharedName) < amount)
            return false;
        inv.RemoveItem(sharedName, amount);
        return true;
    }
}

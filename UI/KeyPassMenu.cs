using System;
using System.Collections.Generic;
using DrakeModsLibs.API;
using DrakeModsLibs.UI;
using LockSmith.Access;
using UnityEngine;

namespace LockSmith.UI;

/// <summary>
/// Locksmith key inventory menu — RenameIt-looking chrome via DrakeModsLibs wood panels.
/// Registers as a <see cref="DrakeTabHost"/> tab (default on Locksmith keys).
/// Inventory open chord is owned by Libs (<c>Integration.InventoryOpenModifier</c>).
/// </summary>
public static class KeyPassMenu
{
    public const string TabId = DrakeTabRegistration.LockSmithKeyPassTabId;

    static readonly DrakeWoodActionMenu ActionMenu = new DrakeWoodActionMenu("locksmith_key_action_menu");
    static readonly DrakeConfirmPanel Confirm = new DrakeConfirmPanel("locksmith_key_confirm");
    static readonly DrakeTextPromptPanel RelabelPrompt = new DrakeTextPromptPanel("locksmith_key_relabel");
    static bool _tabRegistered;

    public static bool IsOpen =>
        ActionMenu.IsOpen
        || Confirm.IsOpen
        || RelabelPrompt.IsOpen
        || (DrakeTabHost.IsOpen && DrakeTabHost.ActiveTabId == TabId);

    public static void RegisterTab()
    {
        if (_tabRegistered)
            return;
        _tabRegistered = true;

        try
        {
            DrakeTabHost.Register(
                id: TabId,
                title: "Lock",
                priority: DrakeTabRegistration.DefaultFeaturePriority,
                isAvailable: item =>
                    LockSmithConfig.EnableKeyPasses
                    && ChestAccessService.IsLocksmithKey(item)
                    && IsItemInLocalInventory(item),
                show: ctx => Open(ctx.Item),
                claimDefault: item =>
                    LockSmithConfig.EnableKeyPasses && ChestAccessService.IsLocksmithKey(item),
                hide: HideForHost,
                getHintPhrase: () => LockSmithLocalization.T(LockSmithLocalization.InventoryHintPhraseToken),
                getTitle: () => LockSmithLocalization.T(LockSmithLocalization.InventoryTabTitleToken));
        }
        catch (Exception ex)
        {
            _tabRegistered = false;
            LockSmith.Log?.LogError($"DrakeTabHost.Register failed (libs API mismatch?): {ex.Message}");
        }
    }

    public static void CloseAll()
    {
        RelabelPrompt.Close();
        Confirm.Close();
        ActionMenu.CloseSilent();
    }

    static void HideForHost()
    {
        RelabelPrompt.Close();
        Confirm.Close();
        ActionMenu.CloseSilent();
    }

    /// <summary>Open via libs tab host (Lock default on keys).</summary>
    public static bool TryOpen(ItemDrop.ItemData? item)
    {
        RegisterTab();
        return DrakeTabHost.OpenForItem(item, preferredTabId: TabId);
    }

    static void Open(ItemDrop.ItemData item)
    {
        ChestAccessService.EnsureRenameHandOff(item);

        var title = CustomizeLibsAPI.GetDisplayNameForUi(item, localize: true);
        if (string.IsNullOrEmpty(title))
            title = LockSmithConfig.KeyName;

        var subtitle = KeyPassService.FormatMembershipSubtitle(item);
        var actions = new List<DrakeMenuAction>
        {
            new DrakeMenuAction("Relabel…", () =>
            {
                ActionMenu.CloseSilent();
                OpenRelabel(item);
            }),
            new DrakeMenuAction("Grab nearby person", () =>
            {
                ActionMenu.CloseSilent();
                KeyPassService.TryGrabNearby(item);
                Open(item);
            }),
            new DrakeMenuAction("Clear names…", () =>
            {
                ActionMenu.CloseSilent();
                Confirm.Show(
                    "Clear names?",
                    "Remove guest/team names from this key?\n\nTip: Ctrl+C a door first if you need a backup.",
                    onYes: () =>
                    {
                        KeyPassService.ClearPass(item);
                        AccessFeedback.ShowRaw(Player.m_localPlayer, "Key names cleared.");
                        Open(item);
                    },
                    onNo: () => Open(item));
            }, enabled: KeyPassService.HasPassPayload(item)),
            new DrakeMenuAction(
                "Clone key (" + LockSmithConfig.KeyMaterials + ")",
                () =>
                {
                    ActionMenu.CloseSilent();
                    if (KeyPassService.TryCloneKey(item, out var detail))
                        AccessFeedback.ShowRaw(Player.m_localPlayer, detail);
                    DrakeTabHost.NotifyFeatureClosed(TabId);
                })
        };

        ActionMenu.Open(
            title,
            subtitle,
            actions,
            cancelLabel: "Close",
            onClosed: () => DrakeTabHost.NotifyFeatureClosed(TabId),
            showCancel: true);
    }

    static void OpenRelabel(ItemDrop.ItemData item)
    {
        var bracket = KeyPassService.GetLabelBracket(item);
        RelabelPrompt.Show(
            "Relabel",
            bracket,
            onOk: text =>
            {
                KeyPassService.SetLabelBracket(item, text);
                Open(item);
            },
            onCancel: () => Open(item),
            charLimit: 48);
    }

    static bool IsItemInLocalInventory(ItemDrop.ItemData item)
    {
        var player = Player.m_localPlayer;
        if (!player)
            return false;
        var inv = player.GetInventory();
        if (inv == null)
            return false;
        foreach (var stacked in inv.GetAllItems())
        {
            if (ReferenceEquals(stacked, item))
                return true;
        }
        return false;
    }
}

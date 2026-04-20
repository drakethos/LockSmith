using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using BepInEx.Logging;

namespace DrakeLabs
{
    /// <summary>
    /// Harmony patches enforcing per-object access control on all Containers and Doors.
    ///
    /// ── Access Modes (cycle with [LeftAlt + E / hold-interact]) ──────────────────
    ///   Public      – anyone may interact (arrows in HUD: → Private)
    ///   Private     – ward members only; no bypass possible
    ///   Named       – ward members + players in the allow list + key holders
    ///   KeyRequired – ward members + players carrying DL_WardKey
    ///
    /// Cycling requires ward-member access at the object's position.
    ///
    /// ── Named mode: adding / removing players ────────────────────────────────────
    ///   Console commands (requires ward access, crosshair on the target object):
    ///     dl_allow  <name>  – add a player to the allow list
    ///     dl_remove <name>  – remove a player from the allow list
    ///     dl_list           – show the current mode and allow list
    ///
    ///   The "Named" workflow for sharing access without console:
    ///     Set the object to KeyRequired, craft a DL_WardKey, and hand it to the
    ///     target player.  They can then open the door/chest while carrying the key.
    ///
    /// ── Config overrides ─────────────────────────────────────────────────────────
    ///   RequireKeyForDoors  / RequireKeyForChests (Plugin.cs, IsAdminOnly):
    ///     Forces ALL Public-mode objects to also need a key.  Ward members are
    ///     always exempt.
    /// </summary>
    public static class Patches
    {
        private static ManualLogSource Log => Plugin.Log;

        // -----------------------------------------------------------------------
        // Shared utilities
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns the ZNetView of the Container or Door the local player is looking at.
        /// Walks up the hierarchy from the hovered GameObject.
        /// </summary>
        private static ZNetView GetFocusedNView()
        {
            Player local = Player.m_localPlayer;
            if (local == null) return null;

            GameObject hoverObj = local.GetHoverObject();
            if (hoverObj == null) return null;

            Container container = hoverObj.GetComponentInParent<Container>();
            if (container != null && container.m_nview != null && container.m_nview.IsValid())
                return container.m_nview;

            Door door = hoverObj.GetComponentInParent<Door>();
            if (door != null && door.m_nview != null && door.m_nview.IsValid())
                return door.m_nview;

            return null;
        }

        private static string CycleHint =>
            $"[<color=yellow>$KEY_AltPlace</color>+$KEY_Use] Cycle mode";

        // -----------------------------------------------------------------------
        // Container patches
        // -----------------------------------------------------------------------

        [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
        public static class Container_Interact_Prefix
        {
            static bool Prefix(Container __instance, Humanoid character, bool hold, ref bool __result)
            {
                Player player = character as Player;
                if (player == null) return true;

                ZNetView nview = __instance.m_nview;
                if (nview == null || !nview.IsValid()) return true;

                // ── Mode-cycle: LeftAlt + hold-interact, ward members only ──────
                if (hold && Input.GetKey(KeyCode.LeftAlt))
                {
                    if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, true))
                    {
                        player.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                        __result = true;
                        return false;
                    }

                    AccessMode newMode = AccessControl.CycleMode(nview);
                    player.Message(MessageHud.MessageType.Center,
                        $"Chest access mode: {AccessControl.ModeLabel(newMode)}");
                    Log.LogInfo($"Container '{__instance.name}' access mode → {newMode}");
                    __result = true;
                    return false;
                }

                // ── Normal interaction: enforce access ───────────────────────────
                bool configKey = Plugin.RequireKeyForChests.Value;
                if (!AccessControl.CanAccess(nview, player, configKey))
                {
                    player.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Container), nameof(Container.GetHoverText))]
        public static class Container_GetHoverText_Postfix
        {
            static void Postfix(Container __instance, ref string __result)
            {
                ZNetView nview = __instance.m_nview;
                if (nview == null || !nview.IsValid()) return;

                AccessMode mode = AccessControl.GetMode(nview);
                bool hasWardAccess = PrivateArea.CheckAccess(__instance.transform.position, 0f, false);

                __result += $"\n{AccessControl.ModeLabel(mode)}";

                if (mode == AccessMode.Named && hasWardAccess)
                {
                    List<string> allowed = AccessControl.GetAllowed(nview);
                    string names = allowed.Count > 0 ? string.Join(", ", allowed) : "none";
                    __result += $"\n<color=grey>Allowed: {names}</color>";
                }

                if (hasWardAccess)
                    __result += $"\n{CycleHint} (→ {AccessControl.NextModeLabel(mode)})";
            }
        }

        // -----------------------------------------------------------------------
        // Door patches
        // -----------------------------------------------------------------------

        [HarmonyPatch(typeof(Door), nameof(Door.Interact))]
        public static class Door_Interact_Prefix
        {
            static bool Prefix(Door __instance, Humanoid character, bool hold, ref bool __result)
            {
                Player player = character as Player;
                if (player == null) return true;

                ZNetView nview = __instance.m_nview;
                if (nview == null || !nview.IsValid()) return true;

                // ── Mode-cycle ────────────────────────────────────────────────────
                if (hold && Input.GetKey(KeyCode.LeftAlt))
                {
                    if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, true))
                    {
                        player.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                        __result = true;
                        return false;
                    }

                    AccessMode newMode = AccessControl.CycleMode(nview);
                    player.Message(MessageHud.MessageType.Center,
                        $"Door access mode: {AccessControl.ModeLabel(newMode)}");
                    Log.LogInfo($"Door '{__instance.name}' access mode → {newMode}");
                    __result = true;
                    return false;
                }

                // ── Normal interaction ─────────────────────────────────────────
                bool configKey = Plugin.RequireKeyForDoors.Value;
                if (!AccessControl.CanAccess(nview, player, configKey))
                {
                    player.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                    __result = true;
                    return false;
                }

                return true;
            }
        }

        [HarmonyPatch(typeof(Door), nameof(Door.GetHoverText))]
        public static class Door_GetHoverText_Postfix
        {
            static void Postfix(Door __instance, ref string __result)
            {
                ZNetView nview = __instance.m_nview;
                if (nview == null || !nview.IsValid()) return;

                AccessMode mode = AccessControl.GetMode(nview);
                bool hasWardAccess = PrivateArea.CheckAccess(__instance.transform.position, 0f, false);

                __result += $"\n{AccessControl.ModeLabel(mode)}";

                if (mode == AccessMode.Named && hasWardAccess)
                {
                    List<string> allowed = AccessControl.GetAllowed(nview);
                    string names = allowed.Count > 0 ? string.Join(", ", allowed) : "none";
                    __result += $"\n<color=grey>Allowed: {names}</color>";
                }

                if (hasWardAccess)
                    __result += $"\n{CycleHint} (→ {AccessControl.NextModeLabel(mode)})";
            }
        }

        // -----------------------------------------------------------------------
        // Console commands
        // -----------------------------------------------------------------------

        public static void RegisterCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new AllowCommand());
            CommandManager.Instance.AddConsoleCommand(new RemoveCommand());
            CommandManager.Instance.AddConsoleCommand(new ListCommand());
        }

        // ------------------------------------------------------------------
        // dl_allow
        // ------------------------------------------------------------------
        private class AllowCommand : ConsoleCommand
        {
            public override string Name => "dl_allow";
            public override string Help => "<playerName> – add a player to the Named allow list for the focused chest/door";

            public override void Run(string[] args, Terminal context)
            {
                if (args.Length < 1)
                {
                    context.AddString("Usage: dl_allow <playerName>");
                    return;
                }

                ZNetView nview = GetFocusedNView();
                if (nview == null) { context.AddString("No chest or door in crosshair."); return; }

                if (!PrivateArea.CheckAccess(nview.transform.position, 0f, true))
                {
                    context.AddString("You need ward access to manage this object's allow list.");
                    return;
                }

                string name = string.Join(" ", args);   // support names with spaces
                bool changed = AccessControl.AddAllowed(nview, name);
                context.AddString(changed
                    ? $"Added '{name}' to the allow list of {nview.gameObject.name}. " +
                      $"Set mode to Named with [Alt+E] so it takes effect."
                    : $"'{name}' is already in the allow list.");
            }
        }

        // ------------------------------------------------------------------
        // dl_remove
        // ------------------------------------------------------------------
        private class RemoveCommand : ConsoleCommand
        {
            public override string Name => "dl_remove";
            public override string Help => "<playerName> – remove a player from the Named allow list for the focused chest/door";

            public override void Run(string[] args, Terminal context)
            {
                if (args.Length < 1)
                {
                    context.AddString("Usage: dl_remove <playerName>");
                    return;
                }

                ZNetView nview = GetFocusedNView();
                if (nview == null) { context.AddString("No chest or door in crosshair."); return; }

                if (!PrivateArea.CheckAccess(nview.transform.position, 0f, true))
                {
                    context.AddString("You need ward access to manage this object's allow list.");
                    return;
                }

                string name = string.Join(" ", args);
                bool changed = AccessControl.RemoveAllowed(nview, name);
                context.AddString(changed
                    ? $"Removed '{name}' from the allow list of {nview.gameObject.name}."
                    : $"'{name}' was not in the allow list.");
            }
        }

        // ------------------------------------------------------------------
        // dl_list
        // ------------------------------------------------------------------
        private class ListCommand : ConsoleCommand
        {
            public override string Name => "dl_list";
            public override string Help => "– show the access mode and Named allow list for the focused chest/door";

            public override void Run(string[] args, Terminal context)
            {
                ZNetView nview = GetFocusedNView();
                if (nview == null) { context.AddString("No chest or door in crosshair."); return; }

                AccessMode mode = AccessControl.GetMode(nview);
                List<string> list = AccessControl.GetAllowed(nview);
                context.AddString(
                    $"{nview.gameObject.name}  mode={mode}  " +
                    $"named_list=[{(list.Count > 0 ? string.Join(", ", list) : "empty")}]");
            }
        }
    }
}

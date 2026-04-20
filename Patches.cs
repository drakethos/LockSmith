using System.Collections.Generic;
using HarmonyLib;
using Jotunn.Entities;
using Jotunn.Managers;
using UnityEngine;
using BepInEx.Logging;

namespace DrakeLabs
{
    /// <summary>
    /// Harmony patches that enforce per-object privacy on containers and doors.
    ///
    /// Workflow (privacy toggle):
    ///   A player stands next to a chest or door inside a ward and presses
    ///   [Alt + E] (hold-interact with LeftAlt held).
    ///   This requires ward-member access and toggles the private flag on that
    ///   specific ZDO.
    ///
    /// Workflow (per-object allow list):
    ///   Console commands registered by this class let a ward owner grant or
    ///   revoke access for a named player while looking at a chest or door.
    ///
    ///   dl_allow  <playerName>  – add player to the focused object's allow list
    ///   dl_remove <playerName>  – remove player from the focused object's allow list
    ///   dl_list                 – print current allow list for the focused object
    ///
    /// Access rules (see AccessControl.CanAccess):
    ///   1. Object is not private   → anyone may use it.
    ///   2. Player has ward access  → allowed (vanilla behaviour preserved).
    ///   3. Player is in per-object allow list → allowed.
    ///   4. Player carries DL_WardKey item     → allowed.
    /// </summary>
    public static class Patches
    {
        private static ManualLogSource Log => Plugin.Log;

        // -----------------------------------------------------------------------
        // Container patches
        // -----------------------------------------------------------------------

        [HarmonyPatch(typeof(Container), nameof(Container.Interact))]
        public static class Container_Interact_Prefix
        {
            /// <summary>
            /// Intercepts Container.Interact.
            /// LeftAlt + hold → toggle privacy (ward members only).
            /// Normal interact → block if private and not allowed.
            /// </summary>
            static bool Prefix(Container __instance, Humanoid character, bool hold, ref bool __result)
            {
                Player player = character as Player;
                if (player == null) return true;

                ZNetView nview = __instance.m_nview;
                if (nview == null || !nview.IsValid()) return true;

                if (hold && Input.GetKey(KeyCode.LeftAlt))
                {
                    if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, true))
                    {
                        player.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                        __result = true;
                        return false;
                    }

                    bool isNowPrivate = AccessControl.TogglePrivate(nview);
                    player.Message(MessageHud.MessageType.Center,
                        Localization.instance.Localize(
                            isNowPrivate ? "$dl_container_set_private" : "$dl_container_set_public"));
                    Log.LogInfo($"Container '{__instance.name}' → private={isNowPrivate}");
                    __result = true;
                    return false;
                }

                if (!AccessControl.CanAccess(nview, player))
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

                bool isPrivate = AccessControl.IsPrivate(nview);
                bool hasWardAccess = PrivateArea.CheckAccess(__instance.transform.position, 0f, false);

                if (isPrivate)
                {
                    __result += "\n<color=red>[Private]</color>";
                    if (hasWardAccess)
                        __result += $"\n[<color=yellow>$KEY_AltPlace</color>+$KEY_Use] Set Public";
                }
                else if (hasWardAccess)
                {
                    __result += "\n[Public]";
                    __result += $"\n[<color=yellow>$KEY_AltPlace</color>+$KEY_Use] Set Private";
                }
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

                if (hold && Input.GetKey(KeyCode.LeftAlt))
                {
                    if (!PrivateArea.CheckAccess(__instance.transform.position, 0f, true))
                    {
                        player.Message(MessageHud.MessageType.Center, "$piece_noaccess");
                        __result = true;
                        return false;
                    }

                    bool isNowPrivate = AccessControl.TogglePrivate(nview);
                    player.Message(MessageHud.MessageType.Center,
                        Localization.instance.Localize(
                            isNowPrivate ? "$dl_door_set_private" : "$dl_door_set_public"));
                    Log.LogInfo($"Door '{__instance.name}' → private={isNowPrivate}");
                    __result = true;
                    return false;
                }

                if (!AccessControl.CanAccess(nview, player))
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

                bool isPrivate = AccessControl.IsPrivate(nview);
                bool hasWardAccess = PrivateArea.CheckAccess(__instance.transform.position, 0f, false);

                if (isPrivate)
                {
                    __result += "\n<color=red>[Private]</color>";
                    if (hasWardAccess)
                        __result += $"\n[<color=yellow>$KEY_AltPlace</color>+$KEY_Use] Set Public";
                }
                else if (hasWardAccess)
                {
                    __result += "\n[Public]";
                    __result += $"\n[<color=yellow>$KEY_AltPlace</color>+$KEY_Use] Set Private";
                }
            }
        }

        // -----------------------------------------------------------------------
        // Console commands (registered via Jotunn CommandManager)
        // -----------------------------------------------------------------------

        /// <summary>
        /// Registers the three DrakeLabs console commands.
        /// Call this once from Plugin.Awake (after CommandManager is ready).
        /// </summary>
        public static void RegisterCommands()
        {
            CommandManager.Instance.AddConsoleCommand(new AllowCommand());
            CommandManager.Instance.AddConsoleCommand(new RemoveCommand());
            CommandManager.Instance.AddConsoleCommand(new ListCommand());
        }

        // ------------------------------------------------------------------
        // Shared utility: resolve the ZNetView of the object under crosshair
        // ------------------------------------------------------------------
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

        // ------------------------------------------------------------------
        // dl_allow
        // ------------------------------------------------------------------
        private class AllowCommand : ConsoleCommand
        {
            public override string Name => "dl_allow";
            public override string Help => "<playerName> – allow a player to use the focused private chest/door";

            public override void Run(string[] args, Terminal context)
            {
                if (args.Length < 1)
                {
                    context.AddString("Usage: dl_allow <playerName>");
                    return;
                }

                string targetName = args[0];
                ZNetView nview = GetFocusedNView();
                if (nview == null)
                {
                    context.AddString("No valid chest or door in crosshair.");
                    return;
                }

                if (!PrivateArea.CheckAccess(nview.transform.position, 0f, true))
                {
                    context.AddString("You don't have ward access here.");
                    return;
                }

                bool changed = AccessControl.AddAllowed(nview, targetName);
                context.AddString(changed
                    ? $"Allowed '{targetName}' on {nview.gameObject.name}."
                    : $"'{targetName}' was already in the allow list.");
            }
        }

        // ------------------------------------------------------------------
        // dl_remove
        // ------------------------------------------------------------------
        private class RemoveCommand : ConsoleCommand
        {
            public override string Name => "dl_remove";
            public override string Help => "<playerName> – remove a player from the focused private chest/door allow list";

            public override void Run(string[] args, Terminal context)
            {
                if (args.Length < 1)
                {
                    context.AddString("Usage: dl_remove <playerName>");
                    return;
                }

                string targetName = args[0];
                ZNetView nview = GetFocusedNView();
                if (nview == null)
                {
                    context.AddString("No valid chest or door in crosshair.");
                    return;
                }

                if (!PrivateArea.CheckAccess(nview.transform.position, 0f, true))
                {
                    context.AddString("You don't have ward access here.");
                    return;
                }

                bool changed = AccessControl.RemoveAllowed(nview, targetName);
                context.AddString(changed
                    ? $"Removed '{targetName}' from {nview.gameObject.name}."
                    : $"'{targetName}' was not in the allow list.");
            }
        }

        // ------------------------------------------------------------------
        // dl_list
        // ------------------------------------------------------------------
        private class ListCommand : ConsoleCommand
        {
            public override string Name => "dl_list";
            public override string Help => "– list the allow list for the focused private chest/door";

            public override void Run(string[] args, Terminal context)
            {
                ZNetView nview = GetFocusedNView();
                if (nview == null)
                {
                    context.AddString("No valid chest or door in crosshair.");
                    return;
                }

                List<string> list = AccessControl.GetAllowed(nview);
                bool priv = AccessControl.IsPrivate(nview);
                context.AddString($"{nview.gameObject.name}: private={priv}, allowed=[{string.Join(", ", list)}]");
            }
        }
    }
}

using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using BepInEx.Logging;

namespace DrakeLabs
{
    /// <summary>
    /// Helpers for reading and writing per-object access-control data on ZDOs.
    ///
    /// ZDO layout (stored on each Container / Door ZDO):
    ///   "DL_IsPrivate"  (bool)   – true = private, only allowed players can interact
    ///   "DL_Allowed"    (string) – comma-separated list of player names that are allowed
    ///
    /// Both values are set only by the owner / someone inside the ward.
    /// </summary>
    public static class AccessControl
    {
        // ZDO key names – hashed internally by Valheim via ZDO.GetHashCode / ZDO.Set overloads.
        public const string ZdoKeyIsPrivate = "DL_IsPrivate";
        public const string ZdoKeyAllowed   = "DL_Allowed";

        // Name of the key item that unlocks private containers/doors for a player.
        public const string KeyItemName = "DL_WardKey";

        // ---------------------------------------------------------------------------
        // Privacy flag helpers
        // ---------------------------------------------------------------------------

        /// <summary>Returns true when the object is marked private.</summary>
        public static bool IsPrivate(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return false;
            return nview.GetZDO().GetBool(ZdoKeyIsPrivate, false);
        }

        /// <summary>Toggles the private flag and returns the new state.</summary>
        public static bool TogglePrivate(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return false;
            bool current = IsPrivate(nview);
            bool next = !current;
            nview.GetZDO().Set(ZdoKeyIsPrivate, next);
            return next;
        }

        // ---------------------------------------------------------------------------
        // Per-object allowed-player list helpers
        // ---------------------------------------------------------------------------

        /// <summary>Returns the list of player names explicitly allowed on this object.</summary>
        public static List<string> GetAllowed(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return new List<string>();
            string raw = nview.GetZDO().GetString(ZdoKeyAllowed, string.Empty);
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            return raw.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                      .Select(n => n.Trim())
                      .ToList();
        }

        /// <summary>Saves the allowed-player list back to the ZDO.</summary>
        private static void SetAllowed(ZNetView nview, List<string> names)
        {
            nview.GetZDO().Set(ZdoKeyAllowed, string.Join(",", names));
        }

        /// <summary>
        /// Grants access to a named player.  Returns true if the list was changed.
        /// </summary>
        public static bool AddAllowed(ZNetView nview, string playerName)
        {
            if (nview == null || !nview.IsValid()) return false;
            var list = GetAllowed(nview);
            if (list.Contains(playerName)) return false;
            list.Add(playerName);
            SetAllowed(nview, list);
            return true;
        }

        /// <summary>
        /// Revokes access from a named player.  Returns true if the list was changed.
        /// </summary>
        public static bool RemoveAllowed(ZNetView nview, string playerName)
        {
            if (nview == null || !nview.IsValid()) return false;
            var list = GetAllowed(nview);
            if (!list.Remove(playerName)) return false;
            SetAllowed(nview, list);
            return true;
        }

        // ---------------------------------------------------------------------------
        // Core access check
        // ---------------------------------------------------------------------------

        /// <summary>
        /// Returns true when <paramref name="player"/> is allowed to interact with
        /// the object represented by <paramref name="nview"/>.
        ///
        /// Access is granted when ANY of the following is true:
        ///   1. The object is not private.
        ///   2. The player has ward access at the object's position
        ///      (they are on the ward list – standard Valheim behavior).
        ///   3. The player's name is in the object's own per-object allowed list.
        ///   4. The player carries the DL_WardKey item.
        /// </summary>
        public static bool CanAccess(ZNetView nview, Player player)
        {
            if (!IsPrivate(nview)) return true;
            if (player == null) return false;

            // Ward owners / permitted players pass through without any extra check.
            if (PrivateArea.CheckAccess(nview.transform.position, 0f, false))
                return true;

            // Per-object allow-list.
            if (GetAllowed(nview).Contains(player.GetPlayerName()))
                return true;

            // Key item check.
            if (PlayerHasKey(player))
                return true;

            return false;
        }

        // ---------------------------------------------------------------------------
        // Key item helpers
        // ---------------------------------------------------------------------------

        public static bool PlayerHasKey(Player player)
        {
            if (player == null) return false;
            return player.GetInventory().HaveItem(KeyItemName);
        }
    }
}

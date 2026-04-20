using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace DrakeLabs
{
    /// <summary>
    /// Access modes stored per-object in the ZDO under "DL_Mode" (int).
    ///
    ///   Public      – everyone may interact (default)
    ///   Private     – ward members only; key does NOT override
    ///   Named       – ward members + players in "DL_Allowed" list + key holders
    ///   KeyRequired – ward members + players carrying DL_WardKey
    ///
    /// Ward members always pass regardless of mode.
    /// Outside a ward, all modes pass (PrivateArea.CheckAccess returns true with no ward).
    ///
    /// Additional per-object ZDO field:
    ///   "DL_Allowed" (string) – comma-separated player names (used in Named mode)
    /// </summary>
    public enum AccessMode
    {
        Public      = 0,
        Private     = 1,
        Named       = 2,
        KeyRequired = 3,
    }

    public static class AccessControl
    {
        public const string ZdoKeyMode    = "DL_Mode";
        public const string ZdoKeyAllowed = "DL_Allowed";
        public const string KeyItemName   = "DL_WardKey";

        // -----------------------------------------------------------------------
        // Mode helpers
        // -----------------------------------------------------------------------

        public static AccessMode GetMode(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return AccessMode.Public;
            int raw = nview.GetZDO().GetInt(ZdoKeyMode, (int)AccessMode.Public);
            // Clamp to valid range in case of corrupt ZDO data.
            if (raw < 0 || raw > (int)AccessMode.KeyRequired) return AccessMode.Public;
            return (AccessMode)raw;
        }

        public static void SetMode(ZNetView nview, AccessMode mode)
        {
            if (nview == null || !nview.IsValid()) return;
            nview.GetZDO().Set(ZdoKeyMode, (int)mode);
        }

        /// <summary>
        /// Advances to the next mode in the cycle and returns it.
        /// Cycle order: Public → Private → Named → KeyRequired → Public …
        /// </summary>
        public static AccessMode CycleMode(ZNetView nview)
        {
            AccessMode current = GetMode(nview);
            AccessMode next = (AccessMode)(((int)current + 1) % 4);
            SetMode(nview, next);
            return next;
        }

        public static string ModeLabel(AccessMode mode)
        {
            switch (mode)
            {
                case AccessMode.Public:      return "<color=green>[Public]</color>";
                case AccessMode.Private:     return "<color=red>[Private]</color>";
                case AccessMode.Named:       return "<color=cyan>[Named]</color>";
                case AccessMode.KeyRequired: return "<color=yellow>[Key Required]</color>";
                default:                     return "[Unknown]";
            }
        }

        public static string NextModeLabel(AccessMode current)
        {
            AccessMode next = (AccessMode)(((int)current + 1) % 4);
            switch (next)
            {
                case AccessMode.Public:      return "Public";
                case AccessMode.Private:     return "Private";
                case AccessMode.Named:       return "Named";
                case AccessMode.KeyRequired: return "Key Required";
                default:                     return "?";
            }
        }

        // -----------------------------------------------------------------------
        // Allow-list helpers (used in Named mode)
        // -----------------------------------------------------------------------

        public static List<string> GetAllowed(ZNetView nview)
        {
            if (nview == null || !nview.IsValid()) return new List<string>();
            string raw = nview.GetZDO().GetString(ZdoKeyAllowed, string.Empty);
            if (string.IsNullOrEmpty(raw)) return new List<string>();
            return raw.Split(new char[] { ',' }, System.StringSplitOptions.RemoveEmptyEntries)
                      .Select(n => n.Trim())
                      .Where(n => !string.IsNullOrEmpty(n))
                      .ToList();
        }

        private static void SetAllowed(ZNetView nview, List<string> names)
        {
            nview.GetZDO().Set(ZdoKeyAllowed, string.Join(",", names));
        }

        /// <summary>Adds a player name to the Named allow list. Returns true when changed.</summary>
        public static bool AddAllowed(ZNetView nview, string playerName)
        {
            if (nview == null || !nview.IsValid()) return false;
            var list = GetAllowed(nview);
            if (list.Contains(playerName)) return false;
            list.Add(playerName);
            SetAllowed(nview, list);
            return true;
        }

        /// <summary>Removes a player name from the Named allow list. Returns true when changed.</summary>
        public static bool RemoveAllowed(ZNetView nview, string playerName)
        {
            if (nview == null || !nview.IsValid()) return false;
            var list = GetAllowed(nview);
            if (!list.Remove(playerName)) return false;
            SetAllowed(nview, list);
            return true;
        }

        // -----------------------------------------------------------------------
        // Key item helpers
        // -----------------------------------------------------------------------

        public static bool PlayerHasKey(Player player)
        {
            return player != null && player.GetInventory().HaveItem(KeyItemName);
        }

        // -----------------------------------------------------------------------
        // Core access check
        // -----------------------------------------------------------------------

        /// <summary>
        /// Returns true when <paramref name="player"/> may interact with the object.
        ///
        /// <paramref name="configKeyRequired"/> is the server-config flag that upgrades
        /// Public mode to also require a key (e.g. Plugin.RequireKeyForDoors).
        ///
        /// Logic:
        ///   • No ward in range OR player is a ward member → always allow
        ///   • Public     → allow (or require key when config flag is set)
        ///   • Private    → deny (key does NOT override)
        ///   • Named      → allow if name in allow list OR player carries key
        ///   • KeyRequired→ allow if player carries key
        /// </summary>
        public static bool CanAccess(ZNetView nview, Player player, bool configKeyRequired = false)
        {
            if (nview == null || !nview.IsValid()) return true;
            if (player == null) return false;

            // flash=false: silent check, no visual feedback at this point.
            if (PrivateArea.CheckAccess(nview.transform.position, 0f, false))
                return true;   // No ward here, or player is a ward member.

            // A ward is active and the player is not a member – enforce the mode.
            AccessMode mode = GetMode(nview);
            bool hasKey     = PlayerHasKey(player);

            switch (mode)
            {
                case AccessMode.Public:
                    return !configKeyRequired || hasKey;

                case AccessMode.Private:
                    return false;   // ward-member only; no key bypass

                case AccessMode.Named:
                    return GetAllowed(nview).Contains(player.GetPlayerName()) || hasKey;

                case AccessMode.KeyRequired:
                    return hasKey;

                default:
                    return false;
            }
        }
    }
}

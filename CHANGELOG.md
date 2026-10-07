# Changelog

## Unreleased (version not set yet)

- **The Locksmith key is temporarily removed** while it's rebuilt with Drakes Asset Forge. LockSmith always runs in simple mode (AltPlace+E); the `UseKey` setting is kept so synced configs line up, but has no effect for now. Keys already in inventories disappear until it returns.
- No longer uses DrakeModsLibs' old `ArtItemLoader` (removed in Libs 0.10.0), and the key art (`keys.bundle`, `masterkey*`) is no longer packaged.

## 0.5.0-beta.1

Hexium-only beta (Thunderstore skipped). Requires **DrakeModsLibs 0.9.12+**.


- **No key needed.** **AltPlace+E** (Shift+E by default) on a chest or door opens the Lock menu with every option for players who have access: ward members, the private-chest owner, and guests. Players without access don't see the hint, and AltPlace+E stays vanilla for them. A new synced `RequireKeyForSetup` (default off) brings back "owner tools need the key in hand".
  - New synced `UseKey` (default on). On is **enhanced mode**: the key is craftable, E with it opens the menu, hovers show guest names, and copied guests save on the key. Off is **simple mode**: the key item is not added to the game, hovers show only the state and the Shift+E hint, and guest names appear only in the Lock menu. Restart required after changing.
  - Copy/Paste without a key uses a clipboard that lasts for your play session. With the key in hand, names go on the key as before.
- **One Lock menu instead of key combos.** Press **AltPlace+E** on a chest or door, or **E** while holding the key. A small menu shows the current state (Public/Private, Join open, guest names) and only the buttons you can use: Enable LockSmith, Make public/private, Make personal/team, Open/Close Join, Copy/Paste guests, Leave access, Remove LockSmith.
  - Holding the key and pressing **E** no longer toggles anything by itself.
  - Removed the `ClearModifier` (Alt) and `SetupModifier` (Shift) chords and their config entries.
  - Hover text is now the state plus a single `Lock menu` hint.
  - The menu refreshes as changes confirm, and buttons grey out while a change is syncing, so double-clicks can't send twice.
- **One player in the Lock menu at a time** per door/chest (opening the chest/door itself is unchanged). Opening the menu takes a short lease and, like a vanilla chest, the current owner hands the piece over, so menu actions are local writes instead of requests bouncing between players. Others trying the menu, Join or Paste on that piece see "Name is using this — try again in a moment". The lease renews while the menu or its Yes/No is open, is released on close, and runs out after ~10s if the holder disconnects.
- **Joining is a Lock menu button now.** While a piece shows [Join open], **AltPlace+E → Join access**. Plain E always does the normal open (it just reminds you of Shift+E). Hovers show `[Shift+E] Join access` to players who can join, and nothing to players who can't.
- **Add nearby player** (new synced `EnableAddNearby`, default on): owners get an "Add *name*" button for the closest player within 5m who isn't a guest yet. Works with or without the key.
- **Leave always asks** "Leave access?", and the text says whether you can rejoin.
- Fixed an empty Lock menu (title, no buttons) after logout or a character swap. The fix is in DrakeModsLibs `DrakeWoodActionMenu`.
- **Yes/No popups replace "press again within 5s" warnings.**
  - Remove LockSmith always asks first.
  - Leave access asks only when Join is closed (you can't rejoin until the owner opens it). While Join is open you leave right away.
- **Leaving works any time**, not only while Join is open.
- **Shorter messages** on screen.
- **Settings cleanup.** `EnableKeyMode` is renamed `EnableManagedAccess` (the master switch for the Lock menu on placed pieces), and existing values carry over. All key settings (UseKey, RequireKeyForSetup, EnableKeyExtras, name and recipe) are grouped under **Key (advanced)**. Descriptions no longer mention the removed hotkeys.
- **Piece mode no longer clones world-only chests/doors.** Only pieces in a build tool's piece table count as donors, so dungeon, treasure and Dvergr-town props are left out. `dvergrtown_wood_door` is dropped from the defaults; the allow list still overrides.
- **Key extras off by default:** new synced `EnableKeyExtras` (default off) controls the key's inventory menu (Relabel / Grab nearby / Clear names / Clone key). Copy/Paste guests stays on (`EnableKeyPasses`) as menu buttons and Ctrl+C / Ctrl+V. The key no longer auto-renames itself `[Pass]` / `[Team]` unless extras are on.
- New key description. Configs still holding the old default text pick up the new one automatically.
- **Multiplayer fix:** the piece owner identified who sent a request through `Character.m_nview`, which is protected in the live game. It now uses `GetComponent<ZNetView>()`.

## 0.4.3

- **Multiplayer RPC hardening:** all piece changes (Join open/close, opt-in/out, public/private, team mode, clear, Ctrl+V paste) go through one owner-routed path (`PieceRpc`).
  - Fix Ctrl+V paste writing `locksmith_managed` from a non-owner — that forked the piece ZDO between players (names added but no pink Guests line for some players, flags flipping back when several people edited the same door/chest).
  - Requests to an unowned piece claim it first instead of broadcasting into nobody (opt-in "worked" but never landed on the list).
  - Requester re-sends until its own copy shows the change (covers ownership moving mid-RPC and a new owner writing over a change it never received). Requests are idempotent, so re-sends are safe.
  - "You joined" / "You left" / "Pasted" now show when the change is confirmed; a failure message shows if the owner never applies it.
  - Owner verifies the routed sender is the player id it claims.
  - Piece RPC re-registration no longer relies on try/catch around `Unregister`; chest/door public RPCs unregister before re-binding.

## 0.4.2

- Requires **DrakeModsLibs 0.9.7+** (`CompatHost`).
- **WardIsLove soft-compat:** detect WIL `WardMonoscript` wards for `RequireActiveWard` / permission checks (vanilla `PrivateArea.m_allAreas` stays empty under WIL-only). WIL's door/chest interact and No-access hover patches are removed and only called when the piece is not LockSmith-public, a public hammer clone, a team chest that player can open, a guest bypass, or **Join open** for a player WIL would deny (they can press E to opt in; the piece does not open).
- **ProtectiveWards soft-compat:** Door/Container Interact gated like WIL (public / guest / team / Join-open bypass PW's Priority.First block). Coverage uses PW `InsideEnabledPlayersArea` / `HasAccessToWardOrConnectedWard`. `LockSmithCompatApi.HasLocalWardAccess` / `IsInsideEnabledWard` for RenameIt and third parties. Does not gate ItemStand or other PW Interact surfaces.
- **Arcane Ward soft-compat:** Door/Container Interact gated around KG `ArcaneWardComponent.CheckFlag` (custom ward stack, not PrivateArea). Coverage via enabled fueled Arcane instances for `RequireActiveWard` / RenameIt hover when LockSmith is present.
- Fix hammer **Public** clones: hover/interact no longer re-enables ward checks from a missing `locksmith_public` ZDO, and ward bypass (including WIL) treats `*_public` prefabs as always open.
- **CompatibilityManager:** LockSmith ward facade over DrakeModsLibs **`CompatHost`** (per-plugin). Domain modules under `Compat/<Mod>/`. Public `LockSmith.API.LockSmithCompatApi`. SoftDependency only. See `docs/wiki/Compatibility-API.md`.
- Public hammer doors and chests show a plain hover line (default **[Public]**, synced `PublicPieceHoverText`). Hammer build description stays gold: always open, cannot be locked. `(public)` is appended after the donor name is localized (`PublicPieceNameSuffix`).
- Soft ward bridges are a **courtesy** (not actively monitored); please open an issue if something breaks — PRs and SoftDependency bridges via the Compatibility API are welcome.

## 0.4.1

- Fix multiplayer Join: re-bind piece RPCs after chunk reload (opt-in/out and Join open/close stopped applying).
- Fix Join toggle spam when ZDO sync lags - owner tool no longer flips open/open on stale state.

## 0.4.0

- **Piece mode (Phase 4):** `EnablePieceMode` discovers ward-locked chests/doors/gates (vanilla + mods) and registers always-public Hammer **Public** tab clones (`m_checkGuardStone = false`, no ZDO toggle).
- Skips donors that are already public (e.g. Christmas boxes) and private-family chests.
- Synced `PublicPieceAllowList` / `PublicPieceDenyList`; `PublicPieceNameSuffix` (default on) appends localized ` (public)` to clone names.
- Key mode ignores public clones (hover: always public). Restart after changing piece-mode registration settings.
- **RequireActiveWard** (default on): with no enabled ward covering a normal chest/door, LockSmith cannot manage or toggle it - including already-managed pieces. Personal/Team private-family chests stay exempt. Doors always need the ward up.
- Fix Valheim 1.0 center feedback: `AccessFeedback` no longer calls the removed 4-arg `Character.Message` (was red `MissingMethodException` on key Join/clear/clipboard).
- Drop duplicate Config Manager **masterkey** section from ArtItemLoader; use **03 Key** only for name / recipe.
- Requires **DrakeModsLibs 0.9.4+**.

## 0.3.9

- **Key passes:** inventory menu on the Locksmith key (Relabel, grab nearby, clear, clone at craft cost) via DrakeModsLibs wood UI / `DrakeTabHost`.
- **Clipboard:** Ctrl+C / Ctrl+V copy guest names onto the key and paste onto a looked-at piece.
- Inventory open chord uses **DrakeModsLibs** `Integration.InventoryOpenModifier` (removed LockSmith `KeyMenuOpenModifier`).
- Localized single-mode hint **configure lock tool**; Libs shows **customize** when 2+ tabs are usable (e.g. admin TagBypass + RenameIt). Soft `NoRename` + hard description lock on keys (description only is hard-locked).
- Requires **DrakeModsLibs 0.9.4+**.

## 0.3.8

- Repair flattened Thunderstore installs (Gale puts `keys.bundle` next to the DLL) into `Assets/Items/keys` so the Locksmith Key registers.
- Require **DrakeModsLibs 0.9.3** (Valheim 1.0 `Character.Message` signature ??? fixes pickup `MissingMethodException`).

## 0.3.7

- Require **DrakeModsLibs 0.9.2+** (ArtItemLoader folder packs) so the official Locksmith Key art loads for store installs.

## 0.3.6

- First public release (GitHub). Thunderstore/Hexium upload when store tokens are set.
- Official **Locksmith Key** from `Assets/Items/keys` ArtItem pack (`MasterKey` mesh, bone skull, flipped grip).
- Slim visual-only `keys.bundle` (no drop physics / particle VFX / packed Valheim scripts).
- Removed temporary give-key-on-spawn debug helper.
- README elevator pitch + store description; release categories ready for Thunderstore + Hexium.

## 0.3.5

- **Designate flag** (`locksmith_managed`): key claims a chest/door as a LockSmith piece (`EnableDesignate`).
- After designation, **Alt+E** public/private for anyone with access (ward or guest) ??? no key in hand (`EnableGuestPublicToggle`).
- Strangers never toggle. Public stays open-only (no Join/setup).
- Key still required for Team mode and Join open/close.
- Pre-0.3.5 pieces with public/guests/team state count as already designated.
- **Clear** with key: configurable `ClearModifier`+E (default **Alt**). Guests ??? confirm twice.
- **Join setup** with key: configurable `SetupModifier`+E (default **Shift**) so it no longer collides with Clear when AltPlace is rebound to Shift.
- Fix: Join hover no longer doubles `[Join open]` / `[E] Join access`.
- Guest names: keep playerId, show name; refresh Unknown when online.

## 0.3.4

- Guests on ward chests/doors can **Alt+E** toggle public/private without the Locksmith key (`EnableGuestPublicToggle`).
- Not for private-family chests. While public: Join/setup disabled ??? public means open.
- While Join is open, Alt+E still means Leave; when Join is closed, Alt+E unlocks/locks for everyone.
- Future (drakeVision): access setup menu UI.

## 0.3.3

- Guests [N] vs names-with-key; local TeamLabelColor; name resolve/refresh.

## 0.3.2

- Hover No access fix; opt-out; names; ward bypass for team guests.

## 0.3.1

- Ward-style Join; piece guests.

## 0.3.0

- Personal/Team private chests.

## 0.2.0

- Ward public/private.

## 0.0.1

- Internal prototype.

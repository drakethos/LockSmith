# Changelog

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

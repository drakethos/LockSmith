# LockSmith

Share chests and doors behind your ward without putting people on the ward. Designate a piece with the Locksmith key, then open it publicly or grant guest access to individuals. Also covers Personal/Team private chests for shared stashes.

Ward-friendly access for Valheim bases:

- **Public / private** on normal chests and doors (inside wards)
- **Personal / Team** on private-family chests (shared stash, no ward required)
- **Guests** on ward pieces via a Join list, works similar to ward opt-in
- **Easy to clear** without rebuild

Craft one **Locksmith Key**, designate a piece, then permitted players use **Alt+E** for public/private without holding the key. The key stays required for Team / Join setup.

**Requires (everyone on the server):** [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), [DrakeModsLibs](https://thunderstore.io/c/valheim/p/DrakeMods/DrakeModsLibs/)

Install folder:

```
BepInEx/plugins/DrakeMods-LockSmith/
```

## Version

**0.4.0** — Hammer **Public** tab (`EnablePieceMode`), **RequireActiveWard**, and Valheim 1.0 center-message fix. Requires DrakeModsLibs **0.9.4+**.

**0.3.9** — Key passes (inventory menu + Ctrl+C/V) and shared DrakeModsLibs inventory chord / tab host. Requires DrakeModsLibs **0.9.4+**.

## Craft the key

Default recipe: **1 Wood**, craftable from the inventory (no station). Configurable under `LockSmith` → Key (`KeyMaterials`, `KeyCraftingStation`).

## How to use

### Designate a piece (key)

1. Equip the **Locksmith Key**.
2. Look at a player-built chest, door, or gate.
3. **E** → **Enable LockSmith** (sets `locksmith_managed`). First use claims it.
4. Unequip the key for day-to-day use.
5. **ClearModifier+E** (default **Alt+E**, key equipped) → Clear LockSmith. Guests → confirm twice.
6. **SetupModifier+E** (default **Shift+E**, key equipped) → Open/close Join.

### Ward chests / doors — after designate

1. If you have **ward or guest** access: **Alt+E** → public / private (**no key**).
2. Strangers cannot toggle. **`[Public]`** means open — no Join/setup until private again.
3. Key still needed for **SetupModifier+E** Join open/close (guest list on that piece).

### Private chests — Personal / Team

1. Equip the key on a **private chest**.
2. **E** → Personal ↔ Team (creator) — also designates.
3. **SetupModifier+E** (default **Shift+E**) → open/close **Join** (opt-in ready). Key required.
4. Other character: **E** → Join access. Guests see names on hover; **Alt+E** → Leave access while Join is open.
5. Hover shows Open (not vanilla No access) when you have team/guest rights.

### Public hammer pieces (piece mode)

1. Host sets **`EnablePieceMode=true`** and restarts (clients need the same mod).
2. Hammer → **Public** category: always-open clones of ward-locked chests/doors (vanilla and discovered mod pieces).
3. Names append **`(public)`** when `PublicPieceNameSuffix` is on. Already-public vanilla pieces (e.g. Christmas boxes) are not duplicated.
4. Use `PublicPieceDenyList` / `PublicPieceAllowList` to trim or force donors. Key mode does not designate these clones.

### Ward chests / doors — partial guest access

1. Designate with key, then **SetupModifier+E** (with key) → open/close **Join**.
2. A player **not** on the ward presses **E** while Join is open → guest list.
3. Guests can open that chest/door without ward permit (`EnablePieceGuests`).
4. Guests (and ward members) can **Alt+E** public/private without the key. Private chests are excluded.

### Config (synced)

| Setting | Default | Notes |
| --- | --- | --- |
| `EnableChests` | on | Ward public/private chests |
| `EnableDoors` | on | Ward public/private doors/gates |
| `EnableGroupChests` | on | Team sharing on private-family chests |
| `EnablePersonalPause` | off | Personal↔Team pause switch (keeps names); off = team-only |
| `EnablePieceGuests` | on | Guest ACL on ward chests/doors |
| `EnableOptInAccess` | on | Ward-style Join open / E to opt in |
| `EnableKeyMode` | on | Craft / use the Locksmith key |
| `EnablePieceMode` | off | Hammer **Public** tab: always-open chest/door clones (vanilla + mods); no key/ZDO. Restart after change |
| `PublicPieceAllowList` | empty | Extra donor prefab names to clone (comma-separated). Restart |
| `PublicPieceDenyList` | empty | Donor prefab names never cloned. Restart |
| `PublicPieceNameSuffix` | on | Append localized `(public)` to clone display names. Restart |
| `EnableGuestPublicToggle` | on | Permitted Alt+E public/private (no key) |
| `EnableDesignate` | on | Key must Enable LockSmith before no-key Alt+E |
| `RequireActiveWard` | on | No manage/toggle on normal chests/doors unless inside an enabled ward (private chests exempt) |
| `ClearModifier` | Alt | Local — key + modifier+E clears LockSmith |
| `SetupModifier` | Shift | Local — key + modifier+E opens/closes Join |
| `TeamLabelColor` | `#FF00FF` | Local only — color for Team/Guests labels |

Inventory key menu uses **DrakeModsLibs** `Integration.InventoryOpenModifier` (default Shift) + right-click — shared with RenameIt when both claim an item.

## Multiplayer

Required on **server and all clients** (`EveryoneMustHaveMod`). Feature toggles and key recipe sync via DrakeModsLibs config sync.

## What’s next

See **[`docs/drakeVision.md`](docs/drakeVision.md)**.

| Version | Focus |
| --- | --- |
| **0.4** | Placeable public chests/doors — **shipped in 0.4.0** |
| **0.3–0.4** | Compatibility with devcommands mod / WardIsLove / other ward mods |
| **Later** | Quest/swamp keys, deeper RenameIt, unwarded locks, Halvar chests, … |

## Feedback

Bug reports and “this would help our RP server” notes welcome — [GitHub issues](https://github.com/drakethos/LockSmith/issues).

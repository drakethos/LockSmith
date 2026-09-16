# LockSmith

Ward-friendly access for Valheim bases:

- **Public / private** on normal chests and doors (inside wards)
- **Personal / Team** on private-family chests (shared stash, no ward required)
- **Guests** on ward pieces via a Join list

Craft one **Locksmith Key**, designate a piece, then permitted players use **Alt+E** for public/private without holding the key. The key stays required for Team / Join setup.

**Requires (everyone on the server):** [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), [DrakeModsLibs](https://thunderstore.io/c/valheim/p/DrakeMods/DrakeModsLibs/)

Install folder:

```
BepInEx/plugins/DrakeMods-LockSmith/
```

## Version

**0.3.6** — first Thunderstore ship: official Locksmith Key art, designate + permitted Alt+E, Personal/Team, guests.

| Band | Meaning (this mod) |
| --- | --- |
| **0.2.x** | Chests + doors public/private |
| **0.3.x** | + Personal/Team + guests + designate / no-key toggle |
| **0.4** | Placeable public hammer pieces (no key) |
| **0.5+** | Quest keys, RenameIt, Halvar, … |
| **1.0** | Long-running server confidence |

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
| `EnableGroupChests` | on | Personal/Team private chests |
| `EnablePieceGuests` | on | Guest ACL on ward chests/doors |
| `EnableOptInAccess` | on | Ward-style Join open / E to opt in |
| `EnableKeyMode` | on | Craft / use the Locksmith key |
| `EnablePieceMode` | off | Reserved — Phase 4 hammer publics |
| `EnableGuestPublicToggle` | on | Permitted Alt+E public/private (no key) |
| `EnableDesignate` | on | Key must Enable LockSmith before no-key Alt+E |
| `ClearModifier` | Alt | Local — key + modifier+E clears LockSmith |
| `SetupModifier` | Shift | Local — key + modifier+E opens/closes Join |
| `TeamLabelColor` | `#FF00FF` | Local only — color for Team/Guests labels |

## Multiplayer

Required on **server and all clients** (`EveryoneMustHaveMod`). Feature toggles and key recipe sync via DrakeModsLibs config sync.

## What’s next

See **[`docs/drakeVision.md`](docs/drakeVision.md)**.

| Version | Focus |
| --- | --- |
| **0.4** | Placeable public chests/doors in a hammer tab (no key, no ZDO toggle) |
| **0.3–0.4** | Compat with admin open / WardIsLove / other ward mods |
| **Later** | Quest/swamp keys, RenameIt, unwarded locks, Halvar chests, … |

## Feedback

Bug reports and “this would help our RP server” notes welcome — [GitHub issues](https://github.com/drakethos/LockSmith/issues).

# LockSmith

Ward-friendly **public / private** access for player-built **chests** and **doors/gates**.

Craft one Locksmith key, equip it like a tool, press **E** to flip a piece between ward-locked and open-to-everyone. No ward permit required for guests on public pieces. No new server rulebook for the core loop.

**Requires:** [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), [DrakeModsLibs](https://thunderstore.io/c/valheim/p/DrakeMods/DrakeModsLibs/)

Install folder:

```
BepInEx/plugins/DrakeMods-LockSmith/
```

## Version

**0.2.0** — first public cut.

| Band | Meaning (this mod) |
| --- | --- |
| **0.2.x** | Chests + doors public/private, play-tested. Ready for Thunderstore. |
| **0.3–0.4** | Optional access modes / integrations while the core stays stable. |
| **0.5** | “Desired access story” feels complete enough for wider servers. |
| **1.0** | Long-running server confidence (API settled, few surprise edge cases). |

Version tracks **completeness of the intended access model** and **how hard it has been tested**, not commit count.

## How to use

1. Build a **Key Maker** and craft a **Locksmith Key** (or use your server’s recipe settings).
2. **Equip** the key (tool-style — same idea as hammer / cultivator).
3. Look at a player-built chest, door, or gate.
4. Hover shows **Make public** / **Make private** instead of Open.
5. Press **E** to toggle. Unequip the key to open/use the piece normally.

Public pieces show **`[Public]`** on hover. Personal chests (`PrivacySetting.Private`) are never touched.

### Config (synced)

| Setting | Default | Notes |
| --- | --- | --- |
| `EnableChests` | on | Phase 1 |
| `EnableDoors` | on | Phase 2 (doors + gates) |
| `EnableKeyMode` | on | Craft / use the Locksmith key |
| `EnablePieceMode` | off | Reserved — public hammer clones |
| Key name / materials / station | KeyMaker + Bronze/Wood | Synced via DrakeModsLibs |

## What this is good for (today)

- Shop stalls and “take what you need” chests inside a ward
- Inn doors / gateways guests can use without being on the ward
- Keeping the rest of the base private without teaching players a custom key economy

## What’s next

Version goals and backlog live in **[`docs/drakeVision.md`](docs/drakeVision.md)** (pull into plans). Short map:

| Version | Focus |
| --- | --- |
| **0.3** | Team / Group private chests (finish vanilla `Group` privacy) |
| **0.4** | Placeable public chests/doors in a hammer tab (no key, no ZDO toggle) |
| **0.3–0.4** | Compat with admin open / WardIsLove / other ward mods |
| **Later** | Quest/swamp keys, RenameIt, unwarded locks, Halvar chests, … |

## Design notes (why public/private first)

The original “hand someone a key, oh no they lost it” fantasy is strong for **roleplay**, weak as the **only** model:

| Approach | Use case | Cost |
| --- | --- | --- |
| **Public flag (shipped)** | Shops, inns, open gates | Almost none — ward still protects the rest |
| **Named access on the piece** | One ally, one room | UX + storage; no item loss |
| **Physical key item** | Rentals, quests, “locksmith” RP | Loss, theft, duplication, server drama |

LockSmith keeps the shipped path boring-on-purpose. Extra modes should enhance RP **without** making every server invent key-custody rules.

## Feedback

Bug reports and “this would help our RP server” notes welcome — especially if you tried public chests/doors on a real ward base.

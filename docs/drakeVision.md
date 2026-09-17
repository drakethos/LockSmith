# LockSmith — drakeVision

Living target list for version goals. Pull sections into a Cursor plan when starting a slice; do not treat this as an active implementation plan by itself.

**North star:** useful access options without overwhelming players or forcing RP servers into a thick rulebook.

**Shipped:** `0.3.0` — ward chests/doors public/private + Personal/Team private chests.  
**Also shipped:** `0.3.9` — key passes (inventory menu + clipboard) + Libs inventory chord / tab host.

---

## Version map (priority order)

| Version | Name | Intent |
| --- | --- | --- |
| **0.3** | Phase 3 — team / private chest access | **Shipped in 0.3.0** |
| **0.3.9** | Key passes + inventory tab host | **Shipped** |
| **0.4** | Phase 4 — placeable public pieces (no key) | Next |
| **0.3–0.4** | Compatibility hardening | Parallel concern while building 0.4 |
| **0.5+** | Future options | Quest keys, deeper RenameIt, unwarded locks, Halvar, etc. |

Bump patch (`0.3.1`) for fixes; bump minor when a version goal’s *core* lands and is play-tested.

---

## 0.3 — Phase 3 (shipped in 0.3.0)

**Theme:** Finish vanilla’s unfinished **Group** privacy and make private-style chests actually shareable.

### Shipped

1. **Team / party private chest** — ZDO team mode + member list; `CheckAccess` allows creator + members.
2. **UX** — creator + key: **E** Personal↔Team; **Alt+E** add/remove nearest player (~5m).
3. **0.2 path intact** — ward public/private unchanged; private-family chests are a second surface.

### Explicitly not 0.3 (still)

- Hammer public clone tab (→ 0.4)
- Quest / swamp-style keys (→ later)
- Unwarded generic locks (→ later)

---

## 0.4 — Phase 4 (placeable public, no key)

**Theme:** Servers that don’t want the key tool still get public chests/doors — as **prefabs**, not ZDO toggles.

### Goals

1. **No-key offering**  
   - Loop vanilla **and modded** chest/door prefabs (config allow/deny lists as needed).  
   - Build **public** variants: `m_checkGuardStone = false` (and whatever else makes them honestly public).

2. **Hammer UX**  
   - New hammer **tab / category** (whatever the current Valheim 1.x build menu calls it).  
   - Category holds **Public chests** and **Public doors/gates** — same pieces players know, placeable.

3. **No ZDO drama**  
   - Public-by-prefab → no desync of `locksmith_public`, no RPC toggle for this path.  
   - Key-mode (0.2) and piece-mode (0.4) can coexist via config.

### Done when

- Host can disable key mode, enable piece mode, and place public chests/doors from the hammer tab.  
- Modded pieces appear only when discovery/config says so (no surprise broken prefabs).

---

## 0.3–0.4 — Compatibility hardening (parallel)

Known past pain when touching wards / open paths:

| Area | Risk | Stance |
| --- | --- | --- |
| **Devcommands / infinite hammer / admin open** | Admins (or tools) open through wards anyway; patches fight each other | Detect or fail soft; don’t assume we’re the only Interact prefix |
| **WardIsLove** | Changes what “warded / permitted” means | Prefer vanilla `PrivateArea` public APIs; optional compat hooks later |
| **Azu / other ward mods** | Same — alternate ward stacks | Document “works with vanilla wards first”; test matrix before calling 0.4 done |

**Rule:** every new open/bypass path gets a short compat note in this file or the behavior doc when we learn a conflict.

---

## Later (0.5+ / backlog) — options, not commitments

Ordered loosely by interest; any can jump a version if a plan pulls it in.

### Quest / swamp-door keys (admin-configured)

- Not “Joe’s personal key / oh no we lost it.”  
- **Quest items:** admin-defined keys ↔ doors/chests (loot rooms, dungeon props, event locks).  
- Undestructible / non-teleport / whatever fits quest design.  
- Same idea for **quest chests**.  
- High RP and server-event value; keep **opt-in** and config-driven so casual servers ignore it.

### DrakeMods RenameIt integration

- Soft: labels, tags, hover cues on public / group pieces.  
- Hard binding of access to rename text only if optional and clear.
- **Inventory chord (0.3.9):** Libs owns `InventoryOpenModifier` + usable-tab count. LockSmith supplies localized “configure lock tool”; RenameIt supplies its phrase; 2+ usable (e.g. admin bypass on a key) → Libs “customize”. Feature menus stay mod-owned; tab strip when usable ≥ 2.

### Unwarded locks

- Lock a normal box/door outside a ward.  
- Weaker than Group private chests unless breakability is solved (`WearNTear`).  
- Parked until Group + public-prefab paths are solid.

### Halvar chests (placeable / usable)

- Collectable Halvar chests that become **real placeable containers** after unlock (incl. servers/alts where Halvar isn’t around).  
- Separate content spike; amazing if clean, not required for 0.3/0.4.

### Access setup menu (UI)

- Current Join / Alt+E / key flows are a **good start**.  
- **Later:** a proper Valheim-native panel to configure piece access (guests, public/private, Join) without memorizing keybinds.  
- Keep the hotkey path for power users.

### Clone access onto a reusable key (physical pass) — **shipped in 0.3.9**

- Inventory menu (RenameIt-looking, via DrakeModsLibs wood UI / tab host).
- Ctrl+C / Ctrl+V copy/paste names onto key / piece.
- Relabel, grab nearby, clear (confirm), clone at craft cost.
- Teach via key description + yellow interact hints (Libs chord).

---

## What we are optimizing for

- **Options without overwhelm** — config flags and hammer categories beat twelve key types in the default path.  
- **Prefab public > clever ZDO** when the goal is “always public.”  
- **List / Group access > lost personal keys** for friends and parties.  
- **Quest keys** for authored content; **not** for everyday base sharing.

---

## How to use this in a plan

When starting work, copy the relevant version section into the plan frontmatter todos, e.g.:

```text
Pull from docs/drakeVision.md → ## 0.3 — Phase 3
Exclusions: 0.4 hammer tab, quest keys, Halvar, unwarded locks
Done criteria: (from that section)
```

Update this file when a version ships or a backlog item is promoted/killed.

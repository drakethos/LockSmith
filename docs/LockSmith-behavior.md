# LockSmith — behavior

Status: **0.3.7** — requires DrakeModsLibs 0.9.2+ (ArtItemLoader); official key art; designate + permitted Alt+E; ward public/private; Personal/Team; piece guests.

Version targets / backlog: see [`drakeVision.md`](drakeVision.md).

## One line

Key designates a chest/door as LockSmith. After that, permitted players Alt+E public/private without holding the key; key still required for Team / Join setup.

## Intended

### Designate (0.3.5)

- Equip key → **E** on an eligible chest/door sets `locksmith_managed` (first use claims the piece) when `EnableDesignate` is on.
- Designated ward pieces: **Alt+E** public↔private if you have ward or guest access — **no key required** (`EnableGuestPublicToggle`).
- Random public players never toggle.
- Key still required for Personal↔Team and Join open/close.
- Legacy pieces that already have public/guests/team/opt-in state are treated as managed.

### Phase 1 — Ward chests

After designate: key **E** or permitted **Alt+E** toggles `locksmith_public` + instance `m_checkGuardStone`. Personal (`PrivacySetting.Private`) chests are **not** on this path.

### Phase 2 — Doors / gates

Same as chests for `Door` + `EnableDoors`.

### Phase 3 — Personal / Team private chests

- Targets private-family containers only (`m_privacy` Private or Group).
- ZDO `locksmith_group_mode` (0 personal / 1 team) + `locksmith_group_members` (`id|Name;…`).
- Creator + key: **E** Personal↔Team; **Alt+E** opens Join (opt-in).
- `Container.CheckAccess` prefix: team mode → creator or listed member; personal → vanilla creator-only.
- Does **not** use `locksmith_public` or ward bypass.

### Guests on ward pieces (0.3.1+)

- Creator/ward + key **Alt+E**: open/close Join on a designated ward chest/door.
- Other players **E** join when Join is open; **Alt+E** Leave while Join is open.
- Guests open without ward permit (`EnablePieceGuests`).

### Guest / permitted public toggle (0.3.4–0.3.5)

- On designated ward chests/doors: **Alt+E** public↔private (`EnableGuestPublicToggle`) for ward members and guests.
- While Join is open, Alt+E still means Leave.
- Public = open only — no Join/setup until locked private again.
- Future: access setup menu (drakeVision).

### Phase 4+ — see drakeVision

Hammer public prefabs, quest keys, access menu, etc.

## Assets

`Assets/Items/keys/` ArtItem pack: `masterkey.json` + `keys.bundle` (`MasterKey` prefab) + `masterkey.png`. Optional `Assets/masterkey_icon.png`.

## Fragile patch sites

| Target | Why |
| --- | --- |
| `Container.CheckAccess` | Team mode allow creator + member list |
| `Container.Interact` | Key → ward toggle or team UX (`alt` for members) |
| `Container.GetHoverText` | Key hover for both surfaces |
| `Container.TakeAll` | Ward public bypass |
| `Container.Awake` | RPCs + ZDO sync |
| `Door.Interact` / `GetHoverText` / `Awake` | Phase 2 doors |

Business logic in `Access/`. Do not patch `Humanoid.UseItem` for this UX.

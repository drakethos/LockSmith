# LockSmith — behavior

Status: **0.6.0-beta.1** (key removed; was 0.4.0 when the phases below were written) — requires DrakeModsLibs 0.9.4+; Hammer Public piece mode; RequireActiveWard; AccessFeedback Valheim 1.0 fix; key passes; designate + permitted Alt+E; ward public/private; Personal/Team; piece guests.

Version targets / backlog: see [`drakeVision.md`](drakeVision.md).

## One line

**AltPlace+E** opens the **Lock menu** on a chest/door, for players with access only. Every action is a button there; nothing toggles silently.

## Lock menu (Unreleased)

- `UI/PieceAccessMenu.cs`. It builds context-sensitive buttons and calls the existing services (`ChestAccessService` / `DoorAccessService.RequestSetPublic`, `GroupChestService.RequestSetTeamMode`, `PieceGuestAccess.TryRequestJoinToggle` / `RequestOptOutSelf`, `PieceClearService.RequestClear`,).
- AltPlace + E opens it only when there is a button for that player. Otherwise vanilla runs. Owner tools need no key. Hover hints use `PieceAccessMenu.HasLockMenu`, so strangers never see one.
- One at a time: `Access/PieceMenuLock.cs`. The menu asks the ZDO owner for a lease (`locksmith_menu_holder` / `_name` / `_until`, network ticks, 10s). On yes the owner does `ForceSendZDO` + `SetOwner(requester)` like `Container.RPC_RequestOpen`, so the holder's `PieceRpc` actions apply locally. The holder renews every 3s while the menu or Yes/No is open and releases on close. Join (E), Ctrl+V/menu paste and owner-side `ApplyOptInSelf` refuse while another player holds it. Opening the chest/door is not gated.
- Plain E (no key) = vanilla open. While Join is open for a non-guest it only shows the `[AltPlace+E] Join access` reminder; joining is the menu's **Join access** button.
- Hover hints come from `PieceAccessMenu.LockMenuHint`: the Join hint when that's the only option, the Lock menu hint when there are more, nothing otherwise.
- `EnableAddNearby`: an owner button that merges the closest non-guest player (5m, `NearbyPlayers.FindNearest`) onto the guest list.
- The menu re-renders every 0.25s while open (state plus pending RPCs). A button greys out while its `PieceRpc` request is pending. It closes beyond 6m, when the piece unloads, or when the player walks away.
- Yes/No (`DrakeConfirmPanel`): Remove LockSmith and Leave access, both always.
- The sections below describe the state model. Wherever they mention modifier chords, read that as a menu button now.

## Intended (historical phases)

The phases below were written when the key existed. Read "key + E" as "AltPlace+E" now; the key is gone in 0.6.0-beta.1.

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
- While Join is open, a player WardIsLove would deny still gets the join hover and can press **E** to opt in. The door or chest does not open for them.

### Guest / permitted public toggle (0.3.4–0.3.5)

- On designated ward chests/doors: **Alt+E** public↔private (`EnableGuestPublicToggle`) for ward members and guests.
- While Join is open, Alt+E still means Leave.
- Public = open only — no Join/setup until locked private again.
- Future: access setup menu (drakeVision).

### Phase 4 — Public hammer pieces (`EnablePieceMode`)

- Auto-discovers ward-locked `Piece` + `Container`/`Door` prefabs (vanilla + mods); clones to Hammer **Public**.
- Clone prefabs set `m_checkGuardStone = false` at registration — no `locksmith_public` ZDO for this path.
- Skips already-public donors (`m_checkGuardStone` already false) and private-family chests.
- Optional `(public)` name suffix (`PublicPieceNameSuffix`) on the hammer piece and on door/chest hover names; AllowList / DenyList.
- Placed clones show a plain hover line, default **[Public]** (`PublicPieceHoverText`). Hammer description is gold: always open, cannot be locked.
- Key / guest / clear paths ignore public clones.

### Later — see drakeVision

Quest keys, access menu, etc.

## Fragile patch sites

| Target | Why |
| --- | --- |
| `Container.CheckAccess` | Team mode allow creator + member list |
| `Container.Interact` | AltPlace+E → Lock menu or Join (`alt` for members) |
| `Container.GetHoverText` | Hover hint for both surfaces |
| `Container.TakeAll` | Ward public bypass |
| `Container.Awake` | RPCs + ZDO sync |
| `Door.Interact` / `GetHoverText` / `Awake` | Phase 2 doors |

Business logic in `Access/`. Do not patch `Humanoid.UseItem` for this UX.

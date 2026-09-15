# LockSmith — behavior

Status: **0.2.0** — Phase 1 (chests) + Phase 2 (doors/gates) shipped and play-tested.

Version targets / backlog: see [`drakeVision.md`](drakeVision.md) (pull into plans; not an active implementation plan by itself).


## One line

Ward-aware public/private access for player-built chests and doors/gates via a craftable key. Phase 3: hammer public clones plus per-person key logging/storage.

## Intended

### Phase 1 — Chests

**Chosen UX (only):** equip the Locksmith key as a tool. While equipped, chest interact changes from Open to Make public / Make private (ward members only). Press E to toggle. Unequip to open again. Not “use item on chest.”

- Synced config: `EnableChests`, key name / materials / crafting station, `EnableKeyMode`, `EnableDoors`, `EnablePieceMode` (reserved for Phase 3).
- ZDO flag `locksmith_public` (0/1). Server/owner applies after a ward-permission check by player id.
- Hover: with key equipped + ward access, vanilla Open is replaced by the toggle prompt and `[Public]`/`[Private]`. Without the key, anyone still sees `[Public]` when public.
- Open: if the chest is public, client ward check is bypassed for that interact. Personal (`PrivacySetting.Private`) chests are never modified.
- No named player lists. No guest-held pass for Phase 1.

### Phase 2 — Doors / gates

Same UX as chests — key equipped, ward members toggle, `[Public]`/`[Private]` indicator, open path bypasses ward when public.

- Config: `EnableDoors` (default on).
- Same `locksmith_public` ZDO flag + instance `Door.m_checkGuardStone`.
- **Out of scope for Phase 2:** hammer clones, piece mode, per-person keys, guest passes, named player lists.

### Phase 3 — Hammer spike + per-person keys (not implemented yet)

- **Hammer spike:** when `EnablePieceMode` is on, hammer gets public piece clones from a config list (`m_checkGuardStone = false`). Independent of key-mode toggle UX.
- **Per-person key logging and storing:** record and persist which players hold / are granted keys (and related access), so access can be tracked per character rather than only ward + public flag.
- Exact grant/revoke UX and storage shape are defined when Phase 3 starts.

## Assets

Load `Assets/drake`. Register only `KeyMaker` and one key (`MasterKey`, fallback `PublicKey`). Do not register unrelated prefabs. Do not load `ploam` for content.

## TEMP — REMOVE BEFORE RELEASE

`DebugTemp/TempGiveKeyOnLoad.cs` gives the Locksmith key on local spawn if missing. Delete that file (and its csproj compile entry) before final release. Search tag: `REMOVE_BEFORE_RELEASE:TempGiveKeyOnLoad`.

## Fragile patch sites (update resilience)

All Harmony targets live under `Patches/`:

| Target | Why |
| --- | --- |
| `Container.Interact` | Equipped key → toggle; sync `m_checkGuardStone` from ZDO |
| `Container.GetHoverText` | Equipped key replaces Open; sync guard stone |
| `Container.TakeAll` | Bypass ward when public |
| `Container.Awake` | Register RPC; apply ZDO → `m_checkGuardStone` |
| `Door.Interact` | Equipped key → toggle; sync `m_checkGuardStone` from ZDO |
| `Door.GetHoverText` | Equipped key replaces Open; sync guard stone |
| `Door.Awake` | Register RPC; apply ZDO → `m_checkGuardStone` |

Persistence: ZDO `locksmith_public`. Runtime gate: instance `m_checkGuardStone` (false when public) on `Container` / `Door`. Not WearNTear.`m_triggerPrivateArea` (damage/ward flash only).

Business logic is in `Access/`, not in patch methods. Do not patch `Humanoid.UseItem` for this UX.

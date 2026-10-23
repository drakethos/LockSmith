# LockSmith — ⚠️ Moving to Hexium

> [!IMPORTANT]
> **DrakeMods is moving to Hexium.**
>
> **0.5.0 is the final LockSmith release on Thunderstore.** No new DrakeMods will be released here, and existing mods will get only limited support on Thunderstore from now on.
>
> For the latest updates, newest versions and future mods, follow us on **Hexium**:
> 👉 **[DrakeMods on Hexium](https://valheim.hexium.gg/?q=DrakeMods)**
>
> These Thunderstore listings will be deprecated after this.

> [!TIP]
> ### 🎨 DrakesReskinIt is finally coming, on Hexium!
> Give any item a new inventory icon, a new equipped model and custom colors, then save your looks as presets.
> Lock down your base with LockSmith, then make the gear inside it look the part. ReskinIt runs on the same DrakeModsLibs you already have installed.
> 👉 **[DrakesReskinIt on Hexium](https://valheim.hexium.gg/mods/DrakeMods/DrakesReskinIt)**

Share chests and doors behind your ward without putting people on the ward. Open a door or chest to the public, or give guest access to individual players. Also covers Personal/Shared private chests for shared stashes.

Ward-friendly access for Valheim bases:

- **Public / private** on normal chests and doors (inside wards)
- **Personal / Shared** on private-family chests (shared stash, no ward required)
- **Team** switch on managed chests and doors: players on the same server team as the builder get in (needs the optional DrakesServerTeams mod)
- **Guests** on ward pieces via a Join list, works similar to ward opt-in
- **Easy to clear** without rebuild

Press **Shift+E** (the game's AltPlace key + Use) on a chest or door you have access to. A small **Lock menu** opens with buttons for everything. No key needed, and no combos to learn.

**Requires (everyone on the server):** [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), [DrakeModsLibs](https://thunderstore.io/c/valheim/p/DrakeMods/DrakeModsLibs/)

Install folder:

```
BepInEx/plugins/DrakeMods-LockSmith/
```

## Version

**0.6.0-beta.3** — Hexium beta. Smart tabs (Lock children only while on Lock; Ward/Groups after Enable LockSmith). Requires DrakeModsLibs **1.0.0-beta.3**.

**0.5.0** — **Final Thunderstore release** (updates continue on [Hexium](https://valheim.hexium.gg/?q=DrakeMods)). One Lock menu (Shift+E) instead of key combos, no key needed, one player in the menu at a time, Join as a menu button, Add nearby player, Yes/No popups. The Locksmith key is removed; the Lock menu replaces it. Requires DrakeModsLibs **0.10.0+**.

**0.4.3** — Multiplayer RPC hardening: Join/opt-in, paste, and public/team toggles confirm and re-send until they land; fixes split guest lists between players. Requires DrakeModsLibs **0.9.7+**.

**0.4.2** — Soft ward-stack bridges (WardIsLove / ProtectiveWards / Arcane Ward) + Compatibility API. Requires DrakeModsLibs **0.9.7+**.

**0.4.1** — Multiplayer Join fix (RPC re-bind + Join toggle sync). Requires DrakeModsLibs **0.9.4+**.

**0.4.0** — Hammer **Public** tab (`EnablePieceMode`), **RequireActiveWard**, and Valheim 1.0 center-message fix. Requires DrakeModsLibs **0.9.4+**.

**0.3.9** — Key passes (inventory menu + Ctrl+C/V) and shared DrakeModsLibs inventory chord / tab host. Requires DrakeModsLibs **0.9.4+**.

## How to use

> **Shift+E on a chest or door opens the Lock menu** if you have access there (ward member, chest owner, or guest).
> No access? You won't see it, and Shift+E does the normal thing.
> If a door or chest shows **[Join open]**, press **Shift+E** → **Join access**. Plain E always does the normal open.

### The Lock window

Shift+E opens one window with sub-tabs. **Hold** Shift+E for the wheel instead (see below).

| Tab | What it does | Who sees it |
| --- | --- | --- |
| **Piece** | Public and Join switches, everyone on the piece as a chip (click a name for what they can do, × removes them), nearby players with an Add button, Copy / Paste people, Leave access, Remove LockSmith | Anyone with access; the controls you get depend on your role |
| **Ward** | Ward on/off, who may switch it, people on the ward with their role, remove one person | Only while you stand in a ward you have a role in |
| **Groups** | Saved groups: add one to this chest or door (online or not), add people to it, save the people here or nearby, set what the group's people may do | Ward members and the owner |

The cog in the header holds settings for this device only (nearby list on/off, wheel on/off).

**People and permissions.** A plain guest can open and use the piece and leave it. Click a person's name to also let them *let others on* (add people, open Join, add nearby players) and/or *switch public / private*. A group carries the same two settings and hands them out when you add it.

**The wheel.** Hold Shift+E (about a quarter of a second) on a chest or door. Aim the mouse at a slice and let go of E to choose it: Join, Public/Private, Ward, people on the piece, Menu. Let go in the middle to cancel. A quick tap opens the window.

**Copy and paste.** Copy the people on one piece and paste them onto another, from the Piece tab or with Ctrl+C / Ctrl+V while looking at a piece you manage. The clipboard lasts for the play session.

**Groups file.** `config/LockSmith/groups.txt`, one group per line: `squad (add, toggle): Mira=7231, Tovald=881`. The names after `=` are player ids, which is what lets you add people who are offline. Edit it by hand if you like.

**Ward off.** If a ward is switched off, the menu still opens with just the switch to turn it back on. A guest switching the ward off blocks Join until a member or the owner turns it on again.

### One at a time

Only one player can have the Lock menu open on a door or chest. Anyone else trying the menu or Join there sees "Name is using this — try again in a moment". Opening the door or chest itself is never blocked.

### Leaving

**Shift+E** → **Leave access** → Yes. The popup tells you whether you can come back (only while Join is open).

### Private chests — Personal / Shared

Private chests work the same way. The owner gets Make personal/shared (when `EnablePersonalPause` is on) and Open/Close Join. Shared members can open it, and use **Shift+E** → Leave access.

### Public hammer pieces (piece mode)

1. Host sets **`EnablePieceMode=true`** and restarts (clients need the same mod).
2. Hammer → **Public** category: always-open clones of ward-locked chests/doors (vanilla and discovered mod pieces).
3. Names append **`(public)`** when `PublicPieceNameSuffix` is on. A placed clone shows **[Public]** (`PublicPieceHoverText`). The hammer description is gold: always open, cannot be locked. Already-public vanilla pieces (e.g. Christmas boxes) are not duplicated.
4. Use `PublicPieceDenyList` / `PublicPieceAllowList` to trim or force donors. The Lock menu does not manage these clones.

### Config (synced)

| Setting | Default | Notes |
| --- | --- | --- |
| `EnableChests` | on | Ward public/private chests |
| `EnableDoors` | on | Ward public/private doors/gates |
| `EnableGroupChests` | on | Shared mode on private-family chests |
| `EnablePersonalPause` | off | Personal↔Shared pause switch (keeps names); off = shared-only |
| `EnablePieceGuests` | on | Guest ACL on ward chests/doors |
| `EnableOptInAccess` | on | Ward-style Join open / E to opt in |
| `EnableManagedAccess` | on | Master switch for the Lock menu on placed doors/chests (was `EnableKeyMode`; value carries over) |
| `EnablePieceMode` | off | Hammer **Public** tab: always-open chest/door clones (vanilla + mods); no key/ZDO. Restart after change |
| `PublicPieceAllowList` | empty | Extra donor prefab names to clone (comma-separated). Restart |
| `PublicPieceDenyList` | empty | Donor prefab names never cloned. Restart |
| `PublicPieceNameSuffix` | on | Append localized `(public)` to clone display names. Restart |
| `PublicPieceHoverText` | `[Public]` | Plain hover line on placed Public doors and chests. Empty uses `[Public]` |
| `EnableGuestPublicToggle` | on | Make public/private in the Lock menu (ward members and guests) |
| `EnableAddNearby` | on | Lock menu button to add the closest player (5m) to the guest list |
| `EnableDesignate` | on | A piece must be enabled (Lock menu → **Enable LockSmith**) before other options show |
| `RequireActiveWard` | on | No manage/toggle on normal chests/doors unless inside an enabled ward (private chests exempt) |
| `TeamLabelColor` | `#FF00FF` | Local only — color for Shared/Guests labels |


## Compatibility API

Optional ward / cheat mods (**WardIsLove**, **ProtectiveWards**, **Arcane Ward**, DevCommands, …) are soft-loaded via a **CompatibilityManager**. Missing mods never block LockSmith. Prefer **one** third-party ward stack at a time.

These built-ins are a **courtesy** so common packs work out of the box. They are **not** actively monitored against every upstream ward update. If something breaks, please [open an issue](https://github.com/drakethos/LockSmith/issues) with logs — I may look into it when I can. You’re also welcome to contribute a fix (pull request or your own SoftDependency bridge via the API below).

Third-party authors can register their own module **without** waiting on a LockSmith release:

- SoftDependency GUID: `com.drakesworkshop.locksmith`
- API: `LockSmith.API.LockSmithCompatApi.Register(ICompatModule)` (also `HasLocalWardAccess` / `IsInsideEnabledWard`)
- Full guide: **[`docs/wiki/Compatibility-API.md`](docs/wiki/Compatibility-API.md)** (also intended for the [GitHub wiki](https://github.com/drakethos/LockSmith/wiki))

Pull requests that add or harden a `Compat/<YourMod>/` module are welcome. You’re also free to keep the bridge in your own mod with SoftDependency + `Register`.

## Multiplayer

Required on **server and all clients** (`EveryoneMustHaveMod`). Feature toggles sync via DrakeModsLibs config sync.

## What’s next

See **[`docs/drakeVision.md`](docs/drakeVision.md)**.

| Version | Focus |
| --- | --- |
| **0.4** | Placeable public chests/doors — **shipped in 0.4.0** |
| **0.3–0.4** | Compatibility with devcommands mod / WardIsLove / other ward mods |
| **Later** | Quest/swamp keys, deeper RenameIt, unwarded locks, Halvar chests, … |

## Feedback

Bug reports and “this would help our RP server” notes welcome — [GitHub issues](https://github.com/drakethos/LockSmith/issues).

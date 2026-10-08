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

Share chests and doors behind your ward without putting people on the ward. Open a door or chest to the public, or give guest access to individual players. Also covers Personal/Team private chests for shared stashes.

Ward-friendly access for Valheim bases:

- **Public / private** on normal chests and doors (inside wards)
- **Personal / Team** on private-family chests (shared stash, no ward required)
- **Guests** on ward pieces via a Join list, works similar to ward opt-in
- **Easy to clear** without rebuild

Press **Shift+E** (the game's AltPlace key + Use) on a chest or door you have access to. A small **Lock menu** opens with buttons for everything. No key needed, and no combos to learn.

**Requires (everyone on the server):** [BepInExPack Valheim](https://thunderstore.io/c/valheim/p/denikson/BepInExPack_Valheim/), [Jotunn](https://thunderstore.io/c/valheim/p/ValheimModding/Jotunn/), [DrakeModsLibs](https://thunderstore.io/c/valheim/p/DrakeMods/DrakeModsLibs/)

Install folder:

```
BepInEx/plugins/DrakeMods-LockSmith/
```

## Version

**0.5.0** — **Final Thunderstore release** (updates continue on [Hexium](https://valheim.hexium.gg/?q=DrakeMods)). One Lock menu (Shift+E) instead of key combos, no key needed, one player in the menu at a time, Join as a menu button, Add nearby player, Yes/No popups. The Locksmith key is temporarily removed. Requires DrakeModsLibs **0.10.0+**.

**0.4.3** — Multiplayer RPC hardening: Join/opt-in, paste, and public/team toggles confirm and re-send until they land; fixes split guest lists between players. Requires DrakeModsLibs **0.9.7+**.

**0.4.2** — Soft ward-stack bridges (WardIsLove / ProtectiveWards / Arcane Ward) + Compatibility API. Requires DrakeModsLibs **0.9.7+**.

**0.4.1** — Multiplayer Join fix (RPC re-bind + Join toggle sync). Requires DrakeModsLibs **0.9.4+**.

**0.4.0** — Hammer **Public** tab (`EnablePieceMode`), **RequireActiveWard**, and Valheim 1.0 center-message fix. Requires DrakeModsLibs **0.9.4+**.

**0.3.9** — Key passes (inventory menu + Ctrl+C/V) and shared DrakeModsLibs inventory chord / tab host. Requires DrakeModsLibs **0.9.4+**.

## The Locksmith key

The key is **temporarily removed** in 0.5.0 while it's being rebuilt. Everything works without it through the Shift+E Lock menu. Key settings (`UseKey`, `RequireKeyForSetup`, `EnableKeyExtras`, key recipe) are kept so synced configs line up, but have no effect until the key returns.

## How to use

> **Shift+E on a chest or door opens the Lock menu** if you have access there (ward member, chest owner, or guest).
> No access? You won't see it, and Shift+E does the normal thing.
> If a door or chest shows **[Join open]**, press **Shift+E** → **Join access**. Plain E always does the normal open.
>
> The Locksmith key is temporarily removed (see [The Locksmith key](#the-locksmith-key)), so the Shift+E menu is the way in for everyone.

### The Lock menu

The menu shows what's going on (Public / Private, Join open, guest names) and only the buttons you can use right now:

| Button | Who |
| --- | --- |
| Join access | Anyone, while the piece shows [Join open] |
| Enable LockSmith | Ward member, first time on a piece |
| Make public / Make private | Ward member, or a guest on that piece |
| Make personal / Make team | Private-chest owner (`EnablePersonalPause`) |
| Open Join / Close Join | Ward member, or private-chest owner |
| Add *name* (nearby player) | Ward member or owner, if `EnableAddNearby` is on |
| Copy guests / Paste guests | Ward member or owner |
| Leave access | Guest on that piece |
| Remove LockSmith… | Ward member, or private-chest owner |

Copied guests last for your play session.

### Sharing a door or chest with someone

1. **Shift+E** on the door → **Enable LockSmith** → **Open Join**.
2. Your friend walks up and presses **Shift+E** → **Join access**. They're on the guest list and can now open it without being on your ward. (Or, if they're standing next to you, use **Add *name*** in your menu.)
3. Back in the menu → **Close Join** when everyone's in.
4. Same people on another door? **Copy guests** here, then **Paste guests** there.

### One at a time

Only one player can have the Lock menu open on a door or chest. Anyone else trying the menu, Join or Paste there sees "Name is using this — try again in a moment". Opening the door or chest itself is never blocked.

### Leaving

**Shift+E** → **Leave access** → Yes. The popup tells you whether you can come back (only while Join is open).

### Private chests — Personal / Team

Private chests work the same way. The owner gets Make personal/team (when `EnablePersonalPause` is on) and Open/Close Join. Team members can open it, and use **Shift+E** → Leave access.

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
| `EnableGroupChests` | on | Team sharing on private-family chests |
| `EnablePersonalPause` | off | Personal↔Team pause switch (keeps names); off = team-only |
| `EnablePieceGuests` | on | Guest ACL on ward chests/doors |
| `EnableOptInAccess` | on | Ward-style Join open / E to opt in |
| `EnableManagedAccess` | on | Master switch for the Lock menu on placed doors/chests (was `EnableKeyMode`; value carries over) |
| `EnablePieceMode` | off | Hammer **Public** tab: always-open chest/door clones (vanilla + mods); no key/ZDO. Restart after change |
| `PublicPieceAllowList` | empty | Extra donor prefab names to clone (comma-separated). Restart |
| `PublicPieceDenyList` | empty | Donor prefab names never cloned. Restart |
| `PublicPieceNameSuffix` | on | Append localized `(public)` to clone display names. Restart |
| `PublicPieceHoverText` | `[Public]` | Plain hover line on placed Public doors and chests. Empty uses `[Public]` |
| `EnableGuestPublicToggle` | on | Make public/private in the Lock menu without the key (ward members and guests) |
| `EnableKeyPasses` | on | Copy/Paste guests: menu buttons and Ctrl+C / Ctrl+V |
| `UseKey` | on | *No effect while the key is removed.* Enhanced mode: the Locksmith key exists (craftable; E with it opens the menu; hover shows guest names; stores copied guests, Ctrl+C/V). Off = simple mode: key not added to the game, Shift+E menu only. Restart after change |
| `EnableAddNearby` | on | Lock menu button to add the closest player (5m) to the guest list |
| `RequireKeyForSetup` | off | *No effect while the key is removed.* On = Enable / Join / Personal-Team / Paste / Remove need the key in hand |
| `EnableKeyExtras` | off | *No effect while the key is removed.* Key inventory menu: Relabel, Grab nearby, Clear names, Clone key |
| `EnableDesignate` | on | A piece must be enabled (Lock menu → **Enable LockSmith**) before other options show |
| `RequireActiveWard` | on | No manage/toggle on normal chests/doors unless inside an enabled ward (private chests exempt) |
| `TeamLabelColor` | `#FF00FF` | Local only — color for Team/Guests labels |

With `EnableKeyExtras` on, the key's inventory menu uses **DrakeModsLibs** `Integration.InventoryOpenModifier` (default Shift) + right-click — shared with RenameIt when both claim an item.

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

# LockSmith automated tests

DrakesTestLab suite for LockSmith. It runs unattended; results go to `<profile>\BepInEx\config\DrakesTestLab\results\latest.html`.

- `suites/LockSmith/`: access rules through the menu's request calls (`gen_locksmith_suite.py`).
- `suites/LockSmith-Menu/`, `suites/LockSmith-PieceMode/`: the Lock menu driven like a player, and the always-public hammer pieces (`gen_locksmith_deep.py`).
- The suites are generated (do not edit the JSON by hand): `python gen_locksmith_suite.py`, `python gen_locksmith_deep.py`. The build runs it and copies the suite into the drakeTest profiles; `Tests/` is never packaged.
- `../Testing/LockSmithTestHooks.cs`: the `lock.*` test hooks the suite calls. They use the mod's real code paths; no reference to DrakesTestLab, and without it installed they are never called.

- `suites/LockSmith-WardIsLove/`, `-ArcaneWard/`, `-ProtectiveWards/`: ward-mod integration, 8 tests each (`gen_locksmith_wards.py`): mod active, ward coverage, strangers locked out, public / Join / guests / doors / ward members / ward off.
  Each needs its ward mod loaded and the others off. `Run-WardPasses.ps1 [-Passes "None,WardIsLove,ArcaneWard,ProtectiveWards,All"] [-Profile drakeWardTest]` switches the mods off and on (renames their DLLs to `.dll.old`, puts them back at the end) and keeps each pass's results in `ward-pass-results/`.
  The suites turn off debug mode and god mode: Arcane Ward and Protective Wards let an admin in debug / god mode through every ward.
  Build into the ward profile with `dotnet msbuild LockSmith.csproj -p:ProfileName=drakeWardTest`.

- `suites/LockSmith-Rpc/` and `-Rpc-WardIsLove/`, `-ArcaneWard/`, `-ProtectiveWards/` (`gen_locksmith_rpc.py`): a virtual second player (TestLab `peerJoin`) sends LockSmith's real routed RPCs to this game:
  strangers refused, forged player ids, ward members, Join / Leave, guest permissions (Toggle / Add), remove / clear, doors. Each ward mod's own per-player access path is exercised.
  Passes: `RpcNone`, `RpcWardIsLove`, `RpcArcaneWard`, `RpcProtectiveWards` in `Run-WardPasses.ps1`. `LockSmith-Net/` (hand-written) covers forged sender ids, a client with no character and the menu lease.

## Run

```powershell
..\DrakesWorkshop\DrakesTestLab\tools\Run-TestLabSuites.ps1 -Suites LockSmith -Screenshots Always
```

or everything at once: `..\DrakesWorkshop\scripts\Test-All.ps1`. In game: F9 > Tests.

## Covered

- Warded chests and doors keep strangers out; public opens them, private locks again (pressing E for real as another player)
- Only the owner / ward members manage; RequireActiveWard
- Join: open Join, join as another player, guest gets in, removal, Join closed, guests switched off, Join switched off
- Team chests (private family) with personal pause
- EnableChests, EnableManagedAccess, EnableGuestPublicToggle
- The Lock menu itself: Enable LockSmith (EnableDesignate), per-person permissions (Let others on, Switch public / private),
  Copy / Paste people, Leave access, Remove LockSmith (inline confirms), the Ward tab (switch on/off, hidden from strangers),
  Groups (groups.txt: add a group with its permissions, save the current guests as a group), one player in the menu at a time,
  what a plain guest sees
- Piece mode: public chest and door clones open for anyone inside a ward and are not managed by the Lock menu
  (EnablePieceMode is switched on at game start through the suite's `launchConfig`)

Owned pieces and items are built or crafted through the game; other players are simulated with play-as (Alice, Bob, Carol, Nacho-VIP).
Every test also checks the mod logged no errors.

## Not covered (manual or later)

- The wheel (hold Shift+E) and the settings cog (nearby list, wheel on/off; this device only).
- Piece mode allow / deny lists and the name suffix switch (each needs another game start).
- Add Nearby and "Save people nearby" (need a second real player within 5 m).
- The ward-off state blocking Join, who-can-switch-the-ward levels for guests.
- Ward is Love / Protective Wards / Arcane Ward compatibility: those mods are not in the test profile.
- Real multiplayer (RPCs between two clients): manual.

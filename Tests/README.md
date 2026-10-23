# LockSmith automated tests

DrakesTestLab suite for LockSmith. It runs unattended; results go to `<profile>\BepInEx\config\DrakesTestLab\results\latest.html`.

- `suites/LockSmith/`: access rules through the menu's request calls (`gen_locksmith_suite.py`).
- `suites/LockSmith-Menu/`, `suites/LockSmith-PieceMode/`: the Lock menu driven like a player, and the always-public hammer pieces (`gen_locksmith_deep.py`).
- The suites are generated (do not edit the JSON by hand): `python gen_locksmith_suite.py`, `python gen_locksmith_deep.py`. The build runs it and copies the suite into the drakeTest profiles; `Tests/` is never packaged.
- `../Testing/LockSmithTestHooks.cs`: the `lock.*` test hooks the suite calls. They use the mod's real code paths; no reference to DrakesTestLab, and without it installed they are never called.

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

# LockSmith automated tests

DrakesTestLab suite for LockSmith. It runs unattended; results go to `<profile>\BepInEx\config\DrakesTestLab\results\latest.html`.

- `suites/LockSmith/`: the tests (generated, do not edit by hand).
- `gen_locksmith_suite.py`: writes the suite (`python gen_locksmith_suite.py`). The build runs it and copies the suite into the drakeTest profiles; `Tests/` is never packaged.
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

Owned pieces and items are built or crafted through the game; other players are simulated with play-as (Alice, Bob, Carol, Nacho-VIP).
Every test also checks the mod logged no errors.

## Not covered (manual or later)

- The Lock menu / wheel UI itself (the actions are tested through the same request calls the menu makes).
- Piece mode (Public hammer clones) and its allow/deny lists: need a restart to apply.
- Add Nearby (needs a second real player within 5 m), text-file groups, the guest clipboard.
- Ward is Love / Protective Wards / Arcane Ward compatibility: those mods are not in the test profile.
- Real multiplayer (RPCs between two clients): manual.

# Changelog

## 0.3.5

- **Designate flag** (`locksmith_managed`): key claims a chest/door as a LockSmith piece (`EnableDesignate`).
- After designation, **Alt+E** public/private for anyone with access (ward or guest) — no key in hand (`EnableGuestPublicToggle`).
- Strangers never toggle. Public stays open-only (no Join/setup).
- Key still required for Team mode and Join open/close.
- Pre-0.3.5 pieces with public/guests/team state count as already designated.
- **Clear** with key: configurable `ClearModifier`+E (default **Alt**). Guests → confirm twice.
- **Join setup** with key: configurable `SetupModifier`+E (default **Shift**) so it no longer collides with Clear when AltPlace is rebound to Shift.
- Fix: Join hover no longer doubles `[Join open]` / `[E] Join access`.
- Guest names: keep playerId, show name; refresh Unknown when online.

## 0.3.4

- Guests on ward chests/doors can **Alt+E** toggle public/private without the Locksmith key (`EnableGuestPublicToggle`).
- Not for private-family chests. While public: Join/setup disabled — public means open.
- While Join is open, Alt+E still means Leave; when Join is closed, Alt+E unlocks/locks for everyone.
- Future (drakeVision): access setup menu UI.

## 0.3.3

- Guests [N] vs names-with-key; local TeamLabelColor; name resolve/refresh.

## 0.3.2

- Hover No access fix; opt-out; names; ward bypass for team guests.

## 0.3.1

- Ward-style Join; piece guests.

## 0.3.0

- Personal/Team private chests.

## 0.2.0

- Ward public/private.

## 0.0.1

- Internal prototype.

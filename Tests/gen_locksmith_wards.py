"""Writes the ward-mod integration suites (LockSmith-WardIsLove / -ArcaneWard / -ProtectiveWards) into Tests/suites
(run: python gen_locksmith_wards.py). Each suite needs its ward mod loaded; run it with only that mod enabled
(Run-WardPasses.ps1 cycles the mods)."""
import json, os, shutil

SUITES = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites')
GUID = 'com.drakemods.locksmith'
NOERR = {'check': 'noErrors', 'source': 'LockSmith'}


def H(name, expect=None, label=None, contains=None, notContains=None, stop=False, tag=None, **args):
    s = {'do': 'hook', 'name': 'lock.' + name}
    if tag:
        s['as'] = tag
    if args:
        s['args'] = args
    for k, v in (('expect', expect), ('contains', contains), ('notContains', notContains), ('label', label)):
        if v is not None:
            s[k] = v
    if stop:
        s['stop'] = True
    return s


def play(who):
    return {'do': 'playAs', 'who': who}


def check(kind, label=None, **f):
    s = {'do': 'check', 'check': kind, **f}
    if label:
        s['label'] = label
    return s


def wait(s=0.3):
    return {'do': 'wait', 'seconds': s}


def opens(label, expect=True, tag='chest'):
    """Press E on the chest as the current player and check whether the chest window opened."""
    return [
        {'do': 'closeUi'},
        {'do': 'face', 'target': tag},
        {'do': 'interact', 'target': tag},
        wait(0.4),
        check('uiVisible', label, name='InventoryGui', expect=expect),
        {'do': 'closeUi'},
    ]


def join(label, tag='chest'):
    """Join as the current player the real way: open the Lock menu (AltPlace+E), click Join access, check the guest list."""
    return [H('openMenu', True, 'AltPlace+E opens the Lock menu', stop=True, target=tag), wait(0.4),
            H('menuOpen', True, 'the Lock menu is still open', ),
            H('menuActions', label='what the menu offers Bob (informational)', target=tag),
            H('wardAllows', False, 'Bob is not on the ward', target=tag),
            {'do': 'click', 'button': 'Join access'}, wait(0.5),
            H('closeMenu'), H('isGuest', True, label, target=tag)]


def door_moves(label, expect=True):
    """Press E on the door; it opened when its saved state is no longer 0."""
    s = [{'do': 'face', 'target': 'door'}, {'do': 'interact', 'target': 'door'}, wait(0.6)]
    s.append(check('zdo', label, target='door', key='state', default='0', **({'notContains': '0'} if expect else {'expect': '0'})))
    return s


MODS = {
    'WardIsLove': dict(prefix='WIL', guid='Azumatt.WardIsLove', module='WardIsLove', arcane=False),
    'ProtectiveWards': dict(prefix='PW', guid='shudnal.ProtectiveWards', module='ProtectiveWards', arcane=False),
    'ArcaneWard': dict(prefix='AW', guid='kg.ArcaneWard', module='ArcaneWard', arcane=True,
                       # Arcane pushes strangers out of the ward (flings Bob away and the Lock menu closes): switch that protection off
                       extra=[{'guid': 'kg.ArcaneWard', 'section': 'General', 'key': 'DisabledProtection', 'value': 'Push_Players'}]),
}


def ward_step(m, enabled=True, permitted=()):
    """The ward of this mod, owned by the current player (Alice)."""
    if m['arcane']:
        return H('spawnArcaneWard', tag='ward', radius=15, enabled=enabled, permitted=','.join(permitted))
    s = {'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': enabled, 'radius': 15, 'as': 'ward'}
    if permitted:
        s['permitted'] = list(permitted)
    return s


def base(m, piece='piece_chest_wood', tag='chest', enabled=True, permitted=()):
    """Alice builds the piece with the hammer and puts this mod's ward around it."""
    return [H('debugMode', False, 'debug mode (admin override) off: wards treat us like a normal player', on=False), play('Alice'), {'do': 'place', 'prefab': piece, 'dz': 2.5, 'as': tag}, ward_step(m, enabled, permitted), wait(0.6)]


def build(mod, m):
    tests = {}
    P = m['prefix']

    def T(n, title, steps):
        tid = f'{P}-{n:02d}'
        tests[tid] = {'name': f'{tid} {title}', 'description': f'{mod} integration', 'steps': steps, 'asserts': [NOERR]}

    T(1, f'{mod} is active in LockSmith', [
        H('debugMode', False, on=False),
        H('hasCompat', True, f'LockSmith activated its {mod} module', id=m['module']),
        H('compatActive', label='active ward modules (informational)'),
    ])
    T(2, 'LockSmith sees the ward and who is let in', [
        *base(m),
        H('debugModeIs', False, 'debug mode (admin override) is really off', ),
        H('insideWard', True, f'the chest is inside the enabled {mod} ward', target='chest'),
        H('wardAllows', True, 'Alice (owner) has ward access', target='chest'),
        play('Bob'),
        H('wardAllows', False, 'Bob (stranger) has no ward access', target='chest'),
        play('me'),
    ])
    T(3, 'a warded chest keeps strangers out, owner gets in', [
        *base(m),
        *opens('Alice (ward owner) opens her chest'),
        play('Bob'),
        *opens('Bob cannot open it', expect=False),
        play('me'),
    ])
    T(4, 'make public lets a stranger past the ward, private locks again', [
        *base(m),
        H('isEligible', True, 'a player-built chest in the ward can be managed', target='chest'),
        H('setPublic', True, 'Alice makes it public', target='chest', on=True),
        play('Bob'),
        *opens(f'Bob opens the public chest ({mod} must not block it)'),
        play('Alice'),
        H('setPublic', False, 'Alice makes it private again', target='chest', on=False),
        play('Bob'),
        *opens('Bob is locked out again', expect=False),
        play('me'),
    ])
    T(5, 'Join: a stranger joins, gets in, is removed', [
        *base(m),
        H('openJoin', True, 'Alice opens Join', target='chest', on=True),
        play('Bob'),
        *join('Bob joins with AltPlace+E'),
        *opens(f'the guest opens the chest ({mod} must let a guest through)'),
        play('Alice'),
        H('removeGuest', True, 'Alice removes Bob', target='chest', who='Bob'),
        play('Bob'),
        *opens('removed guests are locked out', expect=False),
        play('me'),
    ])
    T(6, 'doors: locked for strangers, public opens them', [
        *base(m, piece='wood_door', tag='door'),
        play('Bob'),
        *door_moves('Bob cannot open the warded door', expect=False),
        play('Alice'),
        H('setPublic', True, 'Alice makes the door public', target='door', on=True),
        play('Bob'),
        *door_moves('now Bob can open it'),
        play('me'),
    ])
    T(7, 'a ward member keeps full access and may manage', [
        *base(m, permitted=('Bob',)),
        play('Bob'),
        H('wardAllows', True, 'Bob is on the ward and has access', target='chest'),
        *opens('a ward member opens the chest'),
        H('setPublic', True, 'a ward member can make it public', target='chest', on=True),
        H('setPublic', False, 'and private again', target='chest', on=False),
        play('Carol'),
        *opens('Carol (not on the ward) is still locked out', expect=False),
        play('me'),
    ])
    T(8, 'ward switched off: no protection, no managing', [
        *base(m, enabled=False),
        H('insideWard', False, 'a switched-off ward does not count as covering', target='chest'),
        H('setPublic', False, 'nothing to manage outside an active ward', target='chest', on=True),
        play('Bob'),
        *opens('Bob can open it (the ward is off)'),
        play('me'),
    ])
    return tests


def write(mod, m, tests):
    out = os.path.join(SUITES, f'LockSmith-{mod}')
    cfg = {
        '02 Features/EnableChests': 'true', '02 Features/EnableDoors': 'true', '02 Features/EnableGroupChests': 'true',
        '02 Features/EnablePersonalPause': 'false', '02 Features/EnablePieceGuests': 'true', '02 Features/EnableOptInAccess': 'true',
        '02 Features/EnableGuestPublicToggle': 'true', '02 Features/EnableDesignate': 'false', '02 Features/EnableManagedAccess': 'true',
        '02 Features/EnableAddNearby': 'true', '02 Features/RequireActiveWard': 'true',
    }
    suite = {
        'name': f'LockSmith-{mod}',
        'description': f'LockSmith with {mod}: ward coverage, locked / public / Join / guests / doors / ward members / ward off. Run with only {mod} enabled.',
        'requireMods': [{'guid': GUID, 'minVersion': '0.5.0'}, {'guid': m['guid']}],
        'configOverrides': [{'guid': 'com.drakemods.testlab', 'section': 'TestControls', 'key': 'AutoEnableDebugMode', 'value': 'false'}] + m.get('extra', []) + [{'guid': GUID, 'section': k.split('/')[0], 'key': k.split('/')[1], 'value': v} for k, v in cfg.items()],
        'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 0, 'noBuild': 'off', 'hideUnlockPopups': 1},
        'resetBetweenTests': True,
    }
    if os.path.isdir(out):
        shutil.rmtree(out)
    os.makedirs(out)
    with open(os.path.join(out, 'suite.json'), 'w', encoding='utf-8') as f:
        json.dump(suite, f, indent=2, ensure_ascii=False)
    for tid, t in tests.items():
        with open(os.path.join(out, f'{tid}.json'), 'w', encoding='utf-8') as f:
            json.dump(t, f, indent=2, ensure_ascii=False)


if __name__ == '__main__':
    for mod, m in MODS.items():
        t = build(mod, m)
        write(mod, m, t)
        print(mod, len(t), 'tests')

"""Writes the DrakesTestLab suite for LockSmith into Tests/suites/LockSmith (run: python gen_locksmith_suite.py).
The build copies it into the test profiles; Tests/ is never packaged."""
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'LockSmith')
GUID = 'com.drakemods.locksmith'
NOERR = {'check': 'noErrors', 'source': 'LockSmith'}


def H(name, expect=None, label=None, contains=None, notContains=None, stop=False, **args):
    s = {'do': 'hook', 'name': 'lock.' + name}
    if args:
        s['args'] = args
    for k, v in (('expect', expect), ('contains', contains), ('notContains', notContains), ('label', label)):
        if v is not None:
            s[k] = v
    if stop:
        s['stop'] = True
    return s


def C(values):
    return {'do': 'setConfig', 'values': values}


def play(who):
    return {'do': 'playAs', 'who': who}


def check(kind, label=None, **f):
    s = {'do': 'check', 'check': kind, **f}
    if label:
        s['label'] = label
    return s


def wait(s=0.3):
    return {'do': 'wait', 'seconds': s}


def alice_base(piece='piece_chest_wood', tag='chest', ward=True):
    """Alice builds the piece with the hammer (real placement) inside her own enabled ward."""
    steps = [play('Alice'), {'do': 'place', 'prefab': piece, 'dz': 2.5, 'as': tag}]
    if ward:
        steps.append({'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15, 'as': 'ward'})
    steps.append(wait(0.5))
    return steps


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


def join(label, tag='chest', expect=True):
    """Join as the current player the real way: open the Lock menu (AltPlace+E) and click Join access, then check the guest list."""
    if not expect:
        return [H('openMenu', label='AltPlace+E (informational: may open with nothing to do)', target=tag), wait(0.4),
                check('uiVisible', 'no Join access button is offered', name='Join access', expect=False),
                H('closeMenu'), H('isGuest', False, label, target=tag)]
    return [H('openMenu', True, 'AltPlace+E opens the Lock menu', stop=True, target=tag), wait(0.4),
            {'do': 'click', 'button': 'Join access'}, wait(0.5),
            H('closeMenu'), H('isGuest', True, label, target=tag)]


def door_moves(label, expect=True):
    """Press E on the door; it opened when its saved state is no longer 0."""
    s = [{'do': 'face', 'target': 'door'}, {'do': 'interact', 'target': 'door'}, wait(0.6)]
    s.append(check('zdo', label, target='door', key='state', default='0', **({'notContains': '0'} if expect else {'expect': '0'})))
    return s


tests = {}


def T(tid, title, steps, desc=''):
    tests[tid] = {'name': f'{tid} {title}', 'description': desc, 'steps': steps, 'asserts': [NOERR]}


T('L-01', 'a warded chest keeps strangers out', [
    *alice_base(),
    *opens('Alice (ward owner) opens her chest'),
    play('Bob'),
    check('wardAccess', 'Bob has no ward access', expect=False),
    *opens('Bob cannot open it', expect=False),
    play('me'),
])

T('L-02', 'make public lets anyone open, private locks again', [
    *alice_base(),
    H('isEligible', True, 'a player-built chest in a ward can be managed', target='chest'),
    H('setPublic', True, 'Alice makes it public', target='chest', on=True),
    check('hoverText', 'the hover says it is public', target='chest', contains='ublic'),
    play('Bob'),
    *opens('Bob can open a public chest'),
    H('setPublic', True, 'Bob cannot make it private (still public)', target='chest', on=False),
    play('Alice'),
    H('setPublic', False, 'Alice makes it private again', target='chest', on=False),
    play('Bob'),
    *opens('Bob is locked out again', expect=False),
    play('me'),
])

T('L-03', 'strangers cannot make a chest public', [
    *alice_base(),
    play('Bob'),
    H('setPublic', False, 'Bob tries to make it public: refused', target='chest', on=True),
    *opens('still locked', expect=False),
    play('me'),
])

T('L-04', 'RequireActiveWard: no ward, no managing', [
    *alice_base(ward=False),
    H('setPublic', False, 'outside a ward the chest cannot be made public', target='chest', on=True),
    C({'02 Features/RequireActiveWard': 'false'}),
    H('setPublic', label='observed with RequireActiveWard off (informational)', target='chest', on=True),
    play('me'),
])

T('L-05', 'Join: owner opens Join, a player joins, gets in, is removed', [
    *alice_base(),
    H('openJoin', True, 'Alice opens Join', target='chest', on=True),
    play('Bob'),
    *join('Bob joins with AltPlace+E'),
    *opens('a guest can open the chest'),
    H('canManageGuests', False, 'a guest cannot manage the guest list', target='chest'),
    play('Alice'),
    H('guests', label='Bob is listed', contains='Bob', target='chest'),
    H('removeGuest', True, 'Alice removes Bob', target='chest', who='Bob'),
    play('Bob'),
    *opens('removed guests are locked out', expect=False),
    play('me'),
])

T('L-06', 'Join closed: nobody can join', [
    *alice_base(),
    play('Bob'),
    *join('Join is not open: AltPlace+E does not add Bob', expect=False),
    play('me'),
])

T('L-07', 'guests switched off', [
    *alice_base(),
    H('openJoin', True, target='chest', on=True),
    play('Bob'),
    *join('Bob joins'),
    C({'02 Features/EnablePieceGuests': 'false'}),
    *opens('with EnablePieceGuests off a guest is not let in', expect=False),
    play('me'),
])

T('L-08', 'Join switched off', [
    C({'02 Features/EnableOptInAccess': 'false'}),
    *alice_base(),
    H('openJoin', False, 'Join cannot be opened', target='chest', on=True),
    play('me'),
])

T('L-09', 'doors: locked for strangers, public opens them', [
    *alice_base(piece='wood_door', tag='door'),
    play('Bob'),
    *door_moves('Bob cannot open Alice\'s warded door', expect=False),
    play('Alice'),
    H('setPublic', True, 'Alice makes the door public', target='door', on=True),
    play('Bob'),
    *door_moves('now Bob can open it'),
    play('me'),
])

T('L-10', 'team chest: personal pause', [
    C({'02 Features/EnablePersonalPause': 'true'}),
    *alice_base(piece='piece_chest_private', ward=False),
    H('setTeam', True, 'Alice makes her private chest a team chest', target='chest', on=True),
    H('openJoin', True, 'Alice opens Join on it', target='chest', on=True),
    play('Bob'),
    *join('Bob joins the team with AltPlace+E'),
    *opens('a team member opens the private chest'),
    play('Alice'),
    H('setTeam', False, 'Alice makes it personal', target='chest', on=False),
    play('Bob'),
    *opens('personal: friends are locked out (names kept)', expect=False),
    H('isGuest', True, 'Bob is still on the list', target='chest'),
    play('Alice'),
    H('setTeam', True, 'Alice makes it team again', target='chest', on=True),
    play('Bob'),
    *opens('team again: Bob is back in'),
    play('me'),
])

T('L-11', 'features off: chests and managed access', [
    *alice_base(),
    C({'02 Features/EnableChests': 'false'}),
    H('setPublic', False, 'EnableChests off: chests cannot be made public', target='chest', on=True),
    C({'02 Features/EnableChests': 'true', '02 Features/EnableManagedAccess': 'false'}),
    H('setPublic', False, 'EnableManagedAccess off: placed pieces are left alone', target='chest', on=True),
    play('me'),
])

T('L-12', 'guest public toggle needs the Toggle permission', [
    *alice_base(),
    H('openJoin', True, target='chest', on=True),
    play('Bob'),
    *join('Bob joins'),
    H('setPublic', False, 'a plain guest cannot make it public', target='chest', on=True),
    play('Alice'),
    H('grantPerm', 'Toggle', 'Alice gives Bob the Toggle permission', target='chest', who='Bob', perms='toggle'),
    play('Bob'),
    H('setPublic', True, 'now Bob may make it public', target='chest', on=True),
    H('setPublic', False, 'and private again', target='chest', on=False),
    C({'02 Features/EnableGuestPublicToggle': 'false'}),
    H('setPublic', label='observed with EnableGuestPublicToggle off (informational)', target='chest', on=True),
    play('me'),
])

suite = {
    'name': 'LockSmith',
    'description': 'Automated regression for LockSmith: wards, public/private, Join, guests, removal, team/personal chests, doors and the '
                   'feature switches. Pieces are built with the hammer as Alice; access is checked by pressing E as Bob (play-as).',
    'requireMods': [{'guid': GUID, 'minVersion': '0.5.0'}],
    'configOverrides': [{'guid': GUID, 'section': k.split('/')[0], 'key': k.split('/')[1], 'value': v} for k, v in {
        '02 Features/EnableChests': 'true', '02 Features/EnableDoors': 'true', '02 Features/EnableGroupChests': 'true',
        '02 Features/EnablePersonalPause': 'false', '02 Features/EnablePieceGuests': 'true', '02 Features/EnableOptInAccess': 'true',
        '02 Features/EnableGuestPublicToggle': 'true', '02 Features/EnableDesignate': 'false', '02 Features/EnableManagedAccess': 'true',
        '02 Features/EnableAddNearby': 'true', '02 Features/RequireActiveWard': 'true',
    }.items()],
    'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1, 'noBuild': 'off', 'hideUnlockPopups': 1},
    'resetBetweenTests': True,
}

if os.path.isdir(OUT):
    shutil.rmtree(OUT)
os.makedirs(OUT)
with open(os.path.join(OUT, 'suite.json'), 'w', encoding='utf-8') as f:
    json.dump(suite, f, indent=2, ensure_ascii=False)
for tid, t in tests.items():
    with open(os.path.join(OUT, f'{tid}.json'), 'w', encoding='utf-8') as f:
        json.dump(t, f, indent=2, ensure_ascii=False)
print(len(tests), 'LockSmith tests')

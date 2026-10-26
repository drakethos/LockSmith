"""Writes LockSmith's deeper DrakesTestLab suites (run: python gen_locksmith_deep.py).

LockSmith-Menu drives the real Lock menu (Shift+E window): Enable LockSmith (designate), per-person permissions, Copy / Paste
people, Leave, Remove LockSmith, the Ward tab, Groups (groups.txt) and one player in the menu at a time. Results are read back with
the lock.* test hooks. LockSmith-PieceMode builds the always-public hammer clones (EnablePieceMode only applies at game start, so
the run script switches it on with launchConfig). Tests/ is never packaged."""
import json, os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
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


def click(button, label=None):
    s = {'do': 'click', 'button': button}
    if label:
        s['label'] = label
    return s


def seen(name, label, expect=True):
    return check('uiVisible', label, name=name, exact=True, expect=expect)


def alice_base(piece='piece_chest_wood', tag='chest', ward=True, dz=2.5, dx=0):
    steps = [play('Alice'), {'do': 'place', 'prefab': piece, 'dz': dz, 'dx': dx, 'as': tag}]
    if ward:
        steps.append({'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15, 'as': 'ward'})
    steps.append(wait(0.5))
    return steps


def menu(tag='chest', label='AltPlace+E opens the Lock menu'):
    return [H('openMenu', True, label, stop=True, target=tag), wait(0.4)]


def close():
    return [H('closeMenu'), wait(0.2)]


def join_as(who, tag='chest'):
    """The player joins the real way: Lock menu, Join access."""
    return [play(who), *menu(tag, f'{who} opens the Lock menu'), click('Join access'), wait(0.5), *close(),
            H('isGuest', True, f'{who} is a guest now', target=tag)]


def baseline(extra=None):
    values = {
        '02 Features/EnableChests': 'true', '02 Features/EnableDoors': 'true', '02 Features/EnableGroupChests': 'true',
        '02 Features/EnablePersonalPause': 'false', '02 Features/EnablePieceGuests': 'true', '02 Features/EnableOptInAccess': 'true',
        '02 Features/EnableGuestPublicToggle': 'true', '02 Features/EnableDesignate': 'false', '02 Features/EnableManagedAccess': 'true',
        '02 Features/EnableAddNearby': 'true', '02 Features/RequireActiveWard': 'true',
    }
    values.update(extra or {})
    return [{'guid': GUID, 'section': k.split('/')[0], 'key': k.split('/')[1], 'value': v} for k, v in values.items()]


suites = {}


def suite(name, description, launch=None, extra=None):
    suites[name] = {'meta': {
        'name': name, 'description': description,
        'requireMods': [{'guid': GUID, 'minVersion': '0.6.0'}],
        'configOverrides': baseline(extra),
        **({'launchConfig': launch} if launch else {}),
        'setup': {'timeOfDay': 0.5, 'freezeTime': 1, 'god': 1, 'noBuild': 'off', 'hideUnlockPopups': 1},
        'resetBetweenTests': True}, 'tests': {}}


def T(suite_name, tid, title, steps, desc='', **extra):
    t = {'name': f'{tid} {title}', 'description': desc, 'steps': steps, 'asserts': [NOERR]}
    t.update(extra)
    suites[suite_name]['tests'][tid] = t


# ================================================================ the Lock menu
M = 'LockSmith-Menu'
suite(M, 'The Lock menu driven like a player: Enable LockSmith, permissions per person, copy / paste people, leave, remove, '
         'the Ward tab, groups and one player at a time. Pieces are built as Alice; other players are play-as.')

T(M, 'LM-01', 'EnableDesignate: a piece must be switched on before anything else shows', [
    C({'02 Features/EnableDesignate': 'true'}),
    *alice_base(),
    H('needsDesignate', True, 'a new chest is not LockSmith yet', target='chest'),
    *menu(),
    seen('Enable LockSmith', 'the menu offers Enable LockSmith'),
    seen('Public', 'and nothing else yet (no Public switch)', expect=False),
    seen('Ward', 'no Ward tab before it is enabled', expect=False),
    click('Enable LockSmith'), wait(0.5),
    *close(),
    H('needsDesignate', False, 'switched on', target='chest'),
    *menu(label='the menu again'),
    seen('Public', 'now the Public switch shows'),
    seen('Join open', 'and Join'),
    {'do': 'screenshot', 'name': 'lock-menu-enabled'},
    *close(),
    play('me'),
])

T(M, 'LM-02', 'permissions per person in the menu: Let others on, Switch public / private', [
    *alice_base(),
    H('openJoin', True, target='chest', on=True),
    *join_as('Bob'),
    H('canAdd', False, 'a plain guest cannot let others on', target='chest'),
    H('setPublic', False, 'nor switch public', target='chest', on=True),
    play('Alice'),
    *menu(label='Alice opens the menu'),
    click('Bob', 'click Bob on the piece'), wait(0.3),
    seen('Let others on', 'his permissions open'),
    click('Let others on'), wait(1.5),
    {'do': 'screenshot', 'name': 'after-let-others-on'},
    H('guestList', label='observed right after Let others on (informational)', target='chest'),
    click('Switch public / private'), wait(1.5),
    {'do': 'screenshot', 'name': 'after-switch-public'},
    H('guestList', label='observed right after Switch public / private (informational)', target='chest'),
    click('Done'), wait(0.5),
    *close(),
    H('guestList', label='Bob now has both permissions', contains='Bob(Add, Toggle)', target='chest'),
    play('Bob'),
    H('canAdd', True, 'Bob may let others on now', target='chest'),
    H('setPublic', True, 'and switch public', target='chest', on=True),
    H('setPublic', label='and private again (informational)', target='chest', on=False),
    play('me'),
])

T(M, 'LM-03', 'Copy people from one chest and Paste them onto another', [
    *alice_base(),
    {'do': 'place', 'prefab': 'piece_chest_wood', 'dz': 2.5, 'dx': 2.5, 'as': 'chest2'},
    H('openJoin', True, target='chest', on=True),
    *join_as('Bob'),
    *join_as('Carol'),
    play('Alice'),
    *menu(label='Alice opens the menu on the first chest'),
    click('Copy people', 'Copy people (2)'), wait(0.3),
    *close(),
    H('clipboard', 2, 'two people on the clipboard'),
    *menu('chest2', 'the menu on the second chest'),
    click('Paste people', 'Paste people (2)'), wait(0.6),
    *close(),
    H('guestList', label='Bob is on the second chest', contains='Bob', target='chest2'),
    H('guestList', label='and Carol', contains='Carol', target='chest2'),
    play('Carol'),
    H('isGuest', True, 'Carol counts as a guest there', target='chest2'),
    play('me'),
])

T(M, 'LM-04', 'a guest leaves through the menu (with its confirm)', [
    *alice_base(),
    H('openJoin', True, target='chest', on=True),
    *join_as('Bob'),
    *menu(label='Bob opens the menu'),
    click('Leave', 'Leave'), wait(0.3),
    {'do': 'screenshot', 'name': 'leave-confirm'},
    click('Leave', 'confirm Leave'), wait(0.6),
    *close(),
    H('isGuest', False, 'Bob is off the list', target='chest'),
    play('me'),
])

T(M, 'LM-05', 'Remove LockSmith puts the chest back to normal', [
    *alice_base(),
    H('openJoin', True, target='chest', on=True),
    *join_as('Bob'),
    play('Alice'),
    H('setPublic', True, target='chest', on=True),
    H('hasData', True, 'the chest carries LockSmith data', target='chest'),
    *menu(label='Alice opens the menu'),
    click('Remove', 'Remove'), wait(0.3),
    click('Remove', 'confirm Remove'), wait(0.6),
    *close(),
    H('hasData', False, 'no LockSmith data left', target='chest'),
    H('isPublic', False, 'not public any more', target='chest'),
    play('Bob'),
    H('isGuest', False, 'and Bob is no longer a guest', target='chest'),
    play('me'),
])

T(M, 'LM-06', 'the Ward tab: switch the ward off and on', [
    *alice_base(),
    H('wardEnabled', True, 'the ward is on', target='ward'),
    H('wardRole', 'Owner', 'Alice owns it', target='ward', who='Alice'),
    *menu(label='Alice opens the menu'),
    click('Ward', 'the Ward tab'), wait(0.3),
    seen('Ward on', 'it has the Ward on switch'),
    seen('Who can switch it', 'and who may switch it'),
    {'do': 'screenshot', 'name': 'ward-tab'},
    click('Ward on', 'switch the ward off'), wait(0.6),
    H('wardEnabled', False, 'the ward is off', target='ward'),
    click('Ward on', 'and on again'), wait(0.6),
    H('wardEnabled', True, 'on again', target='ward'),
    *close(),
    play('Bob'),
    H('openMenu', label='observed: a stranger in the ward gets a Lock menu? (informational)', target='chest'), wait(0.3),
    seen('Ward', 'a stranger never sees the Ward tab', expect=False),
    *close(),
    play('me'),
])

T(M, 'LM-07', 'Groups: add a saved group to a chest (people offline, with their permissions)', [
    H('writeGroups', 1, 'a groups file with one group', text='# test|Squad (add): Bob=100002, Carol=100003'),
    H('groups', label='the mod reads it', contains='Squad(Add): Bob=100002, Carol=100003'),
    *alice_base(),
    *menu(label='Alice opens the menu'),
    click('Groups', 'the Groups tab'), wait(0.3),
    check('uiVisible', 'the group is listed (SQUAD with its people)', name='Bob, Carol'),
    {'do': 'screenshot', 'name': 'groups-tab'},
    click('Add to this chest', 'Add to this chest'), wait(0.6),
    *close(),
    H('guestList', label='Bob joined with Let others on', contains='Bob(Add)', target='chest'),
    H('guestList', label='and Carol', contains='Carol(Add)', target='chest'),
    play('Carol'),
    H('canAdd', True, 'Carol may let others on (the group carries it)', target='chest'),
    play('me'),
    H('restoreGroups', True, 'put the groups file back'),
])

T(M, 'LM-08', 'Groups: save the people on a chest as a new group', [
    H('writeGroups', 0, 'start with an empty groups file', text='# test'),
    *alice_base(),
    H('openJoin', True, target='chest', on=True),
    *join_as('Bob'),
    *join_as('Carol'),
    play('Alice'),
    *menu(label='Alice opens the menu'),
    click('Groups', 'the Groups tab'), wait(0.3),
    {'do': 'type', 'field': 'Group name', 'text': 'Crew'},
    click('Save current guests'), wait(0.5),
    *close(),
    H('groups', label='Crew saved with both ids (offline adds work later)', contains='Crew(None): Bob=100002, Carol=100003'),
    play('me'),
    H('restoreGroups', True, 'put the groups file back'),
])

T(M, 'LM-09', 'one player in the menu at a time', [
    *alice_base(),
    H('holdMenuAs', True, 'Bob has the Lock menu open on this chest', target='chest', who='Bob'),
    H('heldBy', 'Bob', 'the chest knows who holds it', target='chest'),
    H('openMenu', label='Alice tries the menu meanwhile', target='chest'), wait(0.3),
    H('menuOpen', False, 'no menu opens for her'),
    check('message', 'she is told who is using it', contains='Bob is using this'),
    H('holdMenuAs', True, 'Bob closes his menu', target='chest', who=''),
    H('openMenu', True, 'now Alice gets in', target='chest'), wait(0.3),
    *close(),
    play('me'),
])

T(M, 'LM-10', 'a plain guest gets only Leave: no copy, no permissions, no Join switch', [
    *alice_base(),
    H('openJoin', True, target='chest', on=True),
    *join_as('Bob'),
    *menu(label='Bob opens the menu'),
    check('uiVisible', 'no Copy people for a guest', name='Copy people', expect=False),
    seen('Join open', 'no Join switch for a guest', expect=False),
    seen('Leave access', 'but he can leave'),
    {'do': 'screenshot', 'name': 'guest-menu'},
    *close(),
    play('me'),
])

# ================================================================ always-public hammer pieces
P = 'LockSmith-PieceMode'
suite(P, 'EnablePieceMode: the hammer Public category builds always-open clones of chests and doors; the Lock menu leaves them alone.',
      launch=[{'guid': GUID, 'section': '02 Features', 'key': 'EnablePieceMode', 'value': 'true'}])

T(P, 'LP-01', 'a public chest clone opens for anyone, even inside a ward', [
    *alice_base('piece_chest_wood_public'),
    check('hoverText', 'it says (public) and [Public]', target='chest', contains='public'),
    play('Bob'),
    {'do': 'closeUi'}, {'do': 'face', 'target': 'chest'}, {'do': 'interact', 'target': 'chest'}, wait(0.4),
    check('uiVisible', 'Bob opens it inside Alice ward', name='InventoryGui'),
    {'do': 'closeUi'},
    H('openMenu', False, 'the Lock menu does not manage it', target='chest'),
    play('me'),
])

T(P, 'LP-02', 'a public door clone opens for anyone', [
    *alice_base('wood_door_public', 'door'),
    play('Bob'),
    {'do': 'face', 'target': 'door'}, {'do': 'interact', 'target': 'door'}, wait(0.6),
    check('zdo', 'the door opened for Bob', target='door', key='state', default='0', notContains='0'),
    play('me'),
])

T(P, 'LP-03', 'a normal chest beside it stays locked', [
    *alice_base(),
    play('Bob'),
    {'do': 'closeUi'}, {'do': 'face', 'target': 'chest'}, {'do': 'interact', 'target': 'chest'}, wait(0.4),
    check('uiVisible', 'the normal chest stays shut for Bob', name='InventoryGui', expect=False),
    {'do': 'closeUi'},
    play('me'),
])

# ================================================================ write
for name, s in suites.items():
    folder = os.path.join(HERE, 'suites', name)
    if os.path.isdir(folder):
        shutil.rmtree(folder)
    os.makedirs(folder)
    with open(os.path.join(folder, 'suite.json'), 'w', encoding='utf-8') as f:
        json.dump(s['meta'], f, indent=2, ensure_ascii=False)
    for tid, t in s['tests'].items():
        with open(os.path.join(folder, f'{tid}.json'), 'w', encoding='utf-8') as f:
            json.dump(t, f, indent=2, ensure_ascii=False)
    print(f"{name}: {len(s['tests'])} tests")

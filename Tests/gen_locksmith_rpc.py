"""Writes the LockSmith "over the wire" suites (run: python gen_locksmith_rpc.py).

A virtual player (DrakesTestLab peerJoin) connects like a second client and sends LockSmith's real routed RPCs to the
chest / door owner (this game). That tests what the playAs suites cannot: the server-side permission checks, the
sender check (a client claiming to be someone else) and each ward mod's per-player access path.

Suites: LockSmith-Rpc (vanilla wards; run with no ward mod) and LockSmith-Rpc-<Mod> (the ward mod's own ward).
Needs a DrakesTestLab with virtual players (peerJoin / peerRpc)."""
import importlib.util, json, os, shutil

HERE = os.path.dirname(os.path.abspath(__file__))
SUITES = os.path.join(HERE, 'suites')

# reuse the ward suites' helpers (run with python -I: the script folder is not on sys.path, so load by path)
_spec = importlib.util.spec_from_file_location('gen_locksmith_wards', os.path.join(HERE, 'gen_locksmith_wards.py'))
W = importlib.util.module_from_spec(_spec)
_spec.loader.exec_module(W)
H, play, wait, base, NOERR, GUID = W.H, W.play, W.wait, W.base, W.NOERR, W.GUID

SET_PUBLIC = 'RPC_LockSmithSetChestPublic'
SET_DOOR_PUBLIC = 'RPC_LockSmithSetDoorPublic'
OPT_IN = 'RPC_LockSmithOptInSelf'
OPT_OUT = 'RPC_LockSmithOptOutSelf'
REMOVE_GUEST = 'RPC_LockSmithRemoveGuest'
MERGE = 'RPC_LockSmithMergeGuests'
CLEAR = 'RPC_LockSmithClear'

VANILLA = dict(prefix='RP', suite='LockSmith-Rpc', title='vanilla ward', guid=None, arcane=False, module=None)
MODS = {name: dict(prefix='RP-' + m['prefix'], suite='LockSmith-Rpc-' + name, title=name, guid=m['guid'], arcane=m['arcane'],
                   module=m['module'], extra=m.get('extra', []))
        for name, m in W.MODS.items()}


def join_peer(name):
    return [{'do': 'peerJoin', 'name': name, 'dx': 3, 'dz': 1, 'as': name.lower()},
            {'do': 'check', 'check': 'peerConnected', 'peer': name, 'label': f'{name} is connected like a second client'}]


def rpc(peer, method, args, types, target='chest', sender_claim=None):
    """peerRpc: the virtual player's client sends the RPC to the object's owner (this game)."""
    return {'do': 'peerRpc', 'peer': peer, 'target': target, 'method': method, 'args': args, 'types': types, 'wait': 0.3}


def leave():
    return {'do': 'peerLeave', 'name': '*'}


def is_public(expect, label, target='chest'):
    return H('isPublic', expect, label, target=target)


def is_guest(who, expect, label, target='chest'):
    return H('isGuest', expect, label, target=target, who=who)


def build(m):
    tests = {}
    P = m['prefix']

    def T(n, title, steps):
        tid = f'{P}-{n:02d}'
        tests[tid] = {'name': f'{tid} {title}', 'description': f"over the wire, {m['title']}", 'steps': steps + [leave()], 'asserts': [NOERR]}

    T(1, 'a stranger cannot make a warded chest public over the wire', [
        *base(m), *join_peer('Bob'),
        rpc('Bob', SET_PUBLIC, [1, '@Bob'], ['int', 'long']),
        is_public(False, "Bob's request is refused: the chest stays private"),
        H('setPublic', True, 'Alice makes it public', target='chest', on=True),
        rpc('Bob', SET_PUBLIC, [0, '@Bob'], ['int', 'long']),
        is_public(True, 'Bob cannot make it private either'),
    ])
    T(2, 'a forged player id is refused', [
        *base(m), *join_peer('Bob'),
        rpc('Bob', SET_PUBLIC, [1, '@Alice'], ['int', 'long']),
        is_public(False, "Bob claims to be Alice (the ward owner): the sender check refuses it"),
        rpc('Bob', OPT_IN, ['@Alice', 'Alice'], ['long', 'string']),
        is_guest('Alice', False, 'and cannot put Alice on the list in her name'),
    ])
    T(3, 'a ward member may manage over the wire', [
        *base(m, permitted=('Bob',)), *join_peer('Bob'),
        rpc('Bob', SET_PUBLIC, [1, '@Bob'], ['int', 'long']),
        is_public(True, f"a member of the {m['title']} makes the chest public"),
        rpc('Bob', SET_PUBLIC, [0, '@Bob'], ['int', 'long']),
        is_public(False, 'and private again'),
    ])
    T(4, 'Join over the wire: closed, open, leave', [
        *base(m), *join_peer('Bob'),
        rpc('Bob', OPT_IN, ['@Bob', 'Bob'], ['long', 'string']),
        is_guest('Bob', False, 'Join is closed: the request does nothing'),
        H('openJoin', True, 'Alice opens Join', target='chest', on=True),
        rpc('Bob', OPT_IN, ['@Bob', 'Bob'], ['long', 'string']),
        is_guest('Bob', True, 'Join is open: Bob is on the list'),
        H('guests', label='Bob is listed', contains='Bob', target='chest'),
        rpc('Bob', OPT_OUT, ['@Bob'], ['long']),
        is_guest('Bob', False, 'Bob leaves again'),
    ])
    T(5, 'a plain guest cannot toggle public or add people; the permissions open each', [
        *base(m), *join_peer('Bob'),
        H('openJoin', True, 'Alice opens Join', target='chest', on=True),
        rpc('Bob', OPT_IN, ['@Bob', 'Bob'], ['long', 'string']),
        is_guest('Bob', True, 'Bob is a guest'),
        rpc('Bob', SET_PUBLIC, [1, '@Bob'], ['int', 'long']),
        is_public(False, 'a plain guest cannot make it public'),
        rpc('Bob', MERGE, ['100003|Carol', '@Bob'], ['string', 'long']),
        is_guest('Carol', False, 'a plain guest cannot add Carol'),
        H('grantPerm', 'Toggle', 'Alice gives Bob the Toggle permission', target='chest', who='Bob', perms='toggle'),
        rpc('Bob', SET_PUBLIC, [1, '@Bob'], ['int', 'long']),
        is_public(True, 'with Toggle Bob makes it public'),
        rpc('Bob', MERGE, ['100003|Carol', '@Bob'], ['string', 'long']),
        is_guest('Carol', False, 'Toggle alone still cannot add people'),
        H('grantPerm', 'Add', 'Alice gives Bob the Add permission', target='chest', who='Bob', perms='add'),
        rpc('Bob', MERGE, ['100003|Carol', '@Bob'], ['string', 'long']),
        is_guest('Carol', True, 'with Add Bob puts Carol on the list'),
    ])
    T(6, 'strangers and guests cannot remove people or clear LockSmith', [
        *base(m), *join_peer('Bob'), *join_peer('Carol'),
        H('openJoin', True, 'Alice opens Join', target='chest', on=True),
        rpc('Bob', OPT_IN, ['@Bob', 'Bob'], ['long', 'string']),
        is_guest('Bob', True, 'Bob is a guest'),
        rpc('Carol', REMOVE_GUEST, ['@Bob', '@Carol'], ['long', 'long']),
        is_guest('Bob', True, 'Carol (a stranger) cannot remove Bob'),
        rpc('Bob', REMOVE_GUEST, ['@Bob', '@Bob'], ['long', 'long']),
        H('hasData', True, 'a guest removing people leaves the data alone (informational on the guest list)', target='chest'),
        rpc('Carol', CLEAR, ['@Carol'], ['long']),
        H('hasData', True, 'Carol cannot clear LockSmith off the chest', target='chest'),
        rpc('Bob', CLEAR, ['@Bob'], ['long']),
        H('hasData', True, 'a plain guest cannot clear it either', target='chest'),
        play('Alice'),
        H('removeGuest', True, 'Alice (the owner) removes Bob', target='chest', who='Bob'),
        play('me'),
    ])
    T(7, 'doors over the wire', [
        *W.base(m, piece='wood_door', tag='door'), *join_peer('Bob'),
        rpc('Bob', SET_DOOR_PUBLIC, [1, '@Bob'], ['int', 'long'], target='door'),
        is_public(False, 'a stranger cannot make the warded door public', target='door'),
        rpc('Bob', SET_DOOR_PUBLIC, [1, '@Alice'], ['int', 'long'], target='door'),
        is_public(False, "and cannot do it in Alice's name", target='door'),
    ])
    T(8, 'a ward member and the door', [
        *W.base(m, piece='wood_door', tag='door', permitted=('Bob',)), *join_peer('Bob'),
        rpc('Bob', SET_DOOR_PUBLIC, [1, '@Bob'], ['int', 'long'], target='door'),
        is_public(True, 'a ward member makes the door public', target='door'),
    ])
    return tests


def write(m, tests):
    out = os.path.join(SUITES, m['suite'])
    cfg = {
        '02 Features/EnableChests': 'true', '02 Features/EnableDoors': 'true', '02 Features/EnableGroupChests': 'true',
        '02 Features/EnablePersonalPause': 'false', '02 Features/EnablePieceGuests': 'true', '02 Features/EnableOptInAccess': 'true',
        '02 Features/EnableGuestPublicToggle': 'true', '02 Features/EnableDesignate': 'false', '02 Features/EnableManagedAccess': 'true',
        '02 Features/EnableAddNearby': 'true', '02 Features/RequireActiveWard': 'true',
    }
    requires = [{'guid': GUID, 'minVersion': '0.5.0'}] + ([{'guid': m['guid']}] if m['guid'] else [])
    suite = {
        'name': m['suite'],
        'description': f"LockSmith's routed RPCs from a virtual second player with the {m['title']}: refused strangers, forged ids, "
                       'ward members, Join, guest permissions, remove / clear, doors.',
        'requireMods': requires,
        'configOverrides': [{'guid': 'com.drakemods.testlab', 'section': 'TestControls', 'key': 'AutoEnableDebugMode', 'value': 'false'}]
                           + m.get('extra', [])
                           + [{'guid': GUID, 'section': k.split('/')[0], 'key': k.split('/')[1], 'value': v} for k, v in cfg.items()],
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


for m in [VANILLA, *MODS.values()]:
    t = build(m)
    write(m, t)
    print(m['suite'], len(t), 'tests')

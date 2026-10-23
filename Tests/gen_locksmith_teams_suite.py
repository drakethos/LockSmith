"""Writes the DrakesTestLab suite for LockSmith team access (needs DrakesServerTeams) into Tests/suites/LockSmithTeams
(run: python gen_locksmith_teams_suite.py). The build copies it into the test profiles; Tests/ is never packaged."""
import json, os, shutil

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), 'suites', 'LockSmithTeams')
GUID = 'com.drakesworkshop.locksmith'
TEAMS = 'com.drakemods.serverteams'
NOERR = [{'check': 'noErrors', 'source': 'LockSmith'}, {'check': 'noErrors', 'source': 'DrakesServerTeams'}]


def hook(prefix, name, expect=None, label=None, stop=False, **args):
    s = {'do': 'hook', 'name': prefix + name}
    if args:
        s['args'] = args
    if expect is not None:
        s['expect'] = expect
    if label is not None:
        s['label'] = label
    if stop:
        s['stop'] = True
    return s


def L(name, expect=None, label=None, **args):
    return hook('lock.', name, expect, label, **args)


def TM(name, expect=None, label=None, **args):
    return hook('teams.', name, expect, label, **args)


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


def on_team(who, team):
    return [play(who), TM('assign', team, f'{who} is on {team}', team=team)]


def alice_base(piece='piece_chest_wood', tag='chest', ward=True):
    """Alice (red) builds the piece with the hammer inside her own enabled ward; the server stamps both red."""
    steps = [TM('usePieceTeams', True), *on_team('Alice', 'red'), {'do': 'place', 'prefab': piece, 'dz': 2.5, 'as': tag}]
    if ward:
        steps += [{'do': 'spawn', 'prefab': 'guard_stone', 'dz': -2, 'owner': 'Alice', 'enabled': True, 'radius': 15, 'as': 'ward'},
                  TM('checkPiece', 'red', 'the server stamps the ward red', target='ward')]
    steps += [wait(0.6), TM('pieceTeam', 'red', f'the {tag} is red', target=tag)]
    return steps


def opens(label, expect=True, tag='chest'):
    return [{'do': 'closeUi'}, {'do': 'face', 'target': tag}, {'do': 'interact', 'target': tag}, wait(0.4),
            check('uiVisible', label, name='InventoryGui', expect=expect), {'do': 'closeUi'}]


def door_moves(label, expect=True):
    s = [{'do': 'face', 'target': 'door'}, {'do': 'interact', 'target': 'door'}, wait(0.6)]
    s.append(check('zdo', label, target='door', key='state', default='0', **({'notContains': '0'} if expect else {'expect': '0'})))
    return s


tests = {}


def T(tid, title, steps, desc=''):
    tests[tid] = {'name': f'{tid} {title}', 'description': desc, 'steps': steps + [play('me')], 'asserts': NOERR}


T('LT-01', 'a team-shared chest opens for the team, not for others', [
    *alice_base(),
    *on_team('Carol', 'red'),
    *opens('before sharing, Carol (red) is kept out by the ward', expect=False),
    play('Alice'),
    L('setTeamAccess', True, 'Alice shares the chest with her team', target='chest', on=True),
    *on_team('Carol', 'red'),
    *opens('Carol (red) opens it'),
    *on_team('Bob', 'blue'),
    *opens('Bob (blue) still cannot', expect=False),
])

T('LT-02', 'only the builder switches team access', [
    *alice_base(),
    *on_team('Carol', 'red'),
    L('canSetTeamAccess', False, 'Carol (same team, not the builder) cannot switch it', target='chest'),
    L('setTeamAccess', False, 'her request is refused', target='chest', on=True),
    play('Alice'),
    L('canSetTeamAccess', True, 'Alice can', target='chest'),
])

T('LT-03', 'a team-shared private chest lets the team in', [
    *alice_base(piece='piece_chest_private', ward=False),
    *on_team('Carol', 'red'),
    *opens('Carol cannot open Alice\'s private chest yet', expect=False),
    play('Alice'),
    L('setTeamAccess', True, target='chest', on=True),
    *on_team('Carol', 'red'),
    *opens('after sharing, Carol opens it'),
])

T('LT-04', 'a team-shared door', [
    *alice_base(piece='wood_door', tag='door'),
    play('Alice'),
    L('setTeamAccess', True, target='door', on=True),
    *on_team('Carol', 'red'),
    *door_moves('Carol (red) opens the door'),
    *on_team('Bob', 'blue'),
    *door_moves('Bob (blue) cannot', expect=False),
])

T('LT-05', 'a team-shared ward counts the team as permitted', [
    *alice_base(),
    *on_team('Carol', 'red'),
    check('wardAccess', 'before sharing, Carol has no ward access', expect=False),
    play('Alice'),
    L('setTeamAccess', True, 'Alice opens her ward to her team', target='ward', on=True),
    *on_team('Carol', 'red'),
    check('wardAccess', 'Carol (red) has ward access', expect=True),
    *opens('so Carol opens the warded chest'),
    *on_team('Bob', 'blue'),
    check('wardAccess', 'Bob (blue) does not', expect=False),
])

T('LT-06', 'TeamAccessByDefault opens pieces to their team without a switch', [
    C({'02 Features/TeamAccessByDefault': 'true'}),
    *alice_base(),
    *on_team('Carol', 'red'),
    L('hasTeamAccess', True, 'Carol has team access on the chest', target='chest'),
    L('hasTeamAccess', True, 'and on the ward', target='ward'),
    C({'02 Features/TeamAccessByDefault': 'false'}),
    L('hasTeamAccess', False, 'off again with the default off', target='chest'),
])

T('LT-07', 'a forged team stamp gives no access once the server checks it', [
    *alice_base(),
    play('Alice'),
    L('setTeamAccess', True, target='chest', on=True),
    *on_team('Bob', 'blue'),
    TM('forgeStamp', 'blue', 'a modified client stamps Alice\'s chest blue', target='chest', team='blue'),
    TM('checkPiece', 'red', 'the server puts red back', target='chest'),
    L('hasTeamAccess', False, 'Bob has no team access', target='chest'),
    *opens('Bob cannot open it', expect=False),
])

T('LT-08', 'a builder who changes team keeps the piece on the old team', [
    *alice_base(),
    L('setTeamAccess', True, target='chest', on=True),
    *on_team('Alice', 'blue'),
    TM('checkPiece', 'red', 'the chest stays red', target='chest'),
    *on_team('Carol', 'red'),
    L('hasTeamAccess', True, 'red Carol still has team access', target='chest'),
])

T('LT-09', 'EnableTeamAccess off turns team access off everywhere', [
    *alice_base(),
    L('setTeamAccess', True, target='chest', on=True),
    C({'02 Features/EnableTeamAccess': 'false'}),
    *on_team('Carol', 'red'),
    L('hasTeamAccess', False, 'no team access with the feature off', target='chest'),
    *opens('Carol is kept out by the ward again', expect=False),
    C({'02 Features/EnableTeamAccess': 'true'}),
])

suite = {
    'name': 'LockSmithTeams',
    'description': 'LockSmith team access with DrakesServerTeams: team-shared chests (wood and private), doors and wards, builder-only '
                   'switch, TeamAccessByDefault, forged stamps, a builder changing team, and the feature switch. Pieces are built '
                   'as Alice (red); Carol (red) and Bob (blue) are simulated with play-as.',
    'requireMods': [{'guid': GUID, 'minVersion': '0.6.0'}, {'guid': TEAMS, 'minVersion': '0.1.0'}],
    'configOverrides': [{'guid': GUID, 'section': k.split('/')[0], 'key': k.split('/')[1], 'value': v} for k, v in {
        '02 Features/EnableChests': 'true', '02 Features/EnableDoors': 'true', '02 Features/EnableManagedAccess': 'true',
        '02 Features/RequireActiveWard': 'true', '02 Features/EnableTeamAccess': 'true', '02 Features/TeamAccessByDefault': 'false',
    }.items()] + [{'guid': TEAMS, 'section': '1 Assignment', 'key': 'AssignmentMode', 'value': 'AdminOnly'}],
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
print(len(tests), 'LockSmithTeams tests')

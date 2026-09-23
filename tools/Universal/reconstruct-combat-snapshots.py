#!/usr/bin/env python3
"""Join existing decoded retail records; never fill unknown inputs from emulator defaults."""
import argparse
import collections
import copy
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CAPTURES = {'combat_autoattack.pcapng', 'combat_skills.pcapng',
            'party_battle_leve.pcapng', 'war_quest_update2.pcapng'}
RESULTS = {'0x0139', '0x013A', '0x013B', '0x013C'}

def reconstruct(rows):
    streams = collections.defaultdict(list)
    for line, r in enumerate(rows, 1):
        if r['capture'] in CAPTURES and r['direction'] == 'server-to-client':
            streams[(r['capture'], r['tcpStream'])].append((line, r))
    output = []
    for (capture, stream), events in sorted(streams.items()):
        # Only one direction: frameIndex plus original subpacket order avoids
        # interleaving independently indexed client-to-server frames.
        events.sort(key=lambda pair: (pair[1]['frameIndex'], pair[0]))
        states, generations = {}, collections.Counter()
        def state(actor):
            if actor not in states:
                states[actor] = {'actorId': actor, 'generation': generations[actor],
                                 'properties': {}, 'initializationObserved': False}
            return states[actor]
        for line, r in events:
            actor, op = r['sourceActorId'], r['opcode']
            ref = {'corpusLine': line, 'frameIndex': r['frameIndex'],
                   'timestamp': r.get('frameTimestamp')}
            if op == '0x00CB':
                states.pop(actor, None)
                generations[actor] += 1
                continue
            if op == '0x00CA':
                states.pop(actor, None)
                generations[actor] += 1
            s = state(actor)
            if op == '0x00CC' and 'className' in r:
                s['initializationObserved'] = True
                s['identity'] = {k: r[k] for k in ('className', 'objectName', 'containerKey')}
                s['identity']['source'] = ref
            if op == '0x0137':
                for segment in r.get('segments', []):
                    for value in segment['values']:
                        path = value.get('path')
                        if path and path.startswith(('charaWork.', 'npcWork.', 'playerWork.')):
                            s['properties'][path] = {'encodedValue': {k:v for k,v in value.items() if k not in ('path','h')}, 'source': ref}
            if op == '0x0179':
                s['lastObservedStatusList'] = {'ids': r['statusIds'], 'source': ref}
            if op == '0x00D6':
                s['lastObservedAppearance'] = {'modelId': r['modelId'], 'pairs': r['pairs'], 'source': ref}
            if op in RESULTS:
                attacker = copy.deepcopy(state(r['actor']))
                targets = {a['target']: copy.deepcopy(state(a['target'])) for a in r.get('actions', [])}
                output.append({'capture': capture, 'tcpStream': stream, 'source': ref,
                               'opcode': op, 'commandId': r['commandId'],
                               'declaredActionCount': r['actionCount'],
                               'actions': r.get('actions', []), 'attacker': attacker, 'targets': targets,
                               'missingInputs': ['verified weapon damage and equipped item mapping',
                                   'complete offensive and defensive stat baseline',
                                   'validated status durations and complete effect state',
                                   'validated action amount semantics for each result'],
                               'formulaReady': False})
    return output

if __name__ == '__main__':
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--corpus', type=Path, default=ROOT/'evidence/npc-restoration-2026-09-15/corpus-raw.jsonl')
    p.add_argument('--output', type=Path, default=ROOT/'evidence/combat-snapshots-2026-09-17')
    a = p.parse_args()
    raw = a.corpus.read_bytes()
    snapshots = reconstruct([json.loads(l) for l in raw.splitlines() if l.strip()])
    a.output.mkdir(parents=True, exist_ok=True)
    (a.output/'snapshots.jsonl').write_text(''.join(json.dumps(s, sort_keys=True)+'\n' for s in snapshots))
    summary = {'inputSha256': hashlib.sha256(raw).hexdigest(),
               'resultPackets': len(snapshots),
               'decodedActionEntries': sum(len(s['actions']) for s in snapshots),
               'byCapture': dict(collections.Counter(s['capture'] for s in snapshots)),
               'withAttackerInitialization': sum(s['attacker']['initializationObserved'] for s in snapshots),
               'withAttackerStatObservations': sum(any('generalParameter[' in p for p in s['attacker']['properties']) for s in snapshots),
               'undecodedResultPackets': sum(s['declaredActionCount'] > len(s['actions']) for s in snapshots),
               'formulaReady': 0,
               'limitations': ['Last observed values only; not certified complete current state.',
                   'Same-frame packet order is receive order, not proof of server calculation order.',
                   'Amounts are not automatically damage: results may report healing or other effects.',
                   'No state crosses captures, streams, actor removal or actor-add boundaries.',
                   'Dangling property fragments are not applied without established ownership.',
                   'Appearance models are not equipped item IDs.']}
    (a.output/'summary.json').write_text(json.dumps(summary, indent=2)+'\n')
    print(json.dumps(summary, indent=2))

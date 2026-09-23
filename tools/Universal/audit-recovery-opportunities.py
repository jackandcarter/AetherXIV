#!/usr/bin/env python3
"""Inventory structural decode gaps without treating them as unknown retail behavior."""
import argparse
import collections
import hashlib
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser(description=__doc__)
p.add_argument('--corpus', type=Path, default=ROOT/'evidence/npc-restoration-2026-09-15/corpus-raw.jsonl')
p.add_argument('--output', type=Path, default=ROOT/'evidence/recovery-opportunities-2026-09-17/audit.json')
a = p.parse_args()
raw = a.corpus.read_bytes()
rows = [json.loads(line) for line in raw.splitlines() if line.strip()]
groups = collections.defaultdict(list)
unknown = collections.Counter()
def visit(value):
    if isinstance(value, dict):
        if 'h' in value and 'size' in value and not value.get('path'):
            unknown[str(value['h'])] += 1
        for child in value.values():
            visit(child)
    elif isinstance(value, list):
        for child in value:
            visit(child)
for n, r in enumerate(rows, 1):
    if 'words' in r:
        groups[r['opcode']].append((n, r))
    visit(r)
known = {
    '0x0195': 'src/AetherXIV.Core.Map/Packets/Send/Actor/Battle/SetEnmityIndicatorPacket.cs',
    '0x00DB': 'src/AetherXIV.Core.Map/Packets/Send/Actor/SetActorTargetPacket.cs',
}
result = {
    'input': str(a.corpus.relative_to(ROOT)) if a.corpus.is_relative_to(ROOT) else str(a.corpus),
    'sha256': hashlib.sha256(raw).hexdigest(),
    'records': len(rows),
    'captures': len({r['capture'] for r in rows}),
    'captureStreams': len({(r['capture'], r['tcpStream']) for r in rows}),
    'decodedFlagTrue': sum(r.get('decoded') is True for r in rows),
    'recordsContainingWords': sum(map(len, groups.values())),
    'limitations': [
        'words is a structural triage marker, not proof a packet is semantically unknown.',
        'Existing server names are cross-reference leads, not independent retail validation.',
        'frameIndex is the decoder frame index; corpusLine is one-based.',
        'Unknown hash counts include recursively visited segments and dangling values with a missing or empty path.'
    ],
    'unknownPropertyHashes': dict(sorted(unknown.items())),
    'wordPacketFamilies': []
}
for op, items in sorted(groups.items()):
    family = {'opcode': op, 'records': len(items),
              'captures': sorted({r['capture'] for _, r in items}),
              'payloadLengths': dict(sorted(collections.Counter(r['payloadBytes'] for _, r in items).items())),
              'samples': [{'corpusLine': n, **{k: r[k] for k in ('capture', 'tcpStream', 'direction', 'frameIndex')}} for n,r in items[:3]]}
    if op in known:
        source = ROOT / known[op]
        family['existingImplementation'] = known[op]
        family['implementationSha256'] = hashlib.sha256(source.read_bytes()).hexdigest()
    result['wordPacketFamilies'].append(family)
a.output.parent.mkdir(parents=True, exist_ok=True)
a.output.write_text(json.dumps(result, indent=2)+'\n')
print(json.dumps({k: v for k,v in result.items() if k not in ('wordPacketFamilies', 'limitations')}, indent=2))

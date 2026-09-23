#!/usr/bin/env python3
"""Inventory existing decoded retail inputs without inferring missing stats.

This audits decoded evidence, not every byte of the original captures. Unknown
packet fields remain unknown. Output is research evidence, never runtime config.
"""
import argparse
from collections import Counter
import hashlib
import json
from pathlib import Path
import re
import xml.etree.ElementTree as ET


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def audit(root):
    captures = []
    for name in ('combat_autoattack', 'combat_skills', 'party_battle_leve'):
        path = root / 'evidence/player-growth-2026-09-17' / (name + '.json')
        data = json.loads(path.read_text())
        streams = []
        for stream in data['streams']:
            counts = Counter()
            observations = stream['observations']
            for row in observations:
                for segment in row.get('decoded', {}).get('segments', []):
                    for value in segment.get('values', []):
                        if value.get('path'):
                            counts[value['path']] += 1
            streams.append({'id': stream['id'], 'observations': len(observations),
                            'decodedPathCounts': dict(sorted(counts.items()))})
        captures.append({'file': str(path.relative_to(root)), 'sha256': digest(path),
                         'capture': data['capture'], 'streams': streams})

    logs = []
    for path in sorted((root / 'FFXIV Parse/ParseModXIV/Logs/ParseMod').glob('*_Log.xml')):
        if 'Unmatched' in path.name:
            continue
        rows = ET.parse(path).getroot().findall('Line')
        # The historical parser discussion documents /echo stats: as a manual
        # annotation. Absence does not rule out other undocumented annotations.
        indices = [i for i, row in enumerate(rows, 1)
                   if re.search(r'\bstats\s*:', row.get('Value', ''), re.I)]
        logs.append({'file': str(path.relative_to(root)), 'sha256': digest(path),
                     'lineCount': len(rows), 'manualStatsAnnotationLineIndices': indices})
    return {'schemaVersion': 1, 'captures': captures, 'parserLogs': logs,
            'limitations': [
                'Only named decoded evidence files and original parser logs were inspected.',
                'No stat values are carried between separate captures or actors.',
                'Missing decoded paths do not prove the raw packets lack those inputs.',
                'Damage observations without controlled inputs cannot identify a unique formula.',
                'Emulator-generated observations cannot validate historical retail coefficients.']}


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--root', type=Path, default=Path(__file__).resolve().parents[2])
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = audit(args.root)
    output = args.output or args.root / 'evidence/offensive-scaling-2026-09-17/input-audit.json'
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(json.dumps(result, indent=2) + '\n')
    print(f"Audited {len(result['captures'])} decoded captures and {len(result['parserLogs'])} parser logs: {output}")

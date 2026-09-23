#!/usr/bin/env python3
"""Read-only Alc200 evidence probe. Does not enable or advance a quest.

Uses the existing 1.x decoder. Emits identifiers and provenance, not a copy of
the localized dialogue. Run with /usr/bin/python3 on the evidence workstation.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path,
                        default=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV'))
    args = parser.parse_args()
    root = Path(__file__).resolve().parents[2]
    spec = importlib.util.spec_from_file_location(
        'sheets', root / 'tools/Universal/recover-legacy-enemy-ids.py')
    sheets = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(sheets)
    sheets.ROOT = args.client
    sources = set()

    def select(schema, ids):
        sources.add(schema)
        result = [r for r in sheets.rows(schema) if r['id'] in ids]
        for r in result:
            sources.add(r['dataFile'])
        return result

    expected = {3020401: 'Eye Drops', 11000076: 'Mummified Mole',
                11000077: 'Potent Medication'}
    items = select(189071399, expected)
    actual = {r['id']: r['values'].get(5) for r in items}
    if actual != expected:
        raise ValueError(f'Client item identity mismatch: {actual}')
    quest = select(189072473, {110420})
    if len(quest) != 1 or quest[0]['values'].get(2) != 'Sleep, Cousin of Death':
        raise ValueError('Client quest identity mismatch')
    journal = select(189073482, set(range(68, 73)))
    if len(journal) != 5 or 'Nomomo' not in journal[0]['values'].get(1, ''):
        raise ValueError('Journal source mismatch')
    result = {
        'questId': 110420, 'className': 'Alc200',
        'questTitle': quest[0]['values'][2], 'items': actual,
        'journalRows': [dict(id=r['id'], dataFile=r['dataFile'], offset=r['offset'])
                        for r in journal],
        'questColumnsUninterpreted': select(16974214, {110420}),
        'rewardColumnsUninterpreted': select(16975197, {110420}),
        'sources': [dict(resourceId=i, path=str(sheets.path(i)),
                         sha256=hashlib.sha256(sheets.path(i).read_bytes()).hexdigest())
                    for i in sorted(sources)],
        'limitation': 'Names and journal linkage verified; not recipe quantities, '
                      'synthesis formulas, stage routing, rewards or runtime completion.'
    }
    print(json.dumps(result, indent=2))


if __name__ == '__main__':
    main()

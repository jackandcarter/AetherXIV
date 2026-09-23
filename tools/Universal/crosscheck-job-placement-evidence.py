#!/usr/bin/env python3
"""Read-only marker/grid cross-check and optional collision-height hypotheses.

Writes research artifacts only. Never produces spawn SQL or authoritative Y.
"""
import argparse
import hashlib
import importlib.util
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLIENT = Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
OUT = ROOT / 'evidence/job-npc-placement-2026-09-18'
NAMES = {'Jehantel', 'Pukno Poki', 'Lalai', 'Alberic', 'Erik', 'Widargelt',
         'Curious Gorge', 'Raya-O-Senna', 'Dozol Meloc',
         '269th Order Mendicant Da Za', 'Kazagg Chah'}
# Historical issuer grid cells from official 1.21 notes; not XYZ inputs.
ISSUER_GRIDS = {'Jehantel': [38, 48], 'Lalai': [7, 6], 'Erik': [7, 3],
                'Widargelt': [38, 31], 'Curious Gorge': [15, 33],
                'Raya-O-Senna': [15, 22], 'Dozol Meloc': [11, 28],
                'Alberic': [35, 18]}
# Existing dftwil comments are unverified leads, NOT independent measurements.
COMMENT_Y = {'Lalai': 206, 'Erik': 192.1, 'Widargelt': 251.439,
             'Curious Gorge': 53.2, 'Dozol Meloc': 10.617, 'Kazagg Chah': 10.241}


def module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    result = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(result)
    return result


def save(name, value):
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT / name).write_text(json.dumps(value, indent=2, allow_nan=False) + '\n')


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--geometry', action='store_true')
    args = parser.parse_args()
    sheets = module('placement_sheets', ROOT / 'tools/Universal/recover-legacy-enemy-ids.py')
    sheets.ROOT = CLIENT
    labels = {r['id']: r['values'][1] for r in sheets.rows(189071360)
              if r['values'].get(1) in NAMES}
    actors = [r for r in sheets.rows(16973832) if r['values'].get(5) in labels]
    markers = [r for r in sheets.rows(16974636) if r['values'].get(4) in labels]
    source_ids = {189071360, 16973832, 16974636}
    for schema in list(source_ids):
        for sheet in sheets.xml(schema).findall('sheet'):
            if sheet.get('lang') not in (None, 'en'):
                continue
            for block in sheet.findall('block/file'):
                source_ids.update(int(v) for v in [block.text, block.get('enable'), block.get('offset')] if v)
    sources = [dict(id=i, path=str(sheets.path(i)),
                    sha256=hashlib.sha256(sheets.path(i).read_bytes()).hexdigest())
               for i in sorted(source_ids)]
    nav_path = ROOT / '.local-evidence/umbra-map-travel/sheets/mapNavi_data.json'
    nav = json.loads(nav_path.read_text())
    resources = [dict(path=nav['schemaPath'], sha256=nav['schemaSha256'])]
    for sub in nav['subSheets']:
        for block in sub['blocks']:
            resources.extend(block['resources'])
    for resource in resources:
        assert hashlib.sha256(Path(resource['path']).read_bytes()).hexdigest() == resource['sha256']
    results = []
    samples = {}
    for marker in markers:
        v = marker['values']
        name = labels[v[4]]
        matching = [r for sub in nav['subSheets'] for r in sub['rows']
                    if r['values'][:3] == [v[8], v[9], 0]]
        assert len(matching) == 1, (name, matching)
        maprow = matching[0]
        origin = [-maprow['values'][3], -maprow['values'][4]]
        grid = [(v[2] - origin[0]) / 100, (v[3] - origin[1]) / 100]
        cell = list(map(math.trunc, grid))
        expected = ISSUER_GRIDS.get(name)
        results.append(dict(name=name, marker=marker, navigationRow=maprow,
                            continuousGrid=grid, gridCell=cell,
                            historicalIssuerGrid=expected,
                            matchesIssuerGrid=cell == expected if expected else None))
        if name in COMMENT_Y:
            samples[(name, v[2], v[3])] = dict(name=name, x=v[2], y=COMMENT_Y[name], z=v[3])
    save('marker-grid-crosscheck.json', dict(
        researchOnly=True, formula='trunc((X + nav[3])/100), trunc((Z + nav[4])/100)',
        formulaEvidence='.local-evidence/umbra-map-travel/grid-evidence.json',
        navigationExtractionSha256=hashlib.sha256(nav_path.read_bytes()).hexdigest(),
        navigationResources=resources, markerResources=sources, actors=actors, markers=results,
        limitations=['Map marker is not a spawn record.', 'No elevation, facing or phase derived.',
                     'Nonmatching issuer grids may be alternate quest destinations.']))
    print(json.dumps([dict(name=r['name'], marker=r['marker']['id'], grid=r['gridCell'],
                           issuerMatch=r['matchesIssuerGrid']) for r in results], indent=2))
    if args.geometry:
        probe = module('placement_probe', ROOT / 'tools/Development/probe-zone-geometry.py')
        for lid in range(0x615A0004, 0x615A0009):
            report = probe.probe(CLIENT / 'data', lid, list(samples.values()), 1)
            report['referenceCaution'] = 'Y references are unverified dftwil comments; error metrics are NOT validation.'
            report['scopeCaution'] = 'Five candidate layouts, yaw-only; no definitive zone binding or surface filtering.'
            save(f'job-height-hypotheses-{lid:08X}.json', report)
            print(f'{lid:08X}: ' + json.dumps(report['summary']), flush=True)


if __name__ == '__main__':
    main()

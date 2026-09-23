#!/usr/bin/env python3
"""Test captured actor positions against typed legacy sheets and known layouts.
Research only: observations need not be spawn origins. No server writes.
"""
import collections
import hashlib
import importlib.util
import json
import math
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
CLIENT = Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
OUT = ROOT / 'evidence/captured-placement-probe-2026-09-18'

def load_module(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    m = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(m)
    return m

def save(name, value):
    OUT.mkdir(parents=True, exist_ok=True)
    (OUT/name).write_text(json.dumps(value, indent=2, allow_nan=False)+'\n')

def main():
    source = ROOT/'evidence/npc-restoration-2026-09-15/observations.json'
    observations = json.loads(source.read_text())
    controls = []
    for kind in ('battle', 'populace', 'object-or-other'):
        seen = set()
        for row in observations:
            if row['kind'] != kind or not row['identityChainValid'] or row['correlationWindowMs'] != 0:
                continue
            if row['actorClassId'] in seen:
                continue
            if not all(math.isfinite(row['position'+a]) for a in 'XYZ'):
                continue
            seen.add(row['actorClassId'])
            controls.append(row)
            if len(seen) == 8:
                break
    save('controls.json', dict(sourceSha256=hashlib.sha256(source.read_bytes()).hexdigest(),
         selection='First eight distinct actor classes per kind, valid identity and same-frame position/name; purposive not representative.',
         inputRows=len(observations), controls=controls))
    sheets = load_module('placement_sheets', ROOT/'tools/Universal/recover-legacy-enemy-ids.py')
    sheets.ROOT = CLIENT
    catalog = sheets.xml(0x01030000)
    audit, hits, resource_ids = [], [], {0x01030000}
    for entry in catalog.findall('sheet'):
        name, sid = entry.get('name'), int(entry.get('infofile'))
        resource_ids.add(sid)
        try:
            schema = sheets.xml(sid)
            for sub in schema.findall('sheet'):
                if sub.get('lang') not in (None, 'en'):
                    continue
                for b in sub.findall('block/file'):
                    resource_ids.update(int(v) for v in [b.text,b.get('enable'),b.get('offset')] if v)
            count = 0
            for row in sheets.rows(sid):
                count += 1
                values = row['values']
                for c in controls:
                    identifiers = {i for i in (c['actorClassId'],c['displayNameId']) if isinstance(i,int) and i > 0}
                    identity = [k for k,v in values.items() if isinstance(v,int) and v in identifiers]
                    row_id_match = row['id'] in identifiers
                    name_match = [k for k,v in values.items() if isinstance(v,str) and c['objectName'] in v]
                    # Positional columns unknown: retain distinct float-column X/Z matches.
                    xcols = [k for k,v in values.items() if isinstance(v,float) and abs(v-c['positionX']) <= .05]
                    zcols = [k for k,v in values.items() if isinstance(v,float) and abs(v-c['positionZ']) <= .05]
                    pairs = [(x,z) for x in xcols for z in zcols if x != z]
                    if identity or row_id_match or name_match or pairs:
                        hits.append(dict(sheet=name, schema=sid, actorClassId=c['actorClassId'],
                             row=row, identityColumns=identity, rowIdMatch=row_id_match,
                             objectNameColumns=name_match, coordinateColumnCandidates=pairs))
            audit.append(dict(sheet=name,schema=sid,rows=count,status='decoded'))
        except Exception as e:
            audit.append(dict(sheet=name,schema=sid,status='error',error=str(e)))
        print('sheet', name, audit[-1]['status'], flush=True)
    save('sheet-hits.json', hits)
    save('sheet-audit.json', audit)
    save('sheet-inputs.json', [dict(id=i,path=str(sheets.path(i)),sha256=hashlib.sha256(sheets.path(i).read_bytes()).hexdigest()) for i in sorted(resource_ids) if sheets.path(i).exists()])
    # Expand the successful named-marker join to all valid observations.
    by_display = collections.defaultdict(list)
    for marker in sheets.rows(16974636):
        if marker['values'].get(4,0) > 0:
            by_display[marker['values'][4]].append(marker)
    comparisons = []
    for o in observations:
        if not o['identityChainValid'] or o.get('displayNameId',0) <= 0:
            continue
        for marker in by_display.get(o['displayNameId'],[]):
            v = marker['values']
            dx,dz = v[2]-o['positionX'],v[3]-o['positionZ']
            comparisons.append(dict(capture=o['capture'],frame=o['frameIndex'],actorClassId=o['actorClassId'],
                runtimeActorId=o['runtimeActorId'],kind=o['kind'],zoneId=o['zoneId'],displayNameId=o['displayNameId'],
                markerId=marker['id'],markerMapIds=[v[8],v[9]],dx=dx,dz=dz,
                sameXZ=abs(dx)<=.05 and abs(dz)<=.05,
                caution='Same display name does not establish same zone, phase or actor variant.'))
    save('all-observation-marker-crosscheck.json',comparisons)
    geometry = load_module('placement_geometry', ROOT/'tools/Development/extract-zone-geometry.py')
    inventory_path = ROOT/'.local-evidence/zone-geometry/header-inventory.json'
    inventory = json.loads(inventory_path.read_text())
    layout_audit, layout_hits = [], []
    for item in inventory['layouts_or_collision']:
        if not bytes.fromhex(item['header']).startswith(b'MapLayoutResourceData'):
            continue
        path = CLIENT/'data'/item['path']
        raw = path.read_bytes()
        record = dict(path=str(path),sha256=hashlib.sha256(raw).hexdigest())
        try:
            resources,nodes = geometry.layout(raw)
            record.update(nodes=len(nodes),status='decoded')
            for c in controls:
                if c['objectName'].encode() in raw:
                    layout_hits.append(dict(actorClassId=c['actorClassId'],path=str(path),objectNameMatch=True))
            for node in nodes:
                p = node.get('position')
                for c in controls:
                    near = p and abs(p[0]-c['positionX']) <= .05 and abs(p[2]-c['positionZ']) <= .05
                    if near:
                        layout_hits.append(dict(actorClassId=c['actorClassId'],path=str(path),node=node,
                                                caution='Local transform only, not established world position.'))
        except Exception as e:
            record.update(status='error',error=str(e))
        layout_audit.append(record)
    save('layout-hits.json', layout_hits)
    save('layout-audit.json',dict(inventorySha256=hashlib.sha256(inventory_path.read_bytes()).hexdigest(),layouts=layout_audit))
    summary = dict(controls=len(controls), sheets=len(audit),sheetErrors=[a for a in audit if a['status']=='error'],
                   sheetRows=sum(a.get('rows',0) for a in audit), identityHits=sum(bool(h['identityColumns'] or h['rowIdMatch']) for h in hits),
                   coordinateHits=sum(bool(h['coordinateColumnCandidates']) for h in hits),
                   objectNameHits=sum(bool(h['objectNameColumns']) for h in hits),
                   layouts=len(layout_audit),layoutErrors=sum(a['status']=='error' for a in layout_audit),layoutHits=len(layout_hits),
                   limitations=['Observed enemy positions are not guaranteed spawn origins.',
                     'Only installed sheet catalog and previously inventoried layouts; not all client asset types.',
                     'English/nonlocalized sheets only; decoder errors are coverage gaps.',
                     'Float X/Z tolerance 0.05; packed, integer, relative or indirect encodings may evade search.',
                     'Layout search uses local node positions, not composed world transforms.',
                     'Negative results cannot prove server-only storage.'])
    save('summary.json',summary)
    print(json.dumps(summary,indent=2))

if __name__ == '__main__':
    main()

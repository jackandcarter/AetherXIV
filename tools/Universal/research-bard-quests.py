#!/usr/bin/env python3
"""Read-only Bard source extraction using the existing 1.x sheet decoder.
Unknown positional columns are preserved, not assigned inferred meanings.
Run with /usr/bin/python3 from any directory. No database writes.
"""
import hashlib
import importlib.util
import json
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'evidence/bard-quests-2026-09-17'
CLIENT = Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
spec = importlib.util.spec_from_file_location('sheets', ROOT / 'tools/Universal/recover-legacy-enemy-ids.py')
sheets = importlib.util.module_from_spec(spec)
spec.loader.exec_module(sheets)
sheets.ROOT = CLIENT
OUT.mkdir(parents=True, exist_ok=True)
audit = []

def extract(name, schema, predicate):
    rows = [r for r in sheets.rows(schema) if predicate(r)]
    resources = {schema} | {r['dataFile'] for r in rows}
    audit.append({'name': name, 'schema': schema, 'rows': len(rows), 'resources': [
        {'id': rid, 'path': str(sheets.path(rid)), 'sha256': hashlib.sha256(sheets.path(rid).read_bytes()).hexdigest()}
        for rid in sorted(resources)]})
    (OUT / (name + '.json')).write_text(json.dumps(rows, indent=2) + '\n')
    return rows

names = extract('actor-names', 189071360, lambda r: any(n in str(r['values']).lower()
    for n in ('jehantel', 'pukno', 'bardi', 'phaia', 'qiqirn shirrer')))
name_ids = {r['id'] for r in names}
actors = extract('actor-classes', 16973832, lambda r: r['values'].get(5) in name_ids)
actor_ids = {r['id'] for r in actors}
(OUT / 'actor-identities.json').write_text(json.dumps({'names': names, 'actors': actors}, indent=2) + '\n')
for name, schema in [('quest',16974214), ('_quest',16974969), ('quest_reward',16975138),
                     ('quest_new_reward',16975197), ('xtx_quest',189072473)]:
    extract(name, schema, lambda r: 111301 <= r['id'] <= 111306)
extract('populace',16974189,lambda r:r['id'] in actor_ids)
extract('journal',189073456,lambda r:433 <= r['id'] <= 454)
# Retain rejected marker candidates to document why ID-prefix matching is unsafe.
extract('quest_marker',16974636,lambda r:11130100 <= r['id'] < 11130700)
(OUT / 'sheet-provenance.json').write_text(json.dumps(audit,indent=2)+'\n')
print(json.dumps([{'name':r['name'],'rows':r['rows']} for r in audit],indent=2))

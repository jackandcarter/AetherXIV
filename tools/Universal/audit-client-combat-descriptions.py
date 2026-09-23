#!/usr/bin/env python3
"""Read-only inventory of original client command descriptions and server script rules.
Uses the existing typed sheet decoder; tags are search aids, not proven mechanics.
"""
import argparse, collections, hashlib, importlib.util, json, re
from pathlib import Path
ROOT = Path(__file__).resolve().parents[2]
p = argparse.ArgumentParser()
p.add_argument('--client', type=Path, default=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV'))
p.add_argument('--out', type=Path, default=ROOT/'evidence/combat-description-audit-2026-09-17')
a = p.parse_args()
spec = importlib.util.spec_from_file_location('decoder', ROOT/'tools/Universal/recover-legacy-enemy-ids.py')
d = importlib.util.module_from_spec(spec); spec.loader.exec_module(d); d.ROOT = a.client
patterns = {'combo':r'combo', 'damage':r'damage|attack', 'numeric':r'\d',
 'position_range':r'front|behind|rear|flank|yalm|range', 'critical':r'critical',
 'status':r'inflict|effect|bleed|poison|stun', 'resource':r'\bHP\b|\bMP\b|\bTP\b',
 'element':r'fire|ice|wind|earth|lightning|water|astral|umbral'}
rows=[]
for r in d.rows(189071383):
 v=r['values']; text=v.get(23,'');
 rows.append(dict(id=r['id'],name=v.get(2,''),description=text,
                  tags=[k for k,pat in patterns.items() if re.search(pat,text,re.I)],
                  dataFile=r['dataFile'],offset=r['offset']))
files={r['dataFile'] for r in rows}|{189071383}
script_rules=[]
for path in sorted((ROOT/'Data/scripts').rglob('*.lua')):
 if not ('commands' in path.parts or 'effects' in path.parts):continue
 matches=[{'line':i,'text':line.strip()} for i,line in enumerate(path.read_text().splitlines(),1)
          if not line.lstrip().startswith('--') and re.search(r'action\.amount\s*=|basePotency\s*=|statusMagnitude\s*=|statusChance\s*=|bonusCritRate\s*=|enmityModifier\s*=|numHits\s*=',line)]
 if matches:script_rules.append({'path':str(path.relative_to(ROOT)),'sha256':hashlib.sha256(path.read_bytes()).hexdigest(),'rules':matches})
summary={'rows':len(rows),'withDescriptions':sum(r['description'].strip() not in ('','-') for r in rows),
 'tagCounts':dict(collections.Counter(t for r in rows for t in r['tags'])),
 'scriptsWithRuleAssignments':len(script_rules),
 'caveat':'All command label rows, not a count of implemented or obtainable abilities. Includes variants and noncombat commands. Tags are lexical, not semantic decoding.'}
a.out.mkdir(parents=True,exist_ok=True)
for name,obj in [('command-descriptions.json',rows),('server-script-rules.json',script_rules),('summary.json',summary),
 ('provenance.json',{'clientRoot':str(a.client),'schema':189071383,'nameColumn':2,'descriptionColumn':23,
 'files':[{'id':i,'sha256':hashlib.sha256(d.path(i).read_bytes()).hexdigest()} for i in sorted(files)]})]:
 (a.out/name).write_text(json.dumps(obj,indent=2,ensure_ascii=False)+'\n')
print(json.dumps(summary,indent=2))

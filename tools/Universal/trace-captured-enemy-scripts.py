#!/usr/bin/env python3
"""Trace captured enemy identities through all installed little-endian LPBs.
No gameplay changes. Literal matches are references, not spawn definitions.
"""
import collections
import hashlib
import importlib.util
import json
import re
import sys
from pathlib import Path

ROOT=Path(__file__).resolve().parents[2]
CLIENT=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
OUT=ROOT/'evidence/enemy-script-trace-2026-09-18'

def save(name,value):
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/name).write_text(json.dumps(value,indent=2)+'\n')

def main():
    tool=ROOT/'.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py'
    spec=importlib.util.spec_from_file_location('enemy_lpb',tool)
    lpb=importlib.util.module_from_spec(spec);sys.modules[spec.name]=lpb;spec.loader.exec_module(lpb)
    source=ROOT/'evidence/captured-placement-probe-2026-09-18/controls.json'
    controls=[c for c in json.loads(source.read_text())['controls'] if c['kind']=='battle']
    ids={c[k] for c in controls for k in ('actorClassId','displayNameId')}
    # Positive controls from the prior NPC marker cross-check.
    positive={11042106,11042108,11048102,11066302,11066402,1700001,1000951,1100404,1600102}
    audit=[];hits=[];leads=[]
    for index,path in enumerate(sorted((CLIENT/'client/script').rglob('*.le.lpb'))):
        raw=path.read_bytes();name=lpb.cipher(path.name[:-7])
        row=dict(path=str(path),name=name,sha256=hashlib.sha256(raw).hexdigest())
        try:
            decoded=lpb.decode_lpb(raw);reader=lpb.Reader(decoded);tree=reader.parse_root()
            if reader.p!=len(decoded):raise ValueError(f'Unconsumed bytes: {len(decoded)-reader.p}')
            funcs=[]
            def walk(f):
                funcs.append(f)
                for child in f.protos:walk(child)
            walk(tree)
            row.update(status='decoded',functions=len(funcs),decodedBytes=len(decoded))
            matched=False
            for f in funcs:
                reasons=[];symbols=[]
                numbers={v for t,v in f.constants if t==3}
                for i,(tag,v) in enumerate(f.constants):
                    if tag==3 and v in ids:reasons.append(dict(constant=i,type='enemy-id',value=v))
                    if tag==3 and v in positive:reasons.append(dict(constant=i,type='positive-control-id',value=v))
                    if tag==4:
                        for c in controls:
                            for label,needle in [('runtime-name',c['objectName']),('class-path',c['classPath']),('class-leaf',c['classPath'].rsplit('/',1)[-1]),('object-prefix',c['objectName'].split('_')[0])]:
                                if needle.lower() in v.lower():reasons.append(dict(constant=i,type=label,actorClassId=c['actorClassId'],value=v))
                        if re.search(r'spawn|popgroup|poprange|respawn|territory|encounter|createactor|createnpc|createmonster',v,re.I):symbols.append(v)
                for c in controls:
                    if any(abs(n-c['positionX'])<.05 for n in numbers) and any(abs(n-c['positionZ'])<.05 for n in numbers):
                        reasons.append(dict(type='coordinate-pair-in-function',actorClassId=c['actorClassId']))
                if reasons:
                    hits.append(dict(script=name,path=str(path),source=f.source,lines=[f.linedefined,f.lastlinedefined],reasons=reasons,
                                     constants=[dict(type=t,value=v) for t,v in f.constants]))
                    matched=True
                if symbols:leads.append(dict(script=name,path=str(path),lines=[f.linedefined,f.lastlinedefined],symbols=sorted(set(symbols))))
            if matched or name in {'publicpopgroup','publicpopgroupdirector','pgharvestpointencounter','areabaseclass','judgebaseclass','monsterbaseclass'}:
                OUT.mkdir(parents=True,exist_ok=True)
                # Hash suffix prevents same-name scripts overwriting each other.
                (OUT/(name.replace('/','_')+'-'+row['sha256'][:10]+'.dis.txt')).write_text('\n'.join(lpb.dump_function(tree))+'\n')
        except Exception as exc:row.update(status='error',error=str(exc))
        audit.append(row)
        if index%200==0:print(index,name,flush=True)
    save('audit.json',audit);save('matches.json',hits);save('encounter-symbol-leads.json',leads)
    by_name=collections.defaultdict(list)
    for entry in audit:by_name[entry['name']].append(entry)
    pending=[c['classPath'].rsplit('/',1)[-1].lower() for c in controls]+['publicpopgroup','publicpopgroupdirector','pgharvestpointencounter','areabaseclass']
    dependencies=[];seen=set()
    while pending:
        name=pending.pop()
        if name in seen:continue
        seen.add(name)
        entries=by_name.get(name,[])
        if len(entries)!=1:
            dependencies.append(dict(name=name,status='missing-or-ambiguous',candidates=len(entries)));continue
        entry=entries[0];raw=Path(entry['path']).read_bytes();reader=lpb.Reader(lpb.decode_lpb(raw));tree=reader.parse_root()
        paths=[v for t,v in tree.constants if t==4 and v.startswith('/')]
        dependencies.append(dict(name=name,status='decoded',path=entry['path'],sha256=entry['sha256'],
            rootPathConstants=paths,rootConstants=[dict(type=t,value=v) for t,v in tree.constants],
            rootInstructions=len(tree.code),childFunctions=len(tree.protos),
            caution='Root path constants are dependency candidates; inspect require instructions.'))
        (OUT/(name+'-'+entry['sha256'][:10]+'.dis.txt')).write_text('\n'.join(lpb.dump_function(tree))+'\n')
        pending.extend(v.rsplit('/',1)[-1].lower() for v in paths)
    save('dependency-chains.json',dependencies)
    save('provenance.json',dict(client=str(CLIENT),version=(CLIENT/'game.ver').read_text().strip(),decoderSha256=hashlib.sha256(tool.read_bytes()).hexdigest(),controlsSha256=hashlib.sha256(source.read_bytes()).hexdigest(),controls=controls))
    summary=dict(scripts=len(audit),decoded=sum(r['status']=='decoded' for r in audit),errors=[r for r in audit if r['status']=='error'],
                 matchedFunctions=len(hits),reasonCounts=dict(collections.Counter(r['type'] for h in hits for r in h['reasons'])),encounterSymbolFunctions=len(leads),
                 limitations=['LPB constants only; computed/indirect IDs and packed encounter resources not exhausted.',
                              'Class/prefix matches may be generic behavior, not placement.',
                              'NPC marker positives need not be script literals; absence does not invalidate sheet evidence.'])
    save('summary.json',summary);print(json.dumps(summary,indent=2))

if __name__=='__main__':main()

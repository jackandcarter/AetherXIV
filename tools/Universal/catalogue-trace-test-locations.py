#!/usr/bin/env python3
"""Produce reviewable 1.x NPC/enemy test locations, never spawn SQL.
Validated initialization observations anchor identity. Movement is joined only
until remove/reinitialize/map-context change, within capture and TCP stream.
"""
import collections,hashlib,importlib.util,json,math,struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
SRC=ROOT/'evidence/npc-restoration-2026-09-15'
OUT=ROOT/'evidence/trace-test-locations-2026-09-18'

def save(name,value):
    OUT.mkdir(parents=True,exist_ok=True)
    (OUT/name).write_text(json.dumps(value,indent=2,allow_nan=False)+'\n')

def main():
    observations=json.loads((SRC/'observations.json').read_text())
    valid=[o for o in observations if o['identityChainValid']]
    anchors={(o['capture'],o['tcpStream'],o['frameIndex'],o['runtimeActorId']):o for o in valid}
    capture_hashes={}
    for o in valid:
        if o['capture'] not in capture_hashes:
            capture_hashes[o['capture']]=hashlib.sha256((ROOT/'ffxiv_traces'/o['capture']).read_bytes()).hexdigest()
        assert capture_hashes[o['capture']]==o['captureSha256']
    spec=importlib.util.spec_from_file_location('sheets',ROOT/'tools/Universal/recover-legacy-enemy-ids.py')
    sheets=importlib.util.module_from_spec(spec);spec.loader.exec_module(sheets)
    sheets.ROOT=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
    names={r['id']:r['values'].get(1,'') for r in sheets.rows(189071360)}
    client_classes={r['id']:r['values'].get(5) for r in sheets.rows(16973832)}
    additional=[o for o in observations if not o['identityChainValid']
        and o['checks']['actorIdZone']==o['checks']['containerZone']==o['checks']['objectZone']
        and o['actorClassId'] in client_classes and client_classes[o['actorClassId']]==o['displayNameId']
        and o['kind'] in ('battle','populace')]
    valid += additional
    anchors={(o['capture'],o['tcpStream'],o['frameIndex'],o['runtimeActorId']):o for o in valid}
    for o in additional:
        if o['capture'] not in capture_hashes:
            capture_hashes[o['capture']]=hashlib.sha256((ROOT/'ffxiv_traces'/o['capture']).read_bytes()).hexdigest()
        assert capture_hashes[o['capture']]==o['captureSha256']
    snapshot=json.loads((SRC/'game-data-snapshot.json').read_text())
    zones={int(z['id']):z for z in snapshot['server_zones']}
    rows=[];captures=set();errors=collections.Counter()
    for number,line in enumerate((SRC/'corpus-raw.jsonl').open()):
        r=json.loads(line);captures.add(r['capture'])
        if 'error' in r:errors[r['capture']]+=1
        if r.get('direction')=='server-to-client' and r.get('opcode') in {'0x0005','0x00CA','0x00CB','0x00CC','0x00CE','0x00CF'}:
            rows.append((number,r))
    rows.sort(key=lambda p:(p[1]['capture'],p[1]['tcpStream'],p[1]['frameIndex'],p[0]))
    active={};lifetimes=[];unlinked=collections.Counter();unidentified=[]
    for number,r in rows:
        scope=(r['capture'],r['tcpStream']);key=scope+(r['sourceActorId'],)
        opcode=r['opcode']
        if opcode=='0x0005':
            for k in list(active):
                if k[:2]==scope:
                    active[k]['endReason']='map-context';active[k]['endFrame']=r['frameIndex'];del active[k]
        elif opcode in {'0x00CA','0x00CB','0x00CC'}:
            old=active.pop(key,None)
            if old:old.update(endReason=opcode,endFrame=r['frameIndex'])
            if opcode=='0x00CC':
                o=anchors.get(scope+(r['frameIndex'],r['sourceActorId']))
                if o:
                    life=dict(id=len(lifetimes)+1,anchor=o,endReason='capture-end-or-no-observed-boundary',endFrame=None,positions=[])
                    lifetimes.append(life);active[key]=life
        elif opcode in {'0x00CE','0x00CF'}:
            life=active.get(key)
            raw=bytes.fromhex(r['payloadHex'])
            if len(raw)<24:unlinked['short-payload']+=1;continue
            x,y,z,rot=struct.unpack_from('<4f',raw,8)
            if not all(math.isfinite(v) for v in (x,y,z,rot)):unlinked['nonfinite']+=1;continue
            if not life:
                unlinked[opcode]+=1
                actor=int(r['sourceActorId'],16)
                inferred_zone=((actor>>19)&511) if actor>>28==4 else None
                unidentified.append(dict(capture=r['capture'],tcpStream=r['tcpStream'],frame=r['frameIndex'],
                    runtimeActorId=r['sourceActorId'],opcode=opcode,x=x,y=y,z=z,rotation=rot,
                    zoneIdCandidate=inferred_zone,zoneBasis='runtime-ID bit convention only; not independently corroborated' if inferred_zone is not None else 'unknown',
                    identity='unresolved; may be player, NPC, enemy or object',
                    coordinateMeaning='movement destination' if opcode=='0x00CF' else 'position update'))
                continue
            life['positions'].append(dict(frame=r['frameIndex'],timestamp=r['frameTimestamp'],opcode=opcode,x=x,y=y,z=z,rotation=rot))
    # Keep initial sightings even if position preceded init in a split bundle.
    groups=collections.defaultdict(list)
    for life in lifetimes:
        a=life['anchor'];groups[(a['zoneId'],a['actorClassId'],a['objectName'])].append(life)
    candidates=[]
    for (zone,cid,obj),ls in sorted(groups.items()):
        first=ls[0]['anchor'];allpos=[]
        for life in ls:
            a=life['anchor']
            allpos.append((a['positionX'],a['positionY'],a['positionZ']))
            allpos.extend((p['x'],p['y'],p['z']) for p in life['positions'])
        lo=[min(p[i] for p in allpos) for i in range(3)];hi=[max(p[i] for p in allpos) for i in range(3)]
        candidates.append(dict(candidateId=f'T{len(candidates)+1:04}',name=names.get(first['displayNameId']) or f'Unnamed class {cid}',
            kind=first['kind'],zoneId=zone,zoneName=zones.get(zone,{}).get('placeName') or zones.get(zone,{}).get('zoneName','Unknown'),
            actorClassId=cid,objectName=obj,x=first['positionX'],y=first['positionY'],z=first['positionZ'],rotation=first['rotation'],
            identityTier='prior-catalogue-corroborated' if first['identityChainValid'] else 'capture-zone-and-client-name-corroborated; prior-server-definition-differs',
            basis='first validated initialization sighting; NOT confirmed spawn origin',
            correlationWindowMs=first['correlationWindowMs'],capture=first['capture'],tcpStream=first['tcpStream'],frame=first['frameIndex'],
            lifetimeIds=[l['id'] for l in ls],positionSamples=len(allpos),movementPackets=sum(p['opcode']=='0x00CF' for l in ls for p in l['positions']),
            bounds=dict(min=lo,max=hi),boundsDiagonal=math.dist(lo,hi),
            instancePolicy='unresolved; zone alone is not proof of ambient/root placement'))
    save('lifetimes.json',lifetimes);save('candidates.json',candidates)
    save('unidentified-positions.json',unidentified)
    unknown_groups=collections.defaultdict(list)
    for r in unidentified:unknown_groups[(r['capture'],r['tcpStream'],r['runtimeActorId'])].append(r)
    unknown_lines=['# Unidentified trace sightings','',
        'No spawns added. These records lack a validated identity/lifetime join. They may be players or non-character objects.',
        'One representative coordinate per capture/stream/runtime ID is shown (prefer a position update over a movement destination).',
        'A numeric zone marked ? is inferred from runtime-ID bits only. Unknown zones must not be guessed for warping.',
        'Every raw coordinate observation is retained in `unidentified-positions.json`; this table is a navigation index, not a spawn count.','',
        '| ID | Candidate zone | Runtime actor | X | Y | Z | Basis | Capture / stream / frame |','|---|---|---|---:|---:|---:|---|---|']
    unknown_index=[]
    for key,rs in sorted(unknown_groups.items()):
        r=next((p for p in rs if p['opcode']=='0x00CE'),rs[0]);uid=f'U{len(unknown_index)+1:04}'
        unknown_index.append(dict(id=uid,representative=r,observations=len(rs)))
        zone=f"{r['zoneIdCandidate']} ?" if r['zoneIdCandidate'] is not None else 'unknown'
        unknown_lines.append(f"| {uid} | {zone} | {r['runtimeActorId']} | {r['x']:.3f} | {r['y']:.3f} | {r['z']:.3f} | {r['coordinateMeaning']} | {r['capture']} / {r['tcpStream']} / {r['frame']} |")
    save('unidentified-index.json',unknown_index)
    (OUT/'UNIDENTIFIED.md').write_text('\n'.join(unknown_lines)+'\n')
    supplied={p.name for p in (ROOT/'ffxiv_traces').rglob('*.pcapng')}
    summary=dict(observationRows=len(observations),validAnchors=len(valid),additionalClientCorroboratedAnchors=len(additional),excludedIdentityRows=len(observations)-len(valid),
        lifetimes=len(lifetimes),candidates=len(candidates),kinds=dict(collections.Counter(c['kind'] for c in candidates)),
        unidentifiedGroups=len(unknown_groups),unidentifiedPositionRecords=len(unidentified),
        zones=dict(collections.Counter(c['zoneId'] for c in candidates)),unlinkedPositionPackets=dict(unlinked),
        corpusCaptures=len(captures),suppliedPcapng=len(supplied),capturesNotInCorpus=sorted(supplied-captures),decodeErrors=dict(errors),
        corpusSha256=hashlib.sha256((SRC/'corpus-raw.jsonl').read_bytes()).hexdigest(),captureHashes=capture_hashes,
        limits=['Existing decoded corpus; absent streams and undecodable packets are not recovered by this tool.',
               'Identity validation uses prior database catalogue snapshot; no live DB absence claim.',
               'Initialization is visibility evidence, not a confirmed birth or home point.',
               'Movement destinations may be targets rather than instantaneous occupied positions.',
               'Map context resets conservatively stop movement joins; instance/quest policy unresolved.'])
    save('summary.json',summary)
    lines=['# 1.x trace test locations','',
        'Review list only: no spawns installed. XYZ are world coordinates; rotation is the captured value.',
        'Each row is a first validated initialization sighting. Moving enemies may have wandered or entered combat.',
        'Different runtime names are separate candidates, not proof they should coexist. Quest/private-instance gating is unresolved.',
        'Names are from the installed 1.x client. Zone labels use the prior server catalogue snapshot.',
        'Rows marked † disagree with the prior server definition, but capture zone checks and installed-client class/name mapping agree.',
        'Full precision, capture/frame provenance, movement bounds and lifetime IDs are in `candidates.json` and `lifetimes.json`.',
        '','NPC/enemy rows are listed first; unnamed triggers and other objects are listed separately.','']
    for kindset,title in [({'populace','battle'},'NPCs and enemies'),({'object-or-other'},'Other objects and triggers — not enemy spawns')]:
        lines+=['## '+title,'']
        for zone in sorted({c['zoneId'] for c in candidates if c['kind'] in kindset}):
            selected=[c for c in candidates if c['zoneId']==zone and c['kind'] in kindset]
            lines += [f"### Zone {zone} — {selected[0]['zoneName']}",'','| ID | Name | Class | X | Y | Z | Movement packets |','|---|---|---:|---:|---:|---:|---:|']
            for c in selected:
                flag=' †' if c['identityTier'].startswith('capture-zone') else ''
                lines.append(f"| {c['candidateId']}{flag} | {c['name'].replace('|','/')} | {c['actorClassId']} | {c['x']:.3f} | {c['y']:.3f} | {c['z']:.3f} | {c['movementPackets']} |")
            lines.append('')
    (OUT/'LOCATIONS.md').write_text('\n'.join(lines)+'\n')
    print(json.dumps(summary,indent=2))

if __name__=='__main__':main()

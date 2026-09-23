#!/usr/bin/env python3
"""Research-only joins; never imports placements or modifies the database."""
import hashlib,json,re,struct,urllib.request
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'evidence/npc-quest-crossreference-2026-09-18'
def read(name):return json.loads((OUT/name).read_text())
def save(name,data):(OUT/name).write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n')
def main():
    npcs=read('npc-coverage.json');quests=read('quest-coverage.json')
    # The prior document transcribes the official 1.21 issuer table. Roles are
    # not inferred from script literals; script status is refreshed separately.
    mappings=[]
    for line in (ROOT/'docs/JOB_QUEST_NPC_STATUS_2026-09-17.md').read_text().splitlines():
        fields=[v.strip() for v in line.split('|')[1:-1]]
        if len(fields)!=6 or not fields[1].isdigit():continue
        job,level,action,title,issuer,grid=fields
        matches=[q for q in quests if q['questName']==title]
        mappings.append({'job':job,'level':int(level),'quest':title,'issuer':issuer,'legacyGridNotXYZ':grid,
            'questRows':matches,'issuerClientClasses':[{'actorClassId':n['actorClassId'],'status':n['status'],
            'appearancePresent':n['appearancePresent'],'spawns':n['spawns']} for n in npcs if n['name'].casefold()==issuer.casefold()],
            'source':'https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes'})
    assert len(mappings)==35
    save('official-job-issuer-crossreference.json',mappings)
    commit='70e54f33fc4ea2473d6d82b46b6d0a4567fb2986'
    manifest=[];comparison=[]
    for filename in ['059_restore_private_area_spawns.sql','061_restore_private_area_npc_classpaths.sql','096_man0u1_content_npcs.sql','099_man1l0_spawn_repairs.sql']:
        url=f'https://raw.githubusercontent.com/swstegall/Garlemald-Server/{commit}/common/sql/seed/{filename}'
        raw=urllib.request.urlopen(url,timeout=30).read();source=raw.decode()
        manifest.append({'url':url,'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(raw),'reviewedOn':'2026-09-18'})
        if filename.startswith('061'):
            for path,dn,flags,cid in re.findall(r'SET "classPath"=\x27([^\x27]+)\x27, "displayNameId"=(\d+), "propertyFlags"=(\d+) WHERE "id"=(\d+)',source):
                current=next((n for n in npcs if n['actorClassId']==int(cid)),None)
                if current and current['status']=='placed-unloadable-class':
                    comparison.append({'actorClassId':int(cid),'name':current['name'],'spawnIds':[s['id'] for s in current['spawns']],
                        'upstreamClassPath':path,'upstreamFlags':int(flags),'upstreamDisplayNameId':int(dn),
                        'clientDisplayMatches':int(dn)==current['displayNameId'],
                        'source':url,'caution':'Upstream restoration, not independent retail proof of flags/conditions; retain private-area ownership.'})
    save('external-project-source-manifest.json',manifest);save('upstream-class-repair-overlap.json',comparison)
    # Recheck selected observations against raw packets, independently of the
    # old report's class-path validation (which compares against our blank DB).
    observations=[o for n in npcs if n['actorClassId'] in (1090067,1090068) for o in n['traceObservations']]
    selected=[]
    for line in (ROOT/'evidence/npc-restoration-2026-09-15/corpus-raw.jsonl').open():
        r=json.loads(line)
        if any(r['capture']==o['capture'] and r['tcpStream']==o['tcpStream'] and r.get('sourceActorId')==o['runtimeActorId'] and r['frameIndex']==o['frameIndex'] for o in observations):selected.append(r)
    for o in observations:
        packets=[r for r in selected if r['capture']==o['capture'] and r['frameIndex']==o['frameIndex']]
        init=next(r for r in packets if r['opcode']=='0x00CC')
        pos=next(r for r in packets if r['opcode']=='0x00CE')
        name=next(r for r in packets if r['opcode']=='0x013D')
        assert init['initParams'][6]['v']==o['actorClassId']
        assert struct.unpack_from('<ffff',bytes.fromhex(pos['payloadHex']),8)==tuple(o[k] for k in ['positionX','positionY','positionZ','rotation'])
        assert struct.unpack_from('<I',bytes.fromhex(name['payloadHex']))[0]==o['displayNameId']
        assert hashlib.sha256((ROOT/'ffxiv_traces'/o['capture']).read_bytes()).hexdigest()==o['captureSha256']
    save('selected-trigger-raw-packets.json',selected)
    validation={'officialIssuerRows':len(mappings),'issuerRowsWithExactQuestNameMatch':sum(bool(m['questRows']) for m in mappings),
        'upstreamRepairOverlap':len(comparison),'allOverlapDisplayNamesMatchClient':all(r['clientDisplayMatches'] for r in comparison),
        'selectedTriggerRawChecksPassed':len(observations),
        'caution':'Raw identity/coordinate check does not establish quest gating, home position or a complete event-condition packet.'}
    save('validation.json',validation);print(json.dumps(validation,indent=2))
if __name__=='__main__':main()

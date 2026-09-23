#!/usr/bin/env python3
"""Read-only current NPC/quest coverage joined to local client and trace evidence.
Literal script references are leads, not inferred spawn/quest ownership.
"""
import collections
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import subprocess
import sys

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'evidence/npc-quest-crossreference-2026-09-18'
CLIENT=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')

def load(name,path):
    spec=importlib.util.spec_from_file_location(name,path);m=importlib.util.module_from_spec(spec)
    sys.modules[name]=m;spec.loader.exec_module(m);return m

def save(name,data):
    (OUT/name).write_text(json.dumps(data,indent=2,ensure_ascii=False)+'\n')

def query(table):
    assert re.fullmatch('[a-z_]+',table)
    cols=subprocess.check_output(['mariadb','-N','ffxiv_server','-e','SHOW COLUMNS FROM '+table],text=True)
    names=[line.split('\t')[0] for line in cols.splitlines()]
    expr=','.join("'"+n+"',`"+n+'`' for n in names)
    data=subprocess.check_output(['mariadb','-N','ffxiv_server','-e','SELECT HEX(JSON_OBJECT('+expr+')) FROM '+table],text=True)
    return [json.loads(bytes.fromhex(line)) for line in data.splitlines()]

def main():
    OUT.mkdir(parents=True,exist_ok=True)
    tables=['gamedata_actor_class','gamedata_actor_appearance','gamedata_quests','server_spawn_locations',
        'server_zones','server_battlenpc_spawn_locations','server_battlenpc_groups','server_battlenpc_pools']
    snapshot={t:query(t) for t in tables};save('database-snapshot.json',snapshot)
    sheets=load('npc_sheets',ROOT/'tools/Universal/recover-legacy-enemy-ids.py');sheets.ROOT=CLIENT
    names={r['id']:r['values'].get(1,'') for r in sheets.rows(189071360)}
    actors={r['id']:r for r in sheets.rows(16973832)}
    dbclasses={r['id']:r for r in snapshot['gamedata_actor_class']}
    npcs={i for i in actors if 1000000<=i<2000000}
    # Actor-ID range is a broad catalogue filter; unnamed triggers may remain.
    quests=snapshot['gamedata_quests'];qbyname=collections.defaultdict(list)
    for q in quests:qbyname[q['className'].lower()].append(q)
    source_ids={189071360,16973832}
    for schema in list(source_ids):
        for sheet in sheets.xml(schema).findall('sheet'):
            if sheet.get('lang') not in (None,'en'):continue
            for block in sheet.findall('block/file'):
                source_ids.update(int(v) for v in [block.text,block.get('enable'),block.get('offset')] if v)
    save('client-sheet-source-manifest.json',[{'resourceId':i,'path':str(sheets.path(i)),
        'sha256':hashlib.sha256(sheets.path(i).read_bytes()).hexdigest()} for i in sorted(source_ids)])
    lua=load('npc_lpb',ROOT/'.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py')
    clientrefs=collections.defaultdict(list);scripts=[];client_by_name={}
    for path in sorted((CLIENT/'client/script').rglob('*.le.lpb')):
        name=lua.cipher(path.name[:-7]);client_by_name[name.lower()]=path
        if name.lower() not in qbyname:continue
        raw=path.read_bytes();decoded=lua.decode_lpb(raw);reader=lua.Reader(decoded);tree=reader.parse_root()
        assert reader.p==len(decoded),name
        refs=[];symbols=set()
        def walk(f):
            for t,v in f.constants:
                if isinstance(v,str) and re.match(r'(processEvent|defaultTalk)',v):symbols.add(v)
                if isinstance(v,(int,float)) and not isinstance(v,bool) and v in npcs:
                    refs.append({'actorClassId':int(v),'sourceLines':[f.linedefined,f.lastlinedefined]})
            for sub in f.protos:walk(sub)
        walk(tree)
        entry={'name':name,'questIds':[q['id'] for q in qbyname[name.lower()]],'path':str(path),
            'sha256':hashlib.sha256(raw).hexdigest(),'actorLiteralReferences':refs,'eventSymbols':sorted(symbols),'parsedBytes':reader.p}
        scripts.append(entry)
        for ref in refs:
            for qid in entry['questIds']:clientrefs[ref['actorClassId']].append({'questId':qid,'className':name,'sourceLines':ref['sourceLines']})
    save('client-quest-reference-index.json',scripts)
    serverrefs=collections.defaultdict(list);server_scripts={}
    for path in sorted((ROOT/'Data/scripts/quests').rglob('*.lua')):
        source=path.read_text();server_scripts[path.stem.lower()]=path
        # Remove line comments to avoid treating prose-only mentions as implementation.
        body=re.sub(r'--\[(=*)\[.*?\]\1\]','',source,flags=re.S)
        body=re.sub(r'--[^\n]*','',body)
        ids={int(x) for x in re.findall(r'(?<![\w.])\d{7}(?![\w.])',body)} & npcs
        for cid in ids:serverrefs[cid].append({'script':str(path.relative_to(ROOT)),'questIds':[q['id'] for q in qbyname[path.stem.lower()]]})
    dialogue=collections.defaultdict(list)
    for path in sorted((ROOT/'Data/scripts/quests/dft').glob('*.lua')):
        cp=client_by_name.get(path.stem);constants=set()
        if cp:
            f=lua.Reader(lua.decode_lpb(cp.read_bytes())).parse_root()
            def strings(f):
                for t,v in f.constants:
                    if isinstance(v,str):constants.add(v)
                for sub in f.protos:strings(sub)
            strings(f)
        for line in path.read_text().splitlines():
            m=re.match(r'\s*\[(\d+)\]\s*=\s*"([^"]+)".*?(?:--\s*(.*))?$',line)
            if m:dialogue[int(m[1])].append({'script':path.stem,'function':m[2],'clientSymbolPresent':m[2] in constants,'locationComment':m[3] or ''})
    tracepath=ROOT/'evidence/npc-restoration-2026-09-15/observations.json'
    observations=json.loads(tracepath.read_text());trace=collections.defaultdict(list)
    for o in observations:
        if o['actorClassId'] in npcs:trace[o['actorClassId']].append(o)
    spawns=collections.defaultdict(list);name_spawns=collections.defaultdict(list)
    for s in snapshot['server_spawn_locations']:
        spawns[s['actorClassId']].append(s)
        dn=dbclasses.get(s['actorClassId'],{}).get('displayNameId')
        if dn is not None:name_spawns[dn].append(s)
    appearance_ids={r['actorclassId'] if 'actorclassId' in r else r.get('actorClassId',r.get('id')) for r in snapshot['gamedata_actor_appearance']}
    rows=[]
    for cid in sorted(npcs):
        dn=actors[cid]['values'].get(5);dc=dbclasses.get(cid,{})
        root=[s for s in spawns[cid] if not s['privateAreaName']]
        rows.append({'actorClassId':cid,'displayNameId':dn,'name':names.get(dn,''),
            'databaseClassPresent':bool(dc),'classPath':dc.get('classPath',''),
            'databaseNameMatchesClient':dc.get('displayNameId')==dn,
            'appearancePresent':cid in appearance_ids,'spawns':spawns[cid],
            'sameDisplayNameOtherClassSpawns':[s for s in name_spawns[dn] if dn and s['actorClassId']!=cid],
            'rootSpawnCount':len(root),'privateSpawnCount':len(spawns[cid])-len(root),
            'defaultDialogue':dialogue[cid],'serverQuestLiteralReferences':serverrefs[cid],
            'clientQuestLiteralReferences':clientrefs[cid], 'traceObservations':trace[cid],
            'status':'placed-unloadable-class' if spawns[cid] and not dc.get('classPath') else
                'placed-root' if root else 'placed-private-only' if spawns[cid] else 'no-static-row'})
    save('npc-coverage.json',rows)
    questrows=[]
    for q in quests:
        path=server_scripts.get(q['className'].lower());source=path.read_text() if path else ''
        questrows.append(dict(q,serverScript=str(path.relative_to(ROOT)) if path else None,
            serverSha256=hashlib.sha256(source.encode()).hexdigest() if path else None,
            hasCompleteQuestCall=bool(re.search(r'\bCompleteQuest\s*\(',source)),
            clientScriptPresent=q['className'].lower() in client_by_name,
            referencedNpcClasses=sorted({r['actorClassId'] for r in rows if any(x['questId']==q['id'] for x in r['clientQuestLiteralReferences'])}),
            caution='Script/call presence is not proof of playable objectives, offer wiring or completion.'))
    save('quest-coverage.json',questrows)
    leads=[r for r in rows if r['status']=='no-static-row' and (r['defaultDialogue'] or r['clientQuestLiteralReferences'] or r['serverQuestLiteralReferences'])]
    save('missing-npc-leads.json',leads)
    summary={'staticRows':len(snapshot['server_spawn_locations']),'clientNpcRangeActors':len(rows),
        'nonemptyClientLabelsIncludingPlaceholders':sum(bool(r['name']) for r in rows),
        'statusCounts':dict(collections.Counter(r['status'] for r in rows)),
        'quests':len(questrows),'questsWithServerScript':sum(bool(q['serverScript']) for q in questrows),
        'questsWithClientScript':sum(q['clientScriptPresent'] for q in questrows),
        'clientQuestScriptsParsed':len(scripts),'missingStaticLeads':len(leads),
        'dialogueBackedMissing':sum(bool(r['defaultDialogue']) for r in leads),
        'traceBackedMissingLeads':sum(bool(r['traceObservations']) for r in leads),
        'traceSourceSha256':hashlib.sha256(tracepath.read_bytes()).hexdigest(),
        'notes':['Database read only; no private player data queried.','Actor-class variants are not distinct named people.',
            'Literal references are not proof of issuer role or state ownership.','No static row does not exclude script-created actors.']}
    save('summary.json',summary);print(json.dumps(summary,indent=2))

if __name__=='__main__':main()

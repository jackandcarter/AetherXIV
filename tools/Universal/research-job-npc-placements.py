#!/usr/bin/env python3
"""Read-only job NPC marker, identity and upstream placement investigation."""
import hashlib,importlib.util,json,re,urllib.request,sys
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'evidence/job-npc-placement-2026-09-18'
CLIENT=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
NAMES={'Jehantel','Pukno Poki','Lalai','Alberic','Erik','Widargelt','Curious Gorge','Raya-O-Senna','Dozol Meloc','269th Order Mendicant Da Za'}
def save(name,value):(OUT/name).write_text(json.dumps(value,indent=2,ensure_ascii=False)+'\n')
def main():
    OUT.mkdir(parents=True,exist_ok=True)
    spec=importlib.util.spec_from_file_location('sheets',ROOT/'tools/Universal/recover-legacy-enemy-ids.py')
    sheets=importlib.util.module_from_spec(spec);spec.loader.exec_module(sheets);sheets.ROOT=CLIENT
    labels={r['id']:r['values'][1] for r in sheets.rows(189071360) if r['values'].get(1) in NAMES}
    actors=[r for r in sheets.rows(16973832) if r['values'].get(5) in labels]
    actorids={r['id'] for r in actors};matches={};sources={189071360,16973832}
    for name,schema in [('quest_marker',16974636),('2Dmap_marker',16974684),('2Dmap_actor_data',16974300),('populace',16974189)]:
        matches[name]=[r for r in sheets.rows(schema) if r['id'] in actorids or any(isinstance(v,(int,float)) and v in labels for v in r['values'].values())]
        sources.add(schema)
    for schema in list(sources):
        for sheet in sheets.xml(schema).findall('sheet'):
            if sheet.get('lang') not in (None,'en'):continue
            for b in sheet.findall('block/file'):
                sources.update(int(v) for v in [b.text,b.get('enable'),b.get('offset')] if v)
    save('client-inputs.json',[{'id':i,'path':str(sheets.path(i)),'sha256':hashlib.sha256(sheets.path(i).read_bytes()).hexdigest()} for i in sorted(sources)])
    save('client-identities.json',{'labels':labels,'actors':actors})
    save('client-marker-candidates.json',matches)
    # Match marker IDs actually used by each job script; never equate marker
    # numeric prefixes with database quest IDs (they use different ranges).
    spec=importlib.util.spec_from_file_location('job_lpb',ROOT/'.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py')
    lpb=importlib.util.module_from_spec(spec);sys.modules[spec.name]=lpb;spec.loader.exec_module(lpb)
    markerids={r['id'] for r in matches['quest_marker']};bindings=[]
    for path in sorted((CLIENT/'client/script').rglob('*.le.lpb')):
        name=lpb.cipher(path.name[:-7])
        if not re.fullmatch(r'(brd|blm|whm|mnk|war|drg|pld)0j\d+|dft(fst|wil|roc)',name):continue
        raw=path.read_bytes();decoded=lpb.decode_lpb(raw);reader=lpb.Reader(decoded);tree=reader.parse_root()
        assert reader.p==len(decoded)
        refs=[];symbols=[]
        def walk(f):
            for _,v in f.constants:
                if isinstance(v,(int,float)) and not isinstance(v,bool) and v in markerids:
                    refs.append({'markerId':int(v),'sourceLines':[f.linedefined,f.lastlinedefined]})
                if isinstance(v,str) and v.startswith(('defaultTalk','processEvent')):symbols.append(v)
            for sub in f.protos:walk(sub)
        walk(tree)
        bindings.append({'script':name,'sha256':hashlib.sha256(raw).hexdigest(),'path':str(path),
            'markerLiteralReferences':refs,'eventSymbols':sorted(set(symbols))})
    save('job-script-marker-bindings.json',bindings)
    profile=[]
    for path in (ROOT/'Data/scripts/quests/dft').glob('*.lua'):
        for no,line in enumerate(path.read_text().splitlines(),1):
            m=re.match(r'\s*\[(\d+)\]\s*=\s*"([^"]+)"',line)
            if m and int(m[1]) in actorids:profile.append({'actorClassId':int(m[1]),'function':m[2],'script':str(path.relative_to(ROOT)),'line':no,'comment':line.split('--',1)[-1],'caution':'Unproven comment coordinates are leads, not spawn authority.'})
    save('default-dialogue-leads.json',profile)
    upstream=[]
    repo='Yokimitsuro/MeteorReborn';commit='59155d239a657374a40768cb405ac5972e765681'
    for file in ['data/sql/server_spawn_locations.sql','data/sql/server_eventnpc_spawn_locations.sql','data/sql/gamedata_actor_class.sql']:
        url=f'https://raw.githubusercontent.com/{repo}/{commit}/{file}'
        raw=urllib.request.urlopen(url,timeout=30).read();lines=raw.decode().splitlines()
        hits=[l for l in lines if any(re.search(r'(?<!\d)'+str(i)+r'(?!\d)',l) for i in actorids)]
        upstream.append({'url':url,'sha256':hashlib.sha256(raw).hexdigest(),'matchingLines':hits})
    save('upstream-search.json',upstream)
    corpus=ROOT/'evidence/npc-restoration-2026-09-15/corpus-raw.jsonl'
    observed=[]
    for line in corpus.open():
        packet=json.loads(line)
        params=packet.get('initParams',[])
        if packet.get('opcode')=='0x00CC' and len(params)>6 and params[6].get('v') in actorids:
            observed.append(packet)
    save('trace-init-search.json',{'corpusSha256':hashlib.sha256(corpus.read_bytes()).hexdigest(),
        'actorClassIds':sorted(actorids),'matchingInitializations':observed,
        'limitation':'Search of existing decoded corpus, not proof of absence from retail or unprocessed streams.'})
    print(json.dumps({'actorVariants':len(actors),'markerMatches':{k:len(v) for k,v in matches.items()},'upstreamMatches':[(u['url'],len(u['matchingLines'])) for u in upstream]},indent=2))
if __name__=='__main__':main()

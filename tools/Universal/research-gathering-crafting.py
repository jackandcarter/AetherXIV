#!/usr/bin/env python3
"""Read-only source audit; generated evidence only. Requires local legacy decoders/tshark."""
import concurrent.futures
import hashlib
import importlib.util
import json
from pathlib import Path
import re
import sys
import zipfile

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'evidence/gathering-crafting-2026-09-17'
CLIENT = Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
ARCHIVE = Path('/Volumes/Dev2/ffxiv_traces.zip')

def load(name):
    path = ROOT / '.local-evidence/tools/Universal' / (name + '.py')
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    sys.modules[name] = module
    spec.loader.exec_module(module)
    return module

def save(path, data):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(data, indent=2) + '\n')

def main():
    OUT.mkdir(parents=True, exist_ok=True)
    analyzer = load('analyze-legacy-tcp-stream')
    indexer = load('index-legacy-trace-corpus')
    semantic = load('decode-corpus-wide')
    lua = load('decode-ffxiv-client-lpb')
    hashes = semantic.build_hash_table()
    captures = []
    with zipfile.ZipFile(ARCHIVE) as archive:
        for member in archive.infolist():
            if not member.filename.startswith('ffxiv_traces/') or not member.filename.endswith('.pcapng'):
                continue
            target = OUT / 'captures' / Path(member.filename).name
            target.parent.mkdir(exist_ok=True)
            target.write_bytes(archive.read(member))
            captures.append(target)
    relevant_names = {'change_to_botanist','gather_wood','harvest','switch_to_weaver',
                      'accept_local_leve','local_leve_complete','repair_items'}
    keywords = re.compile(r'craft|harvest|gather|logging|synth|recipe', re.I)

    def inspect(capture):
        row = {'capture':capture.name, 'sha256':hashlib.sha256(capture.read_bytes()).hexdigest(), 'streams':[]}
        for stream_id in indexer.discover_world_streams(capture, 54992):
            stream_row = {'stream':stream_id}
            row['streams'].append(stream_row)
            try:
                endpoints, streams = analyzer.read_follow_stream(str(capture), stream_id)
                segments = analyzer.read_capture_segments(str(capture), stream_id, endpoints)
                directions = []
                for i, data in enumerate(streams):
                    direction = 'server-to-client' if endpoints[i].endswith(':54992') else 'client-to-server'
                    directions.append({'direction':direction,'frames':analyzer.decode_frames(data, segments[i])})
                doc = {'directions':directions}
                packets = indexer.flatten_packets(doc)
                events, records, errors = [], [], []
                for p in packets:
                    packet = p['packet']; opcode = int(packet['opcode'],16)
                    raw = bytes.fromhex(packet['payloadHex'])
                    result = {k:p[k] for k in ('frameNumber','captureTimestamp','direction','packetIndex')}
                    result.update(packet)
                    try:
                        decoded = semantic.decode_payload(opcode, raw, hashes, p['direction'])
                        result['decoded'] = decoded
                    except Exception as exc:
                        result['decodeError'] = str(exc)
                        errors.append({'frame':p['frameNumber'],'opcode':packet['opcode'],'error':str(exc)})
                    records.append(result)
                    if opcode in (0x12d,0x12e,0x12f,0x130,0x131):
                        events.append(result)
                matches = [e for e in events if keywords.search(json.dumps(e.get('decoded',{})))]
                relevant = capture.stem in relevant_names or bool(matches)
                stream_row.update(packetCount=len(records),eventCount=len(events),errors=errors,
                    relevant=relevant,matchingEvents=[{'frame':e['frameNumber'],'decoded':e.get('decoded')} for e in matches])
                if relevant:
                    save(OUT/'streams'/f'{capture.stem}-s{stream_id}.json',records)
                    save(OUT/'events'/f'{capture.stem}-s{stream_id}.json',events)
            except Exception as exc:
                stream_row['error']=str(exc)
        print(capture.name, 'complete', flush=True)
        return row

    if '--client-only' in sys.argv:
        rows=json.loads((OUT/'capture-index.json').read_text())
    else:
        with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
            rows=list(pool.map(inspect,captures))
        save(OUT/'capture-index.json',rows)
    scripts=[]
    for path in sorted((CLIENT/'client/script').rglob('*.le.lpb')):
        name=lua.cipher(path.name[:-7])
        if not re.search(r'craft|harvest|gather|recipe|synth|fellinginput|mininginput|judgebaseclass',name,re.I):continue
        row={'name':name,'path':str(path),'sha256':hashlib.sha256(path.read_bytes()).hexdigest()}
        try:
            decoded=lua.decode_lpb(path.read_bytes()); reader=lua.Reader(decoded); tree=reader.parse_root()
            row.update(decodedBytes=len(decoded),consumed=reader.p)
            output=OUT/'client'/f'{name}.dis.txt';output.parent.mkdir(exist_ok=True)
            output.write_text('\n'.join(lua.dump_function(tree))+'\n')
            functions=[]
            def walk(f):
                functions.append({'source':f.source,'lines':[f.linedefined,f.lastlinedefined],
                    'constants':[v for t,v in f.constants], 'instructions':len(f.code)})
                for sub in f.protos:walk(sub)
            walk(tree);row['functions']=functions
        except Exception as exc:row['error']=str(exc)
        scripts.append(row)
    save(OUT/'client-scripts.json',scripts)
    save(OUT/'provenance.json',{'archive':str(ARCHIVE),'archiveSha256':hashlib.sha256(ARCHIVE.read_bytes()).hexdigest(),
        'client':str(CLIENT),'clientVersion':(CLIENT/'game.ver').read_text().strip(),
        'tools':{n:hashlib.sha256((ROOT/'.local-evidence/tools/Universal'/f'{n}.py').read_bytes()).hexdigest()
        for n in ('analyze-legacy-tcp-stream','decode-corpus-wide','decode-ffxiv-client-lpb')}})
    print('Saved',len(rows),'capture audits and',len(scripts),'client scripts to',OUT)

if __name__=='__main__':main()

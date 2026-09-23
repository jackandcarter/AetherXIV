#!/usr/bin/env python3
"""Collect retail stat observations, not inferred base-stat rows. No database writes."""
import concurrent.futures
import importlib.util
import json
import hashlib
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / 'evidence/player-growth-2026-09-17'
CAPTURES = ROOT / 'evidence/gathering-crafting-2026-09-17/captures'

def load(name):
    path = ROOT / '.local-evidence/tools/Universal' / (name + '.py')
    spec = importlib.util.spec_from_file_location(name, path)
    mod = importlib.util.module_from_spec(spec)
    sys.modules[name] = mod
    spec.loader.exec_module(mod)
    return mod

def main():
    analyzer = load('analyze-legacy-tcp-stream')
    indexer = load('index-legacy-trace-corpus')
    decoder = load('decode-corpus-wide')
    hashes = decoder.build_hash_table()
    OUT.mkdir(parents=True, exist_ok=True)

    def inspect(capture):
        result = {'capture':capture.name, 'streams':[]}
        for sid in indexer.discover_world_streams(capture, 54992):
            row = {'id':sid, 'observations':[]}
            result['streams'].append(row)
            try:
                endpoints, streams = analyzer.read_follow_stream(str(capture), sid)
                segments = analyzer.read_capture_segments(str(capture), sid, endpoints)
                directions = [{'direction':'server-to-client' if endpoints[i].endswith(':54992') else 'client-to-server',
                    'frames':analyzer.decode_frames(data, segments[i])} for i,data in enumerate(streams)]
                for p in indexer.flatten_packets({'directions':directions}):
                    packet = p['packet']
                    if p['direction'] != 'server-to-client' or packet['opcode'] not in ('0x0137','0x01A4'):
                        continue
                    raw = bytes.fromhex(packet['payloadHex'])
                    decoded = decoder.decode_payload(int(packet['opcode'],16), raw, hashes, p['direction'])
                    if packet['opcode']=='0x0137':
                        values = [v for s in decoded['segments'] for v in s['values']] + decoded['dangling']
                        if not any(any(term in v.get('path','') for term in ('generalParameter','state_mainSkill','skillLevel','tribe','hpMax','mpMax','bonus')) for v in values):
                            continue
                    row['observations'].append(dict(packet, frame=p['frameNumber'], timestamp=p['captureTimestamp'], decoded=decoded))
            except Exception as exc:
                row['error'] = repr(exc)
        (OUT/(capture.stem+'.json')).write_text(json.dumps(result,indent=2)+'\n')
        return {'capture':capture.name,'streams':len(result['streams']),
            'observations':sum(len(s['observations']) for s in result['streams']),
            'errors':[s['error'] for s in result['streams'] if 'error' in s]}

    with concurrent.futures.ThreadPoolExecutor(max_workers=4) as pool:
        audit = list(pool.map(inspect,sorted(CAPTURES.glob('*.pcapng'))))
    (OUT/'audit.json').write_text(json.dumps(audit,indent=2)+'\n')
    lua = load('decode-ffxiv-client-lpb')
    selected = {'charabaseclass_parameter','charabaseclass_ffxivbattle','playerbaseclass',
        'playerbaseclass_u','statuswidget','playerparameterwidget','bonuspointassignwidget',
        'bonuspointcommand','boostpointcommand','skilllevelstatus','parametercontrolstatus'}
    scripts=[]
    for path in Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/client/script').rglob('*.le.lpb'):
        name=lua.cipher(path.name[:-7])
        if name not in selected:continue
        raw=path.read_bytes(); decoded=lua.decode_lpb(raw); reader=lua.Reader(decoded);tree=reader.parse_root()
        assert reader.p==len(decoded),(name,reader.p,len(decoded))
        target=OUT/'client'/(name+'.dis.txt');target.parent.mkdir(exist_ok=True)
        target.write_text('\n'.join(lua.dump_function(tree))+'\n')
        scripts.append({'name':name,'path':str(path),'sha256':hashlib.sha256(raw).hexdigest(),'bytes':len(decoded),'consumed':reader.p})
    (OUT/'client-audit.json').write_text(json.dumps(scripts,indent=2)+'\n')
    print(json.dumps(audit,indent=2))

if __name__ == '__main__':
    main()

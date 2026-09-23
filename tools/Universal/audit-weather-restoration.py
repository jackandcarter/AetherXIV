#!/usr/bin/env python3
"""Reproduce weather evidence using existing client/corpus decoders; no runtime writes."""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import sys

ROOT = Path(__file__).resolve().parents[2]

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--client', type=Path, required=True)
    parser.add_argument('--corpus', type=Path, default=ROOT/'evidence/npc-restoration-2026-09-15/corpus-raw.jsonl')
    parser.add_argument('--out', type=Path, default=ROOT/'evidence/weather-restoration-2026-09-19')
    args = parser.parse_args()
    args.out.mkdir(parents=True, exist_ok=True)
    decoder = ROOT/'.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py'
    spec = importlib.util.spec_from_file_location('weather_lua', decoder)
    lua = importlib.util.module_from_spec(spec)
    sys.modules[spec.name] = lua
    spec.loader.exec_module(lua)
    sources = []
    for path in sorted((args.client/'client/script').rglob('*.le.lpb')):
        name = lua.cipher(path.name[:-7])
        if name not in ('weatherdirector', 'weatherdirectorbaseclass'):
            continue
        raw = path.read_bytes()
        tree = lua.Reader(lua.decode_lpb(raw)).parse_root()
        (args.out/(name+'.txt')).write_text('\n'.join(lua.dump_function(tree))+'\n')
        sources.append(dict(name=name, path=str(path), sha256=hashlib.sha256(raw).hexdigest()))
    if len(sources) != 2:
        raise RuntimeError('Expected both weather client scripts')
    rows = []
    captures = set()
    for line_number, line in enumerate(args.corpus.open(), 1):
        row = json.loads(line)
        captures.add(row.get('capture'))
        if row.get('direction') != 'server-to-client':
            continue
        raw = row.get('payloadHex', '').upper()
        opcode = row.get('opcode')
        relevant = opcode in ('0x000D', '0x0010') or (
            opcode == '0x0137' and ('CCB2AF3E' in raw or 'weatherInfo'.encode().hex().upper() in raw)) or (
            opcode in ('0x00CA', '0x00CC') and 'Weather'.encode().hex().upper() in raw)
        if relevant:
            row['corpusLine'] = line_number
            rows.append(row)
    for name, value in [('sources.json', sources), ('retail-weather-packets.json', rows),
                        ('audit.json', dict(corpus=str(args.corpus),
                         corpusSha256=hashlib.sha256(args.corpus.read_bytes()).hexdigest(),
                         captures=len(captures), packets=len(rows)))]:
        (args.out/name).write_text(json.dumps(value, indent=2)+'\n')
    print(f'{len(captures)} captures; {len(rows)} weather-related packets; {len(sources)} client scripts')

if __name__ == '__main__':
    main()

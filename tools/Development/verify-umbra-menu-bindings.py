#!/usr/bin/env python3
"""Read-only verification of Umbra's 1.23b Lua-menu binding against local assets.

Uses the existing LPB decoder; it never edits client files or process memory.
"""
import argparse
import hashlib
import importlib.util
from pathlib import Path
import re
import struct

ROOT = Path(__file__).resolve().parents[2]
BOOT = ROOT / 'AetherXIV Launcher/Umbra/Aether.Umbra.Bootstrap'

def fingerprint(fn):
    data = b''.join(struct.pack('<I', word & 0xffffffff) for word in fn.code)
    for tag, value in fn.constants:
        data += bytes([tag])
        if tag == 4: data += value.encode('latin1') + b'\0'
        elif tag == 3: data += struct.pack('<d', value)
        elif tag == 1: data += bytes([bool(value)])
        elif tag != 0: raise ValueError('Unsupported constant')
    result = 2166136261
    for byte in data: result = ((result ^ byte) * 16777619) & 0xffffffff
    return result

def main():
    ap = argparse.ArgumentParser(description=__doc__)
    ap.add_argument('client', type=Path)
    ap.add_argument('--decoder', type=Path, default=ROOT/'.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py')
    args = ap.parse_args()
    exe = (args.client/'ffxivgame.exe').read_bytes()
    assert hashlib.sha256(exe).hexdigest() == '9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9', 'Unknown executable'
    pe = struct.unpack_from('<I', exe, 0x3c)[0]
    sections, optional = struct.unpack_from('<H', exe, pe+6)[0], struct.unpack_from('<H', exe, pe+20)[0]
    def read(rva, size):
        for i in range(sections):
            entry = pe + 24 + optional + 40*i
            virtual_size, virtual, raw_size, raw = struct.unpack_from('<IIII', exe, entry+8)
            if virtual <= rva and rva+size <= virtual+raw_size:
                return exe[raw+rva-virtual:raw+rva-virtual+size]
        raise ValueError('Unmapped RVA')
    entries = re.findall(r'const BYTE expected\[\] = \{([^}]+)\};\s*if \(memcmp\(base \+ (0x[0-9a-f]+)', (BOOT/'UmbraLuaApiBindings.inl').read_text())
    assert len(entries) == 21
    for encoded, rva in entries:
        expected = bytes(int(s.strip(),16) for s in encoded.split(','))
        assert read(int(rva,16),len(expected)) == expected, f'API mismatch at {rva}'
    assert read(0x9cfc30,10) == bytes.fromhex('56 8b 74 24 08 66 83 46 34 01')
    spec = importlib.util.spec_from_file_location('client_lpb',args.decoder)
    decoder = importlib.util.module_from_spec(spec); spec.loader.exec_module(decoder)
    lpb = (args.client/'client/script/n1635q/x91wx5wpn1635q.le.lpb').read_bytes()
    assert hashlib.sha256(lpb).hexdigest() == 'c0e1782a3ca183fcaa520a16320946f62d3fc861c74978cde19ea20fcd064b0a'
    root = decoder.Reader(decoder.decode_lpb(lpb)).parse_root()
    source = (BOOT/'UmbraLuaMainMenu.inl').read_text()
    for fn in [root,root.protos[0],root.protos[1]]:
        signature = f'{fn.linedefined}, {fn.lastlinedefined}, {fn.numparams}, {len(fn.code)}, {len(fn.constants)}, 0x{fingerprint(fn):08x}'
        assert signature in source, f'Unmatched script fingerprint: {signature}'
    map_lpb = (args.client/'client/script/n1635q/x9uw9o139q1vwn1635q.le.lpb').read_bytes()
    assert hashlib.sha256(map_lpb).hexdigest() == '873ee821c979b9c0959743bc619444ec01bd39c96f71235235278bb602b4d0bb'
    map_root = decoder.Reader(decoder.decode_lpb(map_lpb)).parse_root()
    for fn in [map_root, map_root.protos[2], map_root.protos[5]]:
        signature = f'{fn.linedefined}, {fn.lastlinedefined}, {fn.numparams}, {len(fn.code)}, {len(fn.constants)}, 0x{fingerprint(fn):08x}'
        assert signature in source, f'Unmatched map lifecycle fingerprint: {signature}'
    print('PASS: executable/script SHA-256, 21 Lua API signatures, complete hook instructions, menu and map lifecycle opcode-and-constant fingerprints')
if __name__ == '__main__': main()

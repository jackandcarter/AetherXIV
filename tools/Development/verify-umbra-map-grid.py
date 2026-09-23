#!/usr/bin/env python3
"""Verify the 1.23b grid evidence and export data-derived navigation origins.

Run research-client-sheets.py --maps first. This reads disk only; it does not
enable map selection or claim a verified screen transform / server-zone match.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import struct

ROOT = Path(__file__).resolve().parents[2]


def verify(client, sheets):
    spec = importlib.util.spec_from_file_location('bindings', Path(__file__).with_name('inspect-umbra-map-bindings.py'))
    bindings = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(bindings)
    bindings.inspect(client)  # Exact executable AND map-script hashes.
    data = (client / 'ffxivgame.exe').read_bytes()
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    count = struct.unpack_from('<H', data, pe + 6)[0]
    optional = struct.unpack_from('<H', data, pe + 20)[0]

    def read(va, size):
        rva = va - 0x400000
        for index in range(count):
            entry = pe + 24 + optional + 40 * index
            _, virtual, raw_size, raw = struct.unpack_from('<IIII', data, entry + 8)
            if virtual <= rva and rva + size <= virtual + raw_size:
                return data[raw + rva - virtual:raw + rva - virtual + size]
        raise ValueError(f'Unmapped address {va:#x}')

    checks = {
        'navigation_column_3': (0x6766ba, '6a03'),
        'navigation_column_4': (0x6766e1, '6a04'),
        'negate_and_store_x_origin': (0x6766d3, 'f30f5cc8f30f114f20'),
        'negate_and_store_z_origin': (0x6766fa, 'f30f5cc1f30f114724'),
        'publish_origins_to_control': (0x678131, '8b4820898e380600008b502489963c060000'),
        'actor_x_component': (0x679654, 'f30f1044242c'),
        'actor_z_component': (0x679664, 'f30f10442434'),
        'subtract_x_origin': (0x67587c, 'f20f5cc8'),
        'divide_and_truncate_x': (0x675888, 'f20f5ec8f20f2cc9'),
        'subtract_divide_z': (0x67589b, 'f20f5ccaf20f5ec8'),
        'truncate_z': (0x6758a4, 'f20f2cc1'),
        # Native marker placement computes local drawing coordinates. This
        # does not establish the control-to-screen transform or visibility.
        'marker_scale_a': (0x67c462, 'f30f108010060000'),
        'marker_scale_b': (0x67c46a, 'f30f10880c060000'),
        'marker_scale_ab': (0x67c487, 'f20f59c1'),
        'marker_scale_c': (0x67c48b, 'f30f108814060000'),
        'marker_scale_abc': (0x67c496, 'f20f59c1'),
        'marker_center_x': (0x67c4bd, 'f30f109840060000'),
        'marker_subtract_x': (0x67c4cb, 'f20f5ce3'),
        'marker_multiply_x': (0x67c4d3, 'f20f59e3'),
        'marker_center_z': (0x67c4db, 'f30f10a044060000'),
        'marker_subtract_z': (0x67c4e9, 'f20f5cec'),
        'marker_multiply_z': (0x67c4f1, 'f20f59ec'),
    }
    for label, (va, expected) in checks.items():
        if read(va, len(bytes.fromhex(expected))) != bytes.fromhex(expected):
            raise ValueError(f'Native evidence mismatch: {label}')
    if struct.unpack('<d', read(0xf55ad0, 8))[0] != 100:
        raise ValueError('Unexpected grid spacing')
    if struct.unpack('<f', read(0xf62fa0, 4))[0] != 0:
        raise ValueError('Unexpected origin subtraction constant')
    if struct.unpack('<d', read(0xf59898, 8))[0] != 0.5:
        raise ValueError('Unexpected viewport half-size constant')

    path = sheets / 'mapNavi_data.json'
    sheet = json.loads(path.read_text())
    if 'error' in sheet or sheet['name'] != 'mapNavi_data':
        raise ValueError('Incomplete navigation extraction')
    rows = []
    for sub in sheet['subSheets']:
        if sub['types'] != ['s32'] * 18 or sub['columnIndices'] != list(range(18)):
            raise ValueError('Unsupported navigation column layout')
        for block in sub['blocks']:
            for resource in block['resources']:
                h = f"{resource['id']:08X}"
                source = client / 'data' / h[:2] / h[2:4] / h[4:6] / (h[6:] + '.DAT')
                raw = source.read_bytes()
                if hashlib.sha256(raw).hexdigest() != resource['sha256']:
                    raise ValueError('Sheet resource changed; re-extract before verification')
                if resource['id'] == block['dataId']:
                    begin = int(block['attributes']['begin'])
                    end = begin + int(block['attributes']['count'])
                    values = [r for r in sub['rows'] if begin <= r['id'] < end]
                    packed = b''.join(struct.pack('<18i', *r['values']) for r in values)
                    if packed != raw:
                        raise ValueError('Extracted navigation values differ from source DAT')
        for row in sub['rows']:
            cols = row['values']
            # CVTSI2SS rounds to float before the native origin subtraction.
            f32 = lambda v: struct.unpack('<f', struct.pack('<f', v))[0]
            rows.append({'navigationRowId': row['id'], 'originX': -f32(cols[3]),
                         'originZ': -f32(cols[4])})
    if len({r['navigationRowId'] for r in rows}) != len(rows) or not rows:
        raise ValueError('Empty or ambiguous navigation rows')
    return {'executableSha256': bindings.SHA256,
            'navigationExtractionSha256': hashlib.sha256(path.read_bytes()).hexdigest(),
            'gridSpacing': 100, 'displayRounding': 'truncate toward zero',
            'continuousGridFormula': '((actorX - originX) / 100, (actorZ - originZ) / 100)',
            'overviewModeExcluded': True, 'screenTransformVerified': False,
            'nativeMarkerLocalScale': 'float(double(+0x610) * +0x60c * +0x614)',
            'nativeMarkerLocalFormula': 'viewportSize/2 + (worldXZ - centerXZ) * localScale',
            'serverZoneBindingVerified': False,
            'nativeChecks': {k: {'va': hex(v[0]), 'bytes': v[1]} for k, v in checks.items()},
            'rows': rows}


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client', type=Path)
    parser.add_argument('--sheets', type=Path, default=ROOT / '.local-evidence/umbra-map-travel/sheets')
    parser.add_argument('--output', type=Path, default=ROOT / '.local-evidence/umbra-map-travel/grid-evidence.json')
    args = parser.parse_args()
    result = verify(args.client, args.sheets)
    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(json.dumps(result, indent=2) + '\n')
    print(f"PASS: native grid chain and {len(result['rows'])} data-derived origins verified; screen/zone binding remains unresolved.")


if __name__ == '__main__':
    main()

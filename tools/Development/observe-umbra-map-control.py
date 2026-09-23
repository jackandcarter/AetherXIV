#!/usr/bin/env python3
"""Read one candidate native map control through the authenticated dev bridge.

Uses the native update observation by default, avoiding heap scans and cached
pointers. An explicit address is available for research. Neither structural
checks nor update callbacks alone prove visibility. Never writes process memory.
"""
import argparse
import datetime
import importlib.util
import json
from pathlib import Path
import struct
import time

ROOT = Path(__file__).resolve().parents[2]


def load(name, path):
    spec = importlib.util.spec_from_file_location(name, path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def main():
    bridge_module = load('umbra_bridge', Path(__file__).with_name('umbra-lua-hook.py'))
    bindings = load('umbra_map_bindings', Path(__file__).with_name('inspect-umbra-map-bindings.py'))
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client', type=Path)
    parser.add_argument('--address', type=lambda s: int(s, 0))
    parser.add_argument('--control', default=bridge_module.DEFAULT_CONTROL)
    parser.add_argument('--label', default='observation')
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    inventory = bindings.inspect(args.client)
    bridge = bridge_module.Bridge(args.control)
    code, snapshot = bridge._request('GET', '/snapshot', timeout=5)
    build = snapshot.get('client_build', {})
    if code != 200:
        code, snapshot = bridge._request('GET', '/status', timeout=5)
        if code != 200 or snapshot.get('transport') != 'native_fallback':
            raise RuntimeError('Live bridge is unavailable')
        # The fallback has no semantic snapshot. Validate actual executable
        # instructions before interpreting the candidate; local assets were
        # already hash-checked above. This remains a read-only observation.
        base = int(snapshot['process']['main_module']['base_address'], 0)
        code, signature = bridge._request('POST', '/memory/peek',
            {'address': hex(base + 0x279f29), 'size': 6}, timeout=5)
        if code != 200 or signature.get('hex', '').lower() != 'c7068c35fc00' or base != 0x400000:
            raise RuntimeError('Live constructor signature does not match')
    elif not build.get('verified') or build.get('module', {}).get('sha256') != bindings.SHA256:
        raise RuntimeError('Live build does not match the verified executable')
    base = int(snapshot['process']['main_module']['base_address'], 0)
    if args.address is None:
        code, result = bridge._request('GET', '/map/observation', timeout=5)
        # The request arms capture on demand. Give the game thread a bounded
        # interval to publish its first frame, without using an old pointer.
        for _ in range(5):
            if code != 200 or (result.get('available') and result.get('age_ms', 999999) <= 1000):
                break
            time.sleep(0.1)
            code, result = bridge._request('GET', '/map/observation', timeout=5)
        if code != 200 or not result.get('available') or result.get('age_ms', 999999) > 1000:
            raise RuntimeError('No recent native map update observation is available')
        args.address = int(result['control_address'], 0)
    else:
        code, result = bridge._request('POST', '/memory/peek', {'address': hex(args.address), 'size': 0xa70}, timeout=5)
        if code != 200 or not result.get('success'):
            raise RuntimeError('Control memory is unavailable')
    data = bytes.fromhex(result['hex'])
    if len(data) != 0xa70:
        raise RuntimeError('Incomplete control observation')
    for offset, rva in [(0, 0xbc358c), (4, 0xf3f970), (0xb4, 0xbc3464), (0x194, 0xbc3450)]:
        if struct.unpack_from('<I', data, offset)[0] != base + rva:
            raise RuntimeError('Candidate no longer matches MapScreenControl structure')
    values = {}
    for prop in inventory['properties']:
        if prop['storage_offset'] is None:
            continue
        offset = int(prop['storage_offset'], 0)
        value = {'raw': data[offset:offset + 8].hex(), 'signed_word': struct.unpack_from('<i', data, offset)[0]}
        if prop['converter_rva'] == '0x588d50':
            value['float_value'] = struct.unpack_from('<f', data, offset)[0]
        values[prop['name']] = value
    record = dict(timestamp=datetime.datetime.now(datetime.timezone.utc).isoformat(), label=args.label,
        bridge_session_id=snapshot.get('bridge_session_id'), process_id=snapshot['process']['process_id'],
        control_address=hex(args.address),
        active_control_verified=False, coordinate_conversion_verified=False, properties=values)
    record['update_sequence'] = result.get('sequence')
    record['age_ms'] = result.get('age_ms')
    # Raw native rendering inputs; labels deliberately avoid unverified units.
    record['render_fields'] = {hex(offset): struct.unpack_from('<f', data, offset)[0]
        for offset in (0x14c, 0x150, 0x60c, 0x610, 0x614, 0x638, 0x63c,
                       0x640, 0x644, 0x65c, 0x660, 0x664, 0x668, 0x968, 0x96c)}
    record['observed_navigation_row'] = struct.unpack_from('<i', data, 0x9f0)[0]
    record['lifecycle'] = result.get('lifecycle')
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        with args.output.open('a') as stream:
            stream.write(json.dumps(record) + '\n')
    print(json.dumps(record, indent=2))


if __name__ == '__main__':
    main()

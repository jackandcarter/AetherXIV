#!/usr/bin/env python3
"""Read-only 1.23b MapScreenControl property inventory from the exact client.

No assets or process memory are changed. Offsets describe property storage, not
world coordinates. This deliberately does not infer the meaning of Rect/Layout,
map-to-zone identity, screen geometry or game/navmesh axes from property names.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

SHA256 = '9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9'
SCRIPT_SHA256 = '873ee821c979b9c0959743bc619444ec01bd39c96f71235235278bb602b4d0bb'


def inspect(client):
    data = (client / 'ffxivgame.exe').read_bytes()
    if hashlib.sha256(data).hexdigest() != SHA256:
        raise ValueError('Unsupported executable: refusing to interpret offsets')
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    count = struct.unpack_from('<H', data, pe + 6)[0]
    optional = struct.unpack_from('<H', data, pe + 20)[0]
    image_base = struct.unpack_from('<I', data, pe + 24 + 28)[0]

    def read(va, size):
        rva = va - image_base
        for index in range(count):
            entry = pe + 24 + optional + 40 * index
            _, virtual, raw_size, raw = struct.unpack_from('<IIII', data, entry + 8)
            if virtual <= rva and rva + size <= virtual + raw_size:
                return data[raw + rva - virtual:raw + rva - virtual + size]
        raise ValueError(f'Unmapped address {va:#x}')

    def u32(va):
        return struct.unpack('<I', read(va, 4))[0]

    properties = []
    # Each initializer pushes its literal name, constructs a descriptor, and
    # writes its index at descriptor+0x14. Validate the instruction sequence.
    address = 0xf119f0
    while address < 0xf11fa0:
        if read(address, 1) != b'\x68' or read(address + 5, 1) != b'\xb9' or read(address + 10, 1) != b'\xe8':
            raise ValueError(f'Unexpected property initializer at {address:#x}')
        name = read(u32(address + 1), 96).split(b'\0')[0].decode('ascii')
        descriptor = u32(address + 6)
        if read(address + 15, 2) != b'\xc7\x05' or u32(address + 17) != descriptor + 0x14:
            raise ValueError('Unexpected property index write')
        index = u32(address + 21)
        if index != len(properties):
            raise ValueError('Noncontiguous property indices')
        getter = u32(0x673588 + index * 4)
        setter = u32(0x674280 + index * 4)
        offset = None
        conversion = None
        if getter != 0x673582:  # default: no readable property
            # Every observed branch loads the output arg, pushes it, then
            # adds a fixed offset to this. Some converters are cdecl.
            body = read(getter, 32)
            if body[5:7] != b'\x81\xc1':
                raise ValueError(f'Unexpected getter branch for {name}')
            offset = struct.unpack_from('<I', body, 7)[0]
            call_index = 12 if body[11] == 0x51 else 11
            if body[call_index] != 0xe8:
                raise ValueError('Unexpected converter call')
            conversion = getter + call_index + 5 + struct.unpack_from('<i', body, call_index + 1)[0]
        properties.append(dict(name=name, index=index,
            descriptor_rva=hex(descriptor-image_base), getter_rva=hex(getter-image_base),
            setter_rva=hex(setter-image_base), storage_offset=None if offset is None else hex(offset),
            converter_rva=None if conversion is None else hex(conversion-image_base)))
        # Initializers are individually 16-byte aligned, ending with ret then int3.
        end = address + 25
        while read(end, 1) != b'\xc3':
            end += 1
            if end - address > 64:
                raise ValueError('Unbounded initializer')
        address = (end + 16) & ~15
    if len(properties) != 43:
        raise ValueError('Unexpected property count')
    script = client / 'client/script/n1635q/x9uw9o139q1vwn1635q.le.lpb'
    if hashlib.sha256(script.read_bytes()).hexdigest() != SCRIPT_SHA256:
        raise ValueError('Unsupported map script')
    return dict(executable_sha256=SHA256, script_sha256=SCRIPT_SHA256,
        coordinate_conversion_verified=False, properties=properties)


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client', type=Path)
    parser.add_argument('--output', type=Path)
    args = parser.parse_args()
    result = inspect(args.client)
    if args.output:
        args.output.parent.mkdir(parents=True, exist_ok=True)
        args.output.write_text(json.dumps(result, indent=2) + '\n')
    for prop in result['properties']:
        print(f"{prop['index']:2} {prop['name']:28} storage={prop['storage_offset'] or 'write-only':>10} getter={prop['getter_rva']}")
    print('PASS: 43 properties matched; coordinate conversion is NOT verified.')


if __name__ == '__main__':
    main()

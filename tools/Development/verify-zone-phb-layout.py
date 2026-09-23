#!/usr/bin/env python3
"""Read-only check of PHB layout instructions in the researched 1.23b client.

These checks corroborate array offsets/strides and the statically traced character
query filter. They do not establish world transforms or live query execution.
"""
import argparse
import hashlib
import json
from pathlib import Path
import struct

EXPECTED_SHA256='9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9'
CHECKS=[
 ('bounds_offset',0xafebe0,'83c040'),
 ('triangle_count',0xafec04,'8b4064'),
 ('triangle_flags',0xafec65,'8b50608d0cca668b440106'),
 ('triangle_and_vertex_offsets',0xafecfb,'8b50600fb74d0c8b78388d0cca0fb714010fb774010203c80fb74904'),
 ('triangle_vertex_stride',0xafed17,'8d34768d14528d34b7f30f10140603f08d0c498d1497f30f101c0203d08d0c8ff30f10040103c8'),
 ('character_filter_initialization',0x7d4599,'33c08944242066894662894664'),
 ('character_filter_mask',0x7d45b9,'66c746600200'),
 ('character_query_filter_argument',0x7d7a96,'8d566052'),
 ('character_triangle_filter',0xb0c70f,'668b4cca06668b57026623c86623d08b470483e8000fb7c90fb7d2741e83e801741183e8010f85b3010000663bca0f96c0eb0e663bca0f93c0eb06663bca0f94c084c0'),
 ('character_collision_group',0xb0cb06,'8a4f4c8b450cba01000000d3e285502c'),
 ('character_capsule_defaults',0xf62f60,'0000003f00000040'),
]


def verify(path):
    data=path.read_bytes()
    digest=hashlib.sha256(data).hexdigest()
    if digest!=EXPECTED_SHA256:
        raise ValueError('Client hash differs from the researched build')
    pe,=struct.unpack_from('<I',data,0x3c)
    if data[pe:pe+4]!=b'PE\0\0':raise ValueError('Missing PE header')
    sections,=struct.unpack_from('<H',data,pe+6)
    optional_size,=struct.unpack_from('<H',data,pe+20)
    base,=struct.unpack_from('<I',data,pe+24+28)
    table=pe+24+optional_size
    results=[]
    for name,address,expected_hex in CHECKS:
        expected=bytes.fromhex(expected_hex);rva=address-base
        for i in range(sections):
            virtual_size,start,size,raw=struct.unpack_from('<4I',data,table+i*40+8)
            if start<=rva and rva+len(expected)<=start+size:
                actual=data[raw+rva-start:raw+rva-start+len(expected)]
                if actual!=expected:raise ValueError(f'Instruction mismatch: {name}')
                results.append(dict(name=name,address=f'0x{address:08x}',bytes=actual.hex()))
                break
        else:raise ValueError(f'Instruction outside file-backed sections: {name}')
    return dict(client_sha256=digest,verified_checks=results,
                collision_flag_meanings_verified=False,
                static_character_query_filter=dict(mask=2,value=0,comparison='equal',
                                                   live_verified=False))


if __name__=='__main__':
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client',type=Path)
    args=parser.parse_args()
    print(json.dumps(verify(args.client),indent=2))

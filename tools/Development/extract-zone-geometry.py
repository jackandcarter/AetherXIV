#!/usr/bin/env python3
"""Extract research-only 1.x PHB geometry and map placement records.

Layout field references: jpd002/SeventhUmbral dataobjects/MapLayout.cpp.
PHB.GBD fields are observations validated against file bounds, vertex indices,
finite coordinates and stored AABBs, NOT a complete physics-format specification.
Output remains in asset-local coordinates. No server data is changed.
"""
import argparse
import collections
import hashlib
import json
import math
from pathlib import Path
import struct


def unpack(data, fmt, offset):
    size = struct.calcsize(fmt)
    if offset < 0 or offset + size > len(data):
        raise ValueError(f'Out-of-bounds {fmt} at {offset:#x}')
    return struct.unpack_from(fmt, data, offset)


def string(data, offset):
    if not 0 <= offset < len(data):
        raise ValueError('String pointer outside file')
    end = data.find(b'\0', offset, min(offset + 4096, len(data)))
    if end < 0:
        raise ValueError('Unterminated string')
    return data[offset:end].decode('utf-8', errors='strict')


def resource_path(root, resource_id):
    return root / ('/'.join(f'{b:02X}' for b in resource_id.to_bytes(4, 'big')) + '.DAT')


def layout(data):
    if not data.startswith(b'MapLayoutResourceData\0'):
        raise ValueError('Not a MapLayoutResourceData file')
    header, count = unpack(data, '<II', 0x20)
    if count > 100000 or header < 0x40 + count * 0x20:
        raise ValueError('Invalid resource table')
    resources = []
    for i in range(count):
        name, kind, rid, a, b = unpack(data, '<16s4I', 0x40 + i * 32)
        resources.append(dict(name=name.split(b'\0')[0].decode(), type=kind,
                              resource_id=rid, unknown_words=[a, b]))
    if data[header:header+8] != b'SEDBlyb\0':
        raise ValueError('Unsupported layout section')
    base = header + 0x30
    if data[base:base+4] != b'lyb\0':
        raise ValueError('Missing lyb header')
    _, node_count = unpack(data, '<HH', base + 8)
    pointers = unpack(data, f'<{node_count}I', base + 24)[1:]
    nodes = {}
    for ptr in pointers:
        nid, parent, name = unpack(data, '<III', base + ptr)
        nodes[ptr] = dict(offset=ptr, id=nid, parent=parent, name=string(data, base+name))
    for ptr, node in nodes.items():
        node['type'] = nodes.get(node['parent'], {}).get('name', '')
        at = base + ptr
        if node['type'] == 'RefObjects/InstanceObject':
            words = unpack(data, '<16I', at)
            node.update(raw_words=words, position=unpack(data, '<3f', at+0x20),
                        rotation_raw=unpack(data, '<4f', base+words[11]),
                        scale_raw=unpack(data, '<4f', base+words[12]), reference=words[15])
        elif node['type'] in ('BaseObjects/BG/BGPartsBaseObject', 'BaseObjects/BG/BGChipBaseObject'):
            ref, = unpack(data, '<I', at+0x20)
            node['render_resource_name'] = string(data, base+ref)
            node['render_bounds'] = [unpack(data, '<3f', at+0x3c), unpack(data, '<3f', at+0x48)]
        elif node['type'] == 'BaseObjects/Attribute/AttributeBaseObject':
            node['raw_words'] = unpack(data, '<11I', at)
            ref, = unpack(data, '<I', at+0x24)
            node['resource_name_candidate'] = string(data, base+ref)
        elif node['type'] == 'RefObjects/UnitTree/UnitTreeObject':
            table, size = unpack(data, '<II', at+0x30)
            if size > 100000:
                raise ValueError('Invalid unit-tree item count')
            node['items'] = []
            for i in range(size):
                words = unpack(data, '<12I', base+table+i*48)
                node['items'].append(dict(name=string(data, base+words[5]),
                                          reference=words[6], raw_words=words,
                                          position=unpack(data, '<3f', base+table+i*48),
                                          rotation_raw=unpack(data, '<4f', base+words[3]),
                                          scale_raw=unpack(data, '<4f', base+words[4])))
    return resources, list(nodes.values())


def geometry(data):
    if not data.startswith(b'SEDBPHB\0'):
        raise ValueError('Not a SEDB/PHB file')
    meshes = []
    start = 0
    while (base := data.find(b'PHB.GBD\0', start)) >= 0:
        start = base + 8
        size, version = unpack(data, '<II', base+8)
        if version != 0x212 or size < 128 or base+size > len(data):
            raise ValueError('Unsupported or truncated GBD block')
        block = data[base:base+size]
        vo, vc = unpack(block, '<II', 0x38)
        fo, fc = unpack(block, '<II', 0x60)
        if not 0 < vc <= 65536 or not 0 < fc <= 1000000:
            raise ValueError('Unsupported geometry counts')
        if vo < 128 or fo < 128 or vo+vc*12 > size or fo+fc*8 > size:
            raise ValueError('Geometry arrays outside GBD block')
        vertices = [unpack(block, '<3f', vo+i*12) for i in range(vc)]
        faces = [unpack(block, '<4H', fo+i*8) for i in range(fc)]
        if not all(math.isfinite(x) for v in vertices for x in v):
            raise ValueError('Nonfinite vertex')
        if any(max(f[:3]) >= vc for f in faces):
            raise ValueError('Triangle index outside vertex array')
        low = unpack(block, '<3f', 0x40)
        high = unpack(block, '<3f', 0x50)
        actual_low = [min(v[k] for v in vertices) for k in range(3)]
        actual_high = [max(v[k] for v in vertices) for k in range(3)]
        if any(not math.isfinite(x) for x in (*low, *high)) or any(
            low[k] > actual_low[k]+0.001 or high[k] < actual_high[k]-0.001
            or low[k] > high[k] for k in range(3)):
            raise ValueError('Vertices outside stored AABB')
        meshes.append(dict(offset=base, size=size, version=version, vertices=vertices,
                           faces=faces, bounds=[actual_low, actual_high], stored_bounds=[low, high]))
    if not meshes:
        raise ValueError('No supported GBD blocks')
    return meshes


def main():
    p = argparse.ArgumentParser(description=__doc__)
    p.add_argument('--data-root', type=Path, required=True)
    p.add_argument('--layout-id', type=lambda v:int(v,0), required=True)
    p.add_argument('--output', type=Path, required=True)
    args = p.parse_args()
    source = resource_path(args.data_root, args.layout_id)
    data = source.read_bytes()
    resources, nodes = layout(data)
    args.output.mkdir(parents=True, exist_ok=True)
    report = dict(schema=1, research_only=True, world_transform_verified=False,
                  layout_id=f'{args.layout_id:08X}', source=str(source),
                  source_sha256=hashlib.sha256(data).hexdigest(),
                  node_types=dict(collections.Counter(n['type'] for n in nodes)),
                  resources=resources, nodes=nodes, geometry=[], failures=[])
    for resource in resources:
        if resource['type'] != 0x706862:
            continue
        rid = resource['resource_id']
        path = resource_path(args.data_root, rid)
        try:
            raw = path.read_bytes()
            meshes = geometry(raw)
            for index, mesh in enumerate(meshes):
                name=f'{rid:08X}-{index}.obj'
                with (args.output/name).open('w') as f:
                    f.write('# Research PHB geometry: asset-local, not server/world coordinates.\n')
                    for v in mesh['vertices']:
                        f.write('v '+' '.join(f'{x:.9g}' for x in v)+'\n')
                    for face in mesh['faces']:
                        f.write('f '+' '.join(str(x+1) for x in face[:3])+'\n')
                report['geometry'].append(dict(resource_name=resource['name'], resource_id=rid,
                    source_sha256=hashlib.sha256(raw).hexdigest(), block_offset=mesh['offset'],
                    vertices=len(mesh['vertices']), triangles=len(mesh['faces']), bounds=mesh['bounds'], stored_bounds=mesh['stored_bounds'],
                    face_attribute_counts=dict(collections.Counter(f[3] for f in mesh['faces'])), obj=name))
        except (ValueError, OSError, struct.error) as e:
            report['failures'].append(dict(resource_id=rid, reason=str(e)))
    (args.output/'extraction.json').write_text(json.dumps(report, indent=2, allow_nan=False)+'\n')
    print(json.dumps(dict(layout=report['layout_id'], resources=len(resources), nodes=len(nodes),
        phb_resources=sum(r['type']==0x706862 for r in resources),
        extracted_blocks=len(report['geometry']), triangles=sum(m['triangles'] for m in report['geometry']),
        failed_resources=len(report['failures']), report=str(args.output/'extraction.json')),indent=2))

if __name__ == '__main__':
    main()

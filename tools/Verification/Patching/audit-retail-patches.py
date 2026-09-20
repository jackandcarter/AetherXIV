#!/usr/bin/env python3
"""Independently audit a user-owned 1.x patch library and emit local test samples.
No retail data belongs in source control. Output goes to the requested local folder.
"""
import argparse
import collections
import hashlib
import json
import pathlib
import struct
import zlib

parser = argparse.ArgumentParser()
parser.add_argument('library', type=pathlib.Path, help='ffxiv_patches directory')
parser.add_argument('output', type=pathlib.Path)
args = parser.parse_args()
args.output.mkdir(parents=True, exist_ok=True)
magic = bytes.fromhex('915a4950415443480d0a1a0a')
summary = []

def chunk(command, body):
    data = command + body
    return struct.pack('>I', len(body)) + data + struct.pack('>I', zlib.crc32(data))

for path in sorted(args.library.glob('*/patch/*.patch')):
    # Sequential reads avoid hundreds of thousands of seeks on external storage.
    archive = path.read_bytes()
    assert archive[:12] == magic, path
    offset = 12
    counts = collections.Counter()
    shapes = collections.Counter()
    selected = {}
    expected = {}
    header = None
    while offset < len(archive):
        length = struct.unpack_from('>I', archive, offset)[0]
        command = archive[offset + 4:offset + 8]
        end = offset + 8 + length
        assert end + 4 <= len(archive), (path, offset, 'truncated')
        data = memoryview(archive)[offset + 4:end]
        assert zlib.crc32(data) == struct.unpack_from('>I', archive, end)[0], (path, offset, 'crc')
        body = memoryview(archive)[offset + 8:end]
        counts[command.decode()] += 1
        if command == b'FHDR':
            header = struct.unpack('>I4sIII', body)
            assert header[0] == 0x200 and header[1] in (b'HIST', b'DIFF'), (path, header)
        elif command == b'ETRY':
            path_size = struct.unpack_from('>I', body)[0]
            name = bytes(body[4:4 + path_size]).decode('utf-8')
            cursor = 4 + path_size
            item_count = struct.unpack_from('>I', body, cursor)[0]
            cursor += 4
            assert item_count > 0
            for index in range(item_count):
                mode = bytes(body[cursor:cursor + 4])
                destination = bytes(body[cursor + 24:cursor + 44])
                compression = bytes(body[cursor + 44:cursor + 48])
                size, old, new = struct.unpack_from('>III', body, cursor + 48)
                cursor += 60
                assert mode in (b'A\0\0\0', b'M\0\0\0', b'D\0\0\0')
                assert compression in (b'N\0\0\0', b'Z\0\0\0')
                assert index == item_count - 1 or size == 0
                if size:
                    payload = body[cursor:cursor + size]
                    decoded = zlib.decompress(payload) if compression[0] == ord('Z') else payload
                    assert len(decoded) == new, (path, name, 'size')
                    assert hashlib.sha1(decoded).digest() == destination, (path, name, 'sha1')
                cursor += size
            assert cursor == length, (path, name, cursor, length)
            shape = f'{chr(mode[0])}/{chr(compression[0])}/history={item_count > 1}/empty={new == 0}'
            shapes[shape] += 1
            if shape not in selected:
                selected[shape] = archive[offset:end + 4]
                expected[name] = None if mode[0] == ord('D') else {'size': new, 'sha1': destination.hex()}
        offset = end + 4
    assert header is not None
    assert tuple(counts[c] for c in ('ETRY', 'ADIR', 'DELD')) == header[2:], (path, counts, header)
    sample = args.output / path.stem
    sample.mkdir(exist_ok=True)
    sample_header = struct.pack('>I4sIII', 0x200, b'HIST', len(selected), 0, 0)
    (sample / 'sample.patch').write_bytes(magic + chunk(b'FHDR', sample_header) + b''.join(selected.values()))
    (sample / 'expected.json').write_text(json.dumps(expected, indent=2))
    result = {'file': path.name, 'crc32': f'{zlib.crc32(archive):08X}', 'bytes': len(archive),
              'entries': counts['ETRY'], 'shapes': dict(shapes), 'sample_entries': len(selected)}
    summary.append(result)
    print(f"PASS {path.name}: {counts['ETRY']} entries, {len(selected)} samples", flush=True)
(args.output / 'summary.json').write_text(json.dumps(summary, indent=2))
print(f'PASS {len(summary)} archives; {sum(p["entries"] for p in summary)} entries', flush=True)

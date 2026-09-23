#!/usr/bin/env python3
"""Verify named Lua getter -> sheet column mappings against the installed client.
Read-only: never assign unverified values to server combat settings.
"""
import argparse
import hashlib
import importlib.util
import json
from pathlib import Path
import re


def load_module(path):
    spec = importlib.util.spec_from_file_location('lpb', path)
    module = importlib.util.module_from_spec(spec)
    spec.loader.exec_module(module)
    return module


def bound_methods(root):
    """Accept only direct CLOSURE then SETTABLE method definitions."""
    methods = {}
    for pc in range(len(root.code) - 1):
        first, second = (root.code[pc] & 0xffffffff, root.code[pc + 1] & 0xffffffff)
        if first & 63 != 36 or second & 63 != 9:
            continue
        destination = (first >> 6) & 255
        key, value = (second >> 23) & 511, (second >> 14) & 511
        if key < 256 or value != destination:
            continue
        kind, name = root.constants[key - 256]
        if kind == 4:
            methods[name] = root.protos[first >> 14]
    return methods


def sheet_read(function):
    first, second = [word & 0xffffffff for word in function.code[:2]]
    if first & 63 != 11 or second & 63 != 1:
        raise ValueError('Expected SELF followed by LOADK')
    method_index = (first >> 14) & 511
    if method_index < 256:
        raise ValueError('Dynamic getter name')
    method = function.constants[method_index - 256][1]
    column = function.constants[second >> 14][1]
    if method not in ('getGameCommandData', 'getGameCommandBasicData'):
        raise ValueError('Unexpected data source')
    if (second >> 6) & 255 != ((first >> 6) & 255) + 2:
        raise ValueError('Column is not the call argument')
    return ('gameCommandBasic' if method.endswith('BasicData') else 'gameCommand', int(column))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('client', type=Path)
    parser.add_argument('evidence', type=Path)
    parser.add_argument('--decoder', type=Path, default=Path('.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py'))
    args = parser.parse_args()
    decoder = load_module(args.decoder)
    path = decoder.find_lpb(args.client, 'GameCommandBaseClass')
    root = decoder.Reader(decoder.decode_lpb(path.read_bytes())).parse_root()
    methods = bound_methods(root)
    expected = {'getRecastTime': ('gameCommandBasic', 79), 'getRange': ('gameCommand', 64),
                'getBestRange': ('gameCommand', 65), 'getCommandTPCost': ('gameCommandBasic', 115),
                'getCommandMPCost': ('gameCommandBasic', 114), 'getCastTime': ('gameCommandBasic', 76)}
    verified = []
    for name, expected_read in expected.items():
        function = methods[name]
        actual = sheet_read(function)
        if actual != expected_read:
            raise ValueError((name, actual, expected_read))
        verified.append(dict(method=name, sheet=actual[0], column=actual[1],
                             luaLine=function.linedefined,
                             functionCodeSha256=hashlib.sha256(b''.join((i & 0xffffffff).to_bytes(4, 'little') for i in function.code)).hexdigest(),
                             limitation='Base-class read; overrides, unit conversions and actor modifiers must still be respected.'))
    data = json.loads((args.evidence / 'parameter-columns-uninterpreted.json').read_text())
    by_sheet = {name: {r['id']: r for r in rows} for name, rows in data.items()}
    values = []
    for command_id in sorted(by_sheet['gameCommand']):
        values.append(dict(commandId=command_id, baseGetterInputs={v['method']: by_sheet[v['sheet']][command_id]['values'][str(v['column'])] for v in verified},
                           runtimeApproved=False))
    enums = Path('src/AetherXIV.Core.Map/Actors/Chara/Ai/BattleCommand.cs').read_text(encoding='utf-8-sig')
    if not re.search(r'enum BattleCommandValidUser\s*:\s*byte\s*\{\s*All,\s*Player,\s*Monster', enums):
        raise ValueError('Recheck validUser enum mapping')
    output = dict(clientScript=str(path.relative_to(args.client)), clientScriptSha256=hashlib.sha256(path.read_bytes()).hexdigest(),
                  methods=verified, values=values, validUser={'0':'All','1':'Player','2':'Monster'},
                  warning='getCommandMPCost passes column 114 through calculateCommandCost; this is not a flat MP-cost export. getCastTime also applies actor overrides.')
    (args.evidence / 'verified-base-getters.json').write_text(json.dumps(output, indent=2) + '\n')
    print(f'PASS: {len(verified)} bytecode getter mappings, {len(values)} action rows, runtime enum checked')


if __name__ == '__main__':
    main()

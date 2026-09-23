#!/usr/bin/env python3
"""Verify native network placement inputs against eight retail observations.
Requires pefile/capstone; read-only except evidence output. Build-specific.
"""
import hashlib,json,struct
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_32

ROOT=Path(__file__).resolve().parents[2]
CLIENT=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/ffxivgame.exe')
OUT=ROOT/'evidence/native-actor-placement-2026-09-18'

def main():
    raw=CLIENT.read_bytes();pe=pefile.PE(data=raw)
    assert pe.OPTIONAL_HEADER.ImageBase==0x400000
    rd=lambda va,n:pe.get_data(va-0x400000,n)
    u32=lambda va:struct.unpack('<I',rd(va,4))[0]
    cs=Cs(CS_ARCH_X86,CS_MODE_32)
    def instruction(va,mnemonic,operands):
        i=next(cs.disasm(rd(va,16),va));assert (i.mnemonic,i.op_str)==(mnemonic,operands),(hex(va),i.op_str)
    def call(site,target):
        assert rd(site,1)==b'\xe8' and site+5+struct.unpack('<i',rd(site+1,4))[0]==target
    for op,target in [(0xcc,0x58d675),(0xce,0x58ce03),(0xcf,0x58cef7)]:
        index=rd(0x58d7e4+op-0xf,1)[0];assert u32(0x58d788+index*4)==target
    # esi points to the internal opcode header; payload starts 0x10 bytes later.
    for va,offset in [(0x58ce37,0x18),(0x58ce42,0x1c),(0x58ce4d,0x20)]:
        instruction(va,'movss',f'xmm0, dword ptr [esi + {hex(offset)}]')
    instruction(0x58ce82,'fld','dword ptr [esi + 0x24]')
    instruction(0x58ce58,'movq','qword ptr [edi + 0x214], xmm1')
    instruction(0x58ce7a,'movq','qword ptr [edi + 0x21c], xmm1')
    for site,target in [(0x58ceed,0x58b2a0),(0x58d678,0x4d8860),
                        (0x4d8902,0x574780),(0x5747fb,0x774ad0),
                        (0x774bfb,0x447260),(0x774c1c,0x447260),
                        (0x774c36,0x78f810),(0x7750b0,0x774530),
                        (0x58b3d0,0x4d7980)]:call(site,target)
    # Second opcode dispatch: instantiate payload forwarding.
    idx=rd(0x4d8c24,1)[0];assert u32(0x4d8bc0+4*idx)==0x4d88e2
    instruction(0x4d88fd,'add','edi, 0x10')
    instruction(0x774bf0,'lea','edx, [ebp + 4]')
    instruction(0x774c11,'lea','ecx, [ebp + 0x24]')
    instruction(0x774c2e,'add','ebp, 0x44')
    instruction(0x58b3ac,'mov','eax, dword ptr [esp + 0x2c]')
    instruction(0x58b3b0,'movq','xmm0, qword ptr [eax]')
    instruction(0x58b3c6,'push','0x25')
    blocks={}
    for name,va,size in [('opcodeDispatch',0x58cd21,0x34),('positionHandler',0x58ce03,0xf4),
          ('positionConsumer',0x58b2a0,0x1bc),('instantiateForward',0x58d675,8),
          ('instantiateRouter',0x4d8860,0xad),('instantiateBridge',0x574780,0xa5),
          ('instantiatePayloadParsing',0x774bda,0x7a),('instantiateSubmit',0x775084,0x31),
          ('actorCommandHeader',0x4d79c0,0x21)]:
        data=rd(va,size);blocks[name]=dict(va=hex(va),bytes=data.hex(),sha256=hashlib.sha256(data).hexdigest(),
            instructions=[dict(va=hex(i.address),mnemonic=i.mnemonic,operands=i.op_str) for i in cs.disasm(data,va)])
    controls_path=ROOT/'evidence/captured-placement-probe-2026-09-18/controls.json'
    controls=[r for r in json.loads(controls_path.read_text())['controls'] if r['kind']=='battle']
    corpus=ROOT/'evidence/npc-restoration-2026-09-15/corpus-raw.jsonl'
    selected=[]
    for line in corpus.open():
        r=json.loads(line)
        if r.get('direction')!='server-to-client' or r.get('opcode') not in ('0x00CE','0x00CC'):continue
        for c in controls:
            if (r['capture'],r['tcpStream'],r['frameIndex'],r['sourceActorId'])!=(c['capture'],c['tcpStream'],c['frameIndex'],c['runtimeActorId']):continue
            if r['opcode']=='0x00CE':
                floats=struct.unpack_from('<4f',bytes.fromhex(r['payloadHex']),8)
                assert floats==tuple(c[k] for k in ('positionX','positionY','positionZ','rotation'))
            else:
                assert r['initParams'][6]['v']==c['actorClassId']
                assert r['objectName']==c['objectName']
            selected.append(dict(actorClassId=c['actorClassId'],packet=r))
    for c in controls:
        assert {'0x00CC','0x00CE'} <= {r['packet']['opcode'] for r in selected if r['actorClassId']==c['actorClassId']}
    OUT.mkdir(parents=True,exist_ok=True)
    report=dict(clientPath=str(CLIENT),clientSha256=hashlib.sha256(raw).hexdigest(),
        corpusSha256=hashlib.sha256(corpus.read_bytes()).hexdigest(),blocks=blocks,controls=controls,packets=selected,
        verified=['Eight captured enemies have same-frame server instantiate and position packets.',
                  'Raw position payload floats exactly match observed XYZ and rotation.',
                  'Native 0xCE handler reads those payload fields and copies XYZ into actor state.',
                  'Handler forwards position to 0x58b2a0; default branch submits incoming position with command 0x25.',
                  'Native instantiate route forwards payload strings at +4/+0x24 and parses data at +0x44.'],
        limits=['Not a proof of no placement database anywhere in client assets.',
                'Spawn-type branches, native factory internals, downstream collision and visual transforms not fully traced.',
                'Observed transform does not establish spawn origin, patrol or respawn rules.',
                'Names for native functions are inferred roles, not recovered symbols.'])
    (OUT/'verified-chain.json').write_text(json.dumps(report,indent=2)+'\n')
    print(f'PASS: native structural assertions and raw packet pairs for {len(controls)} enemy controls ({len(selected)} packets).')

if __name__=='__main__':main()

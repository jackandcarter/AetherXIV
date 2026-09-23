#!/usr/bin/env python3
"""Record the 2012.09.19 client's 0xDB to look-at consumer chain; no runtime edits.
Requires pefile and capstone. Addresses are build-specific and checked structurally.
"""
import argparse,hashlib,json,struct
from pathlib import Path
import pefile
from capstone import Cs,CS_ARCH_X86,CS_MODE_32
p=argparse.ArgumentParser(description=__doc__)
p.add_argument('client',type=Path)
p.add_argument('--output',type=Path,default=Path('evidence/target-field-2026-09-17/client-chain.json'))
a=p.parse_args();raw=a.client.read_bytes();pe=pefile.PE(data=raw)
assert pe.OPTIONAL_HEADER.ImageBase==0x400000
rd=lambda va,n:pe.get_data(va-0x400000,n)
u32=lambda va:struct.unpack('<I',rd(va,4))[0]
cs=Cs(CS_ARCH_X86,CS_MODE_32)
blocks={}
for name,start,size in [
 ('actorOpcodeDispatch',0x58cd21,0x34),('packetHandler',0x58d1a1,0x28),
 ('packetWrapper',0x589290,0x1e),('scriptWrapper',0x589260,0x21),
 ('sharedTwoChannelSetter',0x587f80,0x45),('stateWrite',0x585f60,0xcc),
 ('stateToSceneCommand',0x58627b,0x63),('sceneCommandSubmit',0x58640a,9),
 ('scriptRegistration',0x72f5b0,0x115),('scriptNative',0x6e43a0,0x103),
 ('scriptBridge',0x75c580,0x29),
 ('sceneForward',0x4e98d1,0x2d),('sceneRoute',0x60c140,0x28),
 ('actorVirtualRoute',0x7c95cb,0x29),('visualDispatch',0x662d5e,0x1a),
 ('visualLookAtCase',0x66340c,0x3f),('visualWrapper',0x65ceb0,0x49),
 ('controllerWrapper',0x854830,0x4b),('controllerState',0x853b50,0xfa),
 ('nodeForward',0x854663,0x21),('nodeTypeGate',0xac99b0,0x27),
 ('nodeFieldSetter',0xadd760,0x70),('lookAtNodeType',0xb8d680,6),
 ('motionFactor',0xaea66e,0x13),('rateScaling',0xaea707,0x92),
 ('motionIntegration',0xaea8fc,0xbb),('nearZeroBranch',0xaed610,0x5d)]:
 data=rd(start,size)
 blocks[name]={'va':hex(start),'bytes':data.hex(),'sha256':hashlib.sha256(data).hexdigest(),
               'instructions':[{'va':hex(i.address),'mnemonic':i.mnemonic,'operands':i.op_str} for i in cs.disasm(data,start)]}
idx=rd(0x58d7e4+0xdb-0xf,1)[0]
assert u32(0x58d788+idx*4)==0x58d1a1
assert rd(0x58d1b8,3)==bytes.fromhex('d94614') # fld [esi+14], payload+4
assert rd(0xfd671c,17)==b'_lookAtCharacter\0'
assert rd(0x72f5e7,5)==bytes.fromhex('bea0436e00') # registered function pointer
assert rd(0x585fed,8)==bytes.fromhex('f30f1180a8020000') # movss state+2a8
for site,dest in [(0x58d1c4,0x589290),(0x5892a6,0x587f80),
                  (0x589279,0x587f80),(0x587fb0,0x585f60),
                  (0x6e4489,0x75c580),(0x75c5a1,0x589260),
                  (0x4e98f9,0x60c140),(0x663439,0x65ceb0),
                  (0x65cef1,0x854830),(0x854873,0x853b50),
                  (0x85467f,0xac99b0),(0xac99cf,0xadd760)]:
 assert rd(site,1)==b'\xe8'
 assert site+5+struct.unpack('<i',rd(site+1,4))[0]==dest
# Resolve the named visual vtable through MSVC RTTI rather than nearby strings.
vtable=0x10a92e4
locator=u32(vtable-4); type_descriptor=u32(locator+12)
assert rd(type_descriptor+8,256).split(b'\0')[0] == b'.?AVLookAtIKObject@IKDynamics@Phieg@Engine@CDev@SQEX@@'
assert u32(vtable+0x14)==0xb8d680
assert rd(0xb8d680,6)==bytes.fromhex('b802000000c3')
assert u32(0xfc0d34+0x274)==0x662d30
visual_case=rd(0x663c08+0xb,1)[0]
assert u32(0x663ae8+visual_case*4)==0x66340c
# Check decoded operations, avoiding assumptions about frame-time units.
def instruction_at(address):
 return next(cs.disasm(rd(address,16),address))
for address,mnemonic,operands in [
 (0xaea66e,'movss','xmm1, dword ptr [esi + 0xd4]'),
 (0xaea676,'mulss','xmm1, dword ptr [esi + 0x1c]'),
 (0xaea711,'mulss','xmm0, xmm1'),
 (0xaea743,'mulss','xmm5, xmm1'),
 (0xaea74d,'mulss','xmm4, xmm1'),
 (0xaea905,'mulss','xmm0, xmm1'),
 (0xaea909,'addss','xmm0, dword ptr [eax]'),
 (0xaea98f,'mulss','xmm7, xmm1'),
 (0xaea993,'addss','xmm7, dword ptr [esi + 0xcc]')]:
 i=instruction_at(address);assert (i.mnemonic,i.op_str)==(mnemonic,operands)
out={'clientSha256':hashlib.sha256(raw).hexdigest(), 'imageBase':'0x400000',
     'opcode':'0x00DB','handler':'0x58d1a1',
     'verified':['payload+4 is loaded as float32',
                 'actor IDs with high three bits 110 are ignored by this handler',
                 'packet and named _lookAtCharacter script path converge at 0x587f80',
                 'packet uses selector 1 and priority 100; script uses selector 0 and priority 200',
                 'shared setter writes the float to indexed state offset 0x2a8'],
     'visualFinding': {
         'meaning': 'look-at motion-rate multiplier in the LookAtIKObject branch',
         'nodeType': 'LookAtIKObject', 'fieldOffset': '0xd4',
         'factor': 'packetFloat * node[0x1c]',
         'scaledTerms': ['node[0x58] acceleration term', 'node[0x64] rate limit', 'node[0x68] rate limit'],
         'nearZeroThreshold': struct.unpack('<f',rd(0xfee2b4,4))[0],
         'limits': ['Names of motion terms are inferred from integration arithmetic.',
                    'No claim of seconds, fixed completion duration or exact visible speed ratio.',
                    'Type-3 node branch remains uncharacterized; no in-client visual test.']},
     'unresolved':['meaning of the two channel indices','type-3 node semantics','visible behavior and server emission policy'],
     'blocks':blocks}
a.output.parent.mkdir(parents=True,exist_ok=True);a.output.write_text(json.dumps(out,indent=2)+'\n')
print('PASS: packet/script chain, visual dispatch, LookAtIKObject RTTI/type and motion-scaling arithmetic')

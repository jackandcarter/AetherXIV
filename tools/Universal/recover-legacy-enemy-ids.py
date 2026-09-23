#!/usr/bin/env python3
"""Read-only 1.x sheet evidence probe.
Format/decoder adapted from jpd002/SeventhUmbral dataobjects/{XmlFileDecoder,SheetData}.cpp
at eead5fef6a2e5db9ffd82e9377bea72b23bf58af. See license below.
No client assets are bundled. Indices are original sheet indices, not interpreted fields.
"""
# Copyright (c) 2013-2014, Jean-Philip Desjardins
# All rights reserved.
#
# Redistribution and use in source and binary forms, with or without
# modification, are permitted provided that the following conditions are met:
#
#  * Redistributions of source code must retain the above copyright notice,
#    this list of conditions and the following disclaimer.
#  * Redistributions in binary form must reproduce the above copyright
#    notice, this list of conditions and the following disclaimer in the
#    documentation and/or other materials provided with the distribution.
#
# THIS SOFTWARE IS PROVIDED BY THE AUTHOR AND CONTRIBUTORS ``AS IS'' AND ANY
# EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
# WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
# DISCLAIMED. IN NO EVENT SHALL THE AUTHOR OR CONTRIBUTORS BE LIABLE FOR ANY
# DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
# (INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR
# SERVICES; LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER
# CAUSED AND ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT
# LIABILITY, OR TORT (INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY
# OUT OF THE USE OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH
# DAMAGE.
from pathlib import Path
import struct,xml.etree.ElementTree as E
ROOT = None
def path(i):return ROOT/'data'/('/'.join(f'{x:02X}' for x in i.to_bytes(4,'big'))+'.DAT')
def xml(i):
 b=bytearray(path(i).read_bytes())
 if b[-1]==241:
  b=b[:-1];n=len(b)
  for a in range(0,n//2,2):b[a],b[n-1-a]=b[n-1-a],b[a]
  xa=(n*7)&65535;xb=struct.unpack_from('<H',b,6)[0]^0x6c6d
  for o in range(0,n-1,2):struct.pack_into('<H',b,o,struct.unpack_from('<H',b,o)[0]^(xa if o%4==0 else xb))
  if n%2:b[-1]^=(xa^xb)&255
 end = bytes(b).find(b"</ssd>")
 assert end >= 0 and len(b) - end - 6 <= 4, "Invalid XML boundary"
 return E.fromstring(bytes(b[:end+6]))
def rows(schema):
 for sheet in xml(schema).findall('sheet'):
  if sheet.get('lang') not in (None,'en'):continue
  ts=[x.text for x in sheet.findall('type/param')];ix=[int(x.text) for x in sheet.findall('index/param')]
  for block in sheet.findall('block/file'):
   data=path(int(block.text)).read_bytes();enable=path(int(block.get('enable'))).read_bytes();pos=0
   for start,count in struct.iter_unpack('<II',enable):
    for rid in range(start,start+count):
     vals=[];offset=pos
     for t in ts:
      if t=='str':
       size=struct.unpack_from('<H',data,pos)[0];pos+=2
       val=bytes(x^115 for x in data[pos+1:pos+size]).rstrip(b'\0').decode('utf8',errors='replace');pos+=size
      else:
       fmt={'bool':'B','s8':'b','u8':'B','s16':'h','u16':'H','s32':'i','u32':'I','float':'f','f16':'H'}[t];val=struct.unpack_from('<'+fmt,data,pos)[0];pos+=struct.calcsize(fmt)
      vals.append(val)
     yield dict(id=rid,values=dict(zip(ix,vals)),dataFile=int(block.text),offset=offset)
   assert pos==len(data),(int(block.text),pos,len(data))

def main():
 import argparse,json,hashlib,collections
 global ROOT
 ap=argparse.ArgumentParser(description=__doc__)
 ap.add_argument('client',type=Path);ap.add_argument('profiles',type=Path)
 ap.add_argument('observations',type=Path);ap.add_argument('corpus',type=Path);ap.add_argument('output',type=Path)
 args=ap.parse_args();ROOT=args.client;out=args.output;out.mkdir(parents=True,exist_ok=True)
 schemas={'names':189071360,'command_names':189071383,'actorclass':16973832,'command':16973987,'gameCommand':16974043,'gameCommandBasic':16974986}
 tables={k:list(rows(v)) for k,v in schemas.items()}
 profiles=json.loads(args.profiles.read_text());names={p['enemy'].lower() for p in profiles}
 actions={a['name'].lower() for p in profiles for a in p['actions']}
 matches=[r for r in tables['command_names'] if str(r['values'].get(2,'')).lower() in actions]
 displays=[r for r in tables['names'] if str(r['values'].get(1,'')).lower() in names]
 display_ids={r['id'] for r in displays}
 actors=[r for r in tables['actorclass'] if r['values'].get(5) in display_ids]
 selected_ids={r['id'] for r in matches}
 flags=[r for r in tables['command'] if r['id'] in selected_ids]
 # Export labels and provenance, not wholesale localized client text/tooltips.
 labels=[dict(id=r['id'],name=r['values'][2],dataFile=r['dataFile'],offset=r['offset']) for r in matches]
 display_labels=[dict(id=r['id'],name=r['values'][1],dataFile=r['dataFile'],offset=r['offset']) for r in displays]
 resolved=[]
 for p in profiles:
  resolved.append(dict(enemy=p['enemy'],displayNameIds=[r['id'] for r in display_labels if r['name'].lower()==p['enemy'].lower()],actions=[dict(name=a['name'],clientLabelIds=[r['id'] for r in labels if r['name'].lower()==a['name'].lower()],enemyPacketConfirmed=False) for a in p['actions']]))
 observations=json.loads(args.observations.read_text());by_actor=collections.defaultdict(list)
 for o in observations:
  if o.get('identityChainValid') and '/Monster/' in o.get('classPath',''):
   by_actor[(o['capture'],o['tcpStream'],o['runtimeActorId'])].append(o)
 for v in by_actor.values():v.sort(key=lambda r:r['frameIndex'])
 command_names={r['id']:r['values'].get(2) for r in tables['command_names']}
 edges=[]
 for line in args.corpus.open():
  r=json.loads(line)
  if 'commandId' not in r or r.get('direction')!='server-to-client':continue
  candidates=[o for o in by_actor.get((r['capture'],r['tcpStream'],r.get('actor')),[]) if o['frameIndex']<=r['frameIndex']]
  if not candidates:continue
  o=candidates[-1]
  edges.append(dict(capture=r['capture'],captureSha256=o['captureSha256'],tcpStream=r['tcpStream'],frameIndex=r['frameIndex'],opcode=r['opcode'],actor=r['actor'],actorClassId=o['actorClassId'],identityFrameIndex=o['frameIndex'],displayNameId=o['displayNameId'],commandId=r['commandId'],clientLabel=command_names.get(r['commandId']),animation=r['animation'],actions=r.get('actions',[]),payloadHex=r['payloadHex'],limitation='Nearest preceding observed initialization; no claim of attack completion from command ID alone.'))
 parameters={k:[r for r in tables[k] if r['id'] in selected_ids] for k in ['gameCommand','gameCommandBasic']}
 schema_metadata={k:[dict(attributes=s.attrib,types=[n.text for n in s.findall('type/param')],indices=[int(n.text) for n in s.findall('index/param')]) for s in xml(v).findall('sheet') if s.get('lang') in (None,'en')] for k,v in schemas.items()}
 source_ids=set(schemas.values())|{0x01030000}
 for schema in schemas.values():
  for sheet in xml(schema).findall('sheet'):
   if sheet.get('lang') not in (None,'en'):continue
   for b in sheet.findall('block/file'):source_ids.update([int(b.text),int(b.get('enable')),int(b.get('offset'))])
 manifest=[dict(resourceId=i,path=str(path(i).relative_to(ROOT)),sha256=hashlib.sha256(path(i).read_bytes()).hexdigest()) for i in sorted(source_ids)]
 for name,data in [('parameter-columns-uninterpreted',parameters),('sheet-schemas',schema_metadata),('action-label-ids',labels),('enemy-display-ids',display_labels),('enemy-actorclass-rows',actors),('command-client-columns',flags),('identity-resolution',resolved),('retail-enemy-command-events',edges),('client-source-manifest',manifest)]:
  (out/(name+'.json')).write_text(json.dumps(data,indent=2)+'\n')
 summary=dict(clientBuild=(ROOT/'game.ver').read_text().strip(),enemyNames=len(names),displayIds=len(displays),actorClassRows=len(actors),distinctActionNames=len(actions),matchingCommandRows=len(matches),runtimeEvents=len(edges),targetEnemyActionPackets=sum(e['actorClassId'] in {a['id'] for a in actors} for e in edges),inputSha256={str(p):hashlib.sha256(p.read_bytes()).hexdigest() for p in [args.profiles,args.observations,args.corpus]},parameterLimit='Recovered 50 gameCommand and 9 gameCommandBasic columns per candidate; original indices preserved, field meanings not yet validated.')
 (out/'summary.json').write_text(json.dumps(summary,indent=2)+'\n');print(json.dumps(summary,indent=2))

if __name__=='__main__':main()

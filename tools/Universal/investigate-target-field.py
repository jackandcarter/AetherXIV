#!/usr/bin/env python3
"""Preserve retail 0x00DB field patterns and same-actor context without assigning semantics."""
import collections
import hashlib
import json
import struct
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
p=ROOT/'evidence/npc-restoration-2026-09-15/corpus-raw.jsonl'
raw=p.read_bytes()
streams=collections.defaultdict(list)
for line,b in enumerate(raw.splitlines(),1):
 r=json.loads(b)
 if r['direction']=='server-to-client':streams[(r['capture'],r['tcpStream'])].append((line,r))
results=[]
for (capture,stream),rows in sorted(streams.items()):
 rows.sort(key=lambda v:(v[1]['frameIndex'],v[0]))
 previous={}
 for line,r in rows:
  actor=r['sourceActorId']
  if r['opcode'] in ('0x00CA','0x00CB'):previous.pop(actor,None)
  if r['opcode']!='0x00DB':continue
  payload=bytes.fromhex(r['payloadHex'])
  assert len(payload)==8
  target,bits=struct.unpack('<II',payload)
  value=struct.unpack('<f',payload[4:])[0]
  nearby=[]
  for otherline,s in rows:
   dt=s['frameTimestamp']-r['frameTimestamp']
   if abs(dt)>1500 or otherline==line:continue
   if s['sourceActorId']!=actor:continue
   if s['opcode'] not in ('0x00CF','0x00DB','0x00DE','0x0139','0x013A','0x013B','0x013C','0x0195'):continue
   nearby.append(dict(corpusLine=otherline,deltaMs=dt,opcode=s['opcode'],
                      **{k:s[k] for k in ('commandId','actions','rotation','payloadHex') if k in s}))
  results.append(dict(corpusLine=line,capture=capture,tcpStream=stream,frameIndex=r['frameIndex'],
                      sourceActorId=actor,targetActorId=f'0x{target:08X}',secondWordHex=f'0x{bits:08X}',
                      floatInterpretation=value,previousTarget=previous.get(actor),nearbySameActor=nearby))
  previous[actor]=f'0x{target:08X}'
out=dict(inputSha256=hashlib.sha256(raw).hexdigest(),records=results,
         counts=dict(collections.Counter(str(r['floatInterpretation']) for r in results)),
         limitations=['Float interpretation is not verified by a client consumer.',
                      'Temporal proximity is not causation; previousTarget is only observed within this actor lifetime.',
                      'No packet replay or runtime behavior was changed.'])
dest=ROOT/'evidence/target-field-2026-09-17/observations.json'
dest.parent.mkdir(parents=True,exist_ok=True)
dest.write_text(json.dumps(out,indent=2)+'\n')
print(out['counts'])
for value in (0.,.5,1.):
 subset=[r for r in results if r['floatInterpretation']==value]
 print(value,collections.Counter('first-observed' if r['previousTarget'] is None else 'unchanged' if r['previousTarget']==r['targetActorId'] else 'changed' for r in subset))

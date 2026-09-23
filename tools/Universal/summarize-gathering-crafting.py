#!/usr/bin/env python3
"""Derive reproducible event pairs and observed steps, without fitting formulas."""
import collections
import importlib.util
import json
from pathlib import Path
import struct

ROOT=Path(__file__).resolve().parents[2]
OUT=ROOT/'evidence/gathering-crafting-2026-09-17'

def save(name,data):
    (OUT/name).write_text(json.dumps(data,indent=2)+'\n')

def encode(params):
    out=bytearray()
    for p in params:
        t=p['t'];v=p.get('v')
        if t in ('i32','u32','actor','u8','u16'):
            code,fmt={'i32':(0,'>i'),'u32':(1,'>I'),'actor':(6,'>I'),'u8':(12,'B'),'u16':(27,'<H')}[t]
            out.append(code);out.extend(struct.pack(fmt,v))
        elif t=='nil':out.append(5)
        elif t=='bool':out.append(3 if v else 4)
        elif t=='str':out.append(2);out.extend(v.encode('ascii')+b'\0')
        else:raise ValueError('Unsupported roundtrip type '+t)
    return bytes(out)+b'\x0f'

def main():
    spec=importlib.util.spec_from_file_location('decode',ROOT/'.local-evidence/tools/Universal/decode-corpus-wide.py')
    decoder=importlib.util.module_from_spec(spec);spec.loader.exec_module(decoder)
    summaries=[];total_checks=0
    for source in sorted((OUT/'events').glob('*.json')):
        events=json.loads(source.read_text());pairs=[];pending=None;unpaired=[]
        for e in events:
            op=e['opcode'];d=e.get('decoded') or {};p=d.get('params',[])
            if op in ('0x0130','0x012E'):
                raw=bytes.fromhex(e['payloadHex']);offset=0x49 if op=='0x0130' else 0x11
                decoded,used=decoder.decode_lua_parameters(raw[offset:])
                assert decoded==p,(source.name,e['frameNumber'],'parameters differ')
                assert encode(p)==raw[offset:offset+used],(source.name,e['frameNumber'],'roundtrip differs')
                total_checks+=1
            if op in ('0x012D','0x0130','0x0131') and pending is not None:
                unpaired.append(pending);pending=None
            if op=='0x0130':
                delegated=d.get('functionName')=='delegateCommand'
                pending={'requestFrame':e['frameNumber'],'timestamp':e['captureTimestamp'],
                    'trigger':d['trigger'],'owner':d['owner'],'event':d['eventName'],
                    'function':p[1]['v'] if delegated else d['functionName'],
                    'arguments':p[3:] if delegated else p}
            elif op=='0x012E' and pending is not None and d['trigger']==pending['trigger']:
                pending.update(responseFrame=e['frameNumber'],response=p,
                    observedRoundTripMs=round((float(e['captureTimestamp'])-float(pending['timestamp']))*1000,3))
                pairs.append(pending);pending=None
        if pending:unpaired.append(pending)
        save(source.stem+'-transactions.json',pairs)
        summaries.append({'stream':source.stem,'eventPackets':len(events),'pairs':len(pairs),
                          'unpaired':unpaired,'functionCounts':dict(collections.Counter(p['function'] for p in pairs))})
        lines=['# '+source.stem,'','Parameters below retain wire types in the companion JSON. Elapsed times include client/user/network time.','',
               '| Request → reply frame | Function | Arguments | Response | Observed ms |','| --- | --- | --- | --- | --- |']
        for p in pairs:
            vals=lambda v:[a.get('v',a) for a in v]
            lines.append(f"| {p['requestFrame']} → {p['responseFrame']} | {p['function']} | `{json.dumps(vals(p['arguments']))}` | `{json.dumps(vals(p['response']))}` | {p['observedRoundTripMs']} |")
        (OUT/(source.stem+'-timeline.md')).write_text('\n'.join(lines)+'\n')
    save('transaction-audit.json',{'streams':summaries,'parameterRoundTripsVerified':total_checks})
    pairs=json.loads((OUT/'local_leve_complete-s0-transactions.json').read_text())
    crafts=[];current=None;action=None
    for p in pairs:
        args=[v.get('v') for v in p['arguments']]
        if p['function']=='openCraftProgressWidget':
            current={'startFrame':p['requestFrame'],'initialDurabilityQualityHQ':args,'steps':[]};crafts.append(current)
        elif p['function']=='craftCommandUI':action=p
        elif p['function']=='updateInfo' and current is not None:
            current['steps'].append({'actionId':action['response'][0]['v'] if action else None,
                'choiceFrame':action['responseFrame'] if action else None,'resultFrame':p['requestFrame'],
                'progressDurabilityQuality':args[:3],'resultItemQualityQuantity':args[3:6],'hqChance':args[6]})
    save('observed-crafting-steps.json',crafts)
    print('Verified parameter byte round trips:',total_checks,'crafts:',len(crafts),'actions:',sum(len(c['steps']) for c in crafts))

if __name__=='__main__':main()

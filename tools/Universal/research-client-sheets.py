#!/usr/bin/env python3
"""Extract typed client sheets; unknown column meanings remain positional.

Format reference: local SeventhUmbral XmlFileDecoder/Sheet/SheetData/FileManager.
No server data writes. Run with /usr/bin/python3 (XML support required).
"""
import hashlib
import json
from pathlib import Path
import re
import struct
import sys
import xml.etree.ElementTree as ET

ROOT=Path(__file__).resolve().parents[2]
CLIENT=Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV')
OUT=ROOT/'evidence/gathering-crafting-2026-09-17/sheets'

def path(fid):
    h=f'{fid:08X}'
    return CLIENT/'data'/h[:2]/h[2:4]/h[4:6]/(h[6:]+'.DAT')

def xml_decode(raw):
    if raw[-1:]!=b'\xf1':return raw
    buf=bytearray(raw[:-1]);n=len(buf)
    left,right=0,n-1
    while left<right:
        buf[left],buf[right]=buf[right],buf[left];left+=2;right-=2
    a=(n*7)&65535;b=struct.unpack_from('<H',buf,6)[0]^0x6c6d
    for start,key in ((0,a),(2,b)):
        for off in range(start,n-1,4):
            struct.pack_into('<H',buf,off,struct.unpack_from('<H',buf,off)[0]^key)
    if n&1:buf[-1]^=(a^b)&255
    return bytes(buf)

def save(name,obj):
    target=OUT/name;target.parent.mkdir(parents=True,exist_ok=True)
    target.write_text(json.dumps(obj,indent=2,ensure_ascii=True)+'\n')

def parse_xml(raw):
    # The reference decoder leaves a terminal padding byte on some files.
    # Retain the entire decoded file separately, parse only its complete XML root.
    end=raw.rfind(b'</ssd>')
    if end<0:raise ValueError('No closing ssd root')
    return ET.fromstring(raw[:end+6])

def main():
    global OUT
    growth = '--growth' in sys.argv
    maps = '--maps' in sys.argv
    if growth and maps:
        raise ValueError('Choose --growth or --maps')
    if growth:
        OUT = ROOT/'evidence/player-growth-2026-09-17/sheets'
    elif maps:
        OUT = ROOT/'.local-evidence/umbra-map-travel/sheets'
    OUT.mkdir(parents=True,exist_ok=True)
    schema=parse_xml(xml_decode(path(0x01030000).read_bytes()))
    sheets=[dict(e.attrib) for e in schema.findall('sheet')]
    save('index.json',sheets)
    selected=[s for s in sheets if re.search('craft|harvest|gather|recipe|synth|guildleve|command|skill',s['name'],re.I)
              or s['name'] in ('weapon','equipment','itemData','xtx/itemName','compatibility','exp_BPCost','facility')]
    if growth:
        selected=[s for s in sheets if s['name'] in ('tribe','boot_charaTemp','boot_skillequip','exp_BPCost','_class','status','xtx/text_attrName','xtx/text_paramName','xtx/text_skillName','xtx/text_jobName')]
    elif maps:
        selected=[s for s in sheets if s['name'] in ('2Dmap_piece','2Dmap_marker','2Dmap_data','2Dmap_actor_data','aetheryte_2Dmap','_region','mapNavi_data','_zoneParam','regionParam','zoneGroupParam')]
    audit=[]
    formats={'s8':'b','u8':'B','bool':'B','s16':'h','u16':'H','f16':'H','s32':'i','u32':'I','float':'f'}
    for entry in selected:
        name=entry['name'];fid=int(entry['infofile']);schema_path=path(fid)
        info={'name':name,'schemaId':fid,'schemaPath':str(schema_path),'schemaSha256':hashlib.sha256(schema_path.read_bytes()).hexdigest(),'subSheets':[]}
        try:
            xml=xml_decode(schema_path.read_bytes());xmlpath=OUT/(name+'.xml');xmlpath.parent.mkdir(parents=True,exist_ok=True);xmlpath.write_bytes(xml)
            info['xmlTrailingHex']=xml[xml.rfind(b'</ssd>')+6:].hex()
            for sheet in parse_xml(xml).findall('sheet'):
                if sheet.attrib.get('lang','en')!='en':continue
                types=[e.text for e in sheet.findall('type/param')]
                sub={'attributes':sheet.attrib,'types':types,'columnIndices':[int(e.text) for e in sheet.findall('index/param')],'blocks':[],'rows':[]}
                info['subSheets'].append(sub)
                for block in sheet.findall('block/file'):
                    dataid=int(block.text);enableid=int(block.attrib['enable']);offsetid=int(block.attrib['offset'])
                    data=path(dataid).read_bytes();enabled=path(enableid).read_bytes();offsets=path(offsetid).read_bytes();off=0
                    assert len(offsets)==4*int(block.attrib['count'])
                    blockinfo={'attributes':block.attrib,'dataId':dataid,'dataBytes':len(data),'resources':[]}
                    for resource in (dataid,enableid,offsetid):
                        rp=path(resource);blockinfo['resources'].append({'id':resource,'path':str(rp),'sha256':hashlib.sha256(rp.read_bytes()).hexdigest()})
                    sub['blocks'].append(blockinfo)
                    assert len(enabled)%8==0
                    for start,count in struct.iter_unpack('<II',enabled):
                        for rowid in range(start,start+count):
                            values=[]
                            for kind in types:
                                if kind=='str':
                                    size=struct.unpack_from('<H',data,off)[0];off+=2
                                    assert size>=2
                                    marker=data[off];raw=bytes(v^0x73 for v in data[off+1:off+size]);off+=size
                                    assert raw[-1:]==b'\0'
                                    values.append({'text':raw[:-1].decode('utf-8','replace'),'rawHex':raw.hex(),'marker':marker})
                                else:
                                    fmt='<'+formats[kind];values.append(struct.unpack_from(fmt,data,off)[0]);off+=struct.calcsize(fmt)
                            sub['rows'].append({'id':rowid,'values':values})
                            expected_end=struct.unpack_from('<I',offsets,4*(rowid-int(block.attrib['begin'])))[0]
                            assert off==expected_end,(name,rowid,'row end mismatch',off,expected_end)
                    blockinfo['consumed']=off
                    assert off==len(data),(name,'unconsumed bytes',off,len(data))
            save(name+'.json',info)
            partial=OUT/(name+'.partial.json')
            if partial.exists():partial.unlink() # Superseded output from this extractor only.
            audit.append({'name':name,'rows':sum(len(s['rows']) for s in info['subSheets']),'subSheets':len(info['subSheets'])})
        except Exception as exc:
            info['error']=repr(exc);save(name+'.partial.json',info);audit.append({'name':name,'error':repr(exc)})
    save('audit.json',audit)
    print(json.dumps(audit,indent=2))

if __name__=='__main__':main()

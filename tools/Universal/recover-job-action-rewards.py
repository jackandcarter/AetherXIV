#!/usr/bin/env python3
"""Read-only evidence recovery; run from the project root. No database writes."""
import importlib.util,pathlib,sys,json,hashlib,re,subprocess
p=pathlib.Path('.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py');spec=importlib.util.spec_from_file_location('lua',p);lua=importlib.util.module_from_spec(spec);sys.modules['lua']=lua;spec.loader.exec_module(lua)
out=pathlib.Path('evidence/ability-unlocks-2026-09-17/client');out.mkdir(exist_ok=True);audit=[];rewards=[]
quests={r.split('\t')[2].lower():r.split('\t') for r in subprocess.check_output(['/usr/local/bin/mariadb','-N','ffxiv_server','-e','SELECT id,questName,className FROM gamedata_quests'],text=True).splitlines()}
for p in pathlib.Path('/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/client/script').rglob('*.le.lpb'):
 name=lua.cipher(p.name[:-7])
 if not re.fullmatch('(brd|pld|mnk|war|drg|blm|whm)0j[1-9]',name):continue
 raw=p.read_bytes();decoded=lua.decode_lpb(raw);r=lua.Reader(decoded);tree=r.parse_root();assert r.p==len(decoded)
 lines=list(lua.dump_function(tree));(out/(name+'.dis.txt')).write_text('\n'.join(lines)+'\n');audit.append({'name':name,'path':str(p),'sha256':hashlib.sha256(raw).hexdigest()})
 for i,line in enumerate(lines):
  if "['showGetJobAbilityWidget']" not in line:continue
  # SELF base Rn; command argument follows self and event arg, at R(n+3).
  m=re.search(r'SELF\s+R(\d+),',line);reg=int(m[1])+3
  block=lines[i+1:i+5];ids=[int(m[1]) for l in block if (m:=re.search(r'LOADK\s+R'+str(reg)+r' = (\d+)$',l))]
  if len(ids)!=1:raise ValueError((name,line,block))
  q=quests.get(name);assert q,name
  entry={'script':name,'questId':int(q[0]),'questName':q[1],'commandId':ids[0],'line':i+1,'sourceFile':str(out/(name+'.dis.txt'))}
  if not any(x['questId']==entry['questId'] and x['commandId']==entry['commandId'] for x in rewards):rewards.append(entry)
(out/'index.json').write_text(json.dumps(audit,indent=2)+'\n');(out.parent/'job-quest-rewards.json').write_text(json.dumps(rewards,indent=2)+'\n');print(json.dumps(rewards,indent=2))

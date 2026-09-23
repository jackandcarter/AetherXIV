#!/usr/bin/env python3
"""Read-only evidence recovery; run from the project root. No database writes."""
import pathlib,re,json,subprocess,hashlib
root=pathlib.Path.cwd();out=root/'evidence/ability-unlocks-2026-09-17';out.mkdir(exist_ok=True)
classes={'Pugilist':2,'Gladiator':3,'Marauder':4,'Archer':7,'Lancer':8,'Thaumaturge':22,'Conjurer':23,'Monk':15,'Paladin':16,'Warrior':17,'Bard':18,'Dragoon':19,'Black Mage':26,'White Mage':27}
rows=[]
for patch,stem in [('1.20','32606'),('1.21','39024')]:
 p=next((root/'evidence/patch-notes-2026-09-17').glob(stem+'*.txt'));text=p.read_text();current=None
 for n,line in enumerate(text.splitlines(),1):
  for name in classes:
   if line.strip()==name or line.rstrip().endswith('| Exclusive'+name): current=name
  m=re.match(r'\s*\| (\d+) \| Action \| ([^|]+) \|',line)
  if m and current:rows.append(dict(patch=patch,className=current,classJob=classes[current],level=int(m[1]),name=m[2].strip(),sourceFile=str(p.relative_to(root)),line=n,sourceSha256=hashlib.sha256(p.read_bytes()).hexdigest()))
raw=subprocess.check_output(['/usr/local/bin/mariadb','-N','ffxiv_server','-e','SELECT id,name,classJob,lvl FROM server_battle_commands'],text=True)
def norm(s):return re.sub('[^a-z0-9]','',s.lower())
db=[x.split('\t') for x in raw.splitlines()]
for r in rows:
 matches=[x for x in db if int(x[2])==r['classJob'] and norm(x[1])==norm(r['name'])]
 r['databaseMatches']=[{'id':int(x[0]),'level':int(x[3]),'name':x[1]} for x in matches]
 r['levelMatches']=len(matches)==1 and int(matches[0][3])==r['level']
(out/'action-level-audit.json').write_text(json.dumps(rows,indent=2)+'\n')
from collections import Counter
print(Counter((r['patch'],r['className']) for r in rows));print('Rows',len(rows),'matched',sum(r['levelMatches'] for r in rows));print([r for r in rows if not r['levelMatches']])

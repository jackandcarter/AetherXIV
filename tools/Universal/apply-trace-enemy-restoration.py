#!/usr/bin/env python3
"""Apply migration 44 only, with stopped-server guard, backup and atomic ledger."""
import argparse, datetime, hashlib, json, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NAME='20260918_000044_captured_shroud_thanalan_enemies.sql'
def query(sql):
    return subprocess.check_output(['mariadb','-N','--raw','ffxiv_server','-e',sql],text=True).strip()
def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--apply',action='store_true');args=ap.parse_args()
    procs=subprocess.check_output(['ps','-axo','comm='],text=True)
    if re.search(r'AetherXIV\.(Core\.(Map|World|Lobby)|UI\.App)',procs):raise SystemExit('Stop Core/server first.')
    if query('SELECT COUNT(*) FROM server_sessions')!='0':raise SystemExit('Active sessions; stopping.')
    path=ROOT/'db/direct-core/migrations'/NAME;raw=path.read_bytes();sha=hashlib.sha256(raw).hexdigest()
    applied=query(f"SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='{NAME}'")
    if applied:
        assert applied==sha,'Migration checksum mismatch'
        print('Already applied; checksum matches.');return
    rows=json.loads((ROOT/'evidence/enemy-restoration-2026-09-18/manifest.json').read_text())['selected']
    ids=','.join(map(str,sorted({r['actorClassId'] for r in rows})))
    tables=['gamedata_actor_class','server_battlenpc_pools','server_battlenpc_groups','server_battlenpc_spawn_locations']
    for t,pk,end in [(tables[1],'poolId',1844031),(tables[2],'groupId',1844056),(tables[3],'bnpcId',1844155)]:
        assert query(f'SELECT COUNT(*) FROM {t} WHERE {pk} BETWEEN 1844000 AND {end}')=='0','Reserved ID collision'
    subprocess.run(['/usr/bin/python3',str(ROOT/'tests/tools/test_trace_enemy_restoration.py')],check=True)
    print('Migration validated; 156 proposed spawn rows. No character/schema-baseline changes.')
    if not args.apply:return
    backup=ROOT/'.local-evidence/restoration-backups'/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ-trace-enemies')
    backup.mkdir(parents=True,exist_ok=False);manifest=[]
    for table in tables:
        cmd=['mariadb-dump','--single-transaction','--no-create-info','--skip-add-locks','--skip-lock-tables','ffxiv_server',table]
        if table=='gamedata_actor_class':cmd+=['--where=id IN ('+ids+')']
        data=subprocess.check_output(cmd);target=backup/(table+'.sql');target.write_bytes(data);target.chmod(0o600)
        manifest.append(dict(file=target.name,sha256=hashlib.sha256(data).hexdigest()))
    (backup/'manifest.json').write_text(json.dumps(manifest,indent=2)+'\n')
    sql='START TRANSACTION;\n'+raw.decode()+f"\nINSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('{NAME}','{sha}');\nCOMMIT;"
    subprocess.run(['mariadb','ffxiv_server'],input=sql,text=True,check=True)
    assert query('SELECT COUNT(*) FROM server_battlenpc_spawn_locations WHERE bnpcId BETWEEN 1844000 AND 1844155')=='156'
    print('Applied with atomic ledger. Backup:',backup)
if __name__=='__main__':main()

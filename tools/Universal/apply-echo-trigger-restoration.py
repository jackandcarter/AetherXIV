#!/usr/bin/env python3
"""Apply only the reviewed 000042/000043 repairs, with backup and atomic ledger.
Uses the invoking user's local MariaDB connection. Does not install a database.
"""
import argparse,datetime,hashlib,json,re,subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NAMES=['20260918_000042_limsa_echo_actor_definitions.sql','20260918_000043_man1g0_captured_triggers.sql']
IDS='1000096,1000097,1000107,1000108,1000109,1000142,1000869,1000870,1000871,1090067,1090068'
def query(sql):return subprocess.check_output(['mariadb','-N','ffxiv_server','-e',sql],text=True).strip()
def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--apply',action='store_true');args=ap.parse_args()
    procs=subprocess.check_output(['ps','-axo','comm='],text=True)
    if re.search(r'AetherXIV\.(Core\.(Map|World|Lobby)|UI\.App)',procs):raise SystemExit('Stop Core/server processes first.')
    if query('SELECT COUNT(*) FROM server_sessions')!='0':raise SystemExit('Active server sessions: refusing migration.')
    assert query(f'SELECT COUNT(*) FROM gamedata_actor_class WHERE id IN ({IDS})')=='11'
    pending=[]
    for name in NAMES:
        path=ROOT/'db/direct-core/migrations'/name;raw=path.read_bytes();sha=hashlib.sha256(raw).hexdigest()
        applied=query(f"SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='{name}'")
        if applied:
            assert applied==sha,('Migration checksum mismatch',name)
        else:pending.append((name,raw.decode(),sha))
    print('Pending:',[n for n,_,_ in pending])
    if not args.apply or not pending:return
    assert query('SELECT COUNT(*) FROM server_spawn_locations WHERE id IN (1072,1073) AND actorClassId NOT IN (1090067,1090068)')=='0'
    backup=ROOT/'.local-evidence/restoration-backups'/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ-echo-triggers')
    backup.mkdir(parents=True,exist_ok=False)
    manifests=[]
    for table,where in [('gamedata_actor_class',f'id IN ({IDS})'),('server_spawn_locations',f'actorClassId IN ({IDS}) OR id IN (1072,1073)')]:
        raw=subprocess.check_output(['mariadb-dump','--single-transaction','--no-create-info','--skip-add-locks','--skip-lock-tables','ffxiv_server',table,'--where='+where])
        target=backup/(table+'.sql');target.write_bytes(raw);target.chmod(0o600)
        manifests.append({'file':target.name,'sha256':hashlib.sha256(raw).hexdigest()})
    (backup/'manifest.json').write_text(json.dumps(manifests,indent=2)+'\n')
    statements=['START TRANSACTION;']
    for name,sql,sha in pending:
        statements.append(re.sub(r'^(START TRANSACTION;|COMMIT;)\s*$', '',sql,flags=re.M))
        statements.append(f"INSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('{name}','{sha}');")
    statements.append('COMMIT;')
    subprocess.run(['mariadb','ffxiv_server'],input='\n'.join(statements),text=True,check=True)
    print('Applied atomically with ledger. Content backup:',backup)
    assert query(f"SELECT COUNT(*) FROM gamedata_actor_class WHERE id IN ({IDS}) AND classPath<>'' AND JSON_VALID(eventConditions)")=='11'
    assert query('SELECT COUNT(*) FROM server_spawn_locations WHERE actorClassId IN (1090067,1090068)')=='2'
    print('Post-application class/event/spawn checks passed. No player data changed.')
if __name__=='__main__':main()

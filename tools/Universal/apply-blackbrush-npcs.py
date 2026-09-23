#!/usr/bin/env python3
"""Back up and apply only the reviewed named Black Brush NPC placements."""
import datetime, hashlib, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NAME='20260919_000046_blackbrush_named_pin_npcs.sql'
def query(sql):
    return subprocess.check_output(['mariadb','-N','ffxiv_server','-e',sql],text=True).strip()
def main():
    import argparse
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--apply',action='store_true');ap.add_argument('--identified',action='store_true');a=ap.parse_args()
    name='20260919_000047_blackbrush_identified_pin_npcs.sql' if a.identified else NAME
    actor_ids='1001392,1000673,1500073,1000672' if a.identified else '1500099,1600062'
    spawn_ids='1076,1077,1078,1079' if a.identified else '1074,1075'
    count='4' if a.identified else '2'
    procs=subprocess.check_output(['ps','-axo','comm='],text=True)
    if re.search(r'AetherXIV\.(Core\.(Map|World|Lobby)|UI\.App)',procs):raise SystemExit('Stop Core first.')
    assert query('SELECT COUNT(*) FROM server_sessions')=='0','Active sessions'
    raw=(ROOT/'db/direct-core/migrations'/name).read_bytes();sha=hashlib.sha256(raw).hexdigest()
    old=query(f"SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='{name}'")
    if old:
        assert old==sha;print('Already applied; ledger matches.');return
    assert query(f'SELECT COUNT(*) FROM server_spawn_locations WHERE id IN({spawn_ids})')=='0','ID collision'
    assert query(f'SELECT COUNT(*) FROM gamedata_actor_class a JOIN gamedata_actor_appearance p USING(id) WHERE a.id IN({actor_ids})')==count
    if not a.apply:print('Ready; use --apply.');return
    backup=ROOT/'.local-evidence/restoration-backups'/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ-blackbrush-npcs');backup.mkdir(parents=True,exist_ok=False)
    for table,where in [('gamedata_actor_class',f'id IN({actor_ids})'),('server_spawn_locations',f'actorClassId IN({actor_ids})'),('server_battlenpc_spawn_audit_pins','zoneId=170')]:
        data=subprocess.check_output(['mariadb-dump','--single-transaction','--no-create-info','--skip-add-locks','--skip-lock-tables','ffxiv_server',table,'--where='+where]);p=backup/(table+'.sql');p.write_bytes(data);p.chmod(0o600)
    sql='START TRANSACTION;\n'+raw.decode()+f"\nINSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('{name}','{sha}');\nCOMMIT;"
    subprocess.run(['mariadb','ffxiv_server'],input=sql,text=True,check=True)
    assert query(f'SELECT COUNT(*) FROM server_spawn_locations WHERE id IN({spawn_ids}) AND zoneId=170')==count
    print('Applied and verified; backup:',backup)
if __name__=='__main__':main()

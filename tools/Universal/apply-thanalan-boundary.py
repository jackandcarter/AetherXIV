#!/usr/bin/env python3
"""Apply only provisional boundary migration 45; never reinstall the database."""
import argparse, datetime, hashlib, re, subprocess
from pathlib import Path
ROOT=Path(__file__).resolve().parents[2]
NAME='20260919_000045_thanalan_west_central_boundary.sql'
def query(sql):
    return subprocess.check_output(['mariadb','-N','ffxiv_server','-e',sql],text=True).strip()
def main():
    ap=argparse.ArgumentParser(description=__doc__);ap.add_argument('--apply',action='store_true');a=ap.parse_args()
    procs=subprocess.check_output(['ps','-axo','comm='],text=True)
    if re.search(r'AetherXIV\.Core\.(Map|World|Lobby)$',procs,re.M):raise SystemExit('Stop stack first.')
    if query('SELECT COUNT(*) FROM server_sessions')!='0':raise SystemExit('Active sessions; refusing.')
    raw=(ROOT/'db/direct-core/migrations'/NAME).read_bytes();sha=hashlib.sha256(raw).hexdigest()
    old=query(f"SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='{NAME}'")
    if old:
        assert old==sha,'Ledger checksum differs'
        print('Already applied; checksum verified.');return
    assert query('SELECT COUNT(*) FROM server_zones WHERE id IN (170,172) AND regionId=104')=='2'
    print('Pending provisional boundary migration:',NAME)
    if not a.apply:return
    backup=ROOT/'.local-evidence/restoration-backups'/datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ-thanalan-boundary')
    backup.mkdir(parents=True,exist_ok=False)
    data=subprocess.check_output(['mariadb-dump','--single-transaction','--no-create-info','--skip-add-locks','--skip-lock-tables','ffxiv_server','server_seamless_zonechange_bounds'])
    target=backup/'server_seamless_zonechange_bounds.sql';target.write_bytes(data);target.chmod(0o600)
    (backup/'backup.sha256').write_text(hashlib.sha256(data).hexdigest()+'  '+target.name+'\n')
    sql='START TRANSACTION;\n'+raw.decode()+f"\nINSERT INTO aether_schema_migrations (migration_name,checksum_sha256) VALUES ('{NAME}','{sha}');\nCOMMIT;"
    subprocess.run(['mariadb','ffxiv_server'],input=sql,text=True,check=True)
    assert query('SELECT COUNT(*) FROM server_seamless_zonechange_bounds WHERE (zoneId1=172 AND zoneId2=170) OR (zoneId1=170 AND zoneId2=172)')=='1'
    print('Applied and verified. Backup:',backup)
if __name__=='__main__':main()

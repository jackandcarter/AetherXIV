#!/usr/bin/env python3
"""Back up, transaction-test, and apply the Camp Horizon screenshot placements."""
import argparse
import datetime
import hashlib
from pathlib import Path
import re
import subprocess

ROOT = Path(__file__).resolve().parents[2]
NAME = '20260919_000048_horizon_named_pin_spawns.sql'
ACTORS = '1600104,1500243,1500075,1500101,1500221,2210507,2210508,2210509,2102301,2102305,2100901,2104501,2100105,2100114,2104011'
TABLES = {
    'gamedata_actor_class': f'id IN ({ACTORS})',
    'server_spawn_locations': 'id BETWEEN 1080 AND 1087',
    'server_battlenpc_pools': 'poolId BETWEEN 1848000 AND 1848006',
    'server_battlenpc_groups': 'groupId BETWEEN 1848069 AND 1848110',
    'server_battlenpc_spawn_locations': 'bnpcId BETWEEN 1848069 AND 1848110',
    'server_battlenpc_spawn_audit_pins': 'pinId BETWEEN 69 AND 110',
}

def query(sql):
    return subprocess.check_output(['mariadb', '-N', 'ffxiv_server', '-e', sql], text=True).strip()

def execute(sql):
    return subprocess.check_output(['mariadb', '-N', 'ffxiv_server'], input=sql, text=True).strip()

def snapshot():
    return {table: query(f'SELECT * FROM {table} WHERE {where} ORDER BY 1') for table, where in TABLES.items()}

def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--apply', action='store_true')
    args = parser.parse_args()
    processes = subprocess.check_output(['ps', '-axo', 'comm='], text=True)
    if re.search(r'AetherXIV\.(Core\.(Map|World|Lobby)|UI\.App)', processes):
        raise SystemExit('Stop Core first.')
    if query('SELECT COUNT(*) FROM server_sessions') != '0':
        raise SystemExit('Active sessions; stop Core first.')
    raw = (ROOT / 'db/direct-core/migrations' / NAME).read_bytes()
    checksum = hashlib.sha256(raw).hexdigest()
    old = query(f"SELECT checksum_sha256 FROM aether_schema_migrations WHERE migration_name='{NAME}'")
    if old:
        if old != checksum:
            raise SystemExit('Applied migration checksum differs.')
        if query('SELECT COUNT(*) FROM server_spawn_locations WHERE id BETWEEN 1080 AND 1087') != '8' or query('SELECT COUNT(*) FROM server_battlenpc_spawn_locations WHERE bnpcId BETWEEN 1848069 AND 1848110') != '34':
            raise SystemExit('Migration is recorded but placements are missing; investigate before reapplying.')
        print('Already applied; ledger and placement counts match.')
        return
    for table, where in TABLES.items():
        if table not in ('gamedata_actor_class', 'server_battlenpc_spawn_audit_pins'):
            if query(f'SELECT COUNT(*) FROM {table} WHERE {where}') != '0':
                raise SystemExit(f'Reserved ID collision: {table}')
    if query(f'SELECT COUNT(*) FROM gamedata_actor_class a JOIN gamedata_actor_appearance p USING(id) WHERE a.id IN ({ACTORS})') != '15':
        raise SystemExit('Missing client identity/appearance.')
    for table in TABLES:
        if query(f"SELECT ENGINE FROM information_schema.TABLES WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table}'") != 'InnoDB':
            raise SystemExit(f'Nontransactional table: {table}')
    if not args.apply:
        print('Ready; use --apply to back up, test rollback/idempotency, and apply.')
        return
    backup = ROOT / '.local-evidence/restoration-backups' / datetime.datetime.now(datetime.timezone.utc).strftime('%Y%m%dT%H%M%SZ-horizon-pins')
    backup.mkdir(parents=True, exist_ok=False)
    for table, where in TABLES.items():
        columns = [row.split('\t') for row in query(
            f"SELECT COLUMN_NAME,DATA_TYPE FROM information_schema.COLUMNS WHERE TABLE_SCHEMA=DATABASE() AND TABLE_NAME='{table}' ORDER BY ORDINAL_POSITION").splitlines()]
        # Dump floats at full stored precision, with no implicit commits/DDL.
        values = []
        for column, data_type in columns:
            value = f'CAST(`{column}` AS DOUBLE)' if data_type == 'float' else f'`{column}`'
            values.append(f"IF(`{column}` IS NULL,'NULL',QUOTE({value}))")
        expression = ",',',".join(values)
        data = query(f"SELECT CONCAT('INSERT INTO `{table}` VALUES (',{expression},');') FROM `{table}` WHERE {where} ORDER BY 1")
        file = backup / (table + '.sql')
        file.write_text(data + '\n')
        file.chmod(0o600)
    undo = ['START TRANSACTION;']
    for table, where in reversed(list(TABLES.items())):
        undo.append(f'DELETE FROM {table} WHERE {where};')
        # Dump files restore original rows; execute via mariadb source in this transaction.
        undo.append((backup / (table + '.sql')).read_text())
    undo.extend([f"DELETE FROM aether_schema_migrations WHERE migration_name='{NAME}' AND checksum_sha256='{checksum}';", 'COMMIT;'])
    rollback = backup / 'rollback.sql'
    rollback.write_text('\n'.join(undo))
    rollback.chmod(0o600)
    before = snapshot()
    counts = "SELECT (SELECT COUNT(*) FROM server_spawn_locations WHERE id BETWEEN 1080 AND 1087),(SELECT COUNT(*) FROM server_battlenpc_spawn_locations WHERE bnpcId BETWEEN 1848069 AND 1848110),(SELECT COUNT(*) FROM server_battlenpc_spawn_audit_pins WHERE pinId BETWEEN 69 AND 110 AND isPromoted=1);"
    result = execute('START TRANSACTION;\n' + raw.decode() + '\n' + counts + '\n' + raw.decode() + '\n' + counts + '\nROLLBACK;')
    if result.splitlines() != ['8\t34\t42', '8\t34\t42'] or snapshot() != before:
        raise SystemExit('Rollback/idempotency verification failed; not applied.')
    result = execute('START TRANSACTION;\n' + raw.decode() + f"\nINSERT INTO aether_schema_migrations(migration_name,checksum_sha256) VALUES('{NAME}','{checksum}');\nCOMMIT;\n" + counts)
    if result != '8\t34\t42':
        raise SystemExit(f'Unexpected post-apply counts: {result}; backup: {backup}')
    print('Applied: 8 NPCs/chocobos, 34 enemies, 42 promoted pins. Rollback and repeat-run checks passed.')
    print('Backup:', backup)

if __name__ == '__main__':
    main()

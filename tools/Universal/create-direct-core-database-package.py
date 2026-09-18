#!/usr/bin/env python3
from __future__ import annotations

import argparse
import hashlib
import shutil
import subprocess
from pathlib import Path


LEGACY_MIGRATIONS = (
    "Data/sql/migrations/20260627_battlenpc_spawn_audit_pins.sql",
    "Data/sql/migrations/20260707_seed_level1_player_base_stats.sql",
)


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--repo-root", required=True, type=Path)
    parser.add_argument("--output-dir", required=True, type=Path)
    args = parser.parse_args()

    root = args.repo_root.resolve()
    output = args.output_dir.resolve()
    identity_catalog = root / "Data/seeds/actor-catalog/native-actor-slot-overrides.json"
    migration_generator = root / "tools/Universal/generate-native-actor-migration.py"
    for target, migration in (
        ("direct-core", root / "db/direct-core/migrations/20260727_000030_native_actor_slots.sql"),
        ("normalized", root / "Data/sql/migrations/20260727_native_actor_slots.sql"),
    ):
        subprocess.run(
            [
                "python3",
                str(migration_generator),
                "--catalog",
                str(identity_catalog),
                "--output",
                str(migration),
                "--target",
                target,
                "--verify",
            ],
            check=True,
        )
    if output.exists():
        shutil.rmtree(output)
    output.mkdir(parents=True, exist_ok=True)
    subprocess.run(
        [
            "python3",
            str(root / "tools/Universal/create-direct-core-baseline.py"),
            "--repo-root",
            str(root),
            "--output-dir",
            str(output),
        ],
        check=True,
    )

    baseline_history = root / "db/direct-core/baseline-history.sha256"
    if not baseline_history.is_file():
        raise SystemExit(f"Missing trusted baseline history: {baseline_history}")
    trusted_hashes = {
        line.split()[0].lower()
        for line in baseline_history.read_text(encoding="utf-8").splitlines()
        if line.strip() and not line.lstrip().startswith("#")
    }
    baseline_hash = hashlib.sha256((output / "ffxiv_server.sql").read_bytes()).hexdigest()
    if baseline_hash not in trusted_hashes:
        raise SystemExit(
            "Generated direct-core baseline is not registered in "
            f"{baseline_history}: {baseline_hash}"
        )
    shutil.copy2(baseline_history, output / baseline_history.name)

    migrations = output / "migrations"
    migrations.mkdir(exist_ok=True)
    direct_core_migrations = sorted(
        (root / "db/direct-core/migrations").glob("*.sql"),
        key=lambda path: path.name,
    )
    migration_sources = [root / relative for relative in LEGACY_MIGRATIONS]
    migration_sources.extend(direct_core_migrations)
    if not direct_core_migrations:
        raise SystemExit("No direct-core migrations were found to package.")

    packaged_names: set[str] = set()
    for source in migration_sources:
        if not source.is_file():
            raise SystemExit(f"Missing direct-core migration: {source}")
        if source.name in packaged_names:
            raise SystemExit(f"Duplicate packaged migration name: {source.name}")
        packaged_names.add(source.name)
        shutil.copy2(source, migrations / source.name)

    for name in ("setup.sh", "setup.ps1", "migration-history.sha256"):
        source = root / "db/direct-core" / name
        if not source.is_file():
            raise SystemExit(f"Missing database setup entry: {source}")
        destination = output / name
        shutil.copy2(source, destination)
        if name.endswith(".sh"):
            destination.chmod(0o755)

    support_tools = (
        (root / "tools/Universal/reset-account-characters.sh", "reset-account-characters.sh"),
        (root / "tools/Windows/reset-account-characters.ps1", "reset-account-characters.ps1"),
    )
    for source, destination_name in support_tools:
        if not source.is_file():
            raise SystemExit(f"Missing packaged support tool: {source}")
        destination = output / destination_name
        shutil.copy2(source, destination)
        if destination.suffix == ".sh":
            destination.chmod(0o755)

    print(f"Packaged direct-core database installer at {output}")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

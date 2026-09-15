#!/usr/bin/env python3
"""Generate database native-slot migrations from the reviewed identity catalog."""

from __future__ import annotations

import argparse
import hashlib
from pathlib import Path

import native_actor_slots


def sql_string(value: str) -> str:
    return "'" + value.replace("'", "''") + "'"


def case_lines(assignments: list[dict], id_column: str, slot_column: str) -> list[str]:
    lines = [f"SET {slot_column} = CASE {id_column}"]
    for assignment in sorted(assignments, key=lambda row: int(row["spawnId"])):
        lines.append(
            f"  WHEN {int(assignment['spawnId'])} "
            f"THEN {int(assignment['nativeActorSlot'])}"
        )
    lines.append(f"  ELSE {slot_column}")
    lines.append("END")
    return lines


def direct_core_sql(catalog: dict, source_hash: str) -> str:
    lines = [
        "-- Generated from Data/seeds/actor-catalog/native-actor-slot-overrides.json.",
        "-- Do not hand-edit native slot assignments in this migration.",
        f"-- source_sha256: {source_hash}",
        "",
        "-- Native 1.x actor slots are wire identities, not repository row numbers.",
        "ALTER TABLE `server_spawn_locations`",
        "  ADD COLUMN IF NOT EXISTS `nativeActorSlot` int(10) unsigned NULL AFTER `id`;",
    ]
    for territory in sorted(catalog["territories"], key=lambda row: int(row["territoryId"])):
        assignments = territory.get("staticActorAssignments", [])
        if not assignments:
            continue
        public_area = territory["publicArea"]
        spawn_ids = ",".join(
            str(int(row["spawnId"]))
            for row in sorted(assignments, key=lambda row: int(row["spawnId"]))
        )
        lines.extend(
            [
                "",
                "-- This territory is catalog-authoritative. Remove earlier guessed or",
                "-- row-order-derived assignments before applying the reviewed slots.",
                "UPDATE `server_spawn_locations`",
                "SET `nativeActorSlot` = NULL",
                f"WHERE `zoneId` = {int(territory['territoryId'])}",
                f"  AND `privateAreaName` = {sql_string(public_area.get('privateAreaName') or '')}",
                f"  AND `privateAreaLevel` = {int(public_area.get('privateAreaLevel', 0))};",
                "",
                "UPDATE `server_spawn_locations`",
                *case_lines(assignments, "`id`", "`nativeActorSlot`"),
                f"WHERE `zoneId` = {int(territory['territoryId'])}",
                f"  AND `privateAreaName` = {sql_string(public_area.get('privateAreaName') or '')}",
                f"  AND `privateAreaLevel` = {int(public_area.get('privateAreaLevel', 0))}",
                f"  AND `id` IN ({spawn_ids});",
            ]
        )
    lines.extend(
        [
            "",
            "ALTER TABLE `server_spawn_locations`",
            "  ADD UNIQUE KEY IF NOT EXISTS `uq_server_spawn_native_slot`",
            "    (`zoneId`,`privateAreaName`,`privateAreaLevel`,`nativeActorSlot`);",
        ]
    )
    return "\n".join(lines) + "\n"


def normalized_update(territory: dict) -> str:
    assignments = territory.get("staticActorAssignments", [])
    public_area = territory["publicArea"]
    spawn_ids = ",".join(
        str(int(row["spawnId"]))
        for row in sorted(assignments, key=lambda row: int(row["spawnId"]))
    )
    lines = [
        "UPDATE `static_actor_spawns`",
        *case_lines(assignments, "`spawn_id`", "`native_actor_slot`"),
        f"WHERE `zone_id` = {int(territory['territoryId'])}",
        "  AND COALESCE(`private_area_name`, '') = "
        + sql_string(public_area.get("privateAreaName") or ""),
        f"  AND `private_area_level` = {int(public_area.get('privateAreaLevel', 0))}",
        f"  AND `spawn_id` IN ({spawn_ids})",
    ]
    return "\n".join(lines)


def normalized_clear(territory: dict) -> str:
    public_area = territory["publicArea"]
    lines = [
        "UPDATE `static_actor_spawns`",
        "SET `native_actor_slot` = NULL",
        f"WHERE `zone_id` = {int(territory['territoryId'])}",
        "  AND COALESCE(`private_area_name`, '') = "
        + sql_string(public_area.get("privateAreaName") or ""),
        f"  AND `private_area_level` = {int(public_area.get('privateAreaLevel', 0))}",
    ]
    return "\n".join(lines)


def normalized_sql(catalog: dict, source_hash: str) -> str:
    lines = [
        "-- Generated from Data/seeds/actor-catalog/native-actor-slot-overrides.json.",
        "-- Do not hand-edit native slot assignments in this migration.",
        f"-- source_sha256: {source_hash}",
        "",
        "-- The guarded statements keep this migration safe for direct-core-only",
        "-- databases that do not contain the normalized repository tables.",
        "SET @has_static_actor_spawns = (",
        "  SELECT COUNT(*)",
        "  FROM information_schema.tables",
        "  WHERE table_schema = DATABASE()",
        "    AND table_name = 'static_actor_spawns'",
        ");",
        "",
        "SET @native_slot_alter = IF(",
        "  @has_static_actor_spawns = 1,",
        "  'ALTER TABLE `static_actor_spawns`",
        "     ADD COLUMN IF NOT EXISTS `native_actor_slot` int(10) unsigned NULL AFTER `spawn_id`,",
        "     ADD COLUMN IF NOT EXISTS `native_actor_scope` varchar(128)",
        "       AS (CONCAT(`zone_id`, '':'', COALESCE(`private_area_name`, ''''), '':'', `private_area_level`)) STORED',",
        "  'SELECT 1'",
        ");",
        "PREPARE native_slot_alter_statement FROM @native_slot_alter;",
        "EXECUTE native_slot_alter_statement;",
        "DEALLOCATE PREPARE native_slot_alter_statement;",
    ]
    for territory in sorted(catalog["territories"], key=lambda row: int(row["territoryId"])):
        if not territory.get("staticActorAssignments"):
            continue
        escaped_clear = normalized_clear(territory).replace("'", "''")
        escaped_update = normalized_update(territory).replace("'", "''")
        lines.extend(
            [
                "",
                "-- Clear earlier guessed or row-order-derived assignments in this",
                "-- catalog-authoritative scope before applying reviewed slots.",
                "SET @native_slot_clear = IF(",
                "  @has_static_actor_spawns = 1,",
                f"  '{escaped_clear}',",
                "  'SELECT 1'",
                ");",
                "PREPARE native_slot_clear_statement FROM @native_slot_clear;",
                "EXECUTE native_slot_clear_statement;",
                "DEALLOCATE PREPARE native_slot_clear_statement;",
                "",
                "SET @native_slot_update = IF(",
                "  @has_static_actor_spawns = 1,",
                f"  '{escaped_update}',",
                "  'SELECT 1'",
                ");",
                "PREPARE native_slot_update_statement FROM @native_slot_update;",
                "EXECUTE native_slot_update_statement;",
                "DEALLOCATE PREPARE native_slot_update_statement;",
            ]
        )
    lines.extend(
        [
            "",
            "SET @native_slot_index = IF(",
            "  @has_static_actor_spawns = 1",
            "    AND NOT EXISTS (",
            "      SELECT 1",
            "      FROM information_schema.statistics",
            "      WHERE table_schema = DATABASE()",
            "        AND table_name = 'static_actor_spawns'",
            "        AND index_name = 'uq_static_actor_native_slot'",
            "    ),",
            "  'ALTER TABLE `static_actor_spawns`",
            "     ADD UNIQUE KEY `uq_static_actor_native_slot` (`native_actor_scope`,`native_actor_slot`)',",
            "  'SELECT 1'",
            ");",
            "PREPARE native_slot_index_statement FROM @native_slot_index;",
            "EXECUTE native_slot_index_statement;",
            "DEALLOCATE PREPARE native_slot_index_statement;",
        ]
    )
    return "\n".join(lines) + "\n"


def main() -> int:
    parser = argparse.ArgumentParser()
    parser.add_argument("--catalog", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument(
        "--target",
        required=True,
        choices=("direct-core", "normalized"),
    )
    parser.add_argument("--verify", action="store_true")
    args = parser.parse_args()

    catalog_path = args.catalog.resolve()
    catalog = native_actor_slots.load(catalog_path)
    source_hash = hashlib.sha256(catalog_path.read_bytes()).hexdigest()
    content = (
        direct_core_sql(catalog, source_hash)
        if args.target == "direct-core"
        else normalized_sql(catalog, source_hash)
    )
    output = args.output.resolve()
    if args.verify:
        if not output.is_file() or output.read_text(encoding="utf-8") != content:
            raise SystemExit(
                f"{output} has drifted from {catalog_path}; regenerate the {args.target} migration"
            )
        print(f"Verified {output} against {catalog_path}.")
        return 0

    output.write_text(content, encoding="utf-8")
    print(f"Generated {output} from {catalog_path}.")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())

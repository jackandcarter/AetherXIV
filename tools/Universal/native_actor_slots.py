"""Validation and application of reviewed native actor identities."""

from __future__ import annotations

import json
from pathlib import Path


SCHEMA = "aetherxiv.native-actor-identities.v2"
MAXIMUM_NATIVE_SLOT = 0x7FFFF


def _native_slot(value: object, owner: str) -> int:
    native_slot = int(value)
    if native_slot <= 0 or native_slot > MAXIMUM_NATIVE_SLOT:
        raise ValueError(f"{owner} has invalid native actor slot {native_slot}")
    return native_slot


def _validate_territory(territory: dict) -> None:
    territory_id = int(territory["territoryId"])
    if territory_id <= 0 or territory_id > 0x1FF:
        raise ValueError(f"Invalid native actor territory {territory_id}")

    public_area = territory["publicArea"]
    area_master_slot = _native_slot(
        public_area["areaMasterNativeSlot"],
        f"Territory {territory_id} area master",
    )
    claimed_slots: dict[int, str] = {area_master_slot: "area master"}

    assignments = territory.get("staticActorAssignments", [])
    seen_spawn_ids: set[int] = set()
    for assignment in assignments:
        spawn_id = int(assignment["spawnId"])
        if spawn_id in seen_spawn_ids:
            raise ValueError(
                f"Territory {territory_id} contains duplicate static spawn {spawn_id}"
            )
        seen_spawn_ids.add(spawn_id)
        native_slot = _native_slot(
            assignment["nativeActorSlot"],
            f"Static spawn {spawn_id}",
        )
        if native_slot in claimed_slots:
            raise ValueError(
                f"Territory {territory_id} native slot {native_slot} is claimed by "
                f"static spawn {spawn_id} and {claimed_slots[native_slot]}"
            )
        claimed_slots[native_slot] = f"static spawn {spawn_id}"

    seen_scripts: set[str] = set()
    for resident in territory.get("residentDirectors", []):
        script_path = str(resident["scriptPath"]).strip()
        if not script_path or script_path in seen_scripts:
            raise ValueError(
                f"Territory {territory_id} has an empty or duplicate resident director script"
            )
        seen_scripts.add(script_path)
        native_slot = _native_slot(
            resident["nativeActorSlot"],
            f"Resident director {script_path}",
        )
        if native_slot in claimed_slots:
            raise ValueError(
                f"Territory {territory_id} native slot {native_slot} is claimed by "
                f"resident director {script_path} and {claimed_slots[native_slot]}"
            )
        claimed_slots[native_slot] = f"resident director {script_path}"
        if int(resident["nativeClassId"]) <= 0:
            raise ValueError(
                f"Resident director {script_path} has invalid native class ID"
            )
        native_class_path = str(resident["nativeClassPath"]).strip()
        if not native_class_path.startswith("/"):
            raise ValueError(
                f"Resident director {script_path} has invalid native class path"
            )

    assignments_by_spawn = {
        int(row["spawnId"]): int(row["nativeActorSlot"])
        for row in assignments
    }
    for pair in territory.get("pairOnlyEvidence", []):
        spawn_ids = [int(value) for value in pair.get("spawnIds", [])]
        native_slots = [int(value) for value in pair.get("nativeSlots", [])]
        if len(spawn_ids) < 2 or len(spawn_ids) != len(native_slots):
            raise ValueError(
                f"Territory {territory_id} has malformed pair-only evidence"
            )
        actual_slots = [assignments_by_spawn.get(spawn_id) for spawn_id in spawn_ids]
        if actual_slots != native_slots:
            raise ValueError(
                f"Territory {territory_id} pair-only evidence disagrees with assignments"
            )


def load(path: Path) -> dict:
    value = json.loads(path.read_text(encoding="utf-8"))
    if value.get("schema") != SCHEMA:
        raise ValueError(f"Unsupported native actor identity schema in {path}")
    territories = value.get("territories", [])
    if not territories:
        raise ValueError(f"Native actor identity catalog has no territories in {path}")
    territory_ids = [int(row["territoryId"]) for row in territories]
    if len(territory_ids) != len(set(territory_ids)):
        raise ValueError(f"Native actor identity catalog has duplicate territories in {path}")
    for territory in territories:
        _validate_territory(territory)
    return value


def apply(spawns: list[dict], catalog: dict) -> None:
    by_spawn_id = {int(row["SpawnId"]): row for row in spawns}
    for territory in catalog["territories"]:
        territory_id = int(territory["territoryId"])
        public_area = territory["publicArea"]
        private_area_name = public_area.get("privateAreaName") or ""
        private_area_level = int(public_area.get("privateAreaLevel", 0))
        source = territory["source"]
        pair_spawn_ids = {
            int(spawn_id)
            for pair in territory.get("pairOnlyEvidence", [])
            for spawn_id in pair.get("spawnIds", [])
        }

        for assignment in territory.get("staticActorAssignments", []):
            spawn_id = int(assignment["spawnId"])
            native_slot = int(assignment["nativeActorSlot"])
            assignment_source = assignment.get("source", source)
            capture = str(assignment_source["capture"])
            row = by_spawn_id.get(spawn_id)
            if row is None:
                raise ValueError(
                    f"Native actor identity catalog references missing spawn {spawn_id}"
                )
            row_scope = (
                int(row["ZoneId"]["Value"]),
                row.get("PrivateAreaName") or "",
                int(row.get("PrivateAreaLevel", 0)),
            )
            expected_scope = (territory_id, private_area_name, private_area_level)
            if row_scope != expected_scope:
                raise ValueError(
                    f"Native actor identity scope mismatch for spawn {spawn_id}: "
                    f"{row_scope} != {expected_scope}"
                )

            row["NativeActorSlot"] = native_slot
            provenance = row.setdefault("Provenance", {})
            if spawn_id in pair_spawn_ids:
                provenance["Notes"] = (
                    "Ported from legacy v1 server_spawn_locations. Official room-exit "
                    "trace confirms this indistinguishable pair's native slots; "
                    "repository order names the equivalent actors."
                )
            else:
                provenance["Notes"] = (
                    "Ported from legacy v1 server_spawn_locations. Native actor slot "
                    f"confirmed by {capture}."
                )

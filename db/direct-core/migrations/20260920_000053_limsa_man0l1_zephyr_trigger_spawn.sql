-- 20260920_000053_limsa_man0l1_zephyr_trigger_spawn.sql
--
-- Restore the physical Man0l1 SEQ_048 Zephyr Gate trigger. The actor class,
-- push condition, and quest callback already exist; the missing piece was the
-- static spawn that QuestState.ResolveQuestNpc can find after the player
-- enters zone 128.
--
-- Provenance:
--   Garlemald-Server commit 70e54f33fc4ea2473d6d82b46b6d0a4567fb2986,
--   common/sql/seed/056_man0l1_zephyr_escort.sql, reproduces the upstream
--   Project Meteor event-NPC row and records:
--     actorClassId 1090004
--     uniqueId      seafld0_push_limsa_entrance
--     zoneId        128
--     position      (-63.25, 33.15, 164.51), rotation 0
--   The local Legacy Meteor snapshot retains the actor class and appearance
--   but not this event-NPC spawn row. The recovered Limsa xE6 client NPC
--   layout is therefore not contradicted: this actor is on the zone-128 side
--   of the transition, not in Limsa zone 230.
--
-- The source row's original push radius is documented as 12.0 by the pinned
-- upstream restoration. AetherXIV's existing 1090004 class restoration uses
-- radius 25.0, which is retained here as an already-reviewed adaptation to
-- this stack's seamless landing geometry. This migration changes no actor
-- class, quest, boundary, or content logic.
--
-- Idempotent: preserve an existing reviewed row and do not allocate a
-- duplicate actor if the migration is run more than once.

START TRANSACTION;

INSERT INTO `server_spawn_locations`
(`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,
 `positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
SELECT
  1090004,
  'seafld0_push_limsa_entrance',
  128,
  '',
  0,
  -63.25,
  33.15,
  164.51,
  0,
  0,
  0,
  NULL
WHERE NOT EXISTS (
  SELECT 1
  FROM `server_spawn_locations`
  WHERE `actorClassId`=1090004
    AND `uniqueId`='seafld0_push_limsa_entrance'
    AND `zoneId`=128
    AND `privateAreaName`=''
    AND `privateAreaLevel`=0
);

COMMIT;

-- 20260913_000036_limsa_mini_aetherytes_and_man0l1_escort.sql
--
-- Two reviewed content restorations for the Limsa opening arc (Man0l1),
-- plus one payout bug carried in the same reviewed migration set.
--
-- ===================================================================
-- 1. Limsa city mini-aetherytes (class 1200288, zone 230)
-- ===================================================================
-- The city spawns its main aetheryte (1280001, spawn 731) but no
-- mini-aetherytes, so players cannot attune from the district
-- aetheryte plazas. The legacy v1 spawn dump never carried them (the
-- same gap exists in the project-meteor dump), but the surviving 1.0
-- client zone layout does:
--
--   The-Primal-Launcher
--   Resources/xml/zones/xE6/npc.xml  (xE6 = zone 230)
--     classId 1200288, PopulaceStandard entries:
--       (-427.9, 42,    367.9) r=2.47
--       (-526.5, 18,    168.6) r=0
--       (-622,   4,     375)   r=0
--       (-493,   44,    64.2)  r=0
--
-- All four carry only `noticeEvent priority=0 enabled=0` in the
-- layout, which is exactly the noticeEvent-only eventConditions shape
-- the existing 1200288 gamedata_actor_class row already holds. The
-- rotations are copied as-is; TPL's r field is the 1.0 spawn rotation
-- in the same convention as the rest of this table. Zone 128 (Camp
-- Bearded Rock) is unaffected: the main crystal 1280002 (spawn 732)
-- was already correct, and the TPL x80 layout places no 1200288 rows
-- there.
--
-- Spawn ids 1056-1059 are the next free ids after the highest
-- migration-assigned spawn row (1055). uniqueIds follow the existing
-- aetheryte naming convention (limsa_aetheryte, camp_beardedrock_...).
--
-- ===================================================================
-- 2. Man0l1 SEQ_048/050 Zephyr Gate escort duty ("Treasures of the
--    Main" — Garlemald-Server #46)
-- ===================================================================
-- Our Man0l1 inherits pmeteor's dead-code escort: startMan0l1Content
-- exists in the quest script but the SEQ_048 push handler carries the
-- upstream skip ("-- DO ESCORT DUTY HERE / For now just skip the
-- sequence"). Garlemald-Server reviewed and live-validated the full
-- duty against 1.23b recordings and packet captures (their migrations
-- 056/063/070/072/073 + SimpleContentMan0l101 /
-- QuestDirectorMan0l101); the Lua port lands in this commit alongside
-- this seed. The data this migration restores:
--
--   * 1090004  ZEPHYR_TRIGGER actor class — stripped ('', 0, 0, null)
--     in the legacy dump at line 2388. pmeteor's gamedata_actor_class
--     row (their sql:2395 per Garlemald 056) is PopulaceStandard,
--     propertyFlags 1, noticeEvent + a pushDefault circle. Radius
--     25.0 (not pmeteor's 12.0): Garlemald 063 packet-capture
--     forensics showed the 133->128 seamless crossing can drop the
--     player up to ~21.5 units from the trigger, so 12.0 only caught
--     some crossing paths. The circle ships disabled and is armed
--     only by the quest's SetENpc(ZEPHYR_TRIGGER, QFLAG_PUSH, ...)
--     at SEQ_048, and onPush still gates on the
--     contentsJoinAskInBasaClass yes/no prompt.
--   * 2290007  Sisipu — escort companion class, stripped in both
--     trees. displayNameId 1500024 resolves "Sisipu"; appearance row
--     2290007 exists. Class path borrows the proven melee-ally kit
--     (FighterAllyOpeningAttacker — same convention the tutorial
--     allies use) per Garlemald 056.
--   * 2205603  ankle biter — the ambush chigoe, stripped in both
--     trees. displayNameId 3205603 = "ankle biter"; appearance row
--     2205603 exists; ChigoeLesserStandard is the family model path
--     (carried by sibling 2105612) per Garlemald 056.
--   * 1290003  ContentPrivateAreaRange escort duty-halo — stripped
--     row filled with the retail ring shape per Garlemald 073: the
--     client attaches the native minimap range marker to actors whose
--     class script defines getMapMarkerRange(), radii resolved from
--     the wire-sent exit/caution push circles. The JSON copies sibling
--     row 1290002 (PrivateAreaPastExit) verbatim — the same shape
--     Garlemald 073 uses.
--   * server_battlenpc_pools/groups: pools 5/6 and groups 5/6 are the
--     next free ids (the tutorial rows occupy 1-4 and the Gridania
--     escort rows occupy 120/121/122, seeded by migration 000025).
--   * server_battlenpc_spawn_locations 25-33: Sisipu beside the
--     gate-side warp-in point (pmeteor's intended coordinates,
--     Garlemald 070) and the eight ankle biters at the eight single
--     on-trail ambush points decoded from the recorded player walk
--     (Garlemald 072 — each point is real walked ground, so the
--     ENGAGE_RADIUS activates exactly one biter per stretch).
--   * server_zones_privateareas row 12: zone 128
--     PrivateAreaMasterPast type 2 (the SEQ_055 lighthouse camp echo
--     the completion warp targets). dayMusic 40 = "The Echo", the
--     track every other PrivateAreaMasterPast echo row uses (Garlemald
--     073 part 2 — the row is new for us rather than an UPDATE
--     because our seed never carried a zone-128 PA at all).
--
-- ===================================================================
-- 3. Man0l1 SEQ_065 Fishermen's Guild payout
-- ===================================================================
-- Garlemald 056 also credits the 3,000 gil the upstream TODO left
-- unimplemented ("player:AddGil(3000)" — era guides: FFXIVenturer
-- "Treasures of the Main": "You will receive 3000 gil"). The Lua side
-- ports in this commit; nothing data-side was missing.

START TRANSACTION;

-- ---- 1. Limsa city mini-aetherytes (client layout xE6) ----
INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,`positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
VALUES
(1056,1200288,'miniaeth_limsa_districts',230,'',0,-427.9,42,367.9,2.47,0,0,NULL),
(1057,1200288,'miniaeth_limsa_hawl',230,'',0,-526.5,18,168.6,0,0,0,NULL),
(1058,1200288,'miniaeth_limsa_upper',230,'',0,-622,4,375,0,0,0,NULL),
(1059,1200288,'miniaeth_limsa_plaza',230,'',0,-493,44,64.2,0,0,0,NULL)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`uniqueId`=VALUES(`uniqueId`),`zoneId`=VALUES(`zoneId`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`);

-- ---- 2a. Escort actor classes (guarded: only claim stripped rows) ----
UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Populace/PopulaceStandard',
    `propertyFlags`=1,
    `eventConditions`='{"talkEventConditions":[],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[],"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":25.0,"secondaryRadius":25.0,"outwards":false,"silent":false,"isDisabled":true,"flags":0,"unknown2":0,"useSourceActorId":true}]}'
WHERE `id`=1090004 AND `classPath`='';

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Monster/Fighter/FighterAllyOpeningAttacker',
    `propertyFlags`=23,
    `eventConditions`='{"talkEventConditions":[],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[],"pushWithCircleEventConditions":[]}'
WHERE `id`=2290007 AND `classPath`='';

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Monster/Chigoe/ChigoeLesserStandard',
    `propertyFlags`=23,
    `eventConditions`='{"talkEventConditions":[],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[],"pushWithCircleEventConditions":[]}'
WHERE `id`=2205603 AND `classPath`='';

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Object/ContentPrivateAreaRange',
    `propertyFlags`=1,
    `eventConditions`='{"talkEventConditions":[],"noticeEventConditions":[],"emoteEventConditions":[],"pushWithCircleEventConditions":[{"conditionName":"exit","radius":60.0,"silent":true,"outwards":false},{"conditionName":"caution","radius":50.0,"silent":true,"outwards":false}]}'
WHERE `id`=1290003 AND `classPath`='';

-- ---- 2b. Escort BNPC pools/groups (ids 5/6; 1-4 and 120-122 taken) ----
INSERT INTO `server_battlenpc_pools`
(`poolId`,`actorClassId`,`name`,`genusId`,`currentJob`,`combatSkill`,`combatDelay`,`combatDmgMult`,`aggroType`,`immunity`,`linkType`,`spellListId`,`skillListId`)
VALUES
(5,2290007,'sisipu',29,2,1,4200,1,0,0,0,0,0),
(6,2205603,'ankle_biter',36,0,1,4200,1,0,0,0,0,0)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`name`=VALUES(`name`),`genusId`=VALUES(`genusId`),
`currentJob`=VALUES(`currentJob`),`combatSkill`=VALUES(`combatSkill`),`combatDelay`=VALUES(`combatDelay`),
`combatDmgMult`=VALUES(`combatDmgMult`),`aggroType`=VALUES(`aggroType`),`immunity`=VALUES(`immunity`),
`linkType`=VALUES(`linkType`),`spellListId`=VALUES(`spellListId`),`skillListId`=VALUES(`skillListId`);

INSERT INTO `server_battlenpc_groups`
(`groupId`,`poolId`,`scriptName`,`minLevel`,`maxLevel`,`respawnTime`,`hp`,`mp`,`dropListId`,`allegiance`,`spawnType`,`animationId`,`actorState`,`privateAreaName`,`privateAreaLevel`,`zoneId`)
VALUES
(5,5,'sisipu',1,1,0,800,0,0,1,1,0,0,'',0,128),
(6,6,'ankle_biter',5,7,0,60,0,0,0,1,0,0,'',0,128)
ON DUPLICATE KEY UPDATE
`poolId`=VALUES(`poolId`),`scriptName`=VALUES(`scriptName`),`minLevel`=VALUES(`minLevel`),
`maxLevel`=VALUES(`maxLevel`),`respawnTime`=VALUES(`respawnTime`),`hp`=VALUES(`hp`),`mp`=VALUES(`mp`),
`dropListId`=VALUES(`dropListId`),`allegiance`=VALUES(`allegiance`),`spawnType`=VALUES(`spawnType`),
`animationId`=VALUES(`animationId`),`actorState`=VALUES(`actorState`),`privateAreaName`=VALUES(`privateAreaName`),
`privateAreaLevel`=VALUES(`privateAreaLevel`),`zoneId`=VALUES(`zoneId`);

INSERT INTO `server_battlenpc_spawn_locations`
(`bnpcId`,`customDisplayName`,`groupId`,`positionX`,`positionY`,`positionZ`,`rotation`)
VALUES
(25,'sisipu',5,-49.0,36.43,162.0,2.2),
(26,'ankle_biter',6,112.30,47.20,148.17,-1.5),
(27,'ankle_biter',6,129.15,44.25,291.87,-1.5),
(28,'ankle_biter',6,69.96,41.22,449.76,-1.5),
(29,'ankle_biter',6,134.69,52.41,597.19,-1.5),
(30,'ankle_biter',6,74.46,61.65,741.17,-1.5),
(31,'ankle_biter',6,-2.35,53.43,878.18,-1.5),
(32,'ankle_biter',6,48.45,43.43,1038.45,-1.5),
(33,'ankle_biter',6,124.43,46.40,1205.66,-1.5)
ON DUPLICATE KEY UPDATE
`customDisplayName`=VALUES(`customDisplayName`),`groupId`=VALUES(`groupId`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`);

-- ---- 2c. SEQ_055 lighthouse camp echo private area (zone 128, type 2) ----
INSERT INTO `server_zones_privateareas`
(`id`,`parentZoneId`,`className`,`privateAreaName`,`privateAreaType`,`dayMusic`,`nightMusic`,`battleMusic`)
VALUES
(12,128,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',2,40,0,0)
ON DUPLICATE KEY UPDATE
`className`=VALUES(`className`),`privateAreaName`=VALUES(`privateAreaName`),
`privateAreaType`=VALUES(`privateAreaType`),`dayMusic`=VALUES(`dayMusic`),
`nightMusic`=VALUES(`nightMusic`),`battleMusic`=VALUES(`battleMusic`);

COMMIT;

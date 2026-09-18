-- Retail observations rechecked against raw same-frame init/position/name,
-- notice, circle and property packets. See NPC_QUEST_RESTORATION_AUDIT and
-- evidence/npc-quest-crossreference-2026-09-18/selected-trigger-raw-packets.json.
-- 1090067: gridania_to_coerthas.pcapng, stream 0/frame 660, SHA256
-- 63152a1ed6e446df5cc4750fe50a0839f9e45ad270c79abfc72b5c09cc47e547.
-- 1090068: moving_around_gridania.pcapng, stream 0/frame 999, SHA256
-- bf6abf7ccc7a16e071846036477c1fadbc3b46385ef6e747b543d836f3f51017.
-- Both publish only property[0]=1, no talk condition; 0x016F has unknown1=1,
-- flags=0, unknown2=3. Do not replace that word with the runtime actor ID.
-- Circles begin disabled by server policy; only the owning quest arms them.
-- West Shroud remains unarmed until its incomplete Echo transition is restored.
START TRANSACTION;

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Populace/PopulaceStandard', `propertyFlags`=1
WHERE `id` IN (1090067,1090068) AND (`classPath`='' OR `classPath` IS NULL);

UPDATE `gamedata_actor_class`
SET `eventConditions`='{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":9.0,"secondaryRadius":9.0,"unknown1":1,"useSourceActorId":false,"flags":0,"unknown2":3,"outwards":false,"silent":false,"isDisabled":true}]}'
WHERE `id`=1090067 AND (`eventConditions` IS NULL OR `eventConditions`='');

UPDATE `gamedata_actor_class`
SET `eventConditions`='{"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":4.0,"secondaryRadius":4.0,"unknown1":1,"useSourceActorId":false,"flags":0,"unknown2":3,"outwards":false,"silent":false,"isDisabled":true}]}'
WHERE `id`=1090068 AND (`eventConditions` IS NULL OR `eventConditions`='');

-- Preserve existing placements. An unrelated collision on a reserved ID fails
-- instead of overwriting another actor. No native actor slot is inferred.
INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,
 `positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
SELECT 1072,1090067,'man1g0_west_shroud_trigger',150,'',0,-642.010009765625,20.06999969482422,-1060.050048828125,-1.8799999952316284,0,0,NULL
WHERE NOT EXISTS (SELECT 1 FROM `server_spawn_locations` WHERE `actorClassId`=1090067 AND `zoneId`=150 AND `privateAreaName`='' AND `privateAreaLevel`=0);

INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,
 `positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
SELECT 1073,1090068,'man1g0_arc_inside_trigger',206,'',0,236.22000122070312,12.0,-1274.5699462890625,-0.9100000262260437,0,0,NULL
WHERE NOT EXISTS (SELECT 1 FROM `server_spawn_locations` WHERE `actorClassId`=1090068 AND `zoneId`=206 AND `privateAreaName`='' AND `privateAreaLevel`=0);

COMMIT;

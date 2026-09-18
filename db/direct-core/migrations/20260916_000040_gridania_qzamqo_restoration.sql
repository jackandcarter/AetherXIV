-- Restore Q'zamqo at Gridania's airship landing, not a guessed camp population.
-- Evidence: tests/fixtures/trace-evidence/qzamqo-restoration.json.
-- Retail moving_around_gridania.pcapng SHA-256:
-- bf6abf7ccc7a16e071846036477c1fadbc3b46385ef6e747b543d836f3f51017
-- TCP stream 0, capture frames 81 and 227 (decoded frames 30 and 97):
-- 0x00CC binds class 1001711 to PopulaceStandard in territory 155;
-- 0x013D binds display-name 1900211; 0x00CE repeats the same transform;
-- 0x0137 sets properties 0, 1 and 4; 0x012E/0x016B publish talk/notice.
-- Client build 2012.09.19.0001, DftFst script SHA-256:
-- cedf0db5f0eda2c78b76f5ef5dca61b7a747002cc049cd326d12cefd3a5e9e44
-- exports defaultTalkWithQZamqo_001, already mapped by dftfst.lua.
-- Existing appearance row 1001711 supplies the captured model and equipment.
-- No special airship behavior, quest state or native actor slot is inferred.

START TRANSACTION;

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Populace/PopulaceStandard',
    `displayNameId`=1900211,
    `propertyFlags`=19,
    `eventConditions`='{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'
WHERE `id`=1001711;

-- Preserve an existing installation's placement of this actor. If the reserved
-- migration ID is occupied by another actor, fail instead of overwriting it.
INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,
 `positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
SELECT 1071,1001711,'q_zamqo',155,'',0,38.89,-10,-1185.37,1.54,0,0,NULL
WHERE NOT EXISTS (
    SELECT 1 FROM `server_spawn_locations`
    WHERE `actorClassId`=1001711 AND `zoneId`=155
      AND `privateAreaName`='' AND `privateAreaLevel`=0
);

COMMIT;

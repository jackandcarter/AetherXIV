-- User-labeled pins 65-68, September 18 local time. Early-1.x screenshot
-- placements reconstructed by the user; names/appearances corroborated in
-- installed 1.23 client. Not claimed as retail-captured 1.23 coordinates.
-- Standard NPCs reuse DftWil dialogue; Ludovraint reuses CampMaster.
-- CampMaster favorite-destination persistence remains incomplete.
UPDATE gamedata_actor_class
SET classPath=IF(classPath='','/Chara/Npc/Populace/PopulaceStandard',classPath),
 propertyFlags=IF(propertyFlags=0,19,propertyFlags),
 eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),
 '{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}',eventConditions)
WHERE id IN(1001392,1000673,1000672);

INSERT INTO server_spawn_locations (id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation)
SELECT 1076,1001392,'nomomo',170,25.2832,200.003,-474.168,0.396746
WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations s JOIN gamedata_actor_class a ON a.id=s.actorClassId WHERE a.displayNameId=1500091 AND s.zoneId=170 AND s.privateAreaName='');
INSERT INTO server_spawn_locations (id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation)
SELECT 1077,1000673,'chechedoba',170,8.66542,199.988,-485.055,-1.27444
WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations s JOIN gamedata_actor_class a ON a.id=s.actorClassId WHERE a.displayNameId=1400032 AND s.zoneId=170 AND s.privateAreaName='');
INSERT INTO server_spawn_locations (id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation)
SELECT 1078,1500073,'ludovraint',170,23.2866,200.004,-491.95,1.05756
WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations s JOIN gamedata_actor_class a ON a.id=s.actorClassId WHERE a.displayNameId=1200106 AND s.zoneId=170 AND s.privateAreaName='');
INSERT INTO server_spawn_locations (id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation)
SELECT 1079,1000672,'blandhem',170,59.3306,199.742,-460.411,0.883558
WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations s JOIN gamedata_actor_class a ON a.id=s.actorClassId WHERE a.displayNameId=1600036 AND s.zoneId=170 AND s.privateAreaName='');

UPDATE server_battlenpc_spawn_audit_pins p
JOIN server_spawn_locations s ON s.zoneId=p.zoneId
 AND ABS(s.positionX-p.positionX)<0.001 AND ABS(s.positionY-p.positionY)<0.001
 AND ABS(s.positionZ-p.positionZ)<0.001
SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,
 p.promotionMigration='20260919_000047_blackbrush_identified_pin_npcs.sql',
 p.promotionNote='User-labeled placement; client identity confirmed; existing NPC event routing reused.'
WHERE p.isPromoted=0 AND
 ((p.enemyName='Nomomo' AND s.actorClassId=1001392)
 OR (p.enemyName='Chechedoba' AND s.actorClassId=1000673)
 OR (p.enemyName='Ludovraint' AND s.actorClassId=1500073)
 OR (p.enemyName IN('Blandem','Blandhem') AND s.actorClassId=1000672));

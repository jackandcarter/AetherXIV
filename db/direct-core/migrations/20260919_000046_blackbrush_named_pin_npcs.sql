-- User-surveyed placements from pins 61 and 64, backed by early-1.x screenshots.
-- 1.23 client confirms identities: Mimina 1500099/display1500060,
-- Benedict 1600062/display1000047. Locations are user-approved reconstructions,
-- not claimed to be independently captured 1.23 positions.
-- Reuse existing camp-service and vendor scripts; do not duplicate shop logic.
-- Unnamed pins 62/63 and early-version marmots are deliberately NOT promoted.

UPDATE gamedata_actor_class
SET propertyFlags=IF(propertyFlags=0,19,propertyFlags),
 eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),
 '{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}',eventConditions)
WHERE id IN (1500099,1600062);

INSERT INTO server_spawn_locations
 (id,actorClassId,uniqueId,zoneId,privateAreaName,privateAreaLevel,positionX,positionY,positionZ,rotation)
SELECT 1074,1500099,'mimina',170,'',0,24.6016,200.004,-471.42,-2.46468
WHERE NOT EXISTS (SELECT 1 FROM server_spawn_locations s JOIN gamedata_actor_class a ON a.id=s.actorClassId WHERE a.displayNameId=1500060 AND s.zoneId=170 AND s.privateAreaName='');

INSERT INTO server_spawn_locations
 (id,actorClassId,uniqueId,zoneId,privateAreaName,privateAreaLevel,positionX,positionY,positionZ,rotation)
SELECT 1075,1600062,'benedict',170,'',0,44.8892,200.035,-460.752,3.03794
WHERE NOT EXISTS (SELECT 1 FROM server_spawn_locations s JOIN gamedata_actor_class a ON a.id=s.actorClassId WHERE a.displayNameId=1000047 AND s.zoneId=170 AND s.privateAreaName='');

-- Pin IDs are local, so associate only matching names and transforms. Do not
-- report promotion if a different preexisting placement suppressed insertion.
UPDATE server_battlenpc_spawn_audit_pins p
JOIN server_spawn_locations s ON s.zoneId=p.zoneId
 AND ABS(s.positionX-p.positionX)<0.001 AND ABS(s.positionY-p.positionY)<0.001
 AND ABS(s.positionZ-p.positionZ)<0.001
SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,
 p.promotionMigration='20260919_000046_blackbrush_named_pin_npcs.sql',
 p.promotionNote='User-surveyed early-1.x placement; identity confirmed in 1.23 client; existing service script reused.'
WHERE p.isPromoted=0 AND ((p.enemyName='Mimina' AND s.actorClassId=1500099) OR (p.enemyName='Benedict' AND s.actorClassId=1600062));

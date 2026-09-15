-- Keep the normalized-data migration path aligned with the direct-core
-- private-area repair. The canonical legacy row uses market instance 102.

START TRANSACTION;

UPDATE `server_spawn_locations`
SET `privateAreaLevel`=102
WHERE `id`=958
  AND `actorClassId`=1500428
  AND `uniqueId`='repairman'
  AND `zoneId`=180
  AND `privateAreaName`='PrivateAreaMasterMarket'
  AND `privateAreaLevel`=0;

COMMIT;

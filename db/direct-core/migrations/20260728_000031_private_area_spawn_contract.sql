-- Restore the canonical market-private-area scope for the reviewed repair NPC.
-- Migration 000008 accidentally replaced the legacy private-area type (102)
-- with the public-area sentinel (0), so Map loaded the row but could not
-- attach it to PrivateAreaMasterMarket.

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

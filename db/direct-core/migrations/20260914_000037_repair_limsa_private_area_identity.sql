-- Repair the private-area primary-key collision introduced by 000036.
--
-- Migration 000021 already owns row 12 for Gridania zone 155, type 3.
-- 000036 accidentally upserted Limsa zone 128, type 2 through that same
-- primary key, leaving Gridania's static scene actor orphaned. Keep 000036
-- immutable because its checksum may already be recorded; restore its
-- original owner and use the next unallocated id for the Limsa area.
START TRANSACTION;

INSERT INTO `server_zones_privateareas`
(`id`,`parentZoneId`,`className`,`privateAreaName`,`privateAreaType`,`dayMusic`,`nightMusic`,`battleMusic`)
VALUES
(12,155,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',3,51,0,0),
(16,128,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',2,40,0,0)
ON DUPLICATE KEY UPDATE
`parentZoneId`=VALUES(`parentZoneId`),`className`=VALUES(`className`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaType`=VALUES(`privateAreaType`),
`dayMusic`=VALUES(`dayMusic`),`nightMusic`=VALUES(`nightMusic`),`battleMusic`=VALUES(`battleMusic`);

COMMIT;

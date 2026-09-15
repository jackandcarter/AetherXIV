-- Restore the static private area entered after Man0l1 SEQ_007's second
-- Isandorel cutscene. The quest has always requested zone 230,
-- PrivateAreaMasterPast type 3, but the legacy catalog carried no matching
-- registration or cast. Map therefore handed the request to World, whose
-- legacy handoff packet cannot represent a private-area name/type, leaving
-- the client at Now Loading.
--
-- Provenance: Garlemald-Server develop, common/sql/seed/
-- 058_restore_private_area_registrations.sql and
-- 059_restore_private_area_spawns.sql (their source notes identify the
-- Project Meteor server private-area records). The rows below are limited to
-- the Man0l1 Musketeers' Guild echo reached by this exact quest branch.
--
-- The explicit ids are the next free ids after migrations 000036/000037 and
-- make an interrupted local migration converge on retry.
START TRANSACTION;

INSERT INTO `server_zones_privateareas`
(`id`,`parentZoneId`,`className`,`privateAreaName`,`privateAreaType`,`dayMusic`,`nightMusic`,`battleMusic`)
VALUES
(17,230,'/Area/PrivateArea/PrivateAreaMasterPast','PrivateAreaMasterPast',3,40,0,0)
ON DUPLICATE KEY UPDATE
`parentZoneId`=VALUES(`parentZoneId`),`className`=VALUES(`className`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaType`=VALUES(`privateAreaType`),
`dayMusic`=VALUES(`dayMusic`),`nightMusic`=VALUES(`nightMusic`),`battleMusic`=VALUES(`battleMusic`);

INSERT INTO `server_spawn_locations`
(`id`,`actorClassId`,`uniqueId`,`zoneId`,`privateAreaName`,`privateAreaLevel`,`positionX`,`positionY`,`positionZ`,`rotation`,`actorState`,`animationId`,`customDisplayName`)
VALUES
(1060,1000869,'man0l1_adventurer1',230,'PrivateAreaMasterPast',3,-602.241,43,-62.3205,0.75078,0,1022,NULL),
(1061,1000870,'man0l1_adventurer2',230,'PrivateAreaMasterPast',3,-602.516,43,-61.557,1.206,0,1022,NULL),
(1062,1000871,'man0l1_adventurer3',230,'PrivateAreaMasterPast',3,-612.158,43,-60.8377,-2.89358,0,1022,NULL),
(1063,1000142,'man0l1_mannskoen',230,'PrivateAreaMasterPast',3,-626.34,43.631,-63.504,1.07,0,1015,NULL),
(1064,1000107,'man0l1_overeager_barracuda',230,'PrivateAreaMasterPast',3,-605.868,43,-75.147,-0.715,0,0,NULL),
(1065,1000108,'man0l1_sophisticated_barracuda',230,'PrivateAreaMasterPast',3,-619.348,43.631,-77.254,0.965,0,1015,NULL),
(1066,1000161,'man0l1_totoruto',230,'PrivateAreaMasterPast',3,-603.41,43,-70.45,-1.21,0,1026,NULL),
(1067,1000109,'man0l1_smirking_barracuda',230,'PrivateAreaMasterPast',3,-612.586,43,-78.737,-0.175,0,1002,NULL),
(1068,1000096,'man0l1_nervous_barracuda',230,'PrivateAreaMasterPast',3,-617.429,43,-54.065,2.953,0,1003,NULL),
(1069,1000097,'man0l1_intimidating_barracuda',230,'PrivateAreaMasterPast',3,-623.407,43,-56.981,2.415,0,1003,NULL),
(1070,1090003,'man0l1_push_exit_echo',230,'PrivateAreaMasterPast',3,-598.37,42.1,-50.28,0.61,0,0,NULL)
ON DUPLICATE KEY UPDATE
`actorClassId`=VALUES(`actorClassId`),`uniqueId`=VALUES(`uniqueId`),`zoneId`=VALUES(`zoneId`),
`privateAreaName`=VALUES(`privateAreaName`),`privateAreaLevel`=VALUES(`privateAreaLevel`),
`positionX`=VALUES(`positionX`),`positionY`=VALUES(`positionY`),`positionZ`=VALUES(`positionZ`),
`rotation`=VALUES(`rotation`),`actorState`=VALUES(`actorState`),`animationId`=VALUES(`animationId`),
`customDisplayName`=VALUES(`customDisplayName`);

COMMIT;

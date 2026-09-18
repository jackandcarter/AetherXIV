-- Existing Man0l1 Musketeers' Guild Echo placements from 000038.
-- No coordinates, private-area ownership, appearances or public actors change.
-- Class/name/flags: Garlemald-Server 70e54f33fc4ea2473d6d82b46b6d0a4567fb2986
-- common/sql/seed/061_restore_private_area_npc_classpaths.sql.
-- Talk/notice: same revision, 060_restore_man0l1_npc_event_conditions.sql.
-- All nine display IDs independently match the installed 1.23 client.
-- These are upstream-restored definitions, not newly captured retail actors.
START TRANSACTION;

UPDATE `gamedata_actor_class`
SET `classPath`='/Chara/Npc/Populace/PopulaceStandard', `propertyFlags`=19
WHERE `id` IN (1000096,1000097,1000107,1000108,1000109,1000142,1000869,1000870,1000871)
  AND (`classPath`='' OR `classPath` IS NULL);

-- Preserve installations with explicitly customized interaction definitions.
UPDATE `gamedata_actor_class`
SET `eventConditions`='{"talkEventConditions":[{"unknown1":4,"isDisabled":false,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'
WHERE `id` IN (1000096,1000097,1000107,1000108,1000109,1000142,1000869,1000870,1000871)
  AND (`eventConditions` IS NULL OR `eventConditions`='');

COMMIT;

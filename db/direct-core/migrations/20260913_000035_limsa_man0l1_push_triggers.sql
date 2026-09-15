-- 20260913_000035_limsa_man0l1_push_triggers.sql
--
-- Restore the missing pushWithCircleEventConditions on the quest "walk into
-- the trigger" actor classes used by the Limsa opening (Man0l1) and Man1l0.
-- The legacy v1 gamedata_actor_class seed (and the project-meteor-server
-- dump it was ported from) carries only talkDefault + noticeEvent for these
-- classes, so a quest SetENpc(<TRIGGER>, QFLAG_PUSH, false, true) publishes
-- the quest "!" graphic but never sends the 0x016F
-- SetPushEventConditionWithCircle geometry packet. The client streams the
-- actor with no push volume to test the player against, so onPush can never
-- fire. Symptom: Man0l1 SEQ_007 Musketeers' Guild "go downstairs" beat —
-- the marker shows but walking into it does nothing.
--
-- Values: conditionName pushDefault, radius 6.0, non-outwards, non-silent,
-- shipped disabled until the owning quest arms it. Copied from the reviewed
-- Garlemald-Server seed migration
-- common/sql/seed/057_fix_push_trigger_event_conditions.sql
-- (github.com/swstegall/Garlemald-Server, develop), whose values were
-- validated against a live 1.23b opening playthrough (Garlemald-Server #46
-- documents the identical symptom for Man0l1 SEQ_007). The radius sits in
-- the same band as the surviving 1.0 client-layout walk-in triggers
-- (The-Primal-Launcher Resources/xml/zones ZoneSwitch pushDefault circles:
-- radius 5-8 in zone x80/xE6).
--
-- Disabled-by-default is safe here and matches the quest contract: the
-- map-server quest ENPC presentation path (Session.SpawnStaticActor /
-- UpdateQuestNpcInInstance) overrides the push SetEventStatus state from
-- each player's quest overlay (quest:SetENpc(..., QFLAG_PUSH, false, true)),
-- so the circle only becomes live for players actually holding the matching
-- quest beat. Players without the quest receive the generic status pass,
-- where isDisabled=true keeps the trigger inert.
--
-- 1090004 (ZEPHYR_TRIGGER, SEQ_048 escort) is intentionally NOT restored
-- here: it has no reviewed spawn row in any source tree, and its radius
-- depends on seamless-boundary crossing geometry that has not been captured
-- for this stack. It needs its own content migration.
--
-- Idempotent: re-running the UPDATEs is a no-op once the rows hold the JSON.

START TRANSACTION;

-- 1090001  MSK_TRIGGER       (Man0l1 SEQ_007 Musketeers' Guild, zone 230 spawn 444)
-- 1090003  ECHO_EXIT_TRIGGER (Man0l1 SEQ_007 echo exit, zone 230 spawn 446)
-- 1090006  FSH_TRIGGER       (Man0l1 SEQ_065 / Man1l0 FSH guild, zone 230 spawn 445)
-- 1090007  ECHO_EXIT_TRIGGER2(Man0l1 SEQ_085 echo exit, zone 230 spawn 463)
UPDATE `gamedata_actor_class`
SET `eventConditions`='{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}],"emoteEventConditions":[],"pushWithCircleEventConditions":[{"conditionName":"pushDefault","radius":6.0,"secondaryRadius":6.0,"outwards":false,"silent":false,"isDisabled":true,"flags":0,"unknown2":0,"useSourceActorId":true}]}'
WHERE `id` IN (1090001, 1090003, 1090006, 1090007);

COMMIT;

-- Restore the caravan guide's supported referral dialogue and talkability.
UPDATE gamedata_actor_class
SET classPath='/Chara/Npc/Populace/PopulaceCaravanGuide',
 eventConditions='{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'
WHERE id=1500221 AND classPath='/Chara/Npc/Populace/PopulaceStandard'
 AND (eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'));

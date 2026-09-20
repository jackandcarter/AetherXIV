-- Wistsunn already has a supported ShopSalesman entry (281 / pack 3008).
-- Restore the missing talk/notice conditions; preserve any existing events.
UPDATE gamedata_actor_class
SET eventConditions='{"talkEventConditions":[{"unknown1":4,"unknown2":0,"conditionName":"talkDefault"}],"noticeEventConditions":[{"unknown1":0,"unknown2":1,"conditionName":"noticeEvent"}]}'
WHERE id=1600104
 AND classPath='/Chara/Npc/Populace/Shop/PopulaceShopSalesman'
 AND (eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'));

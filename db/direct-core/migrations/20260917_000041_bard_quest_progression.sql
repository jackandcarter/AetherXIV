-- Bard prerequisites: official 1.21 chain; client quest sheet column 51 and
-- Brd0j1-6 processEventChuui delegates corroborate levels/classes.
-- Evidence: evidence/bard-quests-2026-09-17/quest.json and client/*.txt.
-- Existing quest records and existing runtime policy; no new schema.
START TRANSACTION;
UPDATE gamedata_quests SET minLevel=30, prerequisite=0 WHERE id=111301 AND className='Brd0j1';
UPDATE gamedata_quests SET minLevel=35, prerequisite=111301 WHERE id=111302 AND className='Brd0j2';
UPDATE gamedata_quests SET minLevel=40, prerequisite=111302 WHERE id=111303 AND className='Brd0j3';
UPDATE gamedata_quests SET minLevel=45, prerequisite=111303 WHERE id=111304 AND className='Brd0j4';
UPDATE gamedata_quests SET minLevel=45, prerequisite=111304 WHERE id=111305 AND className='Brd0j5';
UPDATE gamedata_quests SET minLevel=50, prerequisite=111305 WHERE id=111306 AND className='Brd0j6';
COMMIT;

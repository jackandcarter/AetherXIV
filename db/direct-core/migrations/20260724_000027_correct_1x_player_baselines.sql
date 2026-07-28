-- Correct the earlier ARR-era level 1 values to the archived FFXIV 1.x clan
-- baselines. Contemporary and surviving references agree that every clan
-- totals 90 points and each individual attribute lies between 12 and 18.
-- Retail 1.23b tribe ids 1-15 carry clan and sex; paired sex ids share the
-- same clan baseline.

DELETE FROM server_player_base_stats
WHERE level = 1
  AND tribe BETWEEN 16 AND 19
  AND source = 'user-provided 1.23 race baseline notes';

INSERT INTO server_player_base_stats
  (classId, tribe, level, hp, mp, str, vit, dex, `int`, mnd, pie, source, sourceConfidence)
SELECT
  playable.classId,
  baseline.tribe,
  1,
  0,
  0,
  baseline.str,
  baseline.vit,
  baseline.dex,
  baseline.`int`,
  baseline.mnd,
  baseline.pie,
  'archived 1.x clan baseline consensus (90 total; 12-18 range)',
  'public-confirmed'
FROM (
  SELECT 2 AS classId UNION ALL
  SELECT 3 UNION ALL
  SELECT 4 UNION ALL
  SELECT 7 UNION ALL
  SELECT 8 UNION ALL
  SELECT 22 UNION ALL
  SELECT 23 UNION ALL
  SELECT 29 UNION ALL
  SELECT 30 UNION ALL
  SELECT 31 UNION ALL
  SELECT 32 UNION ALL
  SELECT 33 UNION ALL
  SELECT 34 UNION ALL
  SELECT 35 UNION ALL
  SELECT 36 UNION ALL
  SELECT 39 UNION ALL
  SELECT 40 UNION ALL
  SELECT 41
) AS playable
JOIN (
  SELECT 1 AS tribe, 16 AS str, 15 AS vit, 14 AS dex, 16 AS `int`, 13 AS mnd, 16 AS pie UNION ALL
  SELECT 2, 16, 15, 14, 16, 13, 16 UNION ALL
  SELECT 3, 18, 17, 15, 13, 15, 12 UNION ALL
  SELECT 4, 14, 13, 18, 17, 12, 16 UNION ALL
  SELECT 5, 14, 13, 18, 17, 12, 16 UNION ALL
  SELECT 6, 15, 14, 15, 18, 15, 13 UNION ALL
  SELECT 7, 15, 14, 15, 18, 15, 13 UNION ALL
  SELECT 8, 13, 13, 17, 16, 15, 16 UNION ALL
  SELECT 9, 13, 13, 17, 16, 15, 16 UNION ALL
  SELECT 10, 12, 12, 15, 16, 17, 18 UNION ALL
  SELECT 11, 12, 12, 15, 16, 17, 18 UNION ALL
  SELECT 12, 16, 15, 17, 13, 14, 15 UNION ALL
  SELECT 13, 13, 12, 16, 14, 18, 17 UNION ALL
  SELECT 14, 17, 18, 13, 12, 16, 14 UNION ALL
  SELECT 15, 15, 16, 12, 15, 16, 16
) AS baseline
WHERE 1 = 1
ON DUPLICATE KEY UPDATE
  hp = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.hp, VALUES(hp)),
  mp = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.mp, VALUES(mp)),
  str = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.str, VALUES(str)),
  vit = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.vit, VALUES(vit)),
  dex = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.dex, VALUES(dex)),
  `int` = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.`int`, VALUES(`int`)),
  mnd = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.mnd, VALUES(mnd)),
  pie = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.pie, VALUES(pie)),
  source = IF(server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'), server_player_base_stats.source, VALUES(source)),
  sourceConfidence = IF(
    server_player_base_stats.sourceConfidence IN ('client-confirmed', 'trace-confirmed'),
    server_player_base_stats.sourceConfidence,
    VALUES(sourceConfidence)
  );

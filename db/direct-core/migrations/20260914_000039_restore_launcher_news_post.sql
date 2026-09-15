-- 20260914_000039_restore_launcher_news_post.sql
-- Seed the reviewed AetherXIV 2.1 release post and remove prior placeholders.

START TRANSACTION;

DELETE FROM `launcher_news`
WHERE `title` IN ('Echo Gate service installed', 'AetherXIV 2.0 local stack', 'AetherXIV 2.1 local stack', 'AetherXIV 2.1 Update');

SET @has_launcher_news_v13 := (
  SELECT COUNT(*)
  FROM information_schema.tables
  WHERE table_schema = DATABASE()
    AND table_name = 'launcher_news_v13'
);
SET @sql := IF(
  @has_launcher_news_v13 = 1,
  'DELETE FROM `launcher_news_v13` WHERE `title` = ''Echo Gate service installed''',
  'DO 0'
);
PREPARE stmt FROM @sql;
EXECUTE stmt;
DEALLOCATE PREPARE stmt;

INSERT INTO `launcher_news` (
  `title`, `summary`, `body`, `banner_url`, `link_url`, `published_at`,
  `is_active`, `sort_order`, `title_color`, `summary_color`, `body_color`,
  `created_at`
) VALUES (
  'AetherXIV 2.1 Update',
  'Update Complete',
  'This update includes many new changes, quality of life updates, and gameplay restorations.\n\nSome of the new updates include:\n\n- Restored ambient zone enemies in the shroud and other areas.\n\n- Quest Progression restoration in Limsa, Uldah, and Gridania.\n\n- Auto attack and spell casting restorations.\n\n- Equipment changes and Class changing recalculations.\n\n- Crafting system restorations.\n\nFor more detailed info on these changes check out the release notes in Discord, or on Github.',
  NULL,
  NULL,
  '2026-09-15 01:28:39',
  1,
  0,
  '#8FC9FF',
  '#FFD37A',
  '#D2A8FF',
  '2026-09-14 20:37:20'
);

COMMIT;

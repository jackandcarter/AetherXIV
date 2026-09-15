-- Restore the last known launcher post for normalized/legacy-schema installs.

START TRANSACTION;

DELETE FROM `launcher_news`
WHERE `title` IN ('Echo Gate service installed', 'AetherXIV 2.0 local stack', 'AetherXIV 2.1 local stack', 'AetherXIV 2.1 Update');

INSERT INTO `launcher_news` (
  `title`, `summary`, `body`, `banner_url`, `link_url`, `published_at`,
  `is_published`, `sort_order`
) VALUES (
  'AetherXIV 2.1 Update',
  'Update Complete',
  'This update includes many new changes, quality of life updates, and gameplay restorations.\n\nSome of the new updates include:\n\n- Restored ambient zone enemies in the shroud and other areas.\n\n- Quest Progression restoration in Limsa, Uldah, and Gridania.\n\n- Auto attack and spell casting restorations.\n\n- Equipment changes and Class changing recalculations.\n\n- Crafting system restorations.\n\nFor more detailed info on these changes check out the release notes in Discord, or on Github.',
  NULL,
  NULL,
  '2026-09-15 01:28:39',
  1,
  0
);

COMMIT;

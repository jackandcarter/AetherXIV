-- Adds the mob-skill list table and column so monsters can carry special
-- attacks that are neither standard weapon skills nor spells.
CREATE TABLE IF NOT EXISTS `server_battlenpc_mob_skill_list` (
    `mobSkillListId` int(10) unsigned NOT NULL DEFAULT 0,
    `skillId` int(10) unsigned NOT NULL DEFAULT 0,
    PRIMARY KEY (`mobSkillListId`, `skillId`)
) ENGINE=InnoDB DEFAULT CHARSET=utf8;

ALTER TABLE `server_battlenpc_pools`
    ADD COLUMN IF NOT EXISTS `mobSkillListId` int(10) unsigned NOT NULL DEFAULT 0
    AFTER `spellListId`;
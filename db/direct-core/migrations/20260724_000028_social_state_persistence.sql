-- The legacy tables already hold blacklist and friend-list state, but the
-- blacklist primary key historically allowed only one row per character.
-- Preserve the existing tables and data while making their slot columns the
-- stable per-character ordering used by the retail paged responses.

ALTER TABLE `characters_blacklist`
  DROP PRIMARY KEY,
  ADD PRIMARY KEY (`characterId`, `slot`),
  ADD INDEX `ix_characters_blacklist_character_name` (`characterId`, `name`(32));

ALTER TABLE `characters_friendlist`
  ADD INDEX `ix_characters_friendlist_character_slot` (`characterId`, `slot`),
  ADD INDEX `ix_characters_friendlist_character_name` (`characterId`, `name`(32));

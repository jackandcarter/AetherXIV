-- Attuned-aetheryte persistence (Garlemald parity — characters_aetherytes,
-- migration 068). HasAetheryteNodeUnlocked / UnlockAetheryteNode were
-- in-memory only, so a relog forgot every attunement and the aetheryte
-- menu's child gates re-locked.

CREATE TABLE IF NOT EXISTS `characters_aetherytes` (
  `characterId` int(10) unsigned NOT NULL,
  `aetheryteId` int(10) unsigned NOT NULL,
  PRIMARY KEY (`characterId`, `aetheryteId`),
  CONSTRAINT `FK_characters_aetherytes_characters` FOREIGN KEY (`characterId`) REFERENCES `characters` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

-- Back-fill each existing character's non-zero homepoint so pre-migration
-- characters keep the one attunement the old in-memory path recorded.
INSERT IGNORE INTO `characters_aetherytes` (`characterId`, `aetheryteId`)
SELECT `id`, `homepoint` FROM `characters` WHERE `homepoint` <> 0;

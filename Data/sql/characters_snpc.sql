CREATE TABLE IF NOT EXISTS `characters_snpc` (
  `characterId` int(10) unsigned NOT NULL,
  `nickname` varchar(50) NOT NULL DEFAULT '???',
  `skin` tinyint(3) unsigned NOT NULL DEFAULT '1',
  `personality` tinyint(3) unsigned NOT NULL DEFAULT '1',
  `coordinate` smallint(5) unsigned NOT NULL DEFAULT '1',
  PRIMARY KEY (`characterId`),
  CONSTRAINT `FK_characters_snpc_characters` FOREIGN KEY (`characterId`) REFERENCES `characters` (`id`)
) ENGINE=InnoDB DEFAULT CHARSET=latin1;

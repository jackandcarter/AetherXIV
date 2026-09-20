-- User screenshot pin placements in Western Thanalan; client 1.23b name/appearance identities.

-- Missing levels: provisional aldgoats 30, cactuars 28-29. maxLevel is exclusive.

-- Generic family behavior and existing combat defaults; no recovered special attacks or loot.

-- Pyrausta level 36, HP 16335, MP 676, respawn ~300 seconds from contemporary guide:

-- https://forum.square-enix.com/ffxiv/threads/31475-Kai-Crystallis-Presents-Notorious-Monster-Guide

-- NPCs with missing event definitions are placement-only; existing camp/vendor behavior is preserved.

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Populace/Shop/PopulaceShopSalesman',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=1600104;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Populace/PopulaceStandard',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=1500243;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Populace/PopulaceCampMaster',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=1500075;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Populace/PopulaceCampSubMaster',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=1500101;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Populace/PopulaceStandard',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=1500221;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Chocobo/ChocoboCaravanGuard',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=2210507;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Chocobo/ChocoboCaravanGuard',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=2210508;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Chocobo/ChocoboCaravanGuard',classPath),propertyFlags=IF(propertyFlags=0,19,propertyFlags) WHERE id=2210509;

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Yak/YakMaleStandard',classPath),propertyFlags=IF(propertyFlags=0,23,propertyFlags),eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),'{"talkEventConditions": [], "noticeEventConditions": [{"unknown1": 0, "unknown2": 1, "conditionName": "noticeEvent"}]}',eventConditions) WHERE id=2102301;

INSERT INTO server_battlenpc_pools(poolId,actorClassId,name,genusId,currentJob,combatSkill,combatDelay,combatDmgMult) SELECT 1848000,2102301,'horizon_2102301',1,3,1,4200,1 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_pools WHERE poolId=1848000);

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Yak/YakFemaleStandard',classPath),propertyFlags=IF(propertyFlags=0,23,propertyFlags),eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),'{"talkEventConditions": [], "noticeEventConditions": [{"unknown1": 0, "unknown2": 1, "conditionName": "noticeEvent"}]}',eventConditions) WHERE id=2102305;

INSERT INTO server_battlenpc_pools(poolId,actorClassId,name,genusId,currentJob,combatSkill,combatDelay,combatDmgMult) SELECT 1848001,2102305,'horizon_2102305',1,3,1,4200,1 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_pools WHERE poolId=1848001);

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Cactus/CactusLesserStandard',classPath),propertyFlags=IF(propertyFlags=0,23,propertyFlags),eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),'{"talkEventConditions": [], "noticeEventConditions": [{"unknown1": 0, "unknown2": 1, "conditionName": "noticeEvent"}]}',eventConditions) WHERE id=2100901;

INSERT INTO server_battlenpc_pools(poolId,actorClassId,name,genusId,currentJob,combatSkill,combatDelay,combatDmgMult) SELECT 1848002,2100901,'horizon_2100901',13,3,1,4200,1 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_pools WHERE poolId=1848002);

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Piranha/PiranhaBoggyStandard',classPath),propertyFlags=IF(propertyFlags=0,23,propertyFlags),eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),'{"talkEventConditions": [], "noticeEventConditions": [{"unknown1": 0, "unknown2": 1, "conditionName": "noticeEvent"}]}',eventConditions) WHERE id=2104501;

INSERT INTO server_battlenpc_pools(poolId,actorClassId,name,genusId,currentJob,combatSkill,combatDelay,combatDmgMult) SELECT 1848003,2104501,'horizon_2104501',17,3,1,4200,1 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_pools WHERE poolId=1848003);

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Winglizard/WinglizardStandard',classPath),propertyFlags=IF(propertyFlags=0,23,propertyFlags),eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),'{"talkEventConditions": [], "noticeEventConditions": [{"unknown1": 0, "unknown2": 1, "conditionName": "noticeEvent"}]}',eventConditions) WHERE id=2100105;

INSERT INTO server_battlenpc_pools(poolId,actorClassId,name,genusId,currentJob,combatSkill,combatDelay,combatDmgMult) SELECT 1848004,2100105,'horizon_2100105',45,3,1,4200,1 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_pools WHERE poolId=1848004);

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Winglizard/WinglizardStandard',classPath),propertyFlags=IF(propertyFlags=0,23,propertyFlags),eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),'{"talkEventConditions": [], "noticeEventConditions": [{"unknown1": 0, "unknown2": 1, "conditionName": "noticeEvent"}]}',eventConditions) WHERE id=2100114;

INSERT INTO server_battlenpc_pools(poolId,actorClassId,name,genusId,currentJob,combatSkill,combatDelay,combatDmgMult) SELECT 1848005,2100114,'horizon_2100114',45,3,1,4200,1 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_pools WHERE poolId=1848005);

UPDATE gamedata_actor_class SET classPath=IF(classPath='','/Chara/Npc/Monster/Lemming/HareStandard',classPath),propertyFlags=IF(propertyFlags=0,23,propertyFlags),eventConditions=IF(eventConditions IS NULL OR TRIM(eventConditions) IN ('','{}'),'{"talkEventConditions": [], "noticeEventConditions": [{"unknown1": 0, "unknown2": 1, "conditionName": "noticeEvent"}]}',eventConditions) WHERE id=2104011;

INSERT INTO server_battlenpc_pools(poolId,actorClassId,name,genusId,currentJob,combatSkill,combatDelay,combatDmgMult) SELECT 1848006,2104011,'horizon_2104011',12,3,1,4200,1 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_pools WHERE poolId=1848006);

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848069,1848000,'horizon_pin_69',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848069);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848069,1848069,-1132.30712890625,54.68881607055664,-274.535888671875,2.0031142234802246 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848069);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848069 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Billy' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848070,1848001,'horizon_pin_70',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848070);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848070,1848070,-1146.290771484375,55.8690299987793,-270.8843078613281,-0.8520669937133789 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848070);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848070 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Nanny' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848071,1848000,'horizon_pin_71',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848071);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848071,1848071,-1145.822998046875,55.635719299316406,-289.9143371582031,-2.8366620540618896 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848071);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848071 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Billy' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848072,1848000,'horizon_pin_72',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848072);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848072,1848072,-1123.268798828125,53.50298309326172,-302.1194152832031,-1.5000653266906738 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848072);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848072 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Billy' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848073,1848001,'horizon_pin_73',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848073);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848073,1848073,-1110.5771484375,55.616336822509766,-306.10650634765625,0.2991124093532562 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848073);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848073 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Nanny' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848074,1848000,'horizon_pin_74',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848074);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848074,1848074,-1102.009033203125,58.088985443115234,-285.5815124511719,0.2971181571483612 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848074);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848074 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Billy' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848075,1848001,'horizon_pin_75',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848075);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848075,1848075,-1090.4803466796875,55.868717193603516,-261.6295166015625,0.24911534786224365 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848075);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848075 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Nanny' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848076,1848000,'horizon_pin_76',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848076);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848076,1848076,-1086.7139892578125,55.88041305541992,-242.25277709960938,-0.5088820457458496 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848076);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848076 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Billy' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1080,1600104,'wistsunn',172,-1297.242431640625,56.07341003417969,-138.78497314453125,-1.5628809928894043 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1080);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1080 AND s.actorClassId=1600104 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Wistsunn?' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1081,1500243,'tokiki',172,-1297.863525390625,56.02756881713867,-141.56124877929688,-1.2548766136169434 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1081);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1081 AND s.actorClassId=1500243 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Tokiki' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1082,1500075,'ririgeo',172,-1300.557373046875,56.00062561035156,-149.55471801757812,-1.191664695739746 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1082);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1082 AND s.actorClassId=1500075 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Riregeo?' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1083,1500101,'aistrach',172,-1299.6802978515625,56.00227737426758,-146.4193572998047,-1.1517014503479004 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1083);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1083 AND s.actorClassId=1500101 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Alstrach?' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1084,1500221,'horskfhis',172,-1453.033203125,45.269081115722656,-85.94178771972656,1.860305666923523 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1084);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1084 AND s.actorClassId=1500221 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Horskfhis?' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1085,2210507,'gilly',172,-1455.199951171875,45.3031005859375,-88.67715454101562,1.860304594039917 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1085);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1085 AND s.actorClassId=2210507 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Gilly' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1086,2210508,'nelly',172,-1456.702392578125,45.250797271728516,-90.3672103881836,1.8683032989501953 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1086);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1086 AND s.actorClassId=2210508 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Nelly' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_spawn_locations(id,actorClassId,uniqueId,zoneId,positionX,positionY,positionZ,rotation) SELECT 1087,2210509,'bonny',172,-1460.492431640625,45.070220947265625,-92.1619873046875,1.5063040256500244 WHERE NOT EXISTS(SELECT 1 FROM server_spawn_locations WHERE id=1087);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_spawn_locations s ON s.id=1087 AND s.actorClassId=2210509 AND s.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Bonny' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848085,1848002,'horizon_pin_85',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848085);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848085,1848085,-1468.1075439453125,48.29505157470703,-198.47349548339844,-2.34087872505188 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848085);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848085 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848086,1848002,'horizon_pin_86',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848086);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848086,1848086,-1473.866943359375,49.777462005615234,-175.3426971435547,0.391113817691803 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848086);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848086 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848087,1848002,'horizon_pin_87',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848087);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848087,1848087,-1489.7017822265625,51.78546905517578,-168.3660430908203,-0.3168807029724121 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848087);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848087 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848088,1848002,'horizon_pin_88',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848088);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848088,1848088,-1508.8016357421875,53.21022415161133,-183.00222778320312,0.02112594246864319 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848088);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848088 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848089,1848002,'horizon_pin_89',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848089);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848089,1848089,-1519.159912109375,53.1484375,-156.1747283935547,0.9051191210746765 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848089);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848089 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848090,1848002,'horizon_pin_90',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848090);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848090,1848090,-1491.1324462890625,52.54813766479492,-214.1019744873047,0.3471156060695648 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848090);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848090 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848091,1848002,'horizon_pin_91',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848091);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848091,1848091,-1470.346923828125,52.786537170410156,-229.9923095703125,0.6651207804679871 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848091);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848091 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848092,1848002,'horizon_pin_92',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848092);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848092,1848092,-1450.7532958984375,56.334197998046875,-249.68772888183594,-0.08288002014160156 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848092);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848092 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848093,1848002,'horizon_pin_93',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848093);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848093,1848093,-1437.0074462890625,56.16721725463867,-242.34532165527344,2.0231215953826904 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848093);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848093 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848094,1848002,'horizon_pin_94',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848094);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848094,1848094,-1419.07861328125,55.63584899902344,-253.77146911621094,2.1871256828308105 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848094);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848094 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848095,1848002,'horizon_pin_95',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848095);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848095,1848095,-1421.2926025390625,55.636531829833984,-271.3089599609375,-3.02406644821167 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848095);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848095 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848096,1848002,'horizon_pin_96',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848096);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848096,1848096,-1448.205078125,55.228553771972656,-273.3157958984375,-2.4000606536865234 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848096);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848096 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848097,1848002,'horizon_pin_97',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848097);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848097,1848097,-1465.730712890625,55.81850814819336,-259.59637451171875,-2.6452455520629883 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848097);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848097 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848098,1848002,'horizon_pin_98',28,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848098);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848098,1848098,-1492.48779296875,56.65712356567383,-253.16152954101562,1.3799386024475098 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848098);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848098 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848099,1848002,'horizon_pin_99',28,29,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848099);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848099,1848099,-1705.2784423828125,55.38601303100586,-89.91300964355469,0.8291241526603699 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848099);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848099 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848100,1848002,'horizon_pin_100',28,29,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848100);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848100,1848100,-1695.4569091796875,55.786582946777344,-64.82740020751953,1.195124626159668 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848100);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848100 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848101,1848002,'horizon_pin_101',29,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848101);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848101,1848101,-1740.5687255859375,56.66078186035156,-76.7305908203125,-0.06687068939208984 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848101);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848101 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848102,1848002,'horizon_pin_102',28,29,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848102);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848102,1848102,-1764.1522216796875,54.77137756347656,-82.91728973388672,-2.138871669769287 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848102);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848102 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848103,1848000,'horizon_pin_103',30,31,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848103);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848103,1848103,-1757.026611328125,57.66953659057617,-91.15177154541016,1.7671253681182861 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848103);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848103 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Aldgoat Billy' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848104,1848002,'horizon_pin_104',29,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848104);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848104,1848104,-1756.651123046875,57.13444900512695,-86.48931121826172,1.3791266679763794 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848104);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848104 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848105,1848002,'horizon_pin_105',28,29,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848105);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848105,1848105,-1782.7432861328125,55.83686828613281,-113.81404113769531,-2.569244146347046 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848105);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848105 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848106,1848002,'horizon_pin_106',29,30,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848106);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848106,1848106,-1796.146240234375,55.77107620239258,-135.06468200683594,-2.569241523742676 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848106);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848106 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Cactuar' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848107,1848003,'horizon_pin_107',20,21,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848107);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848107,1848107,-1896.19970703125,56.65711975097656,-1.8391344547271729,0.8227567672729492 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848107);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848107 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Angler' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848108,1848004,'horizon_pin_108',27,28,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848108);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848108,1848108,-1908.6575927734375,56.60160446166992,-36.700931549072266,-1.1552424430847168 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848108);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848108 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Saltspray Pteroc?' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848109,1848005,'horizon_pin_109',36,37,300,16335,676,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848109);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848109,1848109,-1938.2069091796875,56.45532989501953,-62.57229995727539,-1.8612451553344727 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848109);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848109 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Pyrausta?' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

INSERT INTO server_battlenpc_groups(groupId,poolId,scriptName,minLevel,maxLevel,respawnTime,hp,mp,zoneId) SELECT 1848110,1848006,'horizon_pin_110',17,18,10,0,0,172 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_groups WHERE groupId=1848110);

INSERT INTO server_battlenpc_spawn_locations(bnpcId,groupId,positionX,positionY,positionZ,rotation) SELECT 1848110,1848110,-2108.66748046875,56.65711975097656,-360.578857421875,-0.07366275787353516 WHERE NOT EXISTS(SELECT 1 FROM server_battlenpc_spawn_locations WHERE bnpcId=1848110);

UPDATE server_battlenpc_spawn_audit_pins p JOIN server_battlenpc_spawn_locations s ON s.bnpcId=1848110 JOIN server_battlenpc_groups g ON g.groupId=s.groupId AND g.zoneId=p.zoneId SET p.isPromoted=1,p.promotedAt=CURRENT_TIMESTAMP,p.promotionMigration='20260919_000048_horizon_named_pin_spawns.sql',p.promotionNote='User screenshot placement; client identity; provisional combat settings where uncaptured.' WHERE p.isPromoted=0 AND p.zoneId=172 AND p.enemyName='Thistletail Marmot' AND ABS(p.positionX-s.positionX)<0.001 AND ABS(p.positionY-s.positionY)<0.001 AND ABS(p.positionZ-s.positionZ)<0.001 AND ABS(p.rotation-s.rotation)<0.001;

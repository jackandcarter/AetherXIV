# Job quest giver and implementation audit — 2026-09-17

## Finding

Reward acquisition wiring is implemented; these job quest chains are not restored as playable end-to-end content. The live database contains all 42 named quest records, but no matching Brd0j/Pld0j/Mnk0j/War0j/Drg0j/Blm0j/Whm0j server quest Lua scripts were found. All 42 database rows currently have prerequisite=0 and minLevel=15; these do not encode the released quest chains. Do not mistake records or client presentation scripts for implemented server objectives.

The table identifies quest **issuers** from the official patch notes; later events/turn-ins can involve other NPCs. Locations are legacy map-grid coordinates, not world XYZ. They must not be used as fabricated spawn coordinates.

Sources: [Official 1.21 notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes), local archived notes, live database read-only audit, Data/scripts and client reward disassembly in this evidence directory.

## NPC coverage

- Georjeaux: spawn 701, zone 206. Server interaction calls defaultTalkWithGeorjeaux_001 only.
- Lulutsu: spawn 178, zone 209; default dialogue script present.
- Jenlyns: spawn 244, zone 209. Server interaction calls defaultTalkWithJenlyns_001 only.
- Gagaruna: spawn 61, zone 175; default dialogue script present.
- Neale: spawn 315, zone 230. Server interaction calls defaultTalkWithNeale_001 only.
- Haurtefert: spawn 711, zone 206; default dialogue script present.
- Yayake: spawn 175, zone 209; default dialogue script present.
- Soileine: opening-story identities soileine_man0g1 (spawn 1021, zone 206) and man0g1_soileine (spawn 1026, zone 155). These do not establish the White Mage quest giver flow.
- No matching named spawn/server interaction file found for Jehantel, Erik, Widargelt, Curious Gorge, Alberic, Raya-O-Senna, Lalai, Dozol Meloc or 269th Order Mendicant Da Za. This is an identity-name search; unnamed/aliased actors would require a display-ID cross-check before asserting absolute absence.

## All 35 action-granting quests

Every row below currently lacks a corresponding server quest Lua implementation. The action IDs and their grant association were recovered; no quest completion was fabricated.

| Job | Level | Action | Quest | Issuing NPC | Legacy location |
|---|---|---|---|---|---|
| Monk | 30 | Shoulder Tackle | Brother from Another Mother | Gagaruna | Ul’dah Merchant Strip (5,4) |
| Monk | 35 | Spinning Heel | Insulted Intelligence | Erik | Ul’dah Merchant Strip (7,3) |
| Monk | 40 | Fists of Wind | The Pursuit of Power | Erik | Ul’dah Merchant Strip (7,3) |
| Monk | 45 | Dragon Kick | Good Vibrations | Erik | Ul’dah Merchant Strip (7,3) |
| Monk | 50 | Hundred Fists | Return of the King...of Ruin | Erik | Ul’dah Merchant Strip (7,3) |
| Paladin | 30 | Cover | Paladin's Pledge | Lulutsu | Ul’dah, Gladiators’ Guild (5,5) |
| Paladin | 35 | Divine Veil | Honor Lost | Jenlyns | Ul’dah Hustings Strip (6,5) |
| Paladin | 40 | Holy Succor | Power Struggles | Jenlyns | Ul’dah Hustings Strip (6,5) |
| Paladin | 45 | Spirits Within | Parley on High Ground | Jenlyns | Ul’dah Hustings Strip (6,5) |
| Paladin | 50 | Hallowed Ground | Keeping the Oath | Jenlyns | Ul’dah Hustings Strip (6,5) |
| Warrior | 30 | Vengeance | Pride and Duty (Will Take You from the Mountain) | Neale | Limsa Lominsa, Marauders’ Guild (4,7) |
| Warrior | 35 | Antagonize | Embracing the Beast | Curious Gorge | Western Thanalan (15,33) |
| Warrior | 40 | Collusion | Curious Gorge Goes to the Bazaar | Curious Gorge | Western Thanalan (15,33) |
| Warrior | 45 | Steel Cyclone | Proof is in the Pudding | Curious Gorge | Western Thanalan (15,33) |
| Warrior | 50 | Mighty Strikes | How to Quit You | Curious Gorge | Western Thanalan (15,33) |
| Bard | 30 | Ballad of Magi | A Song of Bards and Bowmen | Georjeaux | Gridania, Archers’ Guild (8,5) |
| Bard | 35 | Minuet of Rigor | The Archer's Anthem | Jehantel | South Shroud (38,48) |
| Bard | 40 | Paeon of War | Bard's-Eye View | Jehantel | South Shroud (38,48) |
| Bard | 45 | Rain of Death | Doing It the Bard Way | Jehantel | South Shroud (38,48) |
| Bard | 50 | Battle Voice | Requiem for the Fallen | Jehantel | South Shroud (38,48) |
| Dragoon | 30 | Jump | Eye of the Dragon | Haurtefert | Gridania, Lancers’ Guild (8,2) |
| Dragoon | 35 | Disembowel | Lance of Fury | Alberic | Coerthas Central Highlands (35,18) |
| Dragoon | 40 | Elusive Jump | Unfading Scars | Alberic | Coerthas Central Highlands (35,18) |
| Dragoon | 45 | Ring of Talons | Fatal Seduction | Alberic | Coerthas Central Highlands (35,18) |
| Dragoon | 50 | Dragonfire Dive | Into the Dragon's Maw | Alberic | Coerthas Central Highlands (35,18) |
| Black Mage | 30 | Convert | Hearing Voices | Yayake | Ul’dah, Thaumaturges’ Guild (4,5) |
| Black Mage | 35 | Freeze | A Time to Kill | Lalai | Ul’dah Merchant Strip (7,6) |
| Black Mage | 40 | Flare | International Relations | Lalai | Ul’dah Merchant Strip (7,6) |
| Black Mage | 45 | Sleepga | The Voidgate Breathes Gloomy | Dozol Meloc | Western Thanalan (11,28) |
| Black Mage | 50 | Burst | Always Bet on Black | 269th Order Mendicant Da Za | Western Thanalan (11,28) |
| White Mage | 30 | Presence of Mind | Seeds of Initiative | Soileine | Gridania, Conjurers’ Guild (2,1) |
| White Mage | 35 | Regen | When Sheep Attack | Raya-O-Senna | North Shroud (15,22) |
| White Mage | 40 | Esuna | Lost in Rage | Raya-O-Senna | North Shroud (15,22) |
| White Mage | 45 | Holy | The Wheel of Disaster | Raya-O-Senna | North Shroud (15,22) |
| White Mage | 50 | Benediction | The Chorus of Cataclysm | Raya-O-Senna | North Shroud (15,22) |

## Chain steps without a new action

These must also be implemented for an authentic progression chain: Bard—Pieces of the Past (Jehantel); Paladin—Poisoned Hearts (Jenlyns); Monk—Five Easy Pieces (Widargelt, Eastern Thanalan 38,31); Warrior—Looking the Part (Curious Gorge); Dragoon—Double Dragoon (Alberic); White Mage—In Search of Succor (Raya-O-Senna); Black Mage—Gearing Up (269th Order Mendicant Da Za).

## Next restoration boundary

Start with Bard: recover Georjeaux/Brd0j1 acceptance and completion, establish Jehantel's actual identity and world-space placement from client/trace evidence, then implement quest states, objectives, encounters and rewards for Brd0j2–6. Each completed quest should feed the already implemented reward check. Verify in-client from a character with no injected job completion flags. Repeat for the other jobs once their evidence is established.

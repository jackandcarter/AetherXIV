# NPC and quest restoration: evidence cross-reference

## Outcome

There are useful, concrete restoration candidates, but a bulk client-NPC import would be wrong. The smallest likely repair is **nine already-placed Limsa Echo actors whose class paths are blank**. Two missing Gridania/Shroud quest triggers also have retail capture positions. Major job NPCs have recoverable identities, appearance records and historical quest associations, but their exact world placements and playable quest chains are not established by this audit.

This investigation changed only research tools and generated evidence. No live database, gameplay code, spawn policy, quest implementation or build was changed.

## Fresh coverage, not a completion percentage

Read-only live database snapshot and client scan on 2026-09-18:

| Measure | Result | Interpretation |
|---|---:|---|
| Static spawn rows | 1,052 | Includes objects, triggers, private areas and variants |
| Client actor classes in 1,000,000–1,999,999 | 4,162 | Catalogue filter, not a count of distinct people |
| Classes with root-area rows and nonempty class path | 793 | Placement presence does not establish working dialogue |
| Classes placed only in private areas, nonempty class path | 51 | Must not be promoted to ambient NPCs |
| Placed classes with blank class path in that range | 11 | Nine Echo actors, Audouin, Flame Sergeant Hanette |
| Classes without static rows | 3,307 | Not all should be spawned; script-created actors not excluded |
| No-static-row classes with dialogue or quest-literal leads | 336 | Research shortlist, not an approved import |
| Of those, default-dialogue-backed classes | 181 | Includes mapping evidence; see per-row client symbol checks |
| Database quest records | 524 | All have corresponding client script names |
| Distinct matching client scripts parsed to exact EOF | 520 | Five quest rows share Etc202; retain all five IDs |
| Quest records with matching server Lua file | 78 | **Not** 78 verified playable quests |

There is also a twelfth unloadable static row outside the catalogue join: spawn 943, `mumpish_miqote`, actor class **0**, Ul'dah private area. Do not “repair” it by guessing an identity.

No completion percentage is justified. A client script can contain cutscenes, menus and event functions without authoritative objectives, encounters, offer conditions or rewards. Numeric references miss symbolic NPC event names; the new index also retains `processEvent`/`defaultTalk` symbols for manual investigation. Same display ID is only an alternate-identity lead, not proof of interchangeable actors. Display ID zero is explicitly excluded from alternate-name matching.

## First restoration candidates

### 1. Existing Limsa Echo placements with incomplete class definitions

Classes **1000096, 1000097, 1000107, 1000108, 1000109, 1000142, 1000869, 1000870, 1000871** already have spawn rows 1060–1065 and 1067–1069. These are five Barracuda Knight variants, Mannskoen and three adventurer variants in `PrivateAreaMasterPast`, zone 230.

The current class loader in `src/AetherXIV.Core.Map/WorldManager.cs` selects `WHERE classPath <> ''`. Placement alone therefore cannot load these blank definitions. Garlemald's [class-path restoration migration](https://github.com/swstegall/Garlemald-Server/blob/70e54f33fc4ea2473d6d82b46b6d0a4567fb2986/common/sql/seed/061_restore_private_area_npc_classpaths.sql) independently provides proposed class shapes for all nine, and all nine display IDs match our decoded client labels. It attributes its values to newer Project Meteor data; that attribution is not independent retail proof of every flag.

This is a bounded **repair of existing placements**, not nine new invented coordinates. Before applying: inspect each existing event-condition blob, compare appearance/flags with client or capture evidence, preserve private-area level and quest ownership, and test the corresponding Echo sequences. Do not duplicate the existing rows or expose these variants in public Limsa.

### 2. Whispers in the Wood: captured triggers missing from static data

| Actor class | Capture / frame / zone | Observed XYZ, rotation | Server connection |
|---|---|---|---|
| 1090068 | moving_around_gridania.pcapng / 999 / 206 | 236.220, 12.000, -1274.570; -0.910 | Archers' Guild inside trigger; Man1g0 sequence 55 |
| 1090067 | gridania_to_coerthas.pcapng / 660 / 150 | -642.010, 20.070, -1060.050; -1.880 | WEST_SHROUD_TRIGGER constant; not armed by current onStateChange |

Both have blank database class paths and no static rows. Both retail initialization packets identify `/Chara/Npc/Populace/PopulaceStandard`, their actor class, and an unnamed display. The new verifier checks raw init/name/position packets from the same actor and frame, independently decodes position floats at payload offset 8, and verifies the original capture SHA-256.

**Important:** the older observation records retain `identityChainValid=false`: their class-path comparison was against our blank database definition. The raw checks establish the recorded class and transform; they do not silently override that old status or establish event gating. Exact float values and raw packets are retained in the evidence.

`Data/scripts/quests/man/man1g0.lua` arms 1090068 with `QFLAG_PUSH` at sequence 55 and advances to sequence 60 in `onPush`. `Quest.SetENpc` updates per-player presentation state; it is not a substitute for a world placement. For 1090067, simply adding the observed placement would not fix the missing sequence-30/35 wiring. This script also contains `SHROUD_ECHO_TRIGGER = 0`. Restore the ownership and transition chain, not just the dot on the map.

Next checks: decode the complete event-condition and push-radius data, verify when the trigger is enabled/disabled, distinguish public versus Echo state, then test previous/current/next quest sequences, relog and a second player without the quest. Captured positions remain sightings until reviewed for intended placement.

### 3. Missing job-NPC placement leads

The [official 1.21 notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) establish issuers and legacy grid locations. All 35 action-granting quest names join exactly to current database records. See `official-job-issuer-crossreference.json` for per-quest script status and issuer-class matches. The seven additional chain steps remain listed in the earlier job audit; they are not included in this 35-row export.

| NPC | Client class IDs without static rows | Quest association / placement evidence |
|---|---|---|
| Jehantel | 1002024, 1060039 | Bard chain; South Shroud grid 38,48 |
| Pukno Poki | 1001936 | First Bard quest participant in existing restoration research |
| Lalai | 1060035 | A Time to Kill; International Relations; Ul'dah grid 7,6 |
| Alberic | 1002001 | Dragoon chain after opener; Coerthas grid 35,18 |
| Erik | 1060033 | Monk chain after opener; Ul'dah grid 7,3 |
| Widargelt | 1002023, 1060032 | Five Easy Pieces; Eastern Thanalan grid 38,31 |
| Curious Gorge | 1060028 | Warrior chain after opener; Western Thanalan grid 15,33 |
| Raya-O-Senna | 1001570 | White Mage chain after opener; North Shroud grid 15,22 |
| Dozol Meloc | 1060037 | The Voidgate Breathes Gloomy; Western Thanalan grid 11,28 |
| 269th Order Mendicant Da Za | 1060038 | Always Bet on Black / Gearing Up; same legacy grid 11,28 |

These names have client identities and database appearance records, but **none of the listed classes has an observation in the existing decoded placement corpus**. That is a coverage limitation, not proof they never appeared in retail. Jehantel's [legacy archive entry](https://archiv.ffxiv.sevengamer.de/wiki/Jehantel) independently places him in South Shroud at 38–48, Paths of Tranquility. It does not give world-space height, facing or the correct class variant. Never feed those map-grid values into XYZ or use ARR locations.

Soileine illustrates the alias problem: class 1000234 has no static row, but class 1700030 is placed and opening-story identities also exist. Neither proves the White Mage issuer flow is available.

The old 2026-09-17 job status statement that all job Lua files were absent is now stale: `brd0j1`–`brd0j3` exist, and six Bard quest metadata rows were corrected. The Bard chain is still intentionally not offered pending placements/encounters. Q'zamqo is already applied as spawn 1071; do not re-import it from the older 182-candidate report.

## How other projects restore locations

Reviewed Garlemald commit `70e54f33fc4ea2473d6d82b46b6d0a4567fb2986`, not an unpinned moving branch:

- [Migration 059](https://github.com/swstegall/Garlemald-Server/blob/70e54f33fc4ea2473d6d82b46b6d0a4567fb2986/common/sql/seed/059_restore_private_area_spawns.sql) restores private-area placements from newer Project Meteor data, preserving private-area ownership and translating fields. This is recovering an omitted server dataset, not deriving all NPC positions from names in the client.
- [Migration 099](https://github.com/swstegall/Garlemald-Server/blob/70e54f33fc4ea2473d6d82b46b6d0a4567fb2986/common/sql/seed/099_man1l0_spawn_repairs.sql) distinguishes an upstream trigger position corroborated by a client journal marker from a deliberately synthesized assessor position beside another actor. The latter must remain labeled reconstructed, not retail-captured.
- Migration 096 uses route-interpolated encounter placement. It is another implementation lead, not an authoritative spawn table to copy wholesale.
- The locally checked [SeventhUmbral](https://github.com/jpd002/SeventhUmbral) revision `eead5fef6a2e5db9ffd82e9377bea72b23bf58af` contains small explicit zone-actor samples, not a complete retail population table. Its data readers are useful; sample actor IDs and coordinates cannot be assumed to map directly to our actor-class schema.

## Restoration workflow and verification boundary

1. Repair the nine existing Echo class definitions after the remaining condition checks. Keep changes narrowly content-scoped and idempotent.
2. Restore the Archers' Guild trigger as a sequence-gated vertical slice of Man1g0; then investigate the incomplete Shroud transitions.
3. Finish one job chain, preferably Bard because implementation has started. Locate Jehantel/Pukno using actual upstream rows, retail captures, client scene transforms or terrain-checked historical media. Resolve actor variants before placement.
4. Expand the 181 dialogue-backed leads by region. Archive entries can establish names/roles/grids; capture evidence can establish observed transforms; neither alone establishes all server behavior.
5. For every quest, verify offer prerequisites, accept/decline, journal markers, phase transitions, private-area lifecycle, objectives, encounter ownership, completion/reward exactly once, abandonment and relog persistence. Check players in different quest states simultaneously.

Do not construct a second quest/NPC subsystem. Reuse current `QuestState`, `SetENpc`, spawn ownership, dialogue scripts and diagnostic events. Do not equate seasonal, hamlet-defense, Echo or cutscene variants with permanent ambient population.

## Reproduction and limits

Run from the repository root with the external client mounted and read-only database access:

```sh
/usr/bin/python3 tools/Universal/audit-npc-quest-restoration.py
/usr/bin/python3 tools/Universal/enrich-npc-quest-audit.py
```

Artifacts: `evidence/npc-quest-crossreference-2026-09-18/`. The database snapshot excludes account/player tables. Client sheet resources and scripts, external project sources and selected captures have SHA-256 provenance. Validation: 520 matching client scripts parsed to EOF; 35/35 official issuer quest names matched; 9/9 proposed upstream repair display IDs matched client data; 2/2 selected trigger raw packet/coordinate/capture-hash checks passed.

This is a broad catalogue/script cross-reference plus targeted deep checks, not a full replay of every quest interaction in every capture. It reuses the existing corrected 54-capture decoded corpus rather than claiming a new complete capture decode. Neither runtime playability nor full NPC population coverage has been certified. No rebuild is needed for this research-only change.

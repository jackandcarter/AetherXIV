# NPC and enemy restoration status — September 16, 2026

Update: see [Restoration application and verification](RESTORATION_APPLICATION_2026-09-17.md) for the applied Q’zamqo migration, verified base getters, and audit corrections. The text below records the earlier investigation state.

## Findings

The field regions are substantially incomplete. Working terrain, aetherytes and transitions do not imply a restored NPC or enemy population. No location in the screenshot can be certified complete without a full, version-appropriate roster.

This audit reads the **local `ffxiv_server` database**, the current source tree, all 54 supplied retail captures, and the installed 1.23b client. It does not verify another machine's deployment or a running client's behavior. Research began September 15; the evidence folder retains that date.

| Measure | Verified result |
| --- | ---: |
| Static spawn rows in local database | 1,051 |
| Battle spawn rows | 60 |
| Normal battle spawn rows | 38 |
| Scripted/non-normal battle spawn rows | 22 |
| Retail captures / World TCP streams | 54 / 84 |
| Decoded packet records | 65,864 |
| Correlated non-player instantiation observations | 791 |
| Distinct zone + object-name + actor-class combinations | 510 |
| Enemy object/class combinations without a same-class root placement | 167, across 37 actor classes |
| Unspawned actor classes with existing server dialogue mappings and verified client symbols | 182 |

The 38 normal battle rows consist of **13 in Central Shroud territory 150, 23 in Central Shroud territory 162, and two legacy test rats in Central Thanalan territory 170**. The other 22 rows belong to scripted encounters; they are not an ambient population. Script-created actors outside these tables are not counted as permanent spawn rows.

## Location checklist from the screenshot

The following is a **100-unit three-dimensional proximity survey**, not a completeness score. It searches the relevant region's field territories instead of assuming the screenshot's warp territory is the actor's actual runtime territory. Counts exclude private-area static rows. “Populace” means an existing `/Populace/` class binding; it can include unnamed/invisible actors and does not prove dialogue/service completion. “Trace keys” includes objects, triggers and enemies, not just people. Zero means no qualifying row/sighting within this radius, not proof the entire location has no content.

| Location | Static rows | Populace rows | Battle rows | Trace keys |
| --- | ---: | ---: | ---: | ---: |
| Camp Bearded Rock | 7 | 6 | 0 | 0 |
| Camp Skull Valley | 4 | 3 | 0 | 0 |
| Camp Bald Knoll | 6 | 5 | 0 | 0 |
| Camp Bloodshore | 4 | 3 | 0 | 0 |
| Camp Iron Lake | 1 | 0 | 0 | 0 |
| Camp Dragonhead | 1 | 0 | 0 | 0 |
| Camp Crooked Fork | 1 | 0 | 0 | 0 |
| Camp Glory | 1 | 0 | 0 | 0 |
| Camp Ever Lakes | 1 | 0 | 0 | 0 |
| Camp Riversmeet | 1 | 0 | 0 | 0 |
| Camp Bentbranch | 1 | 0 | 2 | 4 |
| Camp Nine Ivies | 5 | 4 | 0 | 7 |
| Camp Emerald Moss | 7 | 6 | 0 | 15 |
| Camp Crimson Bark | 1 | 0 | 0 | 0 |
| Camp Tranquil | 10 | 9 | 0 | 13 |
| Camp Black Brush | 1 | 0 | 2 | 0 |
| Camp Drybone | 10 | 7 | 0 | 0 |
| Camp Horizon | 1 | 0 | 0 | 0 |
| Camp Bluefog | 1 | 0 | 0 | 0 |
| Camp Broken Water | 1 | 0 | 0 | 0 |
| Camp Brittlebark | 1 | 0 | 0 | 0 |
| Camp Revenant's Toll | 0 | 0 | 0 | 0 |
| Hyrstmill | 0 | 0 | 0 | 0 |
| Aleport | 6 | 6 | 0 | 0 |
| The Golden Bazaar | 0 | 0 | 0 | 0 |
| Halfstone | 0 | 0 | 0 | 0 |
| Red Rooster Stead | 1 | 1 | 0 | 0 |
| The Hawthorne Hut | 0 | 0 | 0 | 0 |
| Buscarron's Fold | 0 | 0 | 0 | 0 |
| The Coffer & Coffin | 0 | 0 | 0 | 0 |
| Mythril Pit T-8 | 0 | 0 | 0 | 0 |

Twenty-one of the 31 survey points have no nearby Populace-class row. Several camps have only their crystal in this radius. Larger-radius review and a full roster are required before calling a hamlet absent or a camp complete.

**Screenshot cautions:** its warp commands often use a region's starting territory (for example 150 for Nine Ivies), while retail observations use the actual territory (151). The Hawthorne Hut is listed as La Noscea/128 in the screenshot; the repository dialogue mapping identifies East Shroud. Its coordinates were surveyed against Shroud field territories, with the screenshot warp ID retained in the CSV. This correction is a cross-reference, not new placement evidence. The Google Sheet's individual tabs were unavailable, so their NPC lists, video contents and Bahamut implementation statuses were not audited or copied as AetherXIV status.

Files: [location checklist](../evidence/npc-restoration-2026-09-15/location-checklist.csv), [whole-zone counts](../evidence/npc-restoration-2026-09-15/zone-coverage.csv).

## What the captures add

Every supplied capture hash matches the existing corpus index. The current run recovered actor identity from `0x00CC`, name ID from `0x013D`, and full-precision position/rotation from `0x00CE`. Territory is checked independently against the actor ID, instantiate container, and object-name suffix. Class path and display-name ID are compared with the database catalog.

Of 791 observations, 713 contain instantiate/name/position in the same decoded protocol frame. The remaining 78 were recovered within a bounded 250 ms window on the same capture, TCP stream and source actor, rejecting intervening instantiations and ambiguous tied payloads. Actual recovered windows are recorded per observation. Protocol frame indices are not pcap frame numbers; both are included in the focused Q'zamqo fixture.

| Comparison across 510 distinct keys | Keys | Interpretation |
| --- | ---: | --- |
| Same class and territory, a database position within 1 unit | 217 | Position proximity corroboration; not a unique identity join or behavior test |
| Same class/territory exists but observed position differs | 31 | Includes 30 enemies that may have moved; not an instruction to relocate them |
| No same-class root placement, catalog identity checks pass | 135 | 87 enemies, 44 other objects, 4 unnamed Populace actors |
| Catalog identity needs review | 127 | 80 enemies, 29 other objects, 18 Populace actors, often blank class stubs |

The **167 enemy candidates** combine the 87 missing-placement and 80 catalog-review cases; none has an existing same-class root placement in its observed territory. This is a review backlog, not 167 approved permanent monsters.

| Territory | Enemy candidate keys |
| --- | ---: |
| North Shroud 152 | 86 |
| Central Shroud 150 | 49 |
| Western Thanalan 172 | 13 |
| Central Shroud 162 | 8 |
| South Shroud 154 | 5 |
| Eastern Lowlands 145 | 3 |
| East Shroud 151 | 3 |

Useful examples include Central Shroud funguar/chigoes, North Shroud hares/bats, South Shroud slugs/monkeys, and a few Coerthas and Western Thanalan enemies. The eight territory-162 candidates are the leve-class piranha/yak actors from `party_battle_leve`; they must remain director-owned rather than being inserted as permanent ambient enemies. Other candidates still need an ownership check too.

The evidence package also records **258 battle-profile observations**, including available initial HP/MP, level, job and property bits with their source frames. It does not fill absent fields with defaults. A visible enemy position is a sighting, not necessarily its home/spawn point. These captures do not establish a complete population, patrol loop, respawn interval, drop table or aggro/link policy.

Files: [all observations](../evidence/npc-restoration-2026-09-15/observations.json), [placement comparison](../evidence/npc-restoration-2026-09-15/placement-comparison.csv), [enemy backlog](../evidence/npc-restoration-2026-09-15/enemy-restoration-backlog.csv), [battle profiles](../evidence/npc-restoration-2026-09-15/battle-profile-observations.json).

## What the client adds

Verified install: `/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV`, build `2012.09.19.0001`; executable SHA-256 `9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9`.

The three regional default-dialogue scripts were decoded in memory and their symbols compared with repository actor/function mappings. **182 distinct mapped classes have no static spawn row, and all 182 function symbols are present in the corresponding client script**: 67 Forest, 36 Sea and 79 Thanalan mappings. No proprietary client source or binaries were added to the repository.

Examples already identified by repository comments:

- Hyrstmill: Livith, Proscen, Tanguistl, Comoere, Lougblaet, Famushi Dumushi, Drystan and Eadbert.
- Hawthorne Hut: Fraemhar, Lora, Arnott and Gerolt.
- Buscarron's Fold: Chamberliaux, X'bhowaqi and Wawaramu.
- Halfstone: Q'molosi, Bran, Faine and Aerghaemr.
- Golden Bazaar: Fromelaut, Zilili, Papala, Sasapano, Bibiroku, Bernier, Jajaba and Jujuya.
- Iron Lake / Broken Water / Bluefog: additional Grand Company personnel mappings.

Those location/name annotations come from repository comments, not a newly decoded client spawn table. Client symbol presence supports a dialogue lead; it does not validate coordinates, argument requirements, quest gating or a working conversation. Coordinate-like comments remain unverified leads.

Earlier client research found map/layout resource graphs, appearance assets and POP APIs, but **did not prove a complete production NPC/enemy placement table in the client**. This pass verifies client identity and mines dialogue symbols; it does not claim to complete that separate binary-format investigation.

Files: [unspawned dialogue candidates](../evidence/npc-restoration-2026-09-15/unspawned-dialogue-candidates.csv), [client script hashes](../evidence/npc-restoration-2026-09-15/client-dialogue-sources.json).

## Concrete restoration prepared: Q'zamqo

A new migration restores **Q'zamqo at Gridania's airship landing**, actor class `1001711`, display-name `1900211`, territory `155`, position `(38.89, -10, -1185.37)`, rotation `1.54`.

Evidence closes the specific gap:

- Retail `moving_around_gridania.pcapng`, TCP stream 0, capture frames **81 and 227**, repeats the same actor/transform and publishes its talk/notice conditions.
- The local catalog has an appearance row, but its actor-class path is blank and it has no spawn row.
- Captured property bits 0, 1 and 4 produce flags **19**.
- The installed client's `DftFst` exports `defaultTalkWithQZamqo_001`; the server already routes actor class 1001711 to that function.
- The migration restores class path, flags, event conditions and one static placement. It does not invent an airship service or change native actor slot allocation.

[Migration 000040](../db/direct-core/migrations/20260916_000040_gridania_qzamqo_restoration.sql) is included in the Operator migration contract. The existing 23-row NPC service evidence catalog was left intact because its current preflight checks a global exact count; appending unrelated NPC evidence there would make preflight report a damaged catalog. Q'zamqo's provenance is instead recorded in migration comments and a focused local trace fixture.

**Deployment state: prepared and tested, not applied to the local game database or live-playtested.** No game database data was changed in this audit. Normal database setup/update will apply the new migration when the updated package is installed. A fresh Map start and an in-game talk check remain the deployment validation.

Validation: applied twice in an isolated MariaDB schema, checked its transform against both raw retail position payloads, verified class/flags/event JSON, preserved an existing natural-key placement, and confirmed an occupied migration ID fails without overwriting an unrelated actor. The disposable schema was removed. Two targeted Operator tests pass. The database release package also builds successfully, preserves the registered baseline hash, and includes the new migration byte-for-byte.

## Current policy and runtime gaps

The research policy requires source-linked evidence and prohibits turning guessed positions, developer pins or individual sightings into complete retail populations. Evidence review happens before migration generation; **the runtime does not filter spawn tables by evidence confidence**.

Important findings from the current data and loading code:

1. **12 static rows reference blank/unloadable actor classes.** These include Audouin and Flame Sergeant Hanette in Eastern Thanalan, the class-zero Mumpish Miqo'te row, and nine recently seeded Limsa echo actors. `LoadActorClasses` excludes empty class paths; `LoadSpawnLocations` skips unavailable classes. A row existing in SQL therefore does not prove it can appear. These existing gaps were recorded, not guessed into working classes.
2. **20 static rows target zones 0/1 without a usable zone name.** These appear to include legacy placeholders; they are not counted as implemented world population.
3. **Limsa MarketEntrance class 1090238 has nonstandard event JSON and legacy `size` metadata.** MariaDB `JSON_VALID` rejects it. Newtonsoft can accept some unquoted keys, so this alone is not proof of a crash; its trigger fields still need separate retail validation.
4. **No broken battle group/pool/genus/class joins were found** in the current 60-row battle data; no orphan private-area static rows were found by the zone/name/type join.
5. **105 static rows lack an appearance row, all in map-object classes.** This is not evidence of 105 missing NPC models; map objects use a different presentation path.
6. **49 developer pins remain unpromoted; 11 are marked promoted.** Existing pin notes and patch-era caveats must travel with future work.
7. Runtime battle startup only adds `spawnType=Normal` actors to zones. Scripted encounters require their owning director. Private-area battle behavior, skill lists, respawn and combat profiles need separate review from a coordinate import.
8. The Hall of Flames currently has **two static rows: the company shop and exit**, confirming that restoring entry/exit did not restore an entire headquarters roster.

Files: [runtime health](../evidence/npc-restoration-2026-09-15/runtime-health.json), [unloadable classes referenced by spawns](../evidence/npc-restoration-2026-09-15/spawn-rows-with-unloadable-class.csv).

## Decoder correction

The research corpus decoder previously read `0x00CE`/`0x00CF` coordinates at payload offset 0 rather than **8**, and treated `0x013D` as a string at offset 0 rather than **name ID at 0, optional text at 4**. Both are corrected in the local research tool. Three raw-capture regression tests pass. The full corpus was regenerated with the fix; the audit independently reads full-precision coordinates from raw payloads.

The September 7 report's “zero undecoded opcodes” is a statement about decoder coverage, not proof every interpreted field was correct. Earlier semantic outputs from these two decoder functions must be regenerated before reuse. The dedicated encounter extractor already used offset 8, and runtime protocol codecs already use the correct layouts; no game packet implementation was changed for this issue.

## Next restoration batches

1. Deploy/playtest the prepared Q'zamqo change and validate talk, appearance, logout/login and duplicate-free reload.
2. Review the 37 enemy classes against extracted profiles and client presentation. Resolve ambient versus quest/leve ownership before authoring population rows; keep unknown spawn origins, respawn timing and drops explicit.
3. Revisit the existing Forest Funguar/Chigoe pins with the newly extracted exact class IDs and profiles. Match species/patch era and source footage before promoting any particular pin.
4. Use the 182 dialogue leads to build missing named-NPC rosters for the sparse camps/hamlets. Seek actor-specific placement evidence; do not treat a camp's center coordinate as every NPC's position.
5. Continue client resource parsing via file-set ownership and typed map-layout records. Require a concrete actor identity → territory → transform join before labeling a result a client-derived spawn.

The available captures are concentrated in Gridania/Shroud and selected routes/quests. They cannot certify complete La Noscea, Mor Dhona, Ul'dah headquarters or all Coerthas camp populations. The remaining locations require further evidence, not fabricated density.

## Reproduction

From the repository root, using the existing local research tools and MariaDB access:

```sh
python3 .local-evidence/tools/Universal/decode-corpus-wide.py --manifest tests/fixtures/trace-evidence/retail-world-trace-corpus-index.json --raw --out evidence/npc-restoration-2026-09-15/corpus-raw.jsonl
python3 evidence/npc-restoration-2026-09-15/audit.py
python3 evidence/npc-restoration-2026-09-15/extend-audit.py
python3 evidence/npc-restoration-2026-09-15/mine-client-dialogue.py
python3 evidence/npc-restoration-2026-09-15/verify-restoration.py
python3 -m unittest discover -s .local-evidence/tests/tools -p test_corpus_spawn_fields.py
```

The audit scripts only read game tables. The migration verifier creates and drops its own uniquely named test schema. Raw captures/client files remain local. This repository intentionally ignores research tools and trace fixtures; retain the local evidence folder for reproduction. Source revisions and hashes are in [run provenance](../evidence/npc-restoration-2026-09-15/run-provenance.json).

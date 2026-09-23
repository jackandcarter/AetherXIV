# Enemy identity and parameter evidence — 2026-09-17

Update: see [Restoration application and verification](RESTORATION_APPLICATION_2026-09-17.md) for the applied Q’zamqo migration, verified base getters, and audit corrections. The text below records the earlier investigation state.

## What is now recovered

Read the installed `2012.09.19.0001` client's original sheets: 18 action names resolve to 21 command-label rows; six enemy names resolve to 10 display-name rows and 26 actor-class rows. The extra rows are real ambiguities, not duplicates to discard. All IDs below are client-backed. An observed English log label joined to a unique client label is strong identity evidence, but is not an enemy execution packet from the May–June log session.

## Action IDs

| Action label | Client command ID(s) |
|---|---|
| Burning Cyclone | 23274 |
| Caudal Spine | 23270 |
| Concussive Blow II | 26616 |
| Decimate | 23199 |
| Devastate | 23198 |
| Flurry | 26678 |
| Foul Bite | 23144 |
| Full Thrust | 27279, 27759 |
| Heavy Strike | 22102 |
| Heavy Thrust | 27273, 27757 |
| Light Strike | 26677 |
| Light Thrust | 22109 |
| Pierce | 27758 |
| Pulverize | 23200 |
| Pummel | 26679, 27110 |
| Raging Horn | 23273 |
| Skewer II | 27693 |
| Smoulder | 23271 |

Pummel, Heavy Thrust, and Full Thrust each have two matching rows. Their localized descriptions differ. The installed player command ID is therefore not an automatic match for the enemy's logged action. Keep both IDs until an execution packet or client call site resolves the variant. Log filenames date the observations to May–June 2012; the client is September 2012, so preserve that version boundary.

## Enemy identity joins

Client `actorclass` column 5 joins to English `xtx/displayName` row ID; original table indices are retained in the evidence.

| Display name | Display ID | Actor-class candidates |
|---|---|---|
| young raptor | 3100211 | 2100211, 2100212, 2100213 |
| watchwolf | 3101417 | 2101418, 2101419, 2101420, 2101421 |
| scarred watchwolf | 3101418 | 2101422 |
| battle drake | 3102219 | 2102220, 2102222 |
| battle drake | 3102221 | 2102223, 2102224 |
| battle drake | 3102222 | 2102225 |
| Zahar'ak halberdier | 3106554 | 2162039, 2162040, 2162041, 2162042, 2162043, 2162044 |
| Zahar'ak drubber | 3106556 | 2162046, 2162047, 2162048, 2162049, 2162050 |
| battle drake | 3202208 | 2202207 |
| battle drake | 3202210 | 2202209 |

These are actor definitions, not spawn IDs or coordinates. The local database has blank class paths for these candidates. Do not assign the generic tutorial wolf path to all watchwolf variants or manufacture placements from their names.

## Combat parameters: recovered bytes versus verified meanings

`parameter-columns-uninterpreted.json` preserves **50 gameCommand columns and 9 gameCommandBasic columns** for each of the 21 command candidates, with data resource ID and row byte offset. `sheet-schemas.json` preserves original column indices and types. The other `command` sheet contains eight client columns; it is not the whole command definition.

For example, Foul Bite has gameCommand column 64 = 6.0, column 75 = 6.0, column 96 = 0.25, and gameCommandBasic column 36 = 30000. These are genuine client values. **No semantic label or unit is yet proved for those columns.** In particular, 30000 is not established as a 30-second cooldown, and 6.0 is not established as a six-yalm range. They must stay out of live server settings until the consuming client code or corroborating packets establish meaning.

Next semantic verification: find the client's accessors for gameCommand/gameCommandBasic and trace reads of these original column indices into action range checks, cast presentation, recast display, targeting, or animation selection. Validate the inferred units against multiple known player actions and independently captured enemy actions. Current server values alone are not independent historical evidence.

## Retail trace cross-check

The existing decoded corpus contains **102 server-to-client command-result events** attributable to a preceding observed monster initialization. Preserved fields include actor/class IDs, capture hash, stream, decoded frame index, opcode, command ID, animation, outcome fields, and raw payload. Raw offsets 0 and 0x24 independently reproduce all exported actor and command IDs.

Useful observed joins include:

- Piranha class 2204502 → command 23061 → client label **Rage of the Deep**.
- Male yak class 2202301 → 23091 and 23167 → both labeled **Head Butt**.
- Female yak class 2202305 → 23091 **Head Butt**, 23092 **Alpine Fury**.

This is direct evidence of why identical names cannot collapse numeric variants. The ledger also retains attack/mode/zero-ID records; 102 is not a count of unique attacks or confirmed special attacks. Different animation/result records may represent readiness, execution, or cancellation. Nearest preceding initialization is retained as a join limitation; this pass does not prove full actor-lifecycle continuity.

**Zero command-result events in this join belong to the 26 target actor-class candidates.** The captured standard wolves use a different display ID. Do not label those as watchwolves. No numeric execution ID was recovered for a target enemy by pretending a family match is exact.

## How the evidence applies to implementation

| Server component | Evidence now available | Still needed before activation |
|---|---|---|
| `gamedata_actor_class` | Client actor-class → display-name joins | Class-path and appearance identity per variant |
| `server_battlenpc_pools` | Candidate identities and per-enemy observed ability lists | Specific variant, level/stats, aggro, encounter ownership |
| `server_battle_commands.id` | Unique label IDs for 15 names; two candidates for each of 3 names | Packet resolution for ambiguous variants; version consistency |
| `server_battle_commands` combat fields | 59 original parameter columns per candidate | Verified column semantics/units; server-only formulas where absent |
| `server_battlenpc_mob_skill_list` | 21 enemy/action relationships from combat logs | Approved numeric action definitions and appropriate dispatch types |
| Battle action presentation | Raw retail animation/result fields for other monsters | Equivalent target-enemy execution evidence or verified client animation mapping |
| Spawn/respawn/link rules | None added by identity sheets | Location/timing/neighbor-selection evidence |

A safe first implementation batch is Foul Bite after its parameter consumers are understood: use command 23144, attach only to verified watchwolf/scarred-watchwolf variants, test ready/hit/miss/parry behavior, and leave unrelated opening wolves alone. Keep live activation separate from reference evidence until those fields are supported. No live database or combat behavior was changed in this pass.

## Reproduce and verify

```sh
/usr/bin/python3 tools/Universal/recover-legacy-enemy-ids.py \
  '/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV' \
  evidence/npc-recovery-2026-09-17/enemy-profiles.json \
  evidence/npc-restoration-2026-09-15/observations.json \
  evidence/npc-restoration-2026-09-15/corpus-raw.jsonl \
  evidence/enemy-ids-2026-09-17
/usr/bin/python3 tests/tools/test_recover_legacy_enemy_ids.py
```

The reader follows [SeventhUmbral's original 1.23b sheet implementation](https://github.com/jpd002/SeventhUmbral/tree/eead5fef6a2e5db9ffd82e9377bea72b23bf58af/dataobjects), with its BSD notice retained. It uses enable-file ID ranges, schema column types, and XOR-decoded strings; it asserts complete consumption of each selected data block. XML is bounded at its closing `ssd` tag with at most four trailing bytes; the root catalog's decoded final byte is non-whitespace, so that trailer is not treated as XML content. Other schemas parse inside the same explicit boundary.

Two regression tests cover sparse row IDs, signed values, original indices, XOR strings, offsets, extra data rejection, and invalid XML boundaries. All selected installed-client blocks consumed exactly. Client resource hashes and input hashes are saved in `client-source-manifest.json` and `summary.json`. The initial broad plaintext DAT scan was stopped once the structured reader worked; no completeness claim is made for that scan. No bulk client text/assets were copied into the output.

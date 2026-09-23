# Quest event gates and major job NPC placement evidence

## Changes implemented

1. Man0l1's Musketeers' Echo cast now requires **territory 230**, in addition to `PrivateAreaMasterPast`, private-area type 3 and quest sequence 7. Area name/type alone are not globally unique. The nine restored actor definitions and their placements are unchanged.
2. Man1g0's push handler accepts only the actor registered for its implemented push sequences: 5→1090204, 15→1090205, 25→1090046, 55→1090068. Wrong actors and stale pushes end without changing quest state. The unsupported 30/35 branches can no longer advance if invoked directly; their unverified Echo transitions remain disabled rather than being fabricated.
3. Added executable tests against the real actor packet builder, quest event router, independent quest-state instances and actual Lua scripts. These use the existing event system, not a parallel policy implementation.

No job-NPC spawn or new database migration was applied in this pass. The 000042/000043 definitions remain installed. No character checkpoint was altered for testing.

## What event verification establishes

- Actual migrated circle JSON produces the expected geometry fields and disabled default status through `Actor.GetEventConditionPackets` / `GetSetEventStatusPackets`.
- A quest overlay enables a push only for its presentation call; it does not mutate the shared actor definition and enable it for a subsequent player.
- The actual quest router rejects an unregistered actor, disabled push, wrong event type and cleared membership.
- Separate Quest instances do not share ENPC membership. Clearing one removes its membership without affecting another.
- MoonSharp executes Man1g0: the guild event advances once from 55 to 60, wrong actors cannot advance any implemented push step, valid existing 5/15/25 triggers retain their transitions, and unresolved Shroud steps do not advance.
- MoonSharp executes Man0l1: public area, wrong territory, wrong private-area name/type and wrong sequence do not register the Echo cast; the existing exit push is enabled only for the expected sub-counter.

These are **automated server-side checks**, not an in-client playthrough. Live animation/cutscene behavior, actual persisted relog and simultaneous network sessions remain manual tests. The tests simulate independent state and packet generation; they do not claim a complete client/server integration session.

## New job NPC location evidence

The prior job audit searched marker IDs using database quest-ID prefixes, which did not identify the Bard markers. This pass joins **client display-name IDs** to the full `quest_marker` sheet instead. It recovers 24 rows labeled with nine target NPC names. These are marker destinations, not automatically approved actor spawn transforms.

| NPC | Display ID | Example marker | Horizontal marker coordinates (X, Z) |
|---|---:|---:|---|
| Jehantel | 1200133 | 11225001 | 737.489990, 1025.729980 |
| Pukno Poki | 2480005 | 11225002 | 1139.020020, 1012.669983 |
| Lalai | 1500186 | 11223503 | 18.160000, 283.670013 |
| Erik | 1000101 | 11221001 | -32.750000, 45.810001 |
| Widargelt | 2200241 | 11221201 | 1213.670044, 107.290001 |
| Curious Gorge | 1600318 | 11220001 | -1116.040039, 285.489990 |
| Raya-O-Senna | 2700007 | 11222001 | -1540.979980, -1588.339966 |
| Dozol Meloc | 2430006 | 11223501 | -1513.660034, -235.220001 |
| Alberic | 1000275 | 11226001 | -179.350006, -303.730011 |

Important qualifiers:

- Widargelt also has marker 11221502 at (-138.759995, 345.190002), in a different map grouping; Raya-O-Senna also has marker 11080205 at (816.590027, 1463.949951). A name can have multiple quest destinations. Do not select all of them as permanent ambient placements.
- No matching name marker was recovered for **269th Order Mendicant Da Za** in this pass.
- Marker rows provide two coordinates, not height or facing. Map grouping columns are retained in raw form; they must be resolved through the map/zone linkage rather than used directly as server territory IDs.
- Existing default-dialogue comments contain candidate XYZ for Curious Gorge, Widargelt, Erik, Lalai and Dozol. Their X/Z values agree with the client markers; that does **not** validate the comments' heights or establish independent provenance. Da Za's comment explicitly says “somewhere” and remains approximate.
- Two Jehantel classes (1002024/1060039) and two Widargelt classes (1002023/1060032) exist. Current default-dialogue mappings use 1060039 and 1060032, but those server mappings alone are not proof of retail spawn ownership. The name search also finds Curious Gorge battle-class 2289037: it must not be confused with populace class 1060028.
- 66 selected job/default-dialogue client scripts parsed successfully to EOF. None contained these complete marker IDs as numeric constants. The name-linked marker evidence is therefore retained separately from proof of an exact quest phase or script call. No prefix-based quest association was invented.

## Independent source check

At pinned [MeteorReborn revision 59155d239a657374a40768cb405ac5972e765681](https://github.com/Yokimitsuro/MeteorReborn/tree/59155d239a657374a40768cb405ac5972e765681), neither `data/sql/server_spawn_locations.sql` nor `data/sql/server_eventnpc_spawn_locations.sql` has a row for any of the 13 target name-matched actor variants. The actor-class table still has blank definitions for the missing job populace classes. It does not provide the missing coordinates or a ready job-NPC import.

The existing corrected decoded trace corpus also contains **zero actor-initialization matches** for those 13 classes. This is absence in the searched corpus, not proof of absence in retail or other captures. Exact inputs and hashes are retained.

## Restoration boundary

The new markers materially narrow the location search: Jehantel and Pukno now have name-linked client destinations, while five other candidate horizontal positions are independently corroborated. They do not finish the spawn specification.

Before permanent spawns: resolve map grouping→territory, choose the appropriate actor variant and quest ownership, recover or validate terrain height and facing, and confirm class flags/event conditions. The current geometry extractor is explicitly asset-local and not approved world-space landing geometry; it cannot safely supply those heights yet.

The next choice is whether to keep only evidence-backed placements enabled, or authorize **clearly labeled reconstructed test placements**. No such choice was assumed here. Even test placements would need usable, verified terrain—not an arbitrary Y=0. Quest offer wiring must remain disabled until destinations and encounters are usable; spawning Jehantel alone would not finish the Bard chain.

## Reproduction and verification

Research tool: `tools/Universal/research-job-npc-placements.py`.
Outputs: `evidence/job-npc-placement-2026-09-18/` (client resource hashes, identity rows, 24 marker candidates, dialogue leads, 66 script inventories/hashes, pinned upstream searches and trace initialization search).

Tests: `Man1g0TriggerRestorationTests`, `RestoredTriggerEventStatusTests`, and `tests/tools/test_echo_trigger_restoration.py`.

The combined Map run still exhibits intermittent Decoy failures (four in this pass), even with requested xUnit serialized settings. Running Decoy separately passes 9/9; the remaining suite passes separately. This does not certify the combined suite as clean or establish concurrency as the definitive cause. No unrelated combat code was changed.

Final verification: six focused event/gating tests passed; six temporary-schema migration/packet checks passed. The remaining Map tests passed separately (461 passed, two skipped), and isolated Decoy passed 9/9. The combined-suite caveat above remains unresolved.

Lua manifests were regenerated and Release Core build 22042 rebuilt into the existing `bin/build/Release/MacOS` package. Both packaged quest scripts and the manifest match source; deep/strict app signature verification passes. Final Lua manifest SHA-256: `205fc741e48f0bd8c7b96727b275b49d46ea2279efa717723396e30c20bf8890`. No live server or game client was started for these checks.

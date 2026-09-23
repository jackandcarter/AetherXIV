# Limsa Testeight: equipment and job test kit

Prepared in the local `ffxiv_server` database on 2026-09-17 for character 57,
`Limsa Testeight`. Map, World, and Lobby processes were stopped during the grant.
This is a character-only test fixture, not a migration or a fresh-install seed.

## Character preparation

Archer and Thaumaturge are now level 30; Conjurer and Pugilist are level 15.
The active class remains Archer, now with its saved active level synchronized to
30. Existing items, equipment references, class allocations, quests, and saved
gearsets were not replaced. Sixteen new normal-quality items occupy normal
inventory slots 7–22 (zero-based). All 14 equipment items have their catalog's
full durability and modifier rows; the two soul crystals are ordinary items.

The production `JobProgressionPolicy` and `TryChangeToCurrentClassJob` currently
require Archer 30 / Conjurer 15 / Soul of the Bard for Bard, and Thaumaturge 30 /
Pugilist 15 / Soul of the Black Mage for Black Mage. This fixture satisfies that
implemented gate; it does not simulate completion of retail job quests.

| Purpose | Granted items |
| --- | --- |
| Job access | Soul of the Bard (2000205), Soul of the Black Mage (2000207) |
| Archer weapons | Elm Velocity Bow (4070011), Ash Composite Bow (4070306) |
| Thaumaturge weapons | Bone Staff (5020106), Taurus Staff (5020107) |
| Archer test appearance | Cotton Coif (8010523), Cotton Tabard (8031121), Cotton Breeches (8050223), Cotton Halfgloves (Green) (8070323), Padded Sheepskin Duckbills (8080224) |
| Caster test appearance | Cotton Sugarloaf Hat of Intelligence (8010923), Cotton Dalmatica of Casting (8030429), Cotton Breeches of Casting (8050234), Cotton Halfgloves (8070322), Leather Crakows (8080513) |

These are catalog-backed test outfits, not a claim of best-in-slot or fully
restored compatibility penalties. All selected equipment is level 30 or lower
and has the unrestricted tribe code 29. New server item IDs are 207–222.

## Playthrough

1. Start the rebuilt Core app in `bin/build/Release/MacOS`.
   Log in as Limsa Testeight and confirm the levels and new inventory items.
2. Equip the Elm Velocity Bow and the Archer outfit. Use the job-change command
   to select Bard. Record displayed attributes and the visual appearance.
3. Equip the Ash Composite Bow. This is a same-class weapon change: Bard and
   the outfit should remain selected. Record the resulting weapon/stat changes.
4. Equip the Bone Staff. This selects Thaumaturge and clears the current job;
   it does **not** automatically select Black Mage. Equip the caster outfit,
   then use the job-change command to select Black Mage.
5. Equip the Taurus Staff. Black Mage and the caster outfit should remain.
6. Switch back to a bow. The Archer outfit should restore; select Bard again.
   Switch back to a staff and check that the caster outfit restores.
7. Log out and back in, then repeat a round trip. Check inventory ownership,
   outfit appearance, active class/job, and attributes. Repeated identical
   swaps must not accumulate bonuses; a swap must not heal current HP/MP.

Gearsets currently belong to base classes, not separate job-specific outfit
slots. The first visit to an unconfigured class has no complete saved outfit;
equipping the supplied pieces establishes it through the normal game path.
Live-client timing and visual correctness remain to be tested.

## Diagnostics

Uses the existing `DevDiagnostics` JSONL logger, not a second trace pipeline.
Core settings now have developer diagnostics, network trace, and server trace
enabled. Restart Core to pick up the saved settings. Logs are under
`/Users/imac/Library/Application Support/AetherXIV/Diagnostics/<run>/`.

- `inventory.equipment.begin/end`: request ID, actor, requested slot/item,
  old/new class and job, success/rejection reason, elapsed milliseconds.
- `inventory.equipment.commit.begin/end`: proposed loadout as
  `wirePoint:serverItemId:catalogId`, destination class, database commit boundary.
- `job.change.begin/end`: job transition, rejection reason, elapsed time.
- Existing `inventory.equipment.changed`, `stats.recalc.begin/end`,
  `stats.layer.equipment.item`, interaction traces, and `wire.subpacket` events
  expose inventory publication, equipment contributions, final attributes,
  opcodes, direction, and byte previews.

Use actor 57 (or `0x39` in older events), timestamps, per-process trace sequence,
and the equipment request ID to follow a swap. The request ID is not a protocol
field and is not propagated into every packet event. A commit-end event means
database commit completed; equipment-end means the server method returned, not
that the client rendered the result. Packet traces describe queue/relay activity,
not client receipt. Wire previews are limited to 256 bytes with a truncation flag;
use packet capture when full payloads or transport timing are needed.

Verbose/network logging may affect timing and disk usage. Disable network trace
after the focused test, and treat captured packet/log data as private.

## Verification and recovery

Database verification found the four expected levels, 16 new items, 14 modifier
rows, no durability mismatches, and unchanged equipment references. The Map test
suite passed 355 tests, including three executable diagnostic-output tests;
one optional isolated database integration test was skipped in this run.
The Release core-only rebuild completed (build 22042); packaged Lua matches
source and deep/strict code-signature verification passed. Packaged Map DLL
SHA-256: `76b21f88e8f36915ae93dbc268848a9a3aaffa3cf768a7837fe19418623c1cf6`.

Pre-grant character rows are backed up at:
`/Users/imac/Library/Application Support/AetherXIV/Backups/Database/limsa-testeight-before-job-kit-20260917.sql`.
The guarded, one-time grant script is local at
`.local-evidence/limsa-testeight-job-kit.sql`; its guards reject a duplicate grant.
Recovery should target this character and the recorded new item IDs while the
server is stopped; do not blindly import the INSERT-only snapshot over live rows.

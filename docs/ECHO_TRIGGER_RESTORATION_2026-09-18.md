# Echo actor and quest-trigger restoration — 2026-09-18

## Applied

- Forward migration **000042** restores missing class paths, flags and talk/notice definitions for nine already-placed Man0l1 Echo actors: 1000096, 1000097, 1000107–1000109, 1000142, 1000869–1000871. Existing placements, appearances, private-area ownership and customized definitions are preserved. Definition provenance is the pinned upstream migrations identified in the SQL; this is not a claim of new retail capture evidence for these nine actors.
- Forward migration **000043** restores class definitions and observed placements for Man1g0 triggers 1090067 (spawn 1072, zone 150) and 1090068 (spawn 1073, zone 206). Raw retail circle packets establish radii 9 and 4 respectively, secondary radii equal to the primary radius, unknown1=1, flags=0, unknown2=3, silent=false. Both carry notice conditions and property bit 0, not talk conditions. Geometry packets are reproduced byte-for-byte by the regression test.
- Both circles default to disabled. The existing Man1g0 sequence-55 SetENpc overlay activates the Archers' Guild trigger. Its onPush handler now also requires the correct actor class before invoking processEvent100 and advancing to 60.
- **West Shroud is deliberately not armed.** The current sequence-30/35 Echo transition and SHROUD_ECHO_TRIGGER=0 remain incomplete. This update restores its data foundation, not the entire Whispers in the Wood quest.

No second quest system, guessed coordinates, global NPC activation, character edits or clean database installation were introduced. Existing quest and event diagnostics remain the logging path.

## Live application and backup

Both migrations were applied together in one transaction with their SHA-256 ledger entries using `tools/Universal/apply-echo-trigger-restoration.py --apply`. No running core/server process or server session was found before application. The separate login-session table was not cleared or modified.

Content backup: `.local-evidence/restoration-backups/20260918T055353Z-echo-triggers/` (actor definitions and affected existing spawn rows, with hashes). Player/account data was not queried for this backup and was not changed.

An initial application attempt failed because the proposed Archers' Guild uniqueId exceeded varchar(32). Its transaction and ledger rolled back; read-back confirmed the original state. The ID was shortened to `man1g0_arc_inside_trigger`, and the SQL tests were upgraded to clone the real table DDL into connection-local temporary tables before the successful retry. The first attempted backup is also retained.

Rollback, if required: stop the servers; review and restore the affected actor definitions from the backup, remove only new trigger rows 1072/1073 after confirming their identities, restore the prior Man1g0 script, and reconcile these two ledger entries with the deployed package. Do not run a clean install or blindly import duplicate INSERT statements from the backup.

## Verification

- Six migration/packet tests passed against temporary tables using the live schema: idempotency, custom-definition preservation, existing-placement preservation, reserved-ID collision rejection, exact captured circle payloads and coordinates, existing quest ownership.
- MoonSharp executes the actual Man1g0 Lua in a regression test: only sequence 55 arms the guild trigger; the wrong actor cannot advance it; the right actor advances once; West Shroud stays unarmed.
- 63 quest-related Map tests passed. Complete Map suite: **465 passed, 2 skipped**, with xUnit test-collection parallelism disabled. The first parallel run failed a Decoy combat test; Decoy alone passed 9/9, and the serialized full suite passed. This indicates concurrency-sensitive test behavior; unrelated combat code was not changed here.
- Lua syntax check passed; source Lua manifest regenerated.
- Live read-back confirmed all 11 class/event definitions and both new spawns. All ten existing Echo placements (including the already-working Totoruto) were compared with the earlier database snapshot and are unchanged.
- Source migration hashes equal packaged migration hashes and ledger values. Packaged Man1g0 and script manifest equal source. Deep/strict app signature verification passed.

## Build

Release Core-only build **22042**, product 2.1.0, completed in `bin/build/Release/MacOS`. Core and Database packages were replaced there; no parallel output package was created. Launcher, Wine and Umbra were not rebuilt or replaced.

The canonical baseline SHA-256 remains `c68dbe452971be9461f4a99a05a5cc030d0d395ee32db47240f52ac199794af1`. These are forward content migrations, not a schema-generation bump. Packaged migration checksums and Lua verification were regenerated through the existing build tools; historical migration hashes were not rewritten.

## Still requires in-client testing

1. Enter the Man0l1 Musketeers' Guild Echo (zone 230, PrivateAreaMasterPast type 3). Confirm the nine actors appear in their existing positions and each conversation returns control. Confirm they do not appear in public Limsa and the existing Echo exit still works.
2. On a test character legitimately at Man1g0 sequence 55, enter the Archers' Guild trigger near (236.22, 12, -1274.57). Confirm processEvent100, the linkshell notification and transition to sequence 60; re-entry must not repeat the event.
3. Verify the guild trigger is inert before/after that sequence, after relog, and for another player without this quest state. Verify West Shroud remains inert.

No server was started and no in-client pass is claimed. The data takes effect on the next normal server start.

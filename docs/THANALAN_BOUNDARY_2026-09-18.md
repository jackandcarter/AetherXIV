# Western / Central Thanalan road boundary

## Confirmed defect

Limsa Testeight walked from Camp Horizon to Camp Black Brush. Runtime positions
advanced from (-1311.23,56.01,-148.376) to (36.989,200.001,-494.566), while
the server retained public zone 172. The database contains no boundary involving
zone 172. Black Brush's crystal is in public zone 170 at (33,201,-482).
All 156 newly restored enemies loaded at startup; absence of a boundary is not
an enemy-loader failure. The restoration adds no new zone-170 population.

## Evidence search and fallback

The decoded corpus contains 0x0005 map-context packets in ten captures, none
of the Thanalan warrior captures. It therefore does not supply a captured
172/170 crossing. Client `_zoneParam` binds 170 to 3001 and 172 to 3003;
zone/region and map-navigation sheets examined do not establish retail server
transition rectangles. Parsed layout node names did not identify a named zone
boundary. This is a bounded search, not proof that no such client data exists.

The user withdrew the minimap-flash observation as uncertain, nominated a road
location, and authorized using it as fallback after evidence checks. Their
position at 2026-09-19T00:00:06.6672320Z was
(-615.6918,122.83063,-433.44693), still public zone 172.

Source local log:
`/Users/imac/Library/Application Support/AetherXIV/Diagnostics/20260918T233114.018Z-6005cac2eef74fa0b9149c02ccc16fca/map-20260918-233116.jsonl`.

Two recorded traversals cover the proposed corridor. Within X -650..-580,
observed Z ranges from approximately -445.41 to -392.97. The provisional Z
extent -465..-375 allows lateral road movement. Zone-172 exit/return box is
X -650..-640; merge is -630..-600; zone-170 arrival/exit box is -590..-580.
Separated boxes provide hysteresis through the existing policy, not a new
coordinate classifier. These are engineering choices for live testing,
**not recovered retail transition coordinates**.

Migration 000045 adds only this missing pair, preserves any existing pair in
either orientation, and does not alter the baseline, character data, actor
placements or transition implementation. The boundary list loads on server
startup, so applying SQL alone is not a live reload. A character already past
the corridor must cross it or explicitly warp to the correct zone.

## Live acceptance checks

1. Start on the west side in public zone 172; walk east through the corridor.
   Confirm `zone.seamless.change.end` reports 170 and Black Brush's crystal
   becomes visible when approached.
2. Walk back west. Confirm zone 172 and Horizon's actors when approached.
3. Turn around within the merge corridor; there must be no repeated zone flips.
4. Verify no unrelated road/territory transitions changed.

Server must be stopped before applying migration and replacing its Release
package. No clean database install is needed.

## Verification and application

The targeted Release test run passed 19 tests across
`ThanalanBoundaryRestorationTests`, `SeamlessBoundaryPolicyTests` and
`ZoneTransitionRecipePolicyTests`. New tests read the migration's actual boxes
and cover both directions, skipped position samples, turning within the merge
corridor and an unrelated road. Connection-local temporary SQL tables verified
idempotency and preservation of a preexisting reversed 170/172 link.

After the user stopped the stack, migration 000045 was applied with its ledger
entry in one transaction. Post-check found exactly one 170/172 link. The prior
boundary table is backed up at
`.local-evidence/restoration-backups/20260919T000345Z-thanalan-boundary`.
Retail boundary accuracy and client-visible transitions remain live-test items.

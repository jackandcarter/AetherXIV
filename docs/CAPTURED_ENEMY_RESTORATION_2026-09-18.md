# Captured Shroud / Thanalan enemy restoration

Migration `20260918_000044_captured_shroud_thanalan_enemies.sql` adds 156
placements, representing 27 localized names, 32 class/job pools and 57
zone/level/HP/MP profiles. It uses the existing BattleNpc loader and tables;
there is no new runtime spawning subsystem.

| Zone | Territory | New placements |
|---|---|---:|
| 150 | Central Shroud | 49 |
| 151 | East Shroud | 3 |
| 152 | North Shroud | 86 |
| 154 | South Shroud | 5 |
| 172 | Western Thanalan | 13 |

## Evidence boundary

The source is `evidence/trace-test-locations-2026-09-18/candidates.json`, joined
to `evidence/npc-restoration-2026-09-15/battle-profile-observations.json` by
capture, stream, initialization frame, class and object name. Original capture
SHA-256 values are rechecked by the generator. Class display-name IDs were
checked against the live definitions; none differed. Name aliases come from
the installed 1.23 client name sheet.

Positions are initial retail sightings, not proven spawn homes. Their use as
test origins follows the user's explicit acceptance of approximate/fight
locations. Selected objects have field-object names and zone-matching root
suffixes. That supports placement testing, but does not establish every
time, weather, quest or respawn condition. The eight leve-owned yak/piranha
sightings remain excluded, as do unidentified actors and out-of-scope Coerthas.
Already restored names, including star marmots and the earlier zone-162
population, remain untouched.

Level, HP maximum, MP maximum, job and class presentation come from captured
initialization data. Genus assignments map the captured model families onto
the existing server taxonomy; they are not decoded retail genus IDs.
Respawn 10, combat skill 1, delay 4200, damage multiplier 1 and neutral aggro
are legacy defaults, not recovered retail policies. Drops, special attacks,
time/weather restrictions and full retail combat balance are not restored
by this migration. Existing genus modifiers and AI still apply.

The migration snapshots existing localized name IDs before insertion, including
static and battle actors. A preexisting name suppresses the whole batch for
that name, including client aliases; rerunning therefore adds nothing.
Reserved-ID collisions fail rather than overwriting unrelated content.
Custom nonempty actor definitions are preserved. All 156 selected definitions
in this installation matched the captured paths after filling missing fields.

## Application and verification

- Applied to `ffxiv_server` with the server stopped and zero active sessions.
- Affected content backed up under `.local-evidence/restoration-backups/20260918T232814Z-trace-enemies`.
- Migration and ledger insertion committed atomically; no player rows changed.
- Six temporary-table tests passed before and after application: repeat safety,
  name-alias suppression, collision failure, loader joins/profile/coordinate
  checks, excluded actors and preservation of existing positions.
- Live zone counts matched the table above; migration ledger checksum verified.
- Release core-only build 22042 completed into `bin/build/Release/MacOS`.
- Packaged migration matches source SHA-256
  `856a5e43a741cd78accb3895de9f4d343ac84677934b34f0c55cddbb3d394cac`.
- Baseline remains unchanged:
  `c68dbe452971be9461f4a99a05a5cc030d0d395ee32db47240f52ac199794af1`.
- App signature verified. Launcher/Wine/Umbra were not replaced.

**Live-client visibility, terrain contact and combat remain to be tested after
starting the server.** No server was started automatically. Full coordinates,
names and capture provenance are in
`evidence/enemy-restoration-2026-09-18/LOCATIONS.md` and `manifest.json`.

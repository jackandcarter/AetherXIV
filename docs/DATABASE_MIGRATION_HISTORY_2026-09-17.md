# Reviewed migration history, 2026-09-17

The installed database has the recognized schema generation 2 / version 2 contract and trusted
baseline `c68dbe452971be9461f4a99a05a5cc030d0d395ee32db47240f52ac199794af1`.
The clean-install offer was caused by five historical migration hash differences,
not a schema-generation change or missing character data.

## Provenance

- `20260718_000008`, `000011`, `000012`, and `000013`: recorded SHA-256 values
  exactly match their Git blobs in release commit
  `5ffdeed8ac3c181f2045497a5c5ded9973119c3f`. The newer versions replace capture
  filenames with `reviewed-local-validation` in evidenceReference strings (first
  three) or a SQL comment (000013). Runtime SQL values are otherwise unchanged.
- `20260727_000030`: recorded SHA-256 exactly matches both
  `/Volumes/Dev2/AetherXIV 2.0/db/direct-core/migrations/20260727_000030_native_actor_slots.sql`
  and the same member of `/Volumes/Dev2/AetherXIV 2.0.zip`. The only difference is
  the `source_sha256` SQL comment. Read-only comparison of the installed database
  against the current native-slot catalog found 154 assignments, zero mismatches.

## Correction

`db/direct-core/migration-history.sha256` records exact migration-name,
historical-hash, and reviewed-current-hash triples. Operator preflight and both
installer entry points use that same packaged history. A historical hash is
accepted only when the migration name AND the actual current file hash match
the reviewed pair. Unknown revisions and later edits are still rejected.

The existing installer exception for the earlier 000036 Limsa area-ID revision
was moved into this same history. Its actual data correction remains in the
mandatory forward migration 000037; it is not classified as metadata-only.

No historical migration file or live database ledger row was rewritten. No clean
install was run. Pending forward migrations remain pending and still require the
normal backed-up updater. In particular, 000040 was absent from the installed
ledger during this investigation; this checksum fix does not mark it applied.

Release Core and Database were rebuilt into `bin/build/Release/MacOS`. The
packaged history matches source and Core's deep/strict signature check passes.
Shell functional checks cover acceptance of all six reviewed histories and
rejection of unknown hashes; C# tests additionally reject wrong names and
changed current hashes. PowerShell uses the same table but was not executed on
this Mac because PowerShell is unavailable.

The production preflight was also executed read-only using the saved Core
settings and live database: it requires no canonical repair or new administrator
credentials, and recognizes the remaining forward migration path.

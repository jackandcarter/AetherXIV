# Client patching verification — 2026-09-18

The launcher uses one managed patch implementation for Windows, Linux, and macOS.
All three build recipes publish `AetherXIV.Launcher.App`, which references the same
`AetherXIV.Launcher.Core` project.

## Upstream comparison

Reviewed the original implementations at these exact revisions:

- [Meteor Patcher.cs, d770d413](https://github.com/ThiconZ/FFXIV-Meteor-Launcher/blob/d770d4132e7d550aa89d6617484933ac5b6a0244/FFXIV%20Meteor%20Launcher/Patcher.cs)
- [Seventh Umbral PatchFile.cpp, eead5fef](https://github.com/jpd002/SeventhUmbral/blob/eead5fef6a2e5db9ffd82e9377bea72b23bf58af/launcher/PatchFile.cpp)

Both walk entry history records and extract the final stored file payload, using
raw data or zlib. These are complete replacement files. Seventh Umbral skips
bodyless entries; Meteor explicitly implements file deletion. AetherXIV retains
terminal deletions while avoiding deletion of an intermediate historical state
when the same entry's final state has a replacement payload.

AetherXIV previously required a terminal deletion's historical source size and
hash to match the installed file. Meteor does not impose that requirement. This
could reject older, modified, or partially patched sources. The helper now follows
Meteor's deletion policy, retaining the original in the active rollback journal.
No affected-user logs were available, so this is a reproduced failure mechanism,
not a claim that it explains every reported failure.

## Changes

- Select remaining boot/game archives independently from known installed versions,
  in canonical manifest order. Reject unknown versions instead of replaying the
  entire chain and stamping the target version onto an unsupported client.
- Require checksum verification for the pending archives, recheck them before
  applying, and permit missing archives that have already been applied.
- Commit each archive and its version file together. Roll back the active archive
  on exceptions or cancellation; retain earlier committed archives.
- Recover an interrupted archive before reading installed versions on retry.
  Recovery is repeatable if interrupted. Failed recovery retains its journal.
- Retire committed journals by directory rename before deleting their contents,
  so interruption during backup cleanup cannot undo committed client data.
- Keep temporary payloads inside the recovery directory, including when the process
  exits during decompression. Lock concurrent patch operations against one client.
- Verify chunk CRC32, header counts, output sizes and destination SHA-1. Reject
  truncated archives and bound decompressed output by its declared size.
- Normalize trailing root separators; reuse existing path casing on case-sensitive
  filesystems. Reject ambiguous case collisions, traversal and linked patch paths.
- Disable launcher play actions during patch operations and mark clients with an
  active recovery journal as requiring patch recovery.
- Persist archive/file failure details and rollback outcomes in
  `.aetherxiv-patch.log`. Checksum inspection now responds to cancellation.

## Validation performed

- Independently audited all **52 retail archives**, totaling **6,306,175,615 bytes**:
  all archive sizes/CRC32 matched the launcher manifest; all chunk CRC32 and FHDR
  counts matched; all stored payloads decompressed to their declared length and
  destination SHA-1. Examined **308,090 ETRY records**, including both HIST and DIFF
  archives and **15,454 terminal deletions**.
- Applied **278 representative retail records across all 52 archives** through the
  C# helper and compared resulting files with independently extracted expectations.
- Applied the entire retail boot and final game archives to a disposable test tree;
  cancelled between archives, verified the boot checkpoint, resumed only the pending
  game archive, and verified that a subsequent apply was a successful no-op.
- **30/30** tracked verification tests passed on macOS, including both opt-in retail
  tests. **20/20** existing local patch/library/client-install tests also passed.
- Launcher cross-builds for `win-x64`, `linux-x64`, and `osx-arm64` succeeded without
  warnings or errors. Native Windows/Linux execution was not performed locally.
  `verify-client-patching.yml` adds native CI coverage on all three operating systems.

The tests did not modify an installed client. Applying the two full archives to a
fixture verifies patch behavior, not completeness of a retail base installation.
Patches cannot reconstruct missing base files that no archive contains. Full
base-to-target installation, in-game launch validation, native Windows/Linux runs,
and power-loss durability across filesystem/hardware combinations remain outside
this local validation. Recovery covers application failures and process interruption;
it is not a general client backup or an undo of earlier successful patches.

## Reproduction

Synthetic tests (no retail assets required):

```sh
dotnet test tools/Verification/Patching/PatchingVerification.csproj --configuration Release
```

Optional retail audit and tests, using a user-owned library:

```sh
python3 tools/Verification/Patching/audit-retail-patches.py \
  /path/to/ffxiv_patches bin/patch-verification/samples
AETHERXIV_PATCH_LIBRARY=/path/to/ffxiv_patches \
AETHERXIV_PATCH_SAMPLES="$PWD/bin/patch-verification/samples" \
  dotnet test tools/Verification/Patching/PatchingVerification.csproj --configuration Release
```

The audit emits small local patch samples and `summary.json` under the supplied
output directory. Keep retail samples out of source control. The two integration
tests are explicitly skipped unless their environment variables are supplied.

## Follow-up investigation — 2026-09-23

Compared the helper shipped at `22c01af` with the current implementation using
an independently extracted record from retail `D2010.09.19.0000.patch` and a
disposable fixture containing an older/different source file. The shipped helper
fails with:

```text
File scheduled for deletion differs from the patch source for
client/chara/pc/c001/equ/e900/dwn_mdl/0001: expected size 1232, got 36.
```

The current helper applies the same sample successfully. This is a controlled
reproduction, not the affected user's exact error. It demonstrates that validating
the historical source size before a terminal deletion was an incompatible added
restriction. Seventh Umbral's original implementation skips bodyless records;
it does not impose this source-size check. Current AetherXIV performs terminal
deletions with rollback protection and without that historical-size restriction.
The correction is already on branch `2.1` and in the pending Linux test build.

Re-audited all 52 local retail archives: all sizes and CRC32 values match the
launcher manifest; all 308,090 entries pass the independent chunk/payload audit.
Added the synthetic regression test
`TerminalDeletionAcceptsDifferentHistoricalSourceSize`.

The existing tracked suite on branch `2.1` also passed on Linux, Windows and macOS:
https://github.com/jackandcarter/AetherXIV/actions/runs/35898793077
Retail files are not supplied to that CI run. No installed client was modified.
The exact affected-user message is still needed to distinguish this deletion
failure from archive-size validation or decompressed-output-size failures.

Local opt-in verification finished with **31 passed, 0 failed, 0 skipped**,
including 278 sampled records spanning every archive and the complete boot/final-game
archive checkpoint/cancellation/resume tests. This does not constitute a full
retail-base-to-target installation test.

# Equipment, class changes, and attributes — 2026-09-17

## Implemented

The existing `EquipCommand -> Player -> ReferencedItemPackage/Database` path now
constructs a proposed loadout before changing live state. A transaction checks
inventory ownership and saves the destination class's equipment, active class,
initial class level, and job reset together. Failed reads and failed commits do
not publish an empty or partially saved loadout. The previous class's loadout is
retained; underwear remains shared through class zero.

Switching weapons within the same class no longer reloads a gearset, resets the
hotbar, or removes class-change status effects. Actual class changes use the
existing hotbar and ability initialization path after the weapon commit.

Equipment-point validation now expands the client's slot groups. This covers
ordinary armor, weapons/tools, flexible accessory slots, and the occupied slots
of two-handed weapons and combined armor. An incoming item removes conflicting
optional equipment in the same transaction; a request that would remove the
mandatory main hand or underwear is rejected. Saved equipment is checked for
ownership, slot validity, duplicate references, tribe, and required level before
restoration. Required levels are distinguished from recommended levels.

Appearance uses the existing packing and paired-weapon code. A complete loadout
is published once and reconstructed from saved equipment on login. The copied
Lua appearance/class orchestration was removed rather than retained as another
active implementation. Inventory changes use a single begin/end batch.

Stat recalculation remains owned by `Character`. During equipment/class changes,
status-loss recalculations are deferred until the final loadout is ready. The
existing armor, ordinary bonus, HQ, tool-stat, and class-allocation calculations
remain in use. Offhand tools no longer count as shields for the blocking flag.

## Evidence

Installed client: `/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV`.
Decoded with `.local-evidence/tools/Universal/decode-ffxiv-client-lpb.py`.

- `ItemBaseClass_common.getEquipmentEquipPointDetail`, source lines 2117–2288:
  allowed and occupied equipment-point groups.
- `ItemBaseClass_common.isConformTribe`, lines 2297–2389: tribe restrictions.
- `ItemBaseClass_common.canEquipSimple`, lines 5694–5716: required-level gate.
- `EquipWidget.equipSlotNumToSlotName`, lines 2484–2532: accessory slot names.
- Wrapped `ItemBaseClass_common` SHA-256:
  `50bd1025ad4ad14c12e51f96f9c73894d990f0c452cd756e0d815d684e5c91f0`.
- Wrapped `EquipWidget` SHA-256:
  `f7959c210e4f06900bde9ae8fc714ac112be50f20459109c4611f2b38844ced3`.

Four captures were extracted from `/Volumes/Dev2/ffxiv_traces.zip` and decoded
with the existing TCP stream analyzer, stream 0, server port 54992:

| Capture | SHA-256 | Event-start requests / command results / EndEvent packets |
| --- | --- | --- |
| change_to_botanist | `8fdc448a17d19bea3d07855a8d8843d78926c50586f3656946c96b6a6cb27d26` | 2 / 2 / 0 |
| gear_changeweapon | `3eec2c2993feeb2b8c47c1e1f553335638a7262ec13b561e2d52d768f0798080` | 1 / 1 / 0 |
| change_bodyarmor | `55ee6035d24e80b97c9354a0800b86a52d0a99e1200cf6875a5b56009215021a` | 1 / 1 / 0 |
| change_helm | `af1e465076c3acf6188f24f084622b22beccc932bb96effd517b2d9a44606e08` | 2 / 2 / 0 |

Botanist capture frame 89 puts the inventory begin/update/end batch before
appearance/class/property updates and command completion. These observations
support the publication shape, not every packet byte or every item restriction.

## Verification and limits

Local tests exercise the actual equipment-stat layer (replacement, above-level
bonus suppression, removal, preservation of external modifiers), deferred
recalculation/resource preservation, Lua request handling, and class/job
attribute isolation. A temporary MariaDB instance exercises the production
database methods for class/weapon round trips, full-width item IDs, ownership
failure, rollback after writes begin, gearset read failure, and allocation reload.

The seed contains 4,482 equipment rows across 23 equipment-point groups, including
210 bracelet/earring/ring rows. A coverage check requires a legal client slot for
every seeded group. This is slot coverage, not proof of 4,482 fully accurate items.
Class selection retains the existing 18 playable weapon/tool families. Five
unreleased placeholder families are not assigned invented playable classes.

Still incomplete: the runtime class/job compatibility sheet and its penalties,
conditional item bonuses, materia contributions, above-level tool scaling, full
level/tribe base-stat growth, and exact combat formulas. This pass does not claim
to restore those rules. No new stat growth values or item bonuses were invented.

A live client armor/weapon/class/relog playthrough remains necessary to confirm
rendering and interaction timing. Automated checks do not establish that result.
Tests and raw evidence stay local under the repository's existing ignore policy.

### Completed build checks

- Full Map test suite: 352 passed, 1 optional database integration test skipped.
- Focused suite with isolated MariaDB on port 33079: 82 passed, including the
  production database round-trip/rollback test. No live database was modified.
- Release core rebuild completed: `./tools/MacOS/build-aetherxiv.sh Release --scope core`.
- Output (canonical path): `bin/build/Release/MacOS/AetherXIV Core.app` (build 22042).
- Packaged equipment Lua and script manifest match their source files byte for
  byte. Deep, strict code-signature verification and `git diff --check` passed.
- Packaged Map DLL SHA-256:
  `8ea944a6eed1337b461ee5c36f05080f7160d44183537d6f151a3db03fa6c5f3`.

The launcher, Wine, and Umbra were outside this core-only rebuild. The isolated
test database process was no longer running at final verification; the existing
user MariaDB service was left untouched.

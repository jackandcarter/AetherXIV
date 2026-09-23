# Crowd-control diminishing returns

## Implemented

Crowd-control resistance now runs inside the existing `StatusEffectContainer.AddStatusEffect` path. Each target owns its own small category history, shared across casters, classes, jobs and commands. Existing accuracy/status-chance rolls still happen before status acceptance. No new scripting format, database table, or parallel status system was added.

The first three accepted applications during a category window receive full, half and quarter duration. Further applications fail until the window expires. Only accepted effects advance the history: rejected overwrites, a full status list, null effects and immunity rejection do not. The normal boolean result propagates failure through existing callers; `BattleUtils.TryStatus` retains its existing generic failure message (32002). No unverified new packet/text arguments were added.

## Source and mapping

[Official patch 1.21, dev1071](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) provides the sequence and cooldowns, explicitly including enemies using control against players. Local archived patch notes were searched through the available later 1.2x notes; no later matching rule change was found in that search.

| Category | Client status ID | Window, seconds |
|---|---|---:|
| Slow | 223003 | 180 |
| Petrification | 223004 | 300 |
| Paralysis | 223005 | 180 |
| Silence | 223006 | 180 |
| Blind | 223007 | 180 |
| Pacification | 223013 | 300 |
| Amnesia | 223014 | 300 |
| Stun | 223015 | 30 |
| Sleep | 228001 | 180 |
| Bind | 228011, 228013 | 180 |
| Heavy | 228021 | 180 |

The installed client `xtx/status` schema 189071475 independently supplies these names and descriptions. A complete exact-name scan returned these twelve rows, saved with data offsets and file hashes under `evidence/crowd-control-2026-09-17/`. Both Bind variants share history. Slowcast, Blindside and Fixation are excluded; similar words do not establish category equivalence.

## Explicit interpretation and limits

The numbered official procedure is implemented as a fixed window starting with the first successful application. Later successes and immune attempts do not restart it. This follows the published steps; a separate retail packet timing experiment has not corroborated the restart interpretation.

History survives ordinary status removal, including the existing death/zoning flag-removal paths, while the target's container exists. No unsupported special reset was introduced. A newly constructed actor/container starts with empty history; it is not persisted across logout, process restart or actor recreation. Retail reset behavior at those boundaries remains unverified.

Existing overwrite eligibility is evaluated before duration reduction. Accepted refreshes count as applications. Fractions are preserved in server expiration times: e.g. 3, 1.5, 0.75 seconds. `StatusEffect` stores duration as double internally and exposes that through the existing getter, while the existing `SetDuration(uint)` script API is unchanged. Existing client timer serialization still has whole-second resolution. Duration display rounding remains an in-client check.

The separate `ReplaceEffect` method is currently used by Life/Power Surge and Blissful Mind, not by the mapped control effects; it remains a transformation helper rather than a new infliction entry point. Future control scripts must use normal status application to receive this policy.

## Verification

Tests exercise all twelve IDs, cooldown boundaries, independent categories/targets, shared Bind history, multiple casters, actual Player/BattleNpc target types in both directions, accepted refreshes, immunity, unchanged rejected effects, full-capacity and overwrite rejection, fractional expiration, and death/zoning flag removal. Fixtures defer world-dependent stat recalculation; they test production status acceptance and policy, not a connected client session.

## In-client checks still needed

- Apply the same control three times to one target with fresh attempts before its category window expires; observe full/half/quarter durations and fourth-attempt failure.
- Repeat with a second caster and with different abilities applying the same status category.
- Verify a different category remains available and that the first category recovers at its deadline.
- Exercise both player-to-enemy and enemy-to-player applications, status icons/timers and failure messages.
- Check death, same-actor zoning, reconnect and actor recreation explicitly against future retail evidence.

No live character data or command potency was changed. The implementation is ready for client testing, not claimed retail-complete.

45 focused tests passed; log saved in `evidence/crowd-control-2026-09-17/tests.log`. Core-only Release packaging completed successfully into `bin/build/Release/MacOS/AetherXIV Core.app`. No server was started and no connected-client test was performed.

# Ability mechanics: first Archer/Bard audit

## Applied changes

Battle Voice's per-target callback now assigns HP Boost to the selected recipient, with the caster retained as its source. Previously every party recipient caused another application to the caster. The song-enhancement effect remains caster-only. Existing durations and HP calculation are unchanged pending lifecycle verification. Removed an unreachable reference to an undefined `ballad` global from Ballad's other-song replacement code.

These are script corrections, not a claim that Battle Voice or Bard combat is fully restored. No database values were changed.

## Recovered period evidence

[Official 1.21 notes](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes) establish party-wide maximum-HP enhancement and enhancement of songs sung during Battle Voice.

[Carraway's guide](https://forum.square-enix.com/ffxiv/threads/20443-Carraway-s-Guide-to-the-New-Archer-1.18-and-Beyond), updated April 16, 2012 for 1.21b, reports:

| Effect | Ordinary | With corresponding Choral gear | Battle Voice | Voice + gear |
|---|---:|---:|---:|---:|
| Minuet accuracy/magic accuracy | 35 | 43 | 71 | 86 |
| Ballad MP per tick | 20 | 26 | 40 | 51 |
| Paeon TP per tick | 50 | 60 | 100 | 120 |

It describes 25% maximum HP from Battle Voice, a 30-second enhancement window, and one song per caster on each recipient. Different Bards can supply different songs concurrently. These are contemporary player reports, not recovered server formulas. The guide does not establish lower-level scaling or exact rounding; its movement-speed figures are explicitly uncertain. [A May 2012 discussion](https://forum.square-enix.com/ffxiv/threads/44650) repeats this guide, so it is not independent corroboration.

Current Minuet code produces 60/120 with gear at level 50, conflicting with the guide. Ballad produces 52 with gear and Voice rather than 51. No guessed multiplier or interpolation was substituted.

## Runtime findings requiring further work

- `StatusEffectContainer.AddStatusEffect` accepts a source argument but does not assign it. `BattleUtils.TryStatus` explicitly assigns source for ordinary command statuses, including songs; therefore this does NOT mean all songs lose ownership. Battle Voice bypasses that helper; its script now explicitly supplies source.
- Status refresh removes the old dictionary entry without calling `onLose`, then calls `onGain`. Additive song modifiers may accumulate across refreshes. Fix needs runtime coverage for other statuses before changing this shared lifecycle.
- HP Boost directly sets maximum HP then the container recalculates stats. Persistence through recalculation, refresh, gear changes, expiration and death needs runtime verification; the targeting correction alone does not establish a working HP buff.
- Barrage sets the next Light Shot hit count and the core loops over those hits. Its accuracy penalty, trait values, hit results and consumption still need quantitative verification.
- Song replacement currently removes the previous song before the new effect's success is known. Full status capacity and overwrite rejection need explicit tests.

## Restoration method and acceptance checks

For each action, link verified client IDs/costs/ranges to the existing database command and Lua script. Record dated patch changes separately from player measurements. Recover missing numeric rules from retail observations or reproducible contemporary tests; single endgame measurements do not prove a formula across levels.

Validate prepare/start/finish, costs and cooldowns, target selection, damage or modifiers, ownership, refresh/expiration, equipment and trait effects, death and relog. Tests must cover the real status/command pipeline as well as Lua callbacks. Finish with an in-client self/party/enemy check and packet comparison where available. Keep unverified fields listed rather than declaring an action complete merely because it appears on the bar.

## Verification

New MoonSharp tests execute the actual scripts with controlled actor/status interfaces: three-recipient Battle Voice routing/source and same/other-caster Ballad replacement. These do not emulate the C# stat lifecycle or prove retail potency. No in-client playthrough performed.

All three focused tests passed. Lua manifest regenerated (1,285 files; tree SHA-256 `f48214798b853c0d04194f67973c826707404a0140f7314142dadaa8eb988d27`). Core-only Release packaging completed successfully; changes are included in `bin/build/Release/MacOS/AetherXIV Core.app`. Packaging did not start the server.

## Follow-up: shared status lifecycle implemented

The source-assignment and refresh findings above are now corrected in the existing `StatusEffectContainer`:

- Accepted replacements run the old effect's cleanup before the new gain callback, preventing additive modifier accumulation. Rejected replacements leave the old effect untouched.
- The supplied caster is assigned centrally; owner remains the recipient.
- Null effects and new effects at the 20-effect capacity return false without gain callbacks. Refreshing an existing effect at capacity remains possible.
- Removing an obsolete effect object cannot remove a newer effect with the same ID.
- Existing `onLose` receives an optional fourth `replacing` argument on refresh. Combo/proc cleanup skips terminal behavior during refresh; Bloodletter still removes its DoT modifier but does not cause its terminal damage on refresh. Normal removal retains existing behavior. This extends the current callback contract, not a separate ability system.

The distinction was required by code inspection: `Player.SetCombos` writes the next command IDs before replacing its status. Calling its normal terminal callback on refresh would clear those IDs. Bloodletter's existing removal callback also causes damage beyond modifier cleanup. Their pre-existing numerical mechanics were not revalidated or changed here.

Verification: 20 focused tests passed across `StatusEffectLifecycleTests`, `BardMechanicsScriptTests`, and `LegacyWarningRegressionTests`. Tests cover the real C# container plus Lua callbacks, the actual three song effect scripts and actual expiration via `Update`, as well as refresh-only guards for combo, four proc scripts and Bloodletter. Character stat recalculation is deliberately deferred in these fixtures to isolate status lifecycle from database/world setup. These tests do not establish HP recalculation, visible status packet presentation, death/relog persistence, or complete in-client behavior.

Still outstanding: Battle Voice's direct maximum-HP mutation is overwritten by ordinary base-stat recalculation; its gain/refresh/expiration and gear-change behavior need a separate stat-pipeline correction and tests. Song potency/rounding conflicts, successful-new-song-before-old-song-removal ordering, and broader combat parameters remain open. No speculative numeric formulas or database migrations were added in this follow-up.

Follow-up Core-only Release packaging succeeded. Lua tree SHA-256: `6fd6c671136b3a21f7a6f4f055f19969085b8d94a0e3d0e8d8409999a05269ed`. The server was not started and no in-client validation was performed.

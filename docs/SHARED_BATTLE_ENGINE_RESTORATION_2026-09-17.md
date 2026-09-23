# Shared battle-engine restoration — all actors

## Scope

Restoration covers all seven battle classes, seven jobs, and enemy commands. Rules belong in the existing common command/status pipeline when actor-independent, and existing Lua scripts when action-specific. No second combat engine, action registry or quest format was introduced.

The command snapshot contains 15 entries each for Pugilist, Gladiator, Marauder, Lancer, Thaumaturge and Conjurer, 17 for Archer, five each for Monk, Paladin, Warrior, Bard, Dragoon, Black Mage and White Mage, and nine classJob=0 entries. ClassJob=0 is not proof of enemy ownership. Client descriptions include 1511 IDs not present in this snapshot; these include traits, old variants and noncombat entries, not 1511 confirmed missing combat actions.

## Implemented common fixes

`BattleUtils.TryResist` now stops at the actual FullResist enum boundary. Previously five successful checks could advance into Hit while still treating the result as resistance. Zero-percent resistance now fails even if the RNG returns exactly zero.

`CalculateResistDamage` now makes FullResist absorb the whole amount. Previously it retained 100% of the damage. This is a correction to the engine's full-resistance contract, not a claim that its partial-resistance percentages or rolling model match retail. Those remain explicitly unverified.

Stoneskin processing now prevents negative barrier capacity from adding damage. The redundant second shield call in the spell finish path was removed. The existing placement of shield absorption before other mitigation was preserved; that ordering remains a separate research question.

These paths have no class/job/caster-ownership filters, so the fixes apply to both player and enemy actions that use them. Enemy scripts that bypass these paths still require audit.

## Evidence boundary

The official 1.20 notes describe Stoneskin as a barrier that prevents a fixed amount of damage. Existing status modifiers and original client command descriptions establish the finite-barrier behavior. The engine's HitType enum, resistance text/effect mappings and ActionLanded exclusion establish that FullResist is not a successful ordinary hit. The fixes repair those internal contracts without inventing offensive-stat coefficients.

Sources already preserved locally:
- `evidence/patch-notes-2026-09-17/32606-patch1.20-Patch-1.20-Notes.txt`
- `evidence/patch-notes-2026-09-17/39024-patch1.21-Patch-1.21-Notes.txt`
- `evidence/combat-description-audit-2026-09-17/command-descriptions.json`

[Official 1.20](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes) and [official 1.21](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes).

## Broader policy evidence recovered: crowd control

The 1.21 notes explicitly describe successive successful applications as full duration, half duration, quarter duration, then immunity until the category cooldown expires. Failed applications do not establish a successful application. Categories have distinct cooldowns:

| Category | Seconds |
|---|---:|
| Heavy, Slow, Paralysis, Silence, Blind, Bind, Sleep | 180 |
| Petrification, Pacification, Amnesia | 300 |
| Stun | 30 |

This is strong support for category-based, per-target diminishing returns shared across actions. It is **not implemented by this pass**. Before wiring it into status acceptance, resolve actual status-ID variants/category joins, refresh-versus-new-application handling, timer restart semantics, later-patch exceptions, and death/zoning reset behavior from client/trace evidence. Timers must advance only after successful application, not on resisted casts, rejected overwrite, full status capacity or preparation.

Other shared work remains: offensive-stat scaling, physical/elemental mitigation, partial resistance, accuracy/evasion, crit coefficients, combo eligibility, HP modifiers through stat recalculation and amount/rounding boundaries. Specific tooltips establish many triggers and percentages, but cannot prove missing base curves.

## Verification

25 focused tests passed: BattleResistanceTests, StatusEffectLifecycleTests and BattleCastPresentationPolicyTests. New tests force RNG boundary outcomes, verify full resistance consumes all incoming damage, and test shield capacities above/below damage, zero and negative values. They exercise production helpers. Existing cast tests cover distinct player and enemy presentation policies. No claim of full client combat verification or recovered retail balance is made.

No database changes or new potency values were introduced. The previous all-100 command potency snapshot remains an explicit unresolved limitation.

A read-only database candidate export is saved as `evidence/combat-description-audit-2026-09-17/crowd-control-status-candidates.tsv`. Exact-name candidates are Slow 223003, Petrification 223004, Paralysis 223005, Silence 223006, Blind 223007, Pacification 223013, Amnesia 223014, Stun 223015, Sleep 228001, Bind 228011, Heavy 228021. Prefix matches `slowcast` and `blindside2` are explicitly excluded from those category assignments; the export is a discovery aid, not an activated policy table.

Core-only Release packaging completed successfully into `bin/build/Release/MacOS/AetherXIV Core.app`. The server was not started; no in-client battle test was performed.

Update: the crowd-control policy described above has now been implemented in the normal status-acceptance path. See `CROWD_CONTROL_RESTORATION_2026-09-17.md` for source-backed mappings, tested behavior, fixed-window interpretation and persistence/client-verification limits.

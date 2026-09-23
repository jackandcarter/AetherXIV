# Patch-note-backed stat restoration

## Scope and source coverage

Retrieved all 23 released patch-note threads listed in the official legacy Patch Notes forum, covering **1.16a through 1.23b**. Multi-post threads were requested with `pp=100`; extracted post bodies, URLs and response SHA-256 hashes are under `evidence/patch-notes-2026-09-17/`. Reproduce with `tools/Universal/research-legacy-patch-notes.py`.

This is a focused review of attribute, HP/MP, class/job and equipment rules, not implementation of every feature described in those notes. Planned-change threads and recipe-only announcements are not counted as released patch-note threads.

Coverage limitation: the official [Past Patch Notes index](https://forum.square-enix.com/ffxiv/threads/5114-Past-Patch-Notes) links 1.16, 1.15b, 1.15a and the November/December 2010 updates to retired pages. All five tested links returned 404. Those documents are not claimed as reviewed. Earlier physical-level-era formulas would also need explicit validation against the 1.19 overhaul before reuse.

## What is now established

| Patch | Relevant conclusion | Application |
| --- | --- | --- |
| 1.16a; 1.17/a/b/c; 1.18/a/b | Historical context predates the automatic class-level growth overhaul. The 1.17 party HP/MP bonuses are also a reminder that displayed totals can include non-base effects. | Do not treat early-era character totals as late-1.x growth rows. |
| 1.19 / 1.19a | Physical levels are replaced by class-level growth, including crafting/gathering. Starting race differences remain; HP growth changes around level 35. | Confirms the current level-one fallback is incomplete, not a valid level-30 model. |
| 1.20 / a / b / c | Allotment and per-stat caps are specified; MP growth and the Gladiator/Marauder HP relationship change. | Verify existing allotment arithmetic; correct class eligibility. Numerical growth coefficients remain missing. |
| 1.21 / a | Jobs change base attributes while retaining base-class allocations. A guild-NPC/item procedure resets allotment. | Keep existing class-scoped allocation ownership; prevent refunding points through ordinary allotment. |
| 1.22 / a / b / c | Equipment/materia adjustments must be considered separately from base growth. | No replacement general growth formula recovered. |
| 1.23 / a / b | No replacement numerical class/level growth table was located in this review. | Do not interpret absence of a formula as proof of ARR equivalence. |

Primary sources and interpretation:

- [1.19](https://forum.square-enix.com/ffxiv/threads/24910-patch1.19-Patch-1.19-Notes): attributes grow automatically with class level; race differences concern starting attributes. Maximum HP grows more strongly around level 35. This establishes structure, not coefficients or a complete curve.
- [1.20](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes): War/Magic classes receive five allocation points at level 10 and one each subsequent level; Hand/Land receive none. Per-stat caps run from 3 at levels 10–11 to 23 at 50. VIT affects maximum HP and PIE maximum MP, but no numeric conversion is supplied. Marauder/Gladiator HP bases and the MP curve changed. These facts do not justify choosing a VIT/PIE multiplier or borrowing ARR values.
- [1.21](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes): job base attributes differ, while allocated points carry over from the base class. Selected HQ bonus parameters are enhanced, not necessarily every bonus. A crafting-class HP/MP level-up defect was fixed. Existing job allocation ownership and selective HQ processing should therefore be preserved.
- [1.21a](https://forum.square-enix.com/ffxiv/threads/40824-patch1.21a-Patch-1.21a-Notes): resetting the current class's allocation requires a Keeper's Hymn exchanged through a guild NPC. This is distinct from undoing uncommitted widget edits.

## Applied changes

The existing Player implementation remains the sole allocation owner. No parallel stat calculator or new database schema was added.

1. `GetAttributePoints` now returns zero allowance, zero cap and zero displayed allocation for non-War/Magic classes. Previously, a high-level crafter/gatherer received a level-derived allowance even though the setter and stat layer rejected/ignored it. Invalid persisted entries are not deleted.
2. `TrySetAttributePoints` rejects any reduction of a committed attribute allocation before database access. The diagnostic reason is `reset-required`. This also applies when playing the associated job, whose allocation belongs to the base class.

Client corroboration: decoded `BonusPointCommand.operateUI` (source lines 195–242) subtracts existing allocations from earned points, opens the widget, and adds nonnegative widget increments to prior allocations. `BonusPointAssignWidget` decrements only the current window's added points; its Undo restores that window's defaults. Therefore the new server guard matches the client's normal request contract rather than breaking legitimate Undo behavior. `available` continues to mean total earned points at the outer command boundary: changing it to remaining points there would subtract spending twice.

The existing `PopulaceGuildShop.lua` contains the reset menu branch but its implementation is still TODO. **This change does not claim to restore the Keeper's Hymn transaction.** The normal retail widget already cannot refund committed points; this closes the server-side bypass. No item was consumed and no live allocation was modified.

## What was deliberately not applied

- No guessed level-30 Thaumaturge/Archer rows, interpolation, ARR growth curve, or new HP/MP multipliers.
- No changes to historically applied migration files or database verification history.
- No automatic test-character hotbar grant or character reset.
- No recreation of equipment/job systems already present.

The core level-appropriate base-stat gap remains. The next useful evidence work is to isolate gear, status, trait and allocation contributions around the recovered within-capture Weaver level 3→4 transition, then validate candidate class-growth models against other retail checkpoints. Raw displayed HP/MP must not be inserted as profile contributions without accounting for later VIT/gear/trait additions.

## Verification

New tests cover all levels 1–50 for the published allocation/cap schedule; all 11 crafting/gathering classes; invalid legacy allocations; and committed-point refund rejection on Thaumaturge and Black Mage. Existing class/job allocation round-trip tests remain in place. Build/test outcome is recorded in the task handoff; live-client testing is still needed.

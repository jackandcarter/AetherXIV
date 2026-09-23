# Restoration application and verification — September 17, 2026

## Applied: Q’zamqo

Applied `20260916_000040_gridania_qzamqo_restoration.sql` to local `ffxiv_server`, including its SHA-256 migration-ledger entry in the same transaction. Backups are in `.local-evidence/restoration-backups/20260917-qzamqo/`.

The previously missing NPC now has class 1001711, display-name 1900211, PopulaceStandard class path, captured interaction flags/conditions, and exactly one spawn (1071) in Gridania territory 155 at (38.89, -10, -1185.37), rotation 1.54. Existing appearance and client-backed default dialogue supply the remaining previously verified pieces. No travel or special quest behavior was invented.

Post-application database checks passed: one spawn, matching transform, class/name/flags, valid event JSON, and recorded checksum. The six isolated migration checks also passed again, including idempotency, collision rejection, preservation of existing placement, and retail packet coordinate comparison. The operator migration compatibility test passed.

**Not yet verified in the game client.** No running core Map/World/Lobby process was found in the process-name check, and no server sessions were present before application. The change takes effect when the server next loads its actor data. The remaining manual check is to visit Q’zamqo at Gridania's airship landing, confirm appearance and positioning, start default dialogue, and close the conversation without losing control.

## Newly verified enemy parameter semantics

The verifier binds method names to their actual Lua child functions in the shipped `GameCommandBaseClass` bytecode and checks the initial sheet read and argument register. Script and function-code hashes are preserved in `evidence/enemy-ids-2026-09-17/verified-base-getters.json`.

| Method | Sheet | Original column | Interpretation limit |
|---|---|---:|---|
| getRange | gameCommand | 64 | Base range getter; do not infer override policy |
| getBestRange | gameCommand | 65 | Preserve -1 sentinel |
| getRecastTime | gameCommandBasic | 79 | Unit conversion and effective runtime scheduling still need checking |
| getCastTime | gameCommandBasic | 76 | Actor overrides are applied by the method |
| getCommandMPCost | gameCommandBasic | 114 | Passed through actor.calculateCommandCost; not a flat MP cost |
| getCommandTPCost | gameCommandBasic | 115 | Base TP-cost getter |

For Foul Bite 23144, Devastate 23198, Decimate 23199, Pulverize 23200, and Raging Horn 23273, these getter inputs are respectively range 6, best range -1, recast 3, cast time 1, MP-cost input 0, and TP cost 1000. All 21 candidate command rows are exported with `runtimeApproved=false`.

An external decompilation writeup was useful as a search lead but mislabeled columns 114/115 as HP/MP. The installed bytecode instead binds them to MP/TP; the base HP-cost method returns zero. This is why research prose alone must not drive migrations. MonsterAttackWeaponSkill also contains per-ID information and method overrides; those must be resolved before treating base getter values as complete effective combat behavior.

## Corrections applied to the evidence tools and reports

- Corrected the earlier `validUser=0` interpretation: the server enum is **All=0, Player=1, Monster=2**. The saved audit and report now reflect this. Duplicate labels still prevent blindly selecting Pummel, Heavy Thrust, or Full Thrust variants.
- Corrected three defects in the local LPB disassembler's displayed instructions: sign extension in jump targets, inverted equality skip descriptions, and LOADNIL register bounds. Focused checks pass. These are research-tool corrections, not game bytecode changes.
- Added `tools/Universal/verify-legacy-command-getters.py`; it rejects unexpected method bindings, data sources, and argument registers. Two synthetic tests and the actual-client verification pass.

## Why the new enemy attacks are not enabled yet

There is still no verified complete definition for damage, action targeting/shape, animation/effect dispatch, or exact enemy variant placement. The inspected packet corpus has no command-result execution joined to any of the 26 target actor-class candidates. Adding a battle-command row now would require filling mandatory fields with guesses. The existing generic damage code is not independent proof of historical values.

The next bounded recovery target is MonsterAttackWeaponSkill's per-command return tuple and its consumers, followed by the cast/recast unit conversions. Then match the actor variant to class path/appearance and placement evidence. A complete verified subset can be activated without waiting for every enemy to be recovered.

## Validation summary

- Six isolated migration checks: passed.
- Five post-application database checks: passed; saved in `qzamqo-live-application.json`.
- One .NET operator compatibility test: passed.
- Nine Python regression tests (combat logs, sheet reader, getter verifier): passed.
- Six getter bindings across 21 actual-client candidate rows: verified.
- Three disassembler regression checks: passed.
- In-client NPC interaction and new enemy combat: **not verified**; no new enemy attacks enabled.

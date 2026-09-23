# Ability unlock restoration — 2026-09-17

## Result

Implemented automatic recovery of missing earned class actions and client-evidenced job-action rewards. This change addresses acquisition/hotbar availability; it does not certify combat potency, status effects, or full job-quest playability.

The existing database already contains 107 native actions across seven battle classes and 35 job actions. Parsing official 1.20/1.21 tables found 142 action records: 141 name/class/level matches; the remaining source name is “Simian Thrust” whereas the existing Pugilist row is `simian_thrash`. That discrepancy is recorded without changing the row or asserting a new ID.

No command-level SQL migration was necessary. The defect was missing catch-up and job reward wiring.

## Runtime changes

- Login, class/job changes, level-up, completed quests and quest replacement reconcile missing native actions up to the character's existing base-class level, capped at 50.
- Jobs inherit earned base-class actions. The first job action requires level 30, the existing secondary-class qualification and the soul crystal. Later actions additionally require the exact recovered completed quest and their action level.
- All 35 job reward associations were recovered from client `showGetJobAbilityWidget` calls. This matters: Paladin, Warrior and Dragoon level-45 rewards refer to their fifth quest, while Bard's reward is in its fourth. Quest ordinal alone is not a safe rule.
- Inserts preserve saved slots and recasts; repeated loads are idempotent. Level/quest refresh also preserves active in-memory cooldowns. Full bars are not overwritten; missing actions wait for free slots and another refresh.
- Job changes clear stale commands from the previous in-memory bar.
- Manual action setting uses the same acquired-action gate. Native actions are not blocked by a full cross-class quota; job/base actions cannot be removed through that menu.
- Inventory and completed quest state are loaded before login reconciliation.

## Archer

| Level | Action | Existing command ID |
|---|---|---|
| 1 | Heavy Shot | 27228 |
| 1 | Refill | 27258 |
| 1 | Light Shot | 27259 |
| 2 | Decoy | 27222 |
| 4 | Piercing Arrow | 27233 |
| 6 | Hawk's Eye | 27220 |
| 10 | Leaden Arrow | 27229 |
| 14 | Raging Strike | 27225 |
| 18 | Shadowbind | 27236 |
| 22 | Quelling Strike | 27221 |
| 26 | Swiftsong | 27226 |
| 30 | Gloom Arrow | 27234 |
| 34 | Barrage | 27224 |
| 38 | Quick Nock | 27231 |
| 42 | Chameleon | 27223 |
| 46 | Bloodletter | 27235 |
| 50 | Wide Volley | 27230 |

## Bard

| Level | Action | Quest evidence | Command ID |
|---|---|---|---|
| 30 | Ballad of Magi | A Song of Bards and Bowmen (111301, brd0j1) | 27237 |
| 35 | Minuet of Rigor | The Archer's Anthem (111302, brd0j2) | 27239 |
| 40 | Paeon of War | Bard's-Eye View (111303, brd0j3) | 27238 |
| 45 | Rain of Death | Doing It the Bard Way (111304, brd0j4) | 27232 |
| 50 | Battle Voice | Requiem for the Fallen (111306, brd0j6) | 27227 |

The same mechanisms cover Pugilist/Monk, Gladiator/Paladin, Marauder/Warrior, Lancer/Dragoon, Thaumaturge/Black Mage and Conjurer/White Mage. Crafting/gathering grants were not inferred from these combat tables.

## Evidence and reproduction

- `evidence/ability-unlocks-2026-09-17/action-level-audit.json`: each parsed table entry, source line/hash, database matches and comparison result.
- `evidence/ability-unlocks-2026-09-17/job-quest-rewards.json`: all 35 quest/action links and client disassembly locations.
- `evidence/ability-unlocks-2026-09-17/client/index.json`: original client script paths and SHA-256 hashes. Decoder checks complete byte consumption. Client build: 2012.09.19.0001.
- Read-only tools: `tools/Universal/audit-ability-unlock-levels.py` and `tools/Universal/recover-job-action-rewards.py`; run with `/usr/bin/python3` from project root. They use the existing local MariaDB command catalogue, quest name catalogue and LPB decoder.
- [Official 1.20 tables](https://forum.square-enix.com/ffxiv/threads/32606-patch1.20-Patch-1.20-Notes)
- [Official 1.21 job tables and acquisition rules](https://forum.square-enix.com/ffxiv/threads/39024-patch1.21-Patch-1.21-Notes)

## Verification and deployment

38 focused .NET tests passed: policy level boundaries, all 35 job gates, manual Lua action setting, existing progression/equipment checks, plus an actual isolated MariaDB test of `Database.LoadHotbar`. The database test confirms catch-up, persistence, idempotence, layout/recast preservation, stale-slot clearing and the next level boundary. The disposable database/server was shut down afterward; no live character rows were changed by tests. Lua syntax and source diff whitespace checks passed.

After the user closed Core, the normal `tools/MacOS/build-core-only.sh Release` packaging process completed successfully. Updated app: `bin/build/Release/MacOS/AetherXIV Core.app` (2.1.0, build 22042). Packaged Lua manifest and app code signature verified. The packaged manual-action script matches source, and the Map assembly includes the unlock policy and reconciliation implementation. Map DLL SHA-256: `d8a3707ad7069733008db75944c94ea3ea0b3b0e591436cd09cd3421c2ab92d4`. No SQL migration or live character edit was required; backfill runs when the updated server loads the character. Open the rebuilt Core app, start the servers and log in.

Not yet verified in the actual game client. Test Archer at levels 1/2/4/6/10, switch to a qualified Bard, verify Ballad appears and the four later actions stay gated until their associated quest is completed. Re-login and switch jobs to confirm persistence. Existing combat scripts include uncertain formulas; action availability must not be described as full combat restoration. The recorded reward mapping does not itself implement missing quest NPC flows or grant unearned quest completion.

# Enemy combat recovery — 2026-09-17

## Result

Expanded the previous 339 ready/link observations into **2,430 attributable log events** for six enemy names. Recovered **21 named enemy–action relationships**, up from eight: **13 additional relationships**. These are recovered behavioral specifications, not deployed enemies.

| Enemy | Observed named actions | Additional enemies reported joining |
|---|---|---|
| Watchwolf | Foul Bite | 2, 3, 4 |
| Scarred watchwolf | Foul Bite | 3, 4 |
| Zahar'ak drubber | Concussive Blow II, Decimate, Devastate, Flurry, Heavy Strike, Light Strike, Pulverize, Pummel | 2, 3 |
| Zahar'ak halberdier | Decimate, Devastate, Full Thrust, Heavy Thrust, Light Thrust, Pierce, Skewer II | Not observed in extracted events |
| Battle drake | Burning Cyclone, Caudal Spine, Raging Horn, Smoulder | Not observed in extracted events |
| Young raptor | No named action recovered by this pass | 4 |

The extractor now includes hits, misses, parries, and blocks. Every listed named action has a completed outcome observation; the old pass only captured readiness. Positional suffixes are stored separately, so “Foul Bite from the left” is not counted as another action. Ordinary “attack” events are retained but excluded from the named-action total.

## What this changes for restoration

1. **Enemy-specific action lists can now be reconstructed as specifications.** Do not assign all humanoid or wolf abilities by genus. The drubber and halberdier lists differ, and shared action names do not establish shared numeric IDs.
2. **Call for help is a linking-system recovery target.** Square Enix's October 14, 2011 systems document describes nearby enemies linking when called and distinguishes that from summoned pets. It also mentions party size/member levels and planned 1.20 type restrictions. The document does not supply a complete 1.22/1.23 selection formula. Source: [official systems document, page 1](https://gdl.square-enix.com/ffxiv/download/en/FFXIV_2.0_Systems_and_Content_EN.pdf).
3. **Defensive outcomes are part of the recovered behavior.** Preserve misses, blocks, partial parries, critical hits, and zero-damage outcomes when validating future implementations. Observed damage is a result against a particular target, not base potency.

## Runtime audit: why no live enemy changes were made

- The five initially recovered ability names have no matching entries in the local `server_battle_commands` table.
- Across all 21 enemy/action relationships, only Pummel, Heavy Thrust, and Full Thrust have exact normalized-name database candidates. Correction: all three candidates are marked `validUser=0` (**All**, per `BattleCommandValidUser`), not Player-only. They are **not verified enemy command IDs** and were not repurposed.
- Existing battle pools contain generic and opening-story wolves, but no pools named for these six observed enemies. A wolf family match does not identify a watchwolf. Do not change the opening encounter to imitate these logs.
- `BattleNpcController` can select listed commands, but the pool `linkType` occurs in loader SQL without a corresponding enemy-link implementation found in the combat runtime search.
- The checked client Wolf/Raptor standard and base scripts declare inheritance; they did not provide the missing action IDs, AI parameters, or spawn coordinates. Their paths, hashes, and symbols are recorded separately.

Deployment needs an independently verified actor identity, enemy action IDs, animation/target metadata, damage/cost/timing semantics, and valid placement or encounter ownership. The XML logs cannot supply those fields. No guessed IDs, cooldowns, spawn counts, or damage values were inserted into the live database.

## Evidence and reproducibility

Run from the repository root:

```sh
/usr/bin/python3 tools/Universal/mine-legacy-combat-logs.py \
  'FFXIV Parse/ParseModXIV/Logs/ParseMod' evidence/npc-recovery-2026-09-17
/usr/bin/python3 tests/tools/test_mine_legacy_combat_logs.py
```

Outputs in `evidence/npc-recovery-2026-09-17/`:

- `combat-events-expanded.json`: source file, one-based XML Line-element index, logged time/channel, enemy, action, outcome, observed damage where present. No target/player names or unrelated chat exported.
- `enemy-profiles.json`: observed action lists and reported link counts; unresolved runtime IDs/placements explicitly null.
- `combat-source-manifest.json`: SHA-256 hashes, source row counts, exclusions.
- `runtime-command-audit.json`: all 21 relationships compared with the local command catalog; candidate IDs are explicitly unapproved.
- `client-script-checks.json`: four checked client scripts and their findings.

Counts: 1,679 hits, 165 misses, 223 parries, 18 blocks, 287 readiness messages, 58 link messages. These are **event lines**, not 2,430 unique attacks, enemies, or independent confirmations. The same attack may produce several lines. Compared with the old pass, six additional drubber link messages were recovered because its earlier link regex excluded capitalized enemy names.

Eight main logs were parsed; four derivative Unmatched logs were excluded. Byte-identical main files are also excluded. Source dates are filename-based May–June 2012, not verified client builds. The extractor deliberately targets these six names and does not claim complete coverage of all enemy names or message grammars in the archive. Five synthetic regression tests passed, covering attribution, positional normalization, privacy, link counts, and zero damage; the corpus checks also reproduce the original 287 readiness events.

## Next evidence needed

Match these exact display names and action labels to client string/table IDs, then join those IDs to retail actor/action packets. The useful next trace is a fight with a watchwolf, Zahar'ak combatant, or battle drake that contains both actor initialization and action execution. That can connect the recovered action lists to numeric commands and actual enemy actors. For linking, recover the caller and joining actors across the event before implementing selection rules or XP bonuses.

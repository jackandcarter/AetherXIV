# Damage system: current implementation and recoverable evidence

## Main finding

The server has a working action pipeline, but not a verified retail 1.23b damage model. All 151 live `server_battle_commands` rows currently have basePotency 100. This is a raw starting amount in many scripts, not an established retail potency scale. Fire and Doom Spike override it with 5000. Burst's low-HP combo callback is empty. Gameplay success does not establish correct damage or stat scaling.

This audit is read-only with respect to gameplay: no balance numbers or formulas were changed.

## Current calculation

1. The command script sets `action.amount`, usually from `skill.basePotency`. Combo callbacks may alter that value, hit count, status, recast or enmity.
2. `CommandResult.DoAction` computes hit/crit/block/parry/resist chances. Defender and caster pre-action status callbacks can then alter the amount or chances.
3. Physical actions check miss, apply Stoneskin, then check critical, block, and parry in that order. Spells check resistance and then critical. Spell handling currently calls Stoneskin twice; this needs a regression test.
4. Physical mitigation subtracts `k × defender Defense`. Spell mitigation subtracts `k × (Defense + 0.67 × Vitality)`. Both then multiply by `1 − DamageTakenDown/100`, truncate to integer and clamp to 0–9999.
5. The target loses HP; damage-dealt/taken callbacks and explicit additional-status calls run. Multi-hit commands repeat the per-hit path. Damage over time is handled separately by status regeneration ticks.

Here `d = defender level − attacker level`, and current `k` is:

- d >= 0: clamp(0.35 × d + 0.225, 0, 1)
- d < 0: clamp(0.01 × d + 0.25, 0, 1)

This is explicitly marked as a simplified approximation in the source. It should not be presented as a recovered SE formula. At equal levels, 100 raw damage against 100 Defense becomes 77 after integer conversion, absent other adjustments. This example describes implementation only.

Autoattacks use `AutoAttackPotencyPolicy`: weapon damage plus virtual-ammunition damage if available; otherwise rounded actor Attack, bounded 1–9999. It deliberately defers retail class-stat coefficients. Ordinary weaponskills and attack spells generally do not incorporate weapon damage, Attack, STR/DEX/PIE or magic attack potency in their initial damage. Some individual scripts and status callbacks do use stats. Logging a stat is not proof that the formula uses it.

### Current rates and modifiers

- Hit chance starts at 80%, adjusts raw hit/evade modifiers and command accuracy modifier, and clamps to 0–100. Ordinary Accuracy/Evasion are not used here.
- Critical chance starts at 10%; adds 0.16 × command bonusCritRate and raw critical-rate modifiers. Ordinary critical rating is commented out.
- Critical damage multiplies by clamp(0.04 × d² − 2 × d + 1.20, 1.15, 1.75). Crit potency/resilience contributions are commented out.
- Parry starts at 10% plus 0.1 × Parry and raw parry rate; unavailable from the rear or with blocking enabled. A parry retains 75% of damage.
- Block strength uses Block and Vitality; Aegis Boon forces a full block. This is an implementation, not fully validated retail evidence.
- Resist chance defaults to 15% plus raw resist modifier for eligible actions. Resist damage code currently retains 25%, 50%, etc. as tier increases, contradicting its own reduction comment. Tier iteration bounds also warrant review before changing it.
- Physical/elemental resistances remain TODOs in the final mitigation routines. Status application has its own probability path.

## Damage categories

There are separate axes, not one interchangeable damage-type list:

| Axis | Current representation |
|---|---|
| Command delivery | Autoattack, weaponskill, ability, spell |
| Result processing | Physical, magic, heal, status |
| Physical property | Slashing, piercing, blunt, projectile |
| Elements | Fire, ice, wind, earth, lightning, water |
| Ongoing/direct effects | Bleed/poison and other DoTs; HP drain, healing and direct script HP changes |

Higher property IDs have conflicting comments/names for astral/umbral/healing versus sonic/breath/neutral. Their semantics need client getter/packet verification. Do not assume enum labels are authoritative. Merely defining a resistance modifier does not make that resistance affect damage.

## Client descriptions: what they prove

The installed September 2012 client contains English `xtx/command` schema 189071383. Column 2 is name, column 23 is description. Export retains command ID, raw control characters, data-file ID and row offset. Schema/data hashes are recorded.

Examples read directly from client:

- Heavy Shot: eight-yalm minimum for combo use.
- Bloodletter: bleed chance; combo after Gloom Arrow adds damage when bleed ends.
- Rain of Death: area attack; combo after Quick Nock adds a stun chance.
- Barrage: accuracy reduction and a multifold next Light Shot. Enhanced Barrage adds one attack.
- Thundara: combo trades stun for increased damage and reduced recast. Text does not prove the script's 1.5 damage multiplier or half recast.
- Burst: combo damage rises as current HP falls. Current callback is empty; exact curve is absent from this description.
- Raging Strike: bow attack stacks up to three from Light Shot hits, enmity increase, reset on bow miss. Current effect script increments tier but has an empty miss callback and no damage bonus calculation there.
- Enhanced Hawk's Eye: 50% increase to accuracy gained. This does not by itself identify the base accuracy amount or the resulting hit probability.
- Cleric Stance: attack spell potency +20%, healing potency −20%. The current script changes magic-potency modifiers, but the generic attack-spell damage path does not consume that stat.

Blood for Blood's script explicitly labels its 10% base damage increase as a guess. A trait describing a 25% improvement does not prove a 25-percentage-point increase; distinguish relative effect enhancement from absolute damage increase.

## New evidence artifacts

Run `tools/Universal/audit-client-combat-descriptions.py` with `/usr/bin/python3`. It reuses the existing typed decoder and reads all command labels, including traits, legacy variants, enemy commands and noncombat entries. There are 1662 rows, 1661 non-placeholder descriptions. Lexical search tags find 40 combo, 192 numeric and 22 critical-related descriptions; these are search counts, not independently verified mechanics or distinct playable abilities. The script also indexes rule assignments in 102 Lua files, retaining line numbers and hashes.

Outputs under `evidence/combat-description-audit-2026-09-17/`:

- `command-descriptions.json`: original client descriptions and provenance locations.
- `server-script-rules.json`: searchable implementation assignments.
- `provenance.json` and `summary.json`: reproducibility and scope.
- `server-command-snapshot.tsv`: read-only live command metadata snapshot.

## Period research recovered

[Kaeko, physical damage taken, February 21 2012](https://kanican.livejournal.com/55915.html) reports controlled DEF/VIT and enemy-level tests, approximately ±8% physical damage variation, level-dependent defense slopes and damage floors. Those results offer measured constraints but are from before 1.21 and include estimated points; they do not justify our current simplified coefficient as exact.

[Seiken, Archer STR/DEX/PIE/ATK testing](https://forum.square-enix.com/ffxiv/threads/35795-STR-DEX-PIE-ATK-Testing) includes an explicit 1.21 update: DEX and PIE both affect weaponskill damage, with weapon/target-specific reported caps. Earlier claims in that same post differ. Preserve the patch boundary rather than combining incompatible conclusions.

The magic-testing URL referenced by old code (`https://kanican.livejournal.com/55370.html`) failed to load during this pass; its assertions are not newly verified here.

## How to turn this into a defensible battle model

1. Join command IDs and descriptions to client command/basic-command metadata using verified getter mappings; include traits, weapon/ammo stats and status definitions. Do not guess meanings of unknown columns or multiply them into damage.
2. Build action-by-action comparisons: described behavior, current implementation, retail packet observations, period measurements, unresolved values. An empty callback and a guessed multiplier are different restoration problems.
3. Recover original testing tables/graphs and their patch, attacker level, weapon, stats, target ID/level and buffs. Fit damage scaling only where inputs are sufficiently known. Existing parser damage observations with unknown equipment/target defenses cannot uniquely determine all coefficients.
4. Match retail result packets/logs to ability IDs, hit outcomes, number of hits, costs and status timing. Separate normal/critical/resisted/blocked hits and combo/noncombo samples.
5. Test candidate formulas against withheld observations and ranges, not just averages. Establish rounding, variance, caps/floors and multiplication order separately. A single action at level 50 is not enough to extrapolate all jobs and levels.
6. Fix provable pipeline defects with regression tests independently of disputed balance coefficients. Then verify in client. Current traces are valuable evidence, but running the emulator against itself only verifies implementation consistency, not historical correctness.

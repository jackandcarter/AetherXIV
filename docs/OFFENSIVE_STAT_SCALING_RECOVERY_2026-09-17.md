# Offensive-stat scaling recovery

Continuation: [resistance and Decoy restoration](RESISTANCE_DECOY_RESTORATION_2026-09-17.md)
records the later actionable finding and runtime changes. The initial pass below
did not change offensive coefficients.

## Result

Recovered authoritative stat relationships and conditional physical measurements,
plus original magic experiments. These constrain a future formula, but **do not
yet identify a complete 1.23b offensive damage formula**. No runtime coefficients,
SQL damage values, or packaged binaries were changed by this investigation.

Target client: 2012.09.19.0001. Earlier experiments are versioned evidence, not
automatically applicable constants. Enemy scaling remains independently unresolved.

## Established relationships

[Official patch 1.20](https://forum.square-enix.com/ffxiv/threads/32606)
identifies the following attributes for autoattack damage, including archer Shot:

| Class | Contributing attributes (not coefficient order) |
|---|---|
| Pugilist | INT, STR |
| Gladiator | MND, STR |
| Marauder | VIT, STR |
| Archer | DEX, PIE |
| Lancer | PIE, STR |
| Conjurer | MND, PIE |
| Thaumaturge | MND, PIE |

The same notes connect STR to Attack Power and INT to Attack Magic Potency.
They supply neither numerical conversion rates nor the complete damage formula.
The caster rows concern weapon attacks, not offensive spells.

[Official patch 1.21](https://forum.square-enix.com/ffxiv/threads/39024)
explicitly changes attribute contribution to weaponskill attack power and increases
its maximum influence. Therefore January 2012 WS results cannot be transferred
unchanged into this client.

[Official patch 1.22](https://forum.square-enix.com/ffxiv/threads/43599)
defines the displayed bow DPS as weapon damage plus the strongest normal-quality
ammunition damage at the required level, divided by delay. This is a tooltip
definition; it does not establish that weaponskill damage equals displayed DPS.

Local, previously archived patch text is under `evidence/patch-notes-2026-09-17/`.

## Physical measurements: useful within their conditions

[Seiken, March 26, 2012, post 20](https://forum.square-enix.com/ffxiv/threads/36412-STR-PIE-ATK-Testing?p=607045&viewfull=1)
tested level-52 Zahar'ak Halberdiers, normally with Ifrit weapons, and only one or
two weaponskills per job. Reported damage changes per added stat were:

| Input | Observed damage change | Reported boundary |
|---|---:|---|
| STR, tested DRG/MNK/WAR WS | about 0.8/point | soft cap near 350 |
| STR above that boundary | 0.23–0.25/point | attributed to 3 STR : 2 Attack |
| PIE, tested DRG WS | 0.68–0.70/point | hard cap near 310 |
| Attack, tested AA and WS | 0.35–0.37/point | no obvious cap in this test |

DRG autoattack PIE cap was bracketed at 273–291; 285 was extrapolated.
MNK INT and WAR VIT were reported to behave similarly to DRG PIE.
Sampling used damage extrema, accepting approximately 7.7% spread from the midpoint.

These are target/weapon/action-specific observations, not global constants. They
provide no universal intercept, level curve, rounding order, or enemy coefficient.
The STR result includes its indirect Attack contribution; adding both measured
slopes independently could double-count that effect.

## Magic: original experiments and rejected shortcuts

[Deli's 1.20 testing, December 2011, posts 4/9/10](https://forum.square-enix.com/ffxiv/threads/33403-1.20-Attack-Magic-Test)
includes a 150-kill level-50 Kobold Prelate series with criticals excluded.
Reported mean changes for Thunder/Thundara/Thundaga respectively:

| Equipment change | Mean damage changes |
|---|---|
| +11 INT | +11 / +42 / +16 |
| +7 INT, separate test | +11 / +26 / +14 |
| +8 magic potency | +7 / +28 / +24 |

Later posts describe 810+ raptor kills varying potency and target level, followed
by INT testing. The original author distinguished base-spell and combo gains.
Linked Google data could not be retrieved in this pass. Without the individual
samples and complete input vectors, these differences cannot establish a precise
coefficient, cap, or error interval. Added INT may itself alter magic potency.

[January's “INT working as intended” experiment](https://forum.square-enix.com/ffxiv/threads/35257-Chocobo-music?goto=nextoldest)
is unsuitable for a clean coefficient: a reply identifies different critical-hit
proportions between comparison sets. Its aggregate means must not be fitted as
ordinary-hit damage.

The [May discussion quoting Kaeko](https://forum.square-enix.com/ffxiv/threads/44751-What-are-stats-more-of-a-benefit?mode=threaded&p=684494)
is a lead to the original research, not primary proof of its numerical model.
The original blog material was not recovered here. In particular, no claimed
Thundara combo potency constant was adopted.

## Local evidence check

Run `/usr/bin/python3 tools/Universal/audit-offensive-scaling-inputs.py`.
The generated `evidence/offensive-scaling-2026-09-17/input-audit.json` records
input hashes, streams, decoded field counts, and parser-log annotation searches.

- Three existing decoded combat captures were inventoried.
- `combat_autoattack` contains five retained initialization observations, with
  class/level and HP/MP fields but no decoded offensive-stat vector.
- `combat_skills` has no retained observations in this particular stat decoder.
  This does **not** mean its original capture contains no combat packets.
- `party_battle_leve` has 115 retained observations, including some changing
  `generalParameter` fields, but no demonstrated complete offensive-input snapshot.
- Eight original parser logs contain 6,544 XML line elements and no `stats:`
  annotations. This only checks that documented manual-annotation pattern.

Existing enemy command records recover identity and outcomes, not hidden enemy
Attack/attributes. Neither missing fields nor unknown binary values were filled
from emulator defaults. Separate captures were not combined into an assumed
continuous character state.

## Applying this to the existing engine

Current `AutoAttackPotencyPolicy` selects weapon plus virtual-ammo damage when
available; its Attack fallback is used when no positive weapon base exists.
`BattleUtils.CalculatePhysicalDamageTaken` and `CalculateSpellDamageTaken` then
apply the existing approximate defensive rules. Their diagnostic fields do not
make Attack/attributes/magic potency participate in offensive scaling.
Many command scripts use `basePotency` as a raw amount, so changing its meaning
globally would also require auditing those callers. See the damage-system audit.

The next implementation belongs in that existing action pipeline:

1. Recover complete same-actor, same-capture input state before each hit: class,
   level, weapon/ammo/delay, six attributes, Attack, magic/elemental potency,
   active effects, action/combo and target identity/level. Unknown remains unknown.
2. Recover the original experiment sheets or equivalent controlled retail samples.
   Compare Attack-only changes, primary-stat-only changes, and weapon changes
   separately. For magic, use a grid of INT and potency values and separate combo
   from noncombo spells. Preserve critical/resist/multihit distinctions.
3. Test competing physical models (weapon damage versus DPS dependence; attribute
   caps) and magic models (independent versus interacting INT/potency terms).
   Derive parameter ranges, then check samples not used to fit the model.
4. Implement only accepted relationships through shared calculations, preserving
   command-specific behavior in existing Lua callbacks. Keep player and enemy
   applicability explicit; do not assign player weapon caps to enemies.
5. Verify rounding, caps, buffs and cross-class behavior with recovered fixtures,
   followed by an in-client smoke test. Emulator output validates implementation,
   not the historical correctness of the chosen coefficients.

**Immediate highest-value recovery:** reconstruct offensive state inside
`party_battle_leve` and retrieve the original magic/physical experiment tables.
The available evidence supports these focused steps, not a universal fabricated
damage multiplier.

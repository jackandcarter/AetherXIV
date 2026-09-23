# Resistance and Decoy restoration

## Finding and scope

The offensive-scaling investigation recovered the original
[Kaeko resistance study](https://kanican.livejournal.com/55370.html) by direct
HTTPS retrieval after the search browser could not load it. The study reports
three partial-resist cuts: 25%, 50%, 75%. It separates those outcomes from full
avoidance, including Decoy. It does not supply a precise magic-accuracy formula.
This is pre-1.21 experimental evidence; inspected later patch notes did not name
a change to these cuts, but that does not constitute independent 1.23b testing.

Installed-client descriptions independently confirm Decoy's single ranged/magic
attack avoidance and Enhanced Decoy's additional melee coverage. Their command
IDs are 27222 and 27244. Database status rows 223108/223238 already use the proper
pre-action target callback flag (flags 4113); no SQL migration was needed.

## Restored through existing code

| Outcome | Old damage from 400 input | Restored damage |
|---|---:|---:|
| Single partial resist | 100 | 300 |
| Double partial resist | 200 | 200 |
| Triple partial resist | 300 | 100 |
| Explicit full avoidance | 0 | 0 |

These values describe the resistance stage, before the existing later defensive
calculation. Damage-stage order remains unverified. Integer damage still truncates;
mitigated amount is now the exact difference so odd inputs conserve their total.

- Ordinary resistance selection stops at three partial tiers. Full avoidance is
  explicitly requested on the action rather than manufactured with 400%/750%
  resistance rates. The existing rate-halving probability approximation remains;
  it is not a recovered retail distribution.
- Both Decoy scripts remove the effect from the actual target. Previously they
  referenced undefined `defender`, causing a Lua failure at consumption.
- Physical resolution uses the hit rate already calculated and adjusted by
  pre-action effects. Previously recalculation discarded Decoy's zero hit rate.
- Normal Decoy handles hostile magic and physical commands marked ranged;
  Enhanced Decoy additionally handles melee. Friendly actions, healing and
  status-only actions do not consume it. Missing command objects no longer throw.
  Ranged coverage still depends on existing command metadata; this pass does not
  reconstruct absent ranged flags on enemy/autoattack commands.
- Full spell avoidance preserves Stoneskin instead of consuming its capacity.
- Triple resists now fail the same landed-action gate as other resists. Resisted
  spells no longer set the combo-success visual flag.

`CommandResult.forceFullResist` is internal action state exposed to existing Lua
callbacks. Packet layouts are unchanged. The existing physical miss and magical
full-resist packet outcomes are reused; exact retail Decoy animation/message
matching has not been confirmed in a dedicated capture.

## Verification

75 focused tests pass across BattleResistance, DecoyMechanics, StatusEffectLifecycle,
BardMechanics, CrowdControlResistance and LegacyWarning suites. Tests cover:

- Every partial tier, full avoidance, odd-input conservation and unsupported hit types.
- Three successful resistance rolls stop at triple; zero resistance cannot succeed.
- Explicit avoidance bypasses RNG, and physical pre-action rates are honored.
- Actual Decoy Lua + real status container: eligible/ineligible attack categories,
  one-hit consumption, friendly actions, and missing commands.
- Spell finishing preserves Stoneskin and suppresses combo success on avoidance.

The spell-finish test uses inert Character instances and stops at the attackability
gate after resolution; it is not an in-world damage test. No live client battle
was performed. Evidence and the test log are under
`evidence/resistance-decoy-2026-09-17/`.

Release Core packaging succeeded (2.1.0, build 22042). Packaged Map SHA-256:
`5f3c1bd2bbd31684a6fe4d514cd5375c4b25a0faad3cfd9c7be28e701fad1809`.
The packaged assembly hash and both bundled Decoy scripts were checked against
the build output/source. Core was not launched.

## Offensive scaling remains open

No weapon-damage, Attack, class-attribute, magic-potency or enemy-stat coefficients
were invented or enabled. The old Google experiment sheets returned HTTP 410.
The same researcher's [critical-damage article](https://kanican.livejournal.com/56489.html)
also survives as text, but its hosted data images were unavailable. It explicitly
distinguishes empirical fits from proven formulas and leaves enemy reversal and
critical resilience untested. Do not adopt its approximate fit as a universal
player/enemy damage engine. Our existing critical calculation still needs a
separate review of percentage units and applicability.

This pass restores a supported shared damage rule and one-hit avoidance while
preserving the outstanding offensive-scaling investigation.

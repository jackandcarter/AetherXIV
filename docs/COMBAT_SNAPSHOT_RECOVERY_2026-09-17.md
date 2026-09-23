# Combat snapshot recovery

## Findings

The existing decoded corpus yields **436 command-result packets and 551 decoded action entries** across four combat captures:

| Capture | Result packets |
|---|---:|
| combat_autoattack | 14 |
| combat_skills | 11 |
| party_battle_leve | 338 |
| war_quest_update2 | 73 |

103 snapshots have a preceding attacker instantiation and 107 have some preceding attacker generalParameter observations. These are separate counts, not a count of complete samples. No snapshot is certified ready to fit offensive scaling: weapon/item mapping, full stat baselines, complete status state and result meaning remain unverified. A nonzero result amount is not necessarily damage.

## New packet cross-reference

All 62 server-to-client 0x00DB records have eight-byte payloads. The final four bytes are:

- `0000803F`: 51 records (IEEE little-endian float interpretation: 1.0).
- `0000003F`: 9 records (same interpretation: 0.5).
- `00000000`: 2 records (same interpretation: 0.0).

`SetActorTargetPacket.BuildPacket` currently casts the target ID to ulong and therefore writes zero to the final four bytes. The retail pattern shows information our writer does not represent. **The float interpretation is a hypothesis, not a recovered field meaning.** Do not change the packet writer to a guessed constant. Trace the client's 0x00DB consumer and correlate these three values with target transitions before selecting a parameter/API.

The 43 0x00DE records all have zero final four bytes; that alone does not identify their meaning or prove their first four bytes are zero.

## Reproduction and evidence

Run `/usr/bin/python3 tools/Universal/reconstruct-combat-snapshots.py`.

Outputs under `evidence/combat-snapshots-2026-09-17/`:

- `snapshots.jsonl`: per-result last-observed attacker/target properties, identity, status lists and appearance, with source corpus lines and frame references.
- `summary.json`: counts, original corpus SHA-256 and limitations.
- `verification.json`: boundary-check outcomes and 0x00DB/0x00DE trailing-byte distributions.

The reconstruction consumes the existing decoder output; it does not introduce a second packet decoder. State is scoped to capture, stream and actor lifetime; add/removal boundaries clear prior values. Client-to-server records are excluded, preventing independent directional frame indices from being mixed. Earlier values are copied into each snapshot, so later deltas cannot modify prior observations.

Boundary checks verified removal, addition, stream separation, exclusion of future updates and exclusion of client-direction properties. These checks validate the join behavior, not the historical semantics of decoded fields. Same-frame receive order is not proof of calculation order. Status lists are explicitly last-observed lists, not guaranteed complete active buffs. Appearance is not treated as equipped inventory.

## Next recoverable work

1. Trace 0x00DB's client consumer to identify its second field. This is a concrete, bounded discrepancy with 62 retail observations.
2. Recover equipped item references and baseline stats for the 107 snapshots with observed stat deltas, if the raw captures contain them. Do not borrow values from another capture using actor ID alone.
3. Resolve result text/effect semantics before classifying entries as damage, healing, mitigation or resource changes.
4. Fit candidate scaling rules only once their required inputs are known, then reject or validate them against held-out retail observations.

No runtime, database or packaged Core changes were made. This pass restores evidence linkage, not an offensive damage formula.

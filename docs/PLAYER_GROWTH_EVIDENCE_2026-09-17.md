# Level-appropriate player stats: evidence investigation

Research only. No production stat values, migrations, live character rows or binaries changed.

## Confirmed cause

Limsa Testeight's diagnostic run `20260917T195327.177Z-5af2779463cd4dc2a736c8b6e4191027` records `stats.base.fallback` for actor `0x39`, tribe 3, both Archer (7) and Thaumaturge (22): requested level 30, selected profile level 1. The exact profile lookup failed and the existing at-or-below lookup selected the clan baseline.

`Player.GetCurrentBaseStatProfile` caches that fallback. `Database.GetPlayerBaseStats` orders matching rows by descending level, then exact tribe over tribe zero. This is not a client level-display problem.

Migration `20260724_000027_correct_1x_player_baselines.sql` supplies level-one clan attributes with HP/MP set to zero. These are not a complete playable class/level HP/MP model. `ApplyBaseStatProfile` adds these values as modifier contributions. `CalculateBaseStats` then adds VIT into HP, applies several explicitly incomplete secondary-stat derivations, equipment and traits. Consequently, **displayed retail HP cannot simply be inserted into the profile hp column**: later contributions would be added again. The same distinction applies to equipment and allocated primary attributes.

## New extraction

`tools/Universal/research-player-growth.py` scans all 54 existing SE captures / 84 World streams, retaining 460 property/job-state packets with raw payloads and actor IDs. There were no extraction exceptions. Output: `evidence/player-growth-2026-09-17/`.

It also disassembles 11 relevant client scripts and checks exact EOF consumption. Each source script is SHA-256 fingerprinted. The sheet extractor's new optional `--growth` mode extracts nine candidate sheets (1,992 rows), checking every row against native offset tables and consuming complete data blocks. The default gathering/crafting extraction remains unchanged.

Reproduce:

```sh
/usr/bin/python3 tools/Universal/research-player-growth.py
/usr/bin/python3 tools/Universal/research-client-sheets.py --growth
```

Archive provenance and extracted source captures remain in `evidence/gathering-crafting-2026-09-17/`. The growth extraction uses those capture copies. This audit is a focused property scan, not a claim to have interpreted every capture opcode.

## Retail checkpoints—not base-stat rows

The following values belong to source actor 43723073 (`0x029B2941`). Attribute order is STR/VIT/DEX/INT/MND/PIE. Include both completed property segments and continuation entries (`dangling`) when inspecting the JSON: some attribute blocks continue into another packet. Do not discard those entries or mistake an omitted delta for zero.

| Capture / frame | Context | Maximum HP / MP | Six displayed attributes |
| --- | --- | --- | --- |
| login / 969 | Class 4, level 26 | 758 / 249 | 97 / 92 / 102 / 79 / 76 / 67 |
| gear_changeweapon / 57 | Class 3, level 31 | 1011 / 507 | 120 / 118 / 110 / 88 / 106 / 94 |
| switch_to_weaver / 98 | Weaver 34, level 1 | 111 / 120 | 17 / 16 / 20 / 20 / 20 / 16 |
| local_leve_complete / 648 | Weaver synthesis sequence, level 3 | 126 / 141 | 19 / 18 / 23 / 22 / 22 / 18 |
| local_leve_complete / 1230 | Same synthesis sequence, level 4 | 137 / 153 | 21 / 20 / 25 / 24 / 24 / 20 |

The login capture supplies tribe 6 at frame 973. This is **not** Limsa Testeight's tribe 3. The crafting level updates themselves do not repeat class identity; their Weaver attribution comes from the synthesis workflow and client command UI class argument, not an assumed class field in those packets.

Within the local-leve capture, the level-3 to level-4 transition increases each of the six displayed attributes by 2, maximum HP by 11, and maximum MP by 12. This is a useful observed transition, not a universal +2-per-level rule. Before attributing all differences to base growth, inspect equipment, active traits/statuses and allocation state around that interval.

`gear_changesoul` frame 69 publishes HP 1016, MP 548 and only changed primary attributes STR 115, VIT 123, INT 83. Other attributes are omitted deltas, not proven unchanged across arbitrary separate captures. `add_str` frame 84 publishes STR 116. These are useful controlled-change candidates, but separate captures must not be combined into an assumed uninterrupted session.

No direct active level-30 Thaumaturge checkpoint was found in the source actor's retained class/level updates. A saved level-list entry alone would not supply that class's active stat vector.

## What the client establishes

`charabaseclass_ffxivbattle` getters read `battleTemp.generalParameter`; `charabaseclass_parameter` reads replicated class/level and HP/MP fields. These scripts expose the data consumed by the UI, not a complete growth calculator.

The master sheet index contains no plainly named class-by-level stat-growth table. Candidate extraction includes `tribe`, `boot_charaTemp`, `boot_skillequip`, `exp_BPCost`, status and localized parameter/class/job names. The tribe sheet has only three columns; `boot_charaTemp` has template-like ID pairs. Neither yields a six-attribute/HP/MP growth table from its extracted layout. `exp_BPCost` must not be reinterpreted as stat growth just because it contains level-related numbers. Unknown positional column meanings remain unresolved.

This does **not** prove no native-client routine or unidentified resource contains relevant constants. It limits what this Lua and named-sheet pass recovered.

## Safe next restoration work

1. Treat these snapshots as expected **displayed-result fixtures**, not SQL seed rows. Reconstruct equipment, allocation and status/trait contributions for each observation before deriving any base component.
2. Start with the within-capture Weaver level 3→4 pair: it avoids assuming continuity between different captures. Audit the full packet interval for competing state changes and identify all contributions using existing inventory/status decoders.
3. Use the weapon/job and point-allotment captures to test how class/job conversion and allocation affect totals. Preserve actor ownership and packet ordering.
4. Define the HP/MP profile contract explicitly (base contribution versus final total) and validate existing derivations against the fixtures. Do not fix the level-one fallback by double-counting VIT or gear.
5. Recover additional direct class/level evidence, particularly Thaumaturge/Archer at level 30 and the matching tribe. If exact rows remain unavailable, report the coverage gap rather than interpolate and label it retail-correct.

No defensible complete level-30 Thaumaturge base row or general growth formula was recovered in this pass. The fallback defect is confirmed; the new evidence supplies test targets and a narrower route toward restoration, not permission to invent missing values.

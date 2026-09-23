# Botanist and crafting restoration: evidence audit

Date: 2026-09-17. Research only; no gameplay, character, database, or deployment changes made by this audit.

## Outcome

The available captures support restoration of the real client/server interaction sequences for Botanist logging, offhand harvesting, and a Weaver local-leve synthesis workflow. They do **not** establish the complete retail gathering/crafting formulas or a complete ordinary-recipe catalog. Existing command, equipment/stat, inventory and event systems should be extended rather than replaced with a parallel implementation.

The strongest small starting point is offhand Harvest (command 22007): its captured lifecycle is much smaller than logging's minigame. An authentic outcome implementation still needs verified node content and reward/eligibility rules. We can confidently restore the protocol before claiming those rules are complete.

## Sources, coverage and verification

Local source archive: `/Volumes/Dev2/ffxiv_traces.zip`.
Client: `/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV`, version `2012.09.19.0001`.

Generated evidence lives in [the evidence directory](../evidence/gathering-crafting-2026-09-17/), including source hashes in `provenance.json`.

- Scanned all 54 captures, discovering 84 World TCP streams on port 54992 and framing 65,864 subpackets. The audit records no decoder exceptions. This is not a claim that every opcode's meaning is known.
- Retained full decoded/raw packet evidence for seven relevant capture families: `change_to_botanist`, `gather_wood`, `harvest`, `switch_to_weaver`, `accept_local_leve`, `local_leve_complete`, and `repair_items`. Other streams were searched for relevant event keywords; unnamed or indirect relationships can escape that filter.
- Re-encoded 280 relevant RunEvent/response parameter lists and compared them byte-for-byte with their wire payloads, including type tags and terminators. No unmatched request/reply entries remain in the retained transaction audit.
- Parsed 30 selected client Lua scripts, containing 347 function prototypes, to exact end-of-file. Disassembly is evidence, not recovered original source.
- Extracted 28 client sheets, totaling 33,585 rows across related and localized tables. Every row ending matches the client's own offset table; all selected data blocks are fully consumed. Counts include overlapping/localized records, not 33,585 distinct gameplay definitions.
- No `tcp.analysis.lost_segment` flags appeared in the seven relevant captures. This does not establish that all gameplay scenarios were captured.

The client XML decoder preserves bytes after the closing XML element separately. Sparse column indices and types are retained. Unknown column meanings remain unknown; `f16` values are preserved as raw 16-bit values rather than asserted to be decoded floats. The localized Judge message resources have not been resolved; numeric feedback IDs must not be assigned guessed English meanings.

Reproduction (requires the external drive, tshark, and existing decoders under `.local-evidence/tools/Universal`):

```sh
/usr/bin/python3 tools/Universal/research-gathering-crafting.py
/usr/bin/python3 tools/Universal/research-client-sheets.py
/usr/bin/python3 tools/Universal/summarize-gathering-crafting.py
```

These scripts generate evidence files only. They do not update the live database. The sheet extractor follows the locally available SeventhUmbral XML/Sheet/FileManager implementations.

## Botanist: two different interaction paths

The captured player's actor ID is `43723073` (`0x029B2941`). Attribute state changes using the **source actor**, not just the recipient: nearby players also appear in these streams. The class-change capture reports Botanist class 40; the Weaver capture reports class 34.

### Logging: command 22003

See [logging transaction timeline](../evidence/gathering-crafting-2026-09-17/gather_wood-s0-timeline.md) and the corresponding `streams/gather_wood-s0.json` for raw packet payloads.

The observed order includes `loadTextData`, `targetCancel`, `turnToTarget`, `openInputWidget`, alternating `orderInputWidget` / `textInputWidget` / `askInputWidget`, then `closeInputWidget` on cancellation. There are 43 matched delegated function calls.

Important anchors in `gather_wood.pcapng`:

| Frames | Observation |
| --- | --- |
| 76 → 78 | `openInputWidget(22003, 1)` succeeds; grade argument is 1. |
| 94 → 121 | Phase 1 returns `[22704, 20, true]`: Begin and selected aim value. |
| 135 → 200 | Phase 2 returns `[22705, -4, true]`: Chop with a signed input. |
| 248 → 280 | Another Chop returns signed input -45. |
| 301, 307 | Tinolqa Mistletoe `10009610` inventory entry quantity 2, and feedback 25 carrying item/quality/quantity. |
| 673 | Feedback 56 references Maple Branch `10008104`; no corresponding Maple Branch inventory award was found. Do not count this as a successful award. |
| 699 → 717 | Phase 1 returns Cancel `22706`; widget subsequently closes. |

Client `HarvestJudge` selects `FellingInputWidget` for 22003, and returns Begin 22704, Chop 22705 and Cancel 22706. `FellingInputWidget` preserves signed angular input. Its comparison with goodmin/goodmax controls UI sound; this does not prove the server's success formula. A `textInputWidget` response of false is an observed normal UI return, not by itself a gathering failure.

This capture ends through cancellation. It does not establish node respawn or full exhaustion behavior.

### Offhand harvesting: command 22007

See [harvesting timeline](../evidence/gathering-crafting-2026-09-17/harvest-s0-timeline.md).

Two interactions contain only four delegated calls total: `loadTextData` and `turnToTarget` for each. There is no opening/aiming/chopping widget in these observed interactions. Client `HarvestJudge` also does not open a gathering input widget for 22007.

The stream includes Moko Grass `10005202` inventory entries at frames 97 and 231, with quality fields 1 and 2 respectively. Their quantities 11 and 4 are **stack totals**, not proven per-attempt yields. Self skill-point state changes appear at frames 103 and 237. World-message parameters include 5 and 4 at frames 123 and 266; the exact localized message interpretation still needs confirmation.

Do not route Harvest through the existing mining minigame just because both are gathering.

## Crafting: three actual syntheses recovered

`local_leve_complete.pcapng` contains full synthesis, not just the leve turn-in. See [crafting timeline](../evidence/gathering-crafting-2026-09-17/local_leve_complete-s0-timeline.md) and [all observed action results](../evidence/gathering-crafting-2026-09-17/observed-crafting-steps.json).

Observed local leve: `120202`. Output shown to the client: Hempen Dalmatica `8030420`. The selected material list contains two Undyed Hempen Cloth `10005001`, one Mole Sinew `10007507`, and one Hempen Yarn `10005301`. Recipe confirmation shows one Lightning Shard `1000007` and one Wind Shard `1000005`.

This is a **local-leve supplied-material workflow**. It is not proof of ordinary inventory ingredient consumption or of awarding an ordinary inventory Dalmatica after each synthesis.

| Frames | Observed stage |
| --- | --- |
| 174 → 184 | Select local craft quest actor. |
| 186 → 199 | Confirm leve 120202 and associated item/attempt arguments. |
| 201 → 230 | Start with an eight-slot ingredient array; unused slots are zero. |
| 232 → 237 | Select recipe/output 8030420. |
| 242 → 274 | Confirm output and crystal requirements. |
| 306–652 | First synthesis: six actions, final progress/durability/quality `[100,48,43]`. |
| 728–1230 | Second synthesis: nine actions, final `[100,10,102]`. |
| 1311–1576 | Third synthesis: five actions, final `[100,62,43]`. |
| 658, 1240, 1581 | Exact method name is `askContinueLocalleve`. Responses continue, continue, then leave. |
| 1698–1758 | Separate NPC turn-in, inventory rewards, skill-point update, journal clear and event end. |

Twenty action selections/results were recovered. English client command text identifies Standard Synthesis 22580, Rapid Synthesis 22581, **Careful Synthesis** 22582, and Wait 22506. Do not use the prototype's old “Bold” label as evidence for this client.

The same selected action produces different progress/durability/quality outcomes. The captured HQ chance field remains zero. Neither observation establishes a probability distribution, stat scaling, HQ formula, or universal failure rule. `CraftJudge` forwards server-supplied progress values to the widget; this is not a client-side implementation of the missing outcome formula.

## Client data recovered—and its limits

See [sheet audit](../evidence/gathering-crafting-2026-09-17/sheets/audit.json), individual typed sheet JSON, and `client-scripts.json` / `client/*.dis.txt`.

| Data | Rows | Use and limitation |
| --- | ---: | --- |
| weapon | 1,161 | Weapon/tool definitions; interpret fields through existing bindings. |
| equipment | 4,875 | Equipment metadata; not all entries are gathering/crafting gear. |
| compatibility | 219 | Supports equipment-policy investigation; not all column semantics verified here. |
| itemData / English item names | 8,403 each | Resolves observed item IDs and metadata. |
| command / English command text | 1,662 each | Resolves actual command IDs, names and descriptions. |
| passiveGL_craft | 169 | Local-crafting contracts; not 169 fully decoded ordinary recipes. |
| recipe | **7** | Tiny one-column table, not the full normal-recipe database. |

The `passiveGL_craft` row 120202 contains the captured output ID, a three-completion/five-attempt grouping, and the captured reward item 8032501. These associations corroborate that specific leve; tier-selection and every column's meaning still require binding evidence.

The selected `playerbaseclass_harvest` and `playerbaseclass_craft` scripts have empty roots; they do not reveal server formulas. Client presentation code can recover argument order and UI behavior without revealing authoritative retail calculations.

## Current stack gaps and reuse points

- `Data/scripts/commands/DummyCommand.lua`: hardcodes mining, a test node and test items; Log/Fish branches are empty. Its power-range heuristic and node-exhaustion TODO are not retail evidence.
- `Data/scripts/commands/CraftCommand.lua`: contains hardcoded recipes and explicitly describes its synthesis logic as “smoke and mirrors.” Fixed progress increments and random placeholder losses cannot be treated as restored crafting.
- That crafting script calls `askContinueLocalLeve`; both the client and capture use `askContinueLocalleve`. This is a concrete, narrow future correction, alongside the selection-range guard using `or` instead of a bounded-range conjunction. Neither correction alone completes crafting.
- `Player.ApplyMainHandToolStats` already supplies Craftsmanship, Magic Craftsmanship, Control, Gathering, Output and Perception. Reuse and test that existing path; do not create another tool-stat calculator.
- Reuse the existing command dispatch, delegated-event replies, inventory mutation/persistence, class switching and actor publication paths. Validate their integration before adding new components.

## Restoration sequence and acceptance gates

1. **Offhand Harvest protocol slice:** correct command routing and tool/class checks; captured turn/result/end sequence; validated node configuration; atomic reward and skill-point updates through existing systems. Test cancel/disconnect, out-of-range, full inventory and replayed replies. Until eligibility/yield rules are evidenced, mark any provisional policy explicitly.
2. **Logging protocol slice:** grade, phase-1 aim, signed phase-2 input, feedback ordering and cancellation. Replay the captured request/reply sequence as a regression fixture. Keep guessed success/respawn rules out of an “authentic” claim.
3. **Weaver local-leve slice:** reproduce the verified 120202 selection/confirmation and continue/finish lifecycle. Keep local-leve materials and completion counters separate from ordinary inventory awards. Validate the exact client method casing and eight-slot arrays.
4. **Broader crafting and calculations:** recover authoritative recipe/ingredient/quantity mappings and supporting evidence for action outcomes, conditions, success/HQ rates, stat/level scaling and XP. Do not infer a general formula from these 20 actions or seed a supposed full recipe database from the seven-row table.

For each future implementation, log actor, event/command owner, sequence/state, request/reply parameters with wire types, server outcome inputs and inventory/XP deltas. Correlate timestamps without calling user/network round-trip time server processing time. Verify packet ordering against fixtures and test in the actual client before a Release rebuild to the existing `MacOS` output directory.

**No fixes or rebuild were applied by this evidence audit.** The new research scripts and generated files are separate from pre-existing worktree changes.

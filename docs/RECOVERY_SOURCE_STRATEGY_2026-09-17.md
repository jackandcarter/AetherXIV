# Recovering 1.x behavior through cross-referenced evidence

## Outcome of this audit

The highest-value next investigation is a **combat snapshot reconstruction**, supported by client field consumers and versioned period testing. More rows alone do not resolve server-side formulas. We need observations that connect inputs to outcomes and expose which inputs are missing.

Reproducible inventory: `tools/Universal/audit-recovery-opportunities.py`; results: `evidence/recovery-opportunities-2026-09-17/audit.json`. Run with `/usr/bin/python3 tools/Universal/audit-recovery-opportunities.py`.

The existing corpus contains 65,864 records across 54 captures and 84 capture/stream pairs. Every record has `decoded: true`, but 1,210 contain generic `words` arrays. This flag means a decoder produced output, not that all fields have understood meanings. Conversely, generic words do not prove a packet is unknown: current server code implements 0x0195 as an enmity indicator and 0x00DB as actor targeting, although the corpus decoder still calls them unresolved. These implementations must be reconciled with their original evidence before changing decoder semantics.

Three combat-associated packet families deserve that reconciliation: 0x0195 (149 records), 0x00DB (62), and 0x00DE (43). Their occurrence in the same four captures is a lead, not evidence that they contain damage coefficients. The inventory retains corpus line references, capture names, stream IDs, lengths and input hashes for follow-up.

## Priorities and connections

| Priority | Combine these sources | Recoverable result | Verification boundary |
|---|---|---|---|
| 1 | Retail actor initialization + stat deltas + equipment + status changes + command results | A per-action snapshot of known attacker/defender inputs and outcomes; identify usable formula experiments | Scope identity to capture, stream and actor lifetime. Missing initialization means unknown, not zero. Same numeric actor ID across captures is not continuity. |
| 2 | Client command tables + Lua getters + native field consumers + localized descriptions | Costs, targeting, range, timing, flags, conditional effects and references to shared mechanisms | A column position or plausible value is insufficient. Trace an actual consumer and compare known actions. Client presentation does not prove server calculation. |
| 3 | Period controlled tests + surviving tables/images + patch history | Conditional slopes, caps, rounding, random ranges and candidate offensive scaling models | Separate auto-attacks, weapon skills, spells and DoTs; preserve version, target level, gear and buff conditions. Reserve independent observations to reject overfitting. |
| 4 | Official patch threads + old Lodestone links + Japanese/French/German variants | Version boundaries and details lost in one language or extraction format | Record announcement versus observed behavior. No mention of a change is not confirmation of unchanged mechanics. |
| 5 | Actor lifecycle + quest state + dialogue + period video/map landmarks | NPC presence, encounter-owned enemies, trigger order, reinforcements and return paths | Do not turn temporary encounter actors into permanent spawns. A video position needs zone and coordinate anchoring. |
| 6 | Different client builds or preserved patch files, if available | Changed rows and consumers that narrow when a rule changed | First inventory actual build availability; no multi-build comparison has been established by this audit. |

## Additional angles worth pursuing

- **Recover references rather than only pages:** old spreadsheet keys, image IDs, quoted table headings and outbound links can locate independent archived copies. A quote of the same experiment is not a second experiment. Missing chart images remain missing evidence even when surrounding prose survives.
- **Read consumers backwards:** locate a UI label or getter, follow its field access to a table or actor property, then search packets for that field. This can resolve unknown property hashes without guessing from numeric magnitude.
- **Use boundary observations:** level-up, weapon swap, buff expiration, stance changes, partial resists and combo failures often isolate one variable better than long undifferentiated combat logs.
- **Exploit integer behavior:** minimum/maximum damage, rounding steps and repeated identical inputs can reject candidate operation orders. Many different formulas can fit a few averages; report the surviving range of models instead of selecting a convenient one.
- **Cross-check text and behavior:** descriptions reveal conditions; status application/removal and command results reveal whether the condition occurred. Animation/VFX similarity is only a discovery aid, not proof of shared damage logic.
- **Use failure and cancellation paths:** resource deductions, interrupted casts, invalid targets and cooldown transitions can reveal ordering that successful actions hide.
- **Separate recovery from implementation tests:** an emulator test proves our code follows a proposed rule. Only independent retail evidence supports that rule as historical behavior.

## Patch archive gap confirmed

`tools/Universal/research-legacy-patch-notes.py` discovers version-numbered thread titles from two forum index pages. The current index has 23 entries, beginning at 1.16a. Its title filter excludes the official [Past Patch Notes](https://forum.square-enix.com/ffxiv/threads/5114-Past-Patch-Notes) index. That official post links 1.16, 1.15b, 1.15a and three late-2010 update notices. Those linked bodies have not been recovered by this audit; finding their index is not recovering their contents.

Next archival improvement: preserve outbound links, images/table references, post identity, language and patch dates alongside extracted text. Track discovered, retrieved, parsed and independently checked as separate states. The current text-only extraction can lose evidence embedded in images or links.

## Concrete next experiment

1. Reconstruct a timeline for one combat capture, joining existing decoders rather than creating a second protocol system.
2. Emit each action with known equipment/stats/statuses, actor lifetime, result and explicit missing fields. Preserve source record references.
3. Compare 0x0195/0x00DB to existing packet writers and their evidence; inspect 0x00DE independently through client consumers and neighboring events.
4. Select only complete enough snapshots for offensive scaling; use period tests to supply constraints that retail captures lack.
5. Try competing formulas and rounding orders. Hold out another target, stat configuration or capture for validation. If several formulas survive, retain the ambiguity.
6. Implement only identified rules through existing battle policies and Lua hooks, add regression cases tied to evidence, then verify in client. Keep unsupported coefficients out of runtime defaults.

A faithful reconstruction may combine directly recovered constants with derived rules. Each needs a label and provenance: **observed**, **inferred with validation**, or **unresolved**. We can fill engineering gaps ourselves; we cannot make historical certainty out of an underdetermined formula.

No battle runtime or database changes were made in this investigation.

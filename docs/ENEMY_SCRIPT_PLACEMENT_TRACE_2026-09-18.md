# Captured enemy placement: client script trace

## Outcome

Decoded all **2,517 installed `client/script/**/*.le.lpb` files**, with no parser
exceptions or unconsumed bytes. The eight captured enemy class IDs, their
display-name IDs, full runtime names and sampled X/Z coordinate pairs were not
found as literals in those scripts. This does not prove absence from packed
assets, native code, computed values or server data.

| Actor class | Client display name | Client class |
|---|---|---|
| 2104217 | tree slug | SlugLesserStandard |
| 2100502 | canopy galago | MonkeyLesserStandard |
| 2104028 | star marmot | HareStandard |
| 2104009 | star marmot | HareStandard |
| 2105901 | forest funguar | FunguarLesserStandard |
| 2105612 | chigoe | ChigoeLesserStandard |
| 2105802 | glowfly | FireflyNormalStandard |
| 2104306 | errant spirit | PetitghostLesserStandard |

Names come from the typed client display-name rows recorded by the preceding
placement probe, not modern web references. The two marmot classes illustrate
why a display name alone cannot identify a spawn variant.

## References followed

All seven unique standard monster scripts are eight-instruction class wrappers:
require the species base class, define the class, return. Their species bases
require `MonsterBaseClass`, which requires `NpcBaseClass`. NPC base dependencies
include `_event`, `_battle`, and `_battletest`; the latter is just a return in
this installed client. These chains do not supply the sampled enemy placements.

Related encounter-looking names were inspected rather than interpreted as data:

- `PublicPopGroup` requires `ContentGroupBaseClass`, which requires `GroupBaseClass`.
- `PublicPopGroupDirector` requires `NewPopDirectorBaseClass`, which requires
  `DirectorBaseClass`. Both the public director and new-pop base are class-only
  eight-instruction wrappers, not region spawn lists.
- `PgHarvestPointEncounter` is also a class-only wrapper, inheriting from
  `PassiveGuildleveBaseClass` and the quest base hierarchy.
- `AreaBaseClass_layout` defines `loadCommonTableData`, preparing common sheets
  including quests, guildleves, shops and map navigation. It is not a literal
  NPC/enemy placement table despite its filename.
- `AreaBaseClass` actor-creation paths accept runtime arguments. Existence of
  `_createActor` calls does not establish a hardcoded spawn database.

The name/prefix search also finds quest-specific chigoe and petitghost variants.
Those share naming but are not evidence that the sampled overworld classes use
those quest encounters.

## Checks and artifacts

Reproducer: `tools/Universal/trace-captured-enemy-scripts.py`.
Evidence directory: `evidence/enemy-script-trace-2026-09-18/`.

- `audit.json`: each script path, decoded name, SHA-256 and parse result.
- `matches.json`: 12 matching functions with literal indexes/source lines.
- `dependency-chains.json`: recursively followed root path constants. These
  are dependency candidates; saved disassemblies allow checking `require` calls.
- `encounter-symbol-leads.json`: 20 functions containing selected encounter or
  actor-creation terms; this keyword set is a lead finder, not exhaustive semantics.
- `provenance.json`: client version, decoder hash and captured controls.
- Saved disassemblies for matches and followed dependencies.

One positive-control numeric reference survives: Lonsygg display ID 1600102 in
`man0g0`, source lines 2086–2120. This is an identity reference, not placement.
The previously verified NPC marker IDs are not literal script references here;
their absence does not invalidate their typed-sheet evidence.

Validation checks require 2,517 successful EOF parses, eight controls, no
enemy-ID/runtime-name/coordinate-pair matches, and resolved dependency candidates.
No server, gameplay, database or package changes were made.

## What this changes about the search

Do not spend further time treating these standard monster Lua wrappers as
hidden spawn tables. The next bounded test is resource/native actor-creation
provenance: follow how runtime identity and transform data arrive, and determine
whether any zone-local resource is consulted for these actors. In parallel with
that research, capture lifecycle records remain the direct evidence for observed
positions, but do not by themselves establish home points or respawn rules.

No claim of a complete enemy placement database, server-only ownership, or
recoverable original spawn policy is justified yet.

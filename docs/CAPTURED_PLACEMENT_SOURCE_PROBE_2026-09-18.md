# Can captured placements be recovered from the 1.x client?

## Result

The first controlled test confirms a scalable source for **some NPC X/Z
locations: named quest markers**. It does not reveal a general enemy spawn
table. Negative findings are limited to the representations searched.

Penelope (actor class 1700001, display 1100404) matches markers 11042106,
11042108, 11048102 at X=58.08000183105469, Z=-1183.3299560546875.
Lonsygg (1000951, display 1600102) matches 11066302 and 11066402 at
X=162.7899932861328, Z=-1153.3199462890625. These X/Z values exactly match
the selected retail observations; the search tolerance was 0.05 units.
Markers supply neither actor elevation nor facing nor ambient spawn policy.

Expanding the named-marker comparison to all valid observations found X/Z
agreements within 0.05 for **35 distinct actor classes**, all classified populace
in the capture catalogue. This is stronger than the two initial controls, but
the expanded join does not independently bind client map IDs to server zones
or resolve phase variants. Repeated observations/markers are not counted as
additional distinct classes. Notebook checks and exact initial-control equality
were executed successfully after regenerating the evidence.

## Method and coverage

- Source catalogue: 791 observation rows, 633 with valid identity chains.
- Valid observations form 469 capture/stream/runtime-actor groups. Seven groups
  have differing observed positions; movement, respawn or lifecycle reuse must
  be resolved before deriving spawn origins.
- Controls: first eight distinct actor classes per kind (battle, populace,
  object/other), requiring valid identity and same-frame name/position.
  This is a purposive 24-class test, not a representative estimate of coverage.
- Decoded all 133 catalogued sheets (English/nonlocalized), 107,229 rows, with
  no decoder exceptions and full block-consumption checks.
- Found 123 candidate numeric identity/row-ID joins, five X/Z matches, zero full
  runtime object-name matches. Numeric namespaces can collide: the 123 joins
  are search leads, not verified references.
- Parsed all 287 MapLayoutResourceData files in the existing client inventory:
  no parser errors, no full runtime names or local-node X/Z matches for controls.
  This is NOT a composed-world-transform or exhaustive binary-data search.
- Eight battle controls yielded no typed float X/Z matches in this pass.

The initial exploratory run overmatched zero display IDs against unrelated
zero-valued fields. That run was discarded; the reproducer now excludes
nonpositive identifiers and regenerated the outputs. This quality check prevents
sentinel values from being mistaken for widespread placement references.

## Trust boundaries

High confidence: the two named NPC marker coordinates match the selected
captured coordinates, establishing a positive control for the extraction.

Not established: that enemies are absent from all client assets, that all quest
markers equal actor positions, or that a captured enemy's first position is its
spawn origin. Packed/integer/relative coordinates, compressed assets, client
script constants, encounter resources and composed layout transforms are not
exhausted by this pass. Different client/capture revisions are also possible.

Neither markers nor collision meshes justify inventing respawn intervals,
population counts, patrol radii, quest phases or missing rotations.

## Reproduction and next pass

Run `/usr/bin/python3 tools/Universal/probe-captured-placement-sources.py`.
Outputs are under `evidence/captured-placement-probe-2026-09-18/`, including
controls, resource hashes, per-sheet coverage, raw candidate rows, layout hashes,
and the all-valid-observation named-marker comparison. `review.ipynb` provides
inspectable checks of those outputs. No database/server/client files are changed.

Next: follow references for the eight battle controls into decoded client scripts
and encounter resources, using the matching NPC markers as positive controls.
Expand the capture-side catalogue with lifecycle evidence so moving enemies do
not become duplicate permanent spawns. Screenshots are not required for this
systematic extraction work; reserve them for spot checks.

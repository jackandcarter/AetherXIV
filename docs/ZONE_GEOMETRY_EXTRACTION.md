# Client zone geometry extraction evidence

Research status, 2026-09-17: PHB asset-local triangle extraction succeeds on four
complete layout resource lists. These outputs are not generated navigation meshes
and are not approved server landing geometry.

## Inventory and results

A read-only header scan of the installed 1.x client's `data` directory examined
137,578 DAT files. It found 5,201 `SEDBPHB` files and 287
`MapLayoutResourceData` files. Of those layouts, 104 directly reference PHB
resources. Counts describe files/layouts, not unique playable zones.

| Layout resource ID | PHB resources | Extracted GBD blocks | Triangles | Failed resources |
| --- | ---: | ---: | ---: | ---: |
| `29B00003` | 190 | 190 | 116,717 | 0 |
| `29D90001` | 262 | 262 | 131,621 | 0 |
| `615A0005` | 209 | 209 | 141,799 | 0 |
| `615A0004` | 172 | 172 | 126,318 | 0 |

Resource names associate the first sample with the `f0f0` family and the second
with `s0t0`. Exact server-zone associations still require client data linkage;
they must not be inferred solely from filenames. The first layout contains
12,564 nodes, including 10,811 instance records; the second contains 5,375 nodes.

The extractor writes OBJ files in **asset-local coordinates**, source SHA-256
hashes, bounds, triangle attribute counts, layout resources, and raw placement
records. Files are under `.local-evidence/zone-geometry/`, outside release output.
It changes neither installed client data nor server navigation assets.

## Reproduce

```sh
python3 tools/Development/extract-zone-geometry.py \
  --data-root '/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/data' \
  --layout-id 0x29D90001 \
  --output .local-evidence/zone-geometry/layout-29D90001

python3 -m unittest discover -s tests/tools -p test_extract_zone_geometry.py -v
```

Thirteen synthetic tests across extraction and placement probing cover padded bounds, preserved face attributes, truncated
blocks, bad indices, nonfinite vertices, out-of-bounds vertices, unsupported
versions, and invalid layout tables (the first test covers two properties).
Real-data checks validate every exported index and vertex against its block and
stored bounding box. Structural validation does not establish walkability.

## Format evidence

Layout resource and instance field references come from the locally available
[SeventhUmbral MapLayout reader](https://github.com/jpd002/SeventhUmbral/blob/master/dataobjects/MapLayout.cpp).
Our existing `AetherXIV.ClientData/ClientMapLayoutProbe.cs` extracts printable
metadata only; this research tool does not replace that production pipeline.
Validated decoding should eventually be integrated there rather than introducing
a second runtime asset service.

PHB observations in both samples:

- `SEDBPHB\0` container; embedded `PHB.GBD\0` block, observed version `0x212`.
- GBD-relative `+0x38/+0x3c`: vertex array offset/count; vertices are three
  little-endian float32 values, stride 12.
- `+0x60/+0x64`: triangle array offset/count; records have three uint16 vertex
  indices and a fourth uint16 value whose semantics remain unknown, stride 8.
- `+0x40/+0x50`: minimum/maximum bounds. Some flat resources have padded bounds,
  so containment is checked rather than requiring exact equality.
- Layout attribute-node `+0x24` appears to name a PHB resource. 189 of 190
  attribute nodes initially matched using a name-only lookup. Follow-up found
  that render and physics resources can share names: the lookup must include
  resource type. The placement probe now uses only the PHB namespace and rejects
  ambiguous PHB names. No placement coverage claim is made.

This is not a full PHB specification. Other primitive types, flags and versions
may exist among the untested resources.

## Remaining before navmesh generation

1. Resolve zone-to-layout identities and dependencies, including streamed layouts.
2. Decode and verify complete instance/unit-tree transforms (rotation convention,
   scale, nested transforms, placement variants) against independent landmarks
   and the existing Central Thanalan mesh. Raw transform records are preserved,
   but no world-space mesh is claimed yet.
3. Determine collision/attribute filtering: trigger volumes, water, barriers,
   dynamic objects and decorative geometry must not become walkable ground.
4. Assemble world-space geometry, generate with server-compatible SharpNav, then
   validate heights, clearance, slopes, boundaries and stacked-floor choices.

The Map Travel server must continue refusing destinations without verified
landing geometry. No runtime capability was enabled by this research extraction.

## World-placement comparison follow-up

`tools/Development/probe-zone-geometry.py` now follows instance → unit-tree →
attribute references, decodes unit-tree item translation/rotation/scale records,
and compares vertical triangle intersections against independent XYZ samples.
It preserves source hashes and per-hit resource, block, triangle, instance and
attribute offsets. It tests yaw-only transforms; pitch/roll are explicitly
excluded. It does not filter collision flags or approve travel.

Layout `615A0004` has strong spatial evidence connecting it to Central Thanalan.
Four other `615A0005`–`615A0008` layouts had no yaw-only surface hits at the three
known landmark coordinates. Exact client zone/layout table linkage remains a
separate verification task.

| Landmark | Existing navmesh Y | Extracted PHB Y | Difference |
| --- | ---: | ---: | ---: |
| Black Brush (33, -482) | 200.18182 | 200.00160 | -0.18022 |
| Cactus Basin (639, 122) | 184.10815 | 183.83954 | -0.26861 |
| Four Sisters (539, -14) | 216.26964 | 215.70582 | -0.56382 |

A deterministic sample of 301 positions from the bundled navigation mesh gives:

| Yaw hypothesis | Positions with intersections | Within 0.75 Y | Within 2 Y | Median absolute Y error |
| --- | ---: | ---: | ---: | ---: |
| Positive Y rotation | 301/301 | 207 | 240 | 0.53784 |
| Negative Y rotation | 301/301 | 106 | 130 | 4.16364 |

This supports the positive-Y convention, not full transform correctness. The
positive-Y run includes 2,995 yaw-only physics placements, skips 268 tilted
placements, and has zero unresolved physics resource references. A resource
namespace fix added three placements without changing the sample results.
The negative-Y report predates those three additional placements.

**61 samples still differ by more than 2 units**, with the worst around 25.43.
A hit somewhere beneath a sampled X/Z does not prove the correct floor exists.
Unparsed tilt, nested placement semantics, geometry filtering, and differences
in how the old navmesh was generated remain possible explanations. Do not enable
this extraction as production landing geometry based on the median alone.

```sh
python3 tools/Development/probe-zone-geometry.py \
  --data-root '/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/data' \
  --layout-id 0x615A0004 \
  --samples .local-evidence/zone-geometry/navmesh-samples.json \
  --yaw-sign 1 \
  --output .local-evidence/zone-geometry/navmesh-comparison-1.json
```

Sample generation source is retained in
`.local-backups/zone-geometry-probe/Program.cs`. It queries the existing
`NavmeshLandingResolver` at evenly stepped vertices of the bundled mesh and
retains a matching surface without horizontal snapping. These samples are
validation evidence, not manual coordinate calibration or a new travel system.

## Deeper surface investigation (2026-09-18)

The probe now accepts `--euler-order XYZ` (or any other permutation) to include
pitch/roll as an explicitly unverified hypothesis. It transforms a vertical
world-space line into asset coordinates and intersects actual triangles, keeping
the line parameter in world units even with nonuniform scale. Default behavior
still excludes tilted placements. Seventeen synthetic tests now pass.

All six rotation-order hypotheses were run on the same 301 independent samples.
Each included 268 tilted placements. None changed the nearest-height comparison:
207 samples within 0.75, 240 within 2, median absolute error 0.53784. At most one
sample acquired an additional tilted-object intersection, without improving its
nearest-height match. **These samples cannot distinguish the client Euler order.**
No hypothesis has been promoted to a verified world transform.

To rule out a query-height artifact, a separate probe reconstructed the bundled
navmesh's detail triangles from polygon/detail vertex indices and intersected
them directly. All 301 selected reference elevations matched their detail
triangle intersections. Source is retained at
`.local-evidence/zone-geometry/navmesh-detail-check.cs.txt` and results at
`navmesh-detail-check.json` in that same directory.

The important result is multiple surfaces at identical X/Z:

- Of the 61 samples whose selected reference surface differs by over 2 units,
  48 have **another** navmesh surface within 0.75 of extracted PHB geometry.
- 258 of all 301 samples have some PHB/navmesh surface pair within 0.75.
- Example `(136.76227, -584.9726)`: navmesh elevations 225.54546 and 200.12286;
  extracted PHB elevation 200.11117. The lower surfaces differ by only 0.01169.
- Matching a lower surface does not account for the upper one, establish
  walkability, or justify warping onto either. The upper surfaces could involve
  geometry omitted by the current extraction, different source geometry used for
  the old navmesh, or placement semantics still to recover.

This narrows many apparent coordinate errors to **surface coverage/layer
questions**. The unresolved upper surfaces must remain visible in validation
reports, rather than replacing each reference with whichever layer matches best.
The evidence is in `surface-layer-audit.json`.

### Native PHB triangle flag evidence

Read-only disassembly of the installed `ffxivgame.exe` found two code sites
(`0x00a7ec26` and `0x00a7f602`) that read a 16-bit value at
`triangle_base + triangle_index * 8 + 6`. Subsequent instructions accumulate
bitwise AND and OR over triangle values. This corroborates the extracted record
stride and shows the fourth uint16 participates in flag aggregation.

The executable hash and exact instruction bytes are retained in
`phb-face-mask-native.json`; nearby instructions are in `face-mask-readers.txt`.
No binary was patched. Observed field values across the extracted samples range
from 0 through 7. The meanings of individual bits—walkability, collision type,
material, or other behavior—are **not yet established**, and the extractor does
not discard any value. Finding the query-side consumers is the next useful
reverse-engineering target, alongside tracking the missing upper surfaces.

## Native accessor and upper-surface trace (2026-09-18)

Further read-only disassembly corroborates the PHB layout directly:

| Native VA | Observed operation |
| --- | --- |
| `0x00afebe0` | Return bounds at block + `0x40` |
| `0x00afec04` | Read triangle count at block + `0x64` |
| `0x00afec65` | Read triangle offset at + `0x60`, stride 8, final uint16 at +6 |
| `0x00afecfb` | Read triangle and vertex arrays at + `0x60` and + `0x38` |
| `0x00afed17` | Compute 12-byte vertex addressing and read float XYZ |

`tools/Development/verify-zone-phb-layout.py` verifies the exact executable
SHA-256 and five instruction sequences. It passes on the installed client.
These are static code observations: direct call/pointer references to the small
`0x00afec50` flag getter and `0x00afece0` vertex getter were not found, so they
must not be advertised as live-hook targets or evidence of a particular query's
execution. Related candidate traversal code calls `0x00a7cb20`, which reads three
uint16 triangle indices and vertex XYZ from the same format; the trace is saved
in `native-a7cb20.txt`. Individual flag-bit meanings remain unverified.

RTTI also identifies a `TriangleMeshShape` type and a raycast-result type.
Their candidate vtables are retained in `physics-rtti.json`; entries after the
actual table boundaries are not function claims. This investigation neither
hooks those addresses nor exposes them as Umbra APIs.

### Visual geometry at the worst upper-layer mismatch

The extractor now preserves render-resource names and bounding boxes for BG part
and chip nodes, enabling a comparison against physics geometry without treating
render geometry as collision.

At X/Z `(136.76227, -584.9726)`, the upper navmesh reference is Y `225.54546`.
One yaw-only render object's bounds contain that point:

- Layout instance offset `386512`, positioned at
  `(131.578995, 200.591034, -578.373718)`, yaw `-1.202154`.
- BG node `w0f0_p1_tre1a_h`, resource `897D01C1` (`3eCabJw0f0_p1_h`).
- Resource name suggests a tree. Its local bounding box extends to Y `27.48341`.
- Decoding the paired STMS index/position streams (big-endian uint16 indices,
  signed normalized int16 positions, scaled by the BG bounds) finds a visual
  triangle at world Y `224.607997`, about `0.93746` below the upper navmesh.
- Other intersections are at Y `223.738243`, `222.708232`, and `222.341191`.
  Meanwhile the PHB surface is Y `200.111168`, closely matching the lower
  navmesh layer at Y `200.12286`.

The two visual mesh streams contain 666 and 2,511 triangles; only the first
intersects this vertical line. This is stronger than a bounding-box match, but
it is **not proof that the bundled navmesh was generated from this visual tree**.
LOD/poly-group selection and the original mesh-generation inputs/settings are
not established. It does show why upper layers of that navmesh cannot serve as
unquestioned collision-ground truth.

Reproducible local research scripts/results:

- `probe-upper-bounds.py`, `upper-render-bound-candidates.json`
- `probe-tree-render.py`, `tree-render-surface-check.json`
- `physics-mesh-accessors.txt`, `physics-query-candidates.txt`

All are in `.local-evidence/zone-geometry/`. Render-format references are the
SeventhUmbral `StreamChunk` reader and `UmbralMesh`/`UmbralMap` normalization and
bounds transforms. No render triangles were added to the PHB landing geometry.

Validation: 17 extraction/placement tests and all five native layout checks pass.
Next unresolved work remains identifying the player-collision query's flag
policy, verifying full layout transforms and zone associations, and baking
collision-derived navigation geometry with explicit clearance/slope settings.

## Character-query filtering trace (2026-09-18)

The named `RaptureCharacterController::CreateCapsuleProxy` diagnostic string
references code at `0x007d5b00`. This routine supplies radius `0.5` and total
height `2.0` to `0x00a681c0`. The latter computes the cylinder length as
`max(height - 2 * radius, 0.01)` and the vertical center offset as
`cylinderLength / 2 + radius`, then creates the proxy through `0x00a692b0`.
These are **static defaults**, not verified live dimensions for every character.
A separate bounds-based setup path also exists at `0x00a68840`.

A character proxy constructor at `0x007d4560` initializes an eight-byte filter:

| Proxy offset | Type | Initial value |
| --- | --- | --- |
| `+0x60` | uint16 mask | `0x0002` |
| `+0x62` | uint16 comparison value | `0` |
| `+0x64` | uint32 comparison mode | `0` (equal) |

The constructor installs vtable `0x00fee210`, whose entry at +4 points to
`0x007d8080`. That path calls `0x007d79c0`, which passes `proxy + 0x60`
at `0x007d7a96` to the query wrapper `0x00af94d0`. The statically traced chain is:

`7d79c0 → af94d0 → b016e0 → b006b0 → b0ca00 → b0c970 → b0be90`.

At `0x00b0c70f`, the mesh path reads the triangle's fourth uint16 at
`triangleBase + triangleIndex * 8 + 6`. It compares `flags & mask` with
`value & mask`; modes 0, 1, 2 mean unsigned equal, greater-or-equal, less-or-equal.
A null filter bypasses this test. The constructor's default therefore retains
triangles satisfying **`(flags & 0x0002) == 0`**. This establishes the bit's
behavior in this path, not a universal semantic name such as “non-walkable.”
Setters at `0x007d4640` and `0x007d58a0` also toggle mask bit `0x0008`;
the conditions calling those setters remain to be traced.

Before mesh testing, `0x00b0ca00` applies whole-shape filters:

- At `0x00b0caf3`, shape flags at +0x4b must not overlap the supplied rejection
  mask (this wrapper supplies 3), and bit 0x10 must be set.
- At `0x00b0cb06`, shape group byte +0x4c selects a bit in the query's uint32
  group mask. `0x00af90c0` obtains that mask from a scene table indexed by the
  querying shape's group. This is distinct from triangle attributes.
- Non-mesh shapes may apply the same masked comparison to their +0x4e word.
  Shape type 8 goes through the per-triangle path instead.

A read-only count using the default triangle filter gives:

| Layout | Extracted asset triangles | Rejected by bit 0x0002 | Retained |
| --- | ---: | ---: | ---: |
| `29B00003` | 116,717 | 17,617 | 99,100 |
| `29D90001` | 131,621 | 80,575 | 51,046 |
| `615A0004` | 126,318 | 1,552 | 124,766 |
| `615A0005` | 141,799 | 440 | 141,359 |

These counts are asset-local, not counts after world instancing, object filtering,
clearance, or slope checks. Retained triangles are not automatically valid landing
surfaces. In particular, the large difference in `29D90001` makes unfiltered
geometry an unsuitable substitute for the client's collision policy.

Evidence in `.local-evidence/zone-geometry/` includes `character-query-path.txt`,
`character-query-filter.txt`, `character-mesh-query.txt`,
`character-filter-constructor.txt`, `character-proxy-create.txt`,
`native-character-filter-evidence.json`, and `character-filter-asset-audit.json`.
The hash-gated native verifier now checks eleven byte sequences/constants.
No client hooks, production structs, runtime filters, or zone travel permissions
were added by this trace. Next: recover the live scene group table and relevant
shape flags, trace filter setters, and verify layout-to-shape transforms before
using this predicate in a collision-derived navmesh bake.

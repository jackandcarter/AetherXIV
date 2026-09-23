# Native network actor-placement trace — 1.x

## Finding

For all eight enemy controls, retail server packets carry the observed actor
identity and complete XYZ/rotation. The installed client's native position
handler reads those floats directly, copies XYZ into actor state, and forwards
the transform to its positioning routine. A local spawn-coordinate lookup is
not required to provide the input coordinates on this demonstrated path.

This is stronger than merely failing to find coordinates in scripts. It is
**not** proof that no local placement-related resources exist anywhere, and it
does not recover the original server's home points, spawn groups or respawn rules.

## Verified chain

Addresses are virtual addresses in the executable whose SHA-256 is recorded
in `evidence/native-actor-placement-2026-09-18/verified-chain.json`.

| Stage | Verified observation |
|---|---|
| Actor opcode dispatch | 0x00CE routes to 0x58CE03; 0x00CF to 0x58CEF7; 0x00CC forwards at 0x58D675 |
| Position input | 0x58CE37/42/4D read internal-header offsets 0x18/1C/20, equivalent to payload offsets 8/12/16 |
| Rotation input | 0x58CE82 reads internal-header offset 0x24, payload offset 20 |
| Actor state | 0x58CE58/7A copy the coordinate vector into actor offsets 0x214/21C |
| Position consumer | 0x58CEED calls 0x58B2A0 with the incoming vectors and rotation |
| Default consumer branch | 0x58B3AC–D0 copies the supplied position vector and submits command 0x25 through 0x4D7980 |
| Instantiate dispatch | 0x58D678 → 0x4D8860; second opcode table maps 0xCC to 0x4D88E2 |
| Instantiate payload | 0x4D88FD skips the 16-byte internal header, forwarding payload to 0x574780 → 0x774AD0 |
| Initialization parsing | Reads strings at payload +4 and +0x24; structured parameter data starts +0x44 |
| Initialization submission | 0x7750B0 calls 0x774530; further native factory/queue internals remain incompletely traced |

The instantiate offsets agree with the existing decoder's object name, class
name and initialization-parameter boundaries. Role labels above are descriptive
inferences, not recovered debug-symbol names.

## Capture verification

Read raw payload hex rather than trusting formatted decimal decoder output.
For each selected class, matched capture, TCP stream, frame and source runtime
actor ID. Required both server-to-client 0xCC and 0xCE in that frame.

All **16 packets for eight controls** pass:

- 0xCC initialization parameter 6 equals the selected actor-class ID.
- 0xCC runtime object name matches the control observation.
- 0xCE float32 payload values exactly equal the observed X, Y, Z and rotation.

Classes: 2104217, 2100502, 2104028, 2104009, 2105901, 2105612,
2105802 and 2104306. See the companion script-trace report for display names.

## Limits and practical consequence

The position routine has special spawn-type/local-player branches. Those,
downstream collision/ground correction and final visual transforms are not fully
characterized here. The default branch proof is not a claim that every actor
uses that branch. Class assets and geometry can still be loaded locally without
being the source of that actor's server-supplied position.

The practical evidence source for these observed enemies is therefore the
network capture. To scale restoration, extract and correlate actor lifecycles
across the full capture corpus, keeping exact sightings separate from inferred
home locations. Deduplicate by capture/stream/runtime lifetime, class, zone and
instance context—not display name alone. An actor appearing in visibility range
is not necessarily a newly spawned actor.

Named client quest markers remain a complementary source for NPC X/Z, as
established in the preceding probe. Neither source supplies a complete enemy
population for regions absent from the captures. Additional original server
data or independently sourced reconstructions would still be needed for those.

## Reproduce

`/usr/bin/python3 tools/Universal/verify-network-actor-placement.py`

Requires the already available `pefile` and `capstone` packages. It asserts
opcode table targets, instruction operands, relative-call destinations and raw
capture values, and records executable/corpus hashes and disassembly blocks.
The run passed. No executable, server, database or build was modified.

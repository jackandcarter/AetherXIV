# 0x00DB: a verified client look-at path

## Finding

The second four-byte field is **float32**, and the packet drives the same shared machinery used by the client's named `_lookAtCharacter` script function. This is materially stronger than guessing a float from the values 1.0 and 0.5. Its exact semantic name and units remain unresolved: do not label it seconds, turn speed or blend weight yet.

The existing server class `SetActorTargetPacket` understates the packet's visual role. Its writer emits a target ID followed by four zero bytes, but no call sites were found in current C# source. Therefore this investigation does not establish a currently exercised targeting bug. The active `SetActorTargetAnimatedPacket` is a different opcode, 0x00D3, and must not be conflated with 0x00DB.

## Native proof chain

Installed `ffxivgame.exe`, image base 0x400000; exact executable hash, bytes and disassembly are preserved in `evidence/target-field-2026-09-17/client-chain.json`.

1. Actor dispatcher at **0x58CD21** uses lookup tables 0x58D7E4 and 0x58D788. Opcode 0xDB selects **0x58D1A1**.
2. That branch reads the target from `[esi+0x10]`. It ignores IDs satisfying `(id & 0xE0000000) == 0xC0000000`.
3. At **0x58D1B8**, `fld dword ptr [esi+0x14]` reads payload offset +4 as a 32-bit floating-point value. The branch passes the ID, float and zero flag to **0x589290**.
4. That wrapper calls **0x587F80** with selector 1 and priority-like argument 100. The shared function iterates two channel indices and calls **0x585F60**.
5. The setter compares the supplied priority byte against stored priority, then stores the actor ID at indexed offset +0x294 and float at +0x2A8. Channel names are not yet established.
6. Independently, registration code at **0x72F5B0** binds `_lookAtCharacter` (string 0xFD671C) using function pointer **0x6E43A0**. That function calls **0x75C580**, which calls **0x589260**. This wrapper converges on the same **0x587F80**, using selector 0 and priority 200 instead.
7. The update path beginning **0x5861B0** selects records by priority. At **0x58627B**, it reads the stored float and packages it with the actor ID in an internal scene command (local type 0xB, transformed to 0x20 by 0x4D7980). The final visual consumer remains to be traced.

The initial network callback dispatcher also maps 0xDB to callback slot +0x8C, but the inspected default callback is a no-op. It is **not** the visual handler; the actor dispatch above is the meaningful chain. Recording this distinction avoids mistaking a default stub for absence of client support.

## Retail observations

62 packets across four combat captures:

| Float | Count | Same target as previous observed 0xDB | Changed target | No prior target in lifetime |
|---|---:|---:|---:|---:|
| 1.0 | 51 | 11 | 25 | 15 |
| 0.5 | 9 | 7 | 2 | 0 |
| 0.0 | 2 | 0 | 0 | 2 |

Both zero-valued packets target 0xC0000000, so the identified handler exits before consuming their float. They cannot establish what a zero float does with an ordinary actor target.

The repeated targets show this is not merely a boolean target-change flag. Nearby actions and movement are preserved as discovery context, not causal proof. No damage multiplier can be inferred from 0.5 versus 1.0.

## Reproduction and next verification

- `/usr/bin/python3 tools/Universal/investigate-target-field.py` rebuilds the 62-record trace inventory, tracks previous targets within capture/stream/lifetime, and records nearby same-actor packets.
- `/usr/bin/python3 tools/Universal/verify-target-look-at-client.py '/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/ffxivgame.exe'` validates the opcode lookup, float load, named registration pointer, six call edges and indexed state write. Requires installed `pefile` and `capstone`.

Next: follow internal scene command 0x20 to the visual consumer to distinguish duration, speed and weight. Then use an isolated local-client comparison with a fixed actor pair and values 0.5/1.0 to verify the visible effect. Packet replay should wait until the final semantics and test setup are understood. Do not guess whether channels mean head/body or eyes/body.

No runtime, database or packaged Core changes were made. The verified restoration in this pass is semantic evidence: float type and the look-at subsystem connection.

## Follow-up: visual consumer and motion arithmetic

**The LookAtIKObject consumer uses this field as a motion-rate multiplier.** It is not interpreted there as a duration in seconds or a final pose blend weight. This is established by native data flow and arithmetic; visible behavior has not yet been tested in client. The earlier open question about meaning is now narrowed to this verified consumer branch.

### Extended chain

- `0x4E98F9` forwards the queued scene packet to `0x60C140`, then `0x7C93C0`.
- At `0x7C95CB`, command 0x20 is reduced by 0x15 and routed through actor virtual slot +0x274. The CharaActor table maps this slot to `0x662D30`.
- Local command 0xB resolves to `0x66340C`. Its payload float at +8 flows through `0x65CEB0` and `0x854830` into `0x853B50`, which stores it at record +0x18.
- `0x854600` forwards that record's float through `0xAC99B0` to `0xADD760`. Type-2 nodes receive it at +0xD4; type-3 nodes receive it at +0x5C.
- RTTI identifies the type-2 vtable at `0x10A92E4` as **LookAtIKObject**. Its type method returns 2. Thus the +0xD4 writes connect to a named visual class, not merely to similar-looking arithmetic elsewhere.

### What the float changes

At `0xAEA66E`, the look-at update calculates:

```
factor = node[0xD4] * node[0x1C]
scaledAcceleration = node[0x58] * factor
scaledRateLimitA = node[0x64] * factor
scaledRateLimitB = node[0x68] * factor
```

The term names are inferred from subsequent integration: `0xAEA905–0xAEA90D` multiplies the acceleration term by the update argument and adds it to the current rate (+0x60); the rate is bounded; `0xAEA98F–0xAEA9AF` multiplies directional rates by the update argument and adds them to the current look-direction components (+0xCC/+0xD0). This distinguishes a rate-control factor from a fixed countdown or final pose weight. Exact time units are not needed for that distinction and have not been independently established here.

For identical other inputs, changing 1.0 to 0.5 halves these scaled acceleration/rate-limit terms. It does **not** prove the whole visible motion takes exactly twice as long: clamps, distance-dependent behavior, controller state and other factors affect the result.

The near-zero branch at `0xAED610` compares against approximately 0.00001. Below that threshold, a path copies target look-direction components into current components and resets the rate. This supports special near-zero handling, not a blanket promise that sending zero always causes an immediate visible snap. The packet handler first rejects the sentinel actor category, and the two captured zero-valued packets use that category.

### Practical restoration implications

A future server API can represent this as a **look-at motion rate** rather than a duration. Preserve the observed 0.5 and 1.0 values as context-dependent evidence; do not send 1.0 universally or treat 0.5 as an animation time. Establish when retail emits the packet and verify visible behavior before wiring it into battle or NPC scripts. Keep this separate from gameplay target selection and enmity.

Still unresolved: the type-3 branch, the names of the two channel indices, exact server emission rules, and visual validation. No runtime changes or packet replay were performed. The extended verification tool passes checks for dispatch tables, named RTTI/type, call edges and the scaling/integration instructions; the updated evidence JSON contains the disassembly and executable hash.

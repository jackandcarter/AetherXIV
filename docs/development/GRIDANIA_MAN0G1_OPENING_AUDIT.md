# Gridania `Man0g1` opening handoff audit

## 2026-07-27 supersession

The Release waiter question described below is settled by Test Five. The
remaining failure is the native actor-slot port, not another tutorial UI
lifecycle variant. Retail's public Canopy uses composite, stable identities
for the area master, weather director, resident NPCs, and after-quest
director; the port currently sends a raw zone number for the area master,
omits weather, and allocates the other roles from shared counters.

Use
[`NATIVE_ACTOR_SLOT_PORT_AUDIT.md`](NATIVE_ACTOR_SLOT_PORT_AUDIT.md)
as the implementation contract for the next pass. Preserve the response-aware
waiter and the captured room-exit pacing while fixing actor identity.

## Finding

The phase-zero Canopy handoff assigned the wrong client function to Miounne.
`processEvent100_1` was treated as her linkpearl briefing, but the shipped
1.23b `Man0g1` client script proves that it only displays world-message rows
353 and 354: the explanation of instanced areas shown when the player first
enters the Canopy.

Miounne's actual first story interaction is `processEvent110`. It:

1. clears the active linkpearl/tutorial desktop mode when necessary;
2. starts the `man0g110` cutscene;
3. displays story rows 385 through 388; and
4. calls `startFadeInCutSceneAfterWarp`.

That last client operation is the upstream contract for the following
private-to-public Canopy reload. Replacing it with `processEvent100_1` did not
merely show the wrong text. It omitted the client state transition on which
the reload and tutorial-widget docking depend.

An earlier defect was upstream of the pearl command. Aether treated the
private-to-public Canopy boundary as an in-place content refresh:

`DeleteAllActors -> 0x00E2(0x10) -> immediate bootstrap -> clear actor cache`

That is not the retail room-exit contract. The official
`move_out_of_room.pcapng` capture shows:

`EndEvent -> 0x00E2(0x0F) -> destination bootstrap -> mass-delete keep-list commit -> client 0x0007(-1)`

The room-exit correction remains valid, and the latest run confirms that it
fixed actor delivery performance. It did not fix the linkpearl crash. The
2026-07-27 trace proves the public Canopy bootstrap completed, all destination
actors and event conditions were sent, the keep-list transaction committed,
and the client returned the valid `0x0007(-1)` ready acknowledgement before
the tutorial notice began.

The remaining defect is the tutorial event lifecycle itself. One earlier
combination sent `processEventTu_001` in a legacy `0x2B8`
`RunEventFunction` and queued `EndEvent` in the same World relay frame. That
combination faults while tutorial widget 15 is docking. The next combination
corrected the function packet to the captured `0xB0` envelope but was tested
on a manually reset, already inconsistent character. That run did not receive
an `EventUpdate`; it was not evidence that a clean client transaction is
one-way.

The clean Test Four run resolves the ambiguity. The client starts the
director-owned `noticeEvent` at `20:39:42.045824Z`. Aether sends compact
`processEventTu_001` at `20:39:42.047788Z` and calls `EndEvent` only
0.207 ms later, before any client response. The Confirm window appears, starts
docking, and the client faults at `ffxivgame+0x492550`. That instruction is
`mov 0x4(%ecx), %ecx` with `ECX == 0`: the UI completion path is dereferencing
an event object that Aether already destroyed.

The shipped Lua body does not explicitly yield, but `openTutorialWidget(1,
15)` starts native response-bearing UI work. Server Lua completion and native
UI transaction completion are different lifetimes. The server must retain
the original director owner until the client returns `0x012E EventUpdate`,
then resume the coroutine and send `EndEvent`.

The corrected contract therefore uses `callClientFunction` with the captured
`0xB0` envelope. There is no timeout, detached owner, synthetic reply, or
character recovery path. The later pearl click remains an independent
`commandRequest` event owned by the static NPC-linkshell command actor and
routes to the pending quest's `onNpcLS` handler.

A subsequent Build 21999 run exposed a more fundamental transition and
transport mismatch. Aether generated the entire public-Canopy destination
bootstrap about 5 ms after `0x00E2(0x0F)`, emitted all nearby actors in roughly
0.1 seconds, and the World relay wrapped and flushed every Map subpacket as a
separate uncompressed base frame.

The retail room-exit capture does neither. Frame 49 sends the source
`EndEvent` and `0x00E2(0x0F)`. The first destination `AddActor` is frame 87,
6.059397 seconds later. The remaining destination actors are streamed in
groups of at most eight, about every 120-150 ms, and the keep-list transaction
is committed 1.269541 seconds after the first destination actor. Those actor
records are carried in compressed multi-subpacket World frames.

This timing is part of the protocol state machine, not cosmetic latency. The
old implementation was feeding `AddActor` records into the client while the
room-exit teardown and resource load were still active. It accounted for the
delayed actor appearance and transition instability, but the fully completed
2026-07-27 bootstrap disproves it as the cause of the later tutorial fault.

The corrected implementation is a two-phase transition:

1. send only the source `EndEvent` and room-exit state `0x0F`;
2. hold destination state for six seconds without blocking packet processing;
3. send the player/inventory bootstrap;
4. wait 440 ms, then stream no more than eight destination actors every
   140 ms;
5. commit the actor keep list only after the final actor group; and
6. unlock updates and run destination `onZoneIn`.

The World relay now groups each Map delivery into bounded compressed
multi-subpacket frames instead of flushing each field independently.

Retail frame 117 closes the actor stream with:

`0x0006 -> 0x000A(first 32 IDs) -> 0x0008(8) -> 0x0008(5) -> 0x0007`

These opcodes are a mass-delete keep-list transaction. `0x000A` is the fixed
32-ID body and `0x0008` is a counted body containing up to eight IDs. They are
semantically interchangeable keep-list bodies; `0x000A` is not a required
"primary actor table." Matching the observed `0x000A` form improves wire
parity but is not treated as the cause of the crash.

The client fault at `ffxivgame+0x492550` dereferences the object pointer at
offset four after a caller supplies a null object. Its cross-references sit in
the quest/Lua value-dispatch cluster. It is not an `AddActor` handler. The
earlier AddActor attribution was incorrect.

The repeatedly logged client-to-server opcode `0x0130` is also identified.
It is not an event packet despite sharing the numeric opcode with
server-to-client `RunEventFunction`. The client machine code emits a 32-byte
message (16-byte game payload) from its list-object add/delete completion
path after actor bootstrap. The observed payload is:

`{ actorId, 0x2711, 0, 0 }`

where `0x2711` is the actor-list/party type tag. The client emits this
acknowledgement pair as part of its actor-spawn pipeline. It requires no
server reply. Aether now decodes, validates, and traces it without changing
quest or event state.

## Correct opening sequence

| Stage | Client function/presentation | Server transition |
| --- | --- | --- |
| Wolves complete | `Man0g0.processEvent020_1` | Set `Man0g0` phase 10; enter `PrivateAreaMasterPast` type 1 |
| Opening exit | `Man0g1.processEvent100`; instance help rows 353/354 | Replace quest 110005 with 110006; close the source event; enter Canopy type 2 at phase 0 |
| Private Canopy | Optional `processEvent100_2` through `_9` NPC conversations | Remain phase 0 |
| First Miounne talk | Await `processEvent110` / `man0g110` | Grant Adventurers' Guild linkpearl; set phase 5; close talk; register after-warp director; reload public Canopy |
| Public Canopy room exit | `0x00E2(0x0F)`, six-second teardown, then paced destination bootstrap and keep-list commit | Stream at most eight actors every 120-150 ms in compressed grouped World frames; do not wipe actors; wait for the captured `0x0007(-1)` ready acknowledgement |
| Destination notice | Run response-bearing `processEventTu_001` under the acknowledged type-5 director notice using the retail `0xB0` envelope | Keep the director event alive until client `0x012E EventUpdate`, then end it once |
| Linkpearl click | NPC-linkshell message row 330 | Clear the pending message and finish the tutorial; remain phase 5 |
| Camp Bentbranch attunement | `processEvent013` or `_2` | Advance to phase 10 |
| Return to Miounne | `processEvent114`, then `processEvent115` | Advance through phases 12 and 15 |

The linkpearl read does not advance the main quest. Camp Bentbranch is the
phase-10 boundary.

## Evidence matrix

### Shipped client

The 1.23b `Man0g1` bytecode is authoritative for function meaning and client
side effects:

- `processEvent100` plays `man0g100` and requests fade-in after warp.
- `processEvent100_1` contains only world-message rows 353 and 354.
- `processEvent100_2` through `_9` are Canopy NPC talk turns.
- `processEvent110` performs the desktop-mode reset, plays `man0g110`, emits
  rows 385-388, and requests fade-in after warp.
- `processEvent110_2` is Miounne's repeat talk.
- `processEventTu_001` configures tutorial masks and opens tutorial widget 15.
  Its Lua body contains no explicit dialogue choice, but
  `openTutorialWidget(1, 15)` starts client-owned UI work whose animation
  outlives the immediate script dispatch. Absence of a Lua `return` is not
  evidence that the server may close the owning event in the same packet
  batch.
- `processEvent013` and `_2` are the later NPC-link messages associated with
  the Bentbranch step.

The shipped `Man0l1` and `Man0u1` bytecode contain byte-for-byte equivalent
`processEventTu_001` bodies. This is a shared city-opening client contract,
not a Gridania-specific invented mechanic.

Source inspected:
`/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV/client/script/tp5rq/r75w9s1v/x9w/x9wj3i.le.lpb`.

### Legacy Meteor

Legacy Meteor preserves the matching actor split:

- `gridania_opening_exit.lua` awaits `processEvent100`, replaces the quest,
  sends rows 353/354, and enters Canopy private area 2.
- the private-area Miounne actor awaits `processEvent110` and then reloads the
  public Canopy.

Its commented after-warp block is incomplete, so Legacy is evidence for event
placement rather than a complete runnable implementation.

### Ul'dah / Momodi comparison

The shipped `Man0u1` client contains its own `processEventTu_001`, but Momodi's
server progression does not keep her briefing event open for the later pearl
read. It runs `processEvent010`, grants/queues the pearl, advances to phase 5,
ends the NPC event, and reloads the public Quicksand. Its later pearl click is
handled by `Man0u1.onNpcLS`.

That does not prove Gridania should omit its tutorial: the original Gridania
quest hook explicitly calls the tutorial and its `onNpcLS` closes tutorial
mode. It does prove that the tutorial cannot make the Miounne/director event
the owner of the later pearl click.

### Garlemald

Garlemald supplies useful implementation mechanics for the linkpearl owner actor,
the zero-based player slot versus one-based quest message pack, persisted
calling/extra flags, and the after-warp director.

It is not authoritative for this Gridania ordering. Its unique Miounne actor
still uses `processEvent110`, while its later active quest hook moved
`processEvent100_1` onto Miounne. The first commit of that hook is explicitly
unfinished and even sends a literal `Test` notice. The meteor-decomp project
also describes Garlemald quest Lua as best-effort rather than canonical.
Its current loaded `Man0g1.onNotice` sends raw `processEventTu_001` followed by
an immediate `EndEvent`, the same failing lifecycle now observed in Aether.
Garlemald's own documentation separately diagnoses this exact
`ffxivgame+0x492550` null-owner fault for notice cinematics and prescribes
waiting for `EventUpdate`; its `Man0g1` issue remains open. That implementation
is useful comparison material, but it is not treated as authoritative.

### Official room-exit capture

`ffxiv_traces/move_out_of_room.pcapng` is the closest available retail
transition analogue:

- frame 49 contains `EndEvent` followed by `0x00E2` state `0x0F`;
- frame 87 contains the first destination `AddActor`, 6.059397 seconds later;
- destination actors arrive in batches of at most eight approximately every
  120-150 ms;
- frame 117 closes that bootstrap with `0x0006`, one fixed-width `0x000A`
  keep-list body containing 32 IDs, two counted `0x0008` keep-list bodies
  containing 8 and 5 IDs, then `0x0007`, 1.269541 seconds after frame 87;
- frame 225 is the client zone-ready game message with trailing value `-1`.

The capture contains no leading game-message `DeleteAllActors` before the
destination actor rebuild. The bounded regression fixture is
`tests/fixtures/trace-evidence/world-room-exit-observed.json`.

Across all 54 supplied official packet captures, 200 server-to-client
`RunEventFunction` packets were found:

- all 200 use a `0xB0` subpacket envelope;
- 114 share a compressed frame with unrelated traffic, but none shares a
  frame with `EndEvent`;
- none is followed by `EndEvent` before a client `0x012E EventUpdate`; and
- the shortest observed Run-to-End interval is 0.583 seconds.

The corpus does not contain this exact Gridania tutorial, so it cannot
establish a wall-clock delay for it. It does establish the shared
response-bearing function contract: every observed Run retains its owner
until an `EventUpdate`, and none is immediately closed by the server.

### Runtime diagnostics

Ian Test's final pre-fix trace used for the actor-side diagnosis is:

`20260726T235431.035Z-796c13ff281340a59fee21adaf0f66b5/map-20260726-235434.jsonl`.

Runtime diagnostics are used only to locate where AetherXIV stops and to
verify the packets AetherXIV emitted. They are not evidence of retail order.
For example, the last failing run records:

`processEvent100` -> private Canopy -> Miounne `processEvent110` -> phase 5
and linkpearl grant -> wipe/`0x10` public reload -> delayed destination notice.

The latest failing trace is:

`20260727T141708.164Z-e34a384086f94df88bce4c31559f396c/map-20260727-141800.jsonl`.

It shows the corrected destination actor stream finishing at
`14:34:11.821Z`, the client returning `0x0007(-1)` at `14:34:12.210Z`, and the
director notice starting at `14:34:12.552Z`. Aether then emits the `0x2B8`
`processEventTu_001` call and `EndEvent` together at `14:34:12.553Z`. No
client `EventUpdate` follows; position traffic stops eight seconds later and
the socket closes. This locates the first divergence after a successful
Canopy transition.

An earlier manually reset run is:

`20260727T160504.655Z-ff2c84addab54cdcb153d4877d29ec3a/map-20260727-160538.jsonl`.

It shows a compact Run followed by no EventUpdate. Because that character had
been manually advanced and repeatedly reset across incompatible lifecycle
experiments, this run is retained only as a failed-combination record and is
not treated as client-contract evidence.

The clean organic trace used for the event-lifecycle diagnosis is:

`20260727T202615.456Z-30cebbf655ca4f56939cfe2f2bcb5cef/map-20260727-202619.jsonl`.

It records the director `noticeEvent`, compact `processEventTu_001`, and
premature `EndEvent` within 2.2 ms. The UI then faults at the same null-owner
instruction. All six preceding response-bearing client functions in that
same run receive `EventUpdate` before their owning event ends.

### Historical progression references

Archived 1.x walkthroughs put the actions in the same story order: speak to
Miounne at the Adventurers' Guild, receive the Adventurers' Guild linkpearl
and direction to Camp Bentbranch, then attune at Bentbranch and continue via
the linkpearl. This corroborates the client and script evidence; it is not
being used to infer packet format.

## Failed combinations that must not be repeated

| Combination | Observed failure | Rejected reason |
| --- | --- | --- |
| Omit the tutorial | Pearl glows but click emits no command; actors are delayed | Leaves the client after-warp mode unresolved |
| Detached owner-zero tutorial | Pearl remains unresponsive | No retail ownership evidence; compact event envelope was also violated |
| End notice before detached tutorial | Movement/command state remains inconsistent | Splits one destination notice into an invented second transaction |
| Await `processEventTu_001` while retaining the `0x2B8` envelope and earlier broken transition | Notice coroutine did not resume | Does not test the captured compact envelope on a completed destination bootstrap |
| Run owner-bound tutorial on wipe/`0x10` reload | Widget docks partway and the client may fault | Director/actor ownership was already invalidated upstream |
| Run tutorial and `EndEvent` in the same relay batch | Widget docks partway, then faults at `ffxivgame+0x492550` | Clears the event owner before client UI completion; no official Run/End pair has this ordering |
| Treat the manually reset compact-await run as authoritative | Movement and menus remained locked in that saved state | A stale/manual state cannot disprove the clean client lifecycle or the retail Run/Update/End corpus |
| Substitute `processEvent100_1` for Miounne | Wrong scene and client state | Client bytecode proves it only shows instance-help rows 353/354 |
| Character-specific login repair | Can mask one saved character but not new characters | Not retail behavior and does not repair the transition |
| Release deferred notice on every `0x0007` | Premature/repeated notice dispatch | Retail login emits unrelated `0x0007` messages; ready is the `-1` form |
| Treat `0x000A` as a required primary actor table | Alternates between the same actor failure and an inert pearl | Garlemald and packet structure identify both `0x000A` and `0x0008` as mass-delete keep-list bodies; this did not repair transition timing |
| Send the destination bootstrap immediately after `0x0F` | NPCs arrive late; movement and events remain locked; AddActor may fault | Retail holds the room exit for 6.059397 seconds before the first destination actor |
| Flush every actor field as its own immediate World frame | The client receives an unpaced actor burst unlike retail | Retail uses compressed multi-subpacket frames and batches at most eight actors every 120-150 ms |
| Allow `0x3000` uncompressed bytes in one compressed World frame | New-character login faults before the opening scene | Every compressed World frame in the supplied retail capture corpus is bounded at `0xFE0` uncompressed body bytes; the larger invented limit combined multiple retail frames into one |
| Treat prior emulator runtime traces as retail truth | Repeats locally invented sequences | Runtime traces diagnose Aether output only |

## Event and transition contract

1. Await the doorway `processEvent100`, send its instance-help rows, and end
   that source event before changing private-area actor tables.
2. Await Miounne's `processEvent110` before mutating phase/linkpearl state.
3. Set linkpearl slot 0 to owned/calling/extra and persist phase 5 before the
   public reload.
4. Register the after-warp director as login-scoped and defer its notice until
   the client acknowledges destination zone-in.
5. Cross the private-area boundary with `0x00E2(0x0F)` and no actor wipe.
   Defer destination bootstrap for six seconds, then send the player state,
   pause 440 ms, stream at most eight actors every 140 ms in bounded
   compressed World frames, and only then commit the mass-delete keep list.
6. Release the deferred notice only after a valid client `0x0007` completion
   whose trailing signed value is `-1`.
7. Run response-bearing `processEventTu_001` inside that type-5 destination
   notice using the retail `0xB0` envelope. Park the coroutine until the
   matching client `0x012E EventUpdate`, then end the notice exactly once.
8. Treat the later icon click as its own event owned by actor `0xA0F05E95`,
   route it to `Man0g1.onNpcLS`, display row 330, clear the pending message,
   finish tutorial mode, and end that event.

No character-specific login repair is part of this contract.

## Reusable test state

With the stack offline, Ian Test (character id 19) was transactionally reset
to the server's post-wolves, pre-Canopy checkpoint:

- zone 155, `PrivateAreaMasterPast` type 1;
- position `(175.38, -1.21, -1156.51)`, rotation `-2.1`;
- `Man0g0` (110005) phase 10, flags 3;
- no `Man0g1` row and no NPC-linkshell rows.

The latest pre-reset database backup is
`ffxiv_server-before-ian-test-build-21999-detached-overlay-reset-20260726T221537Z.sql`
with its adjacent SHA-256 file under
`~/Library/Application Support/AetherXIV/Backups/Database`.

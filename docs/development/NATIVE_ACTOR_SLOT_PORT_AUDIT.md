# Native actor-slot port audit

Date: 2026-07-27

Status: implementation-ready diagnosis; no runtime fix is included in this
pass.

## Decision

The Gridania waiter question is closed. Test Five's Release run contains the
trace signature unique to the response-aware waiter. Do not spend the next
pass changing tutorial UI timing, synthesizing replies, or adding another
event-lifecycle workaround.

The remaining defect is actor identity.

Retail does not assign resident actors by database load order. Each area has a
native actor namespace and stable slots within it. The public Carline Canopy
capture proves all of the following:

- area master: `0x44D80001`, native slot `0x01`;
- weather director: `0x44D80002`, native slot `0x02`;
- Miounne: `0x44D80008`, native slot `0x08`;
- after-quest director: `0x44D80037`, native slot `0x37`; and
- every resident actor observed in the destination bootstrap retains its own
  non-contiguous native slot.

For public Canopy, zone ID 155 (`0x09B`), the wire identity is:

```text
0x40000000 | (0x09B << 19) | nativeSlot
          = 0x44D80000 | nativeSlot
```

The area master object name, `_areaMaster@09B00`, also carries the raw zone
and private-area scope. The actor ID and object name are separate parts of one
identity contract; neither may be generated from database row order.

## What the current port does wrong

The current implementation loses the native slot before runtime:

1. `server_spawn_locations` has only its repository primary key `id`; it has
   no native actor slot.
2. `StaticActorSpawnRecord` and the normalized `static_actor_spawns` path also
   omit the native slot.
3. `WorldManager.LoadSpawnLocations` neither selects `id` nor orders the
   query. It constructs `SpawnLocation` without any stable identity field.
4. `Area.SpawnActor` calls `AllocateSpawnedActorNumber()`. Public allocation
   begins at 1 and private allocation begins at the invented value `0x700`.
5. Battle NPCs, resident NPCs, scripted spawns, and directors share these
   counters. Startup battle-NPC counts and runtime execution history can
   therefore renumber later actors even if SQL happens to return the same row
   order.
6. `Area` is constructed with `base(id)`, so its inherited `actorId` is the
   raw zone number. Public Canopy's area master is consequently sent as
   `0x0000009B`, not `0x44D80001`.
7. `mWeatherDirector` is declared and sent conditionally, but nothing creates
   or assigns it. Slot `0x02` is absent as a weather director.
8. `CreateDirector` takes the next shared counter value, so the after-quest
   director is not guaranteed to be `0x44D80037`.
9. `SendZoneInstanceSnapshot` commits `zone.actorId`, so the final keep-list
   reinforces the raw area-master ID and the shifted actor table.

Assuming the current SQL happens to be read in primary-key order, public
Canopy begins like this:

```text
current slot 0x01 = Emoni          (retail area master slot)
current slot 0x02 = retainer bell  (retail weather-director slot)
current slot 0x0E = Miounne        (retail Miounne slot is 0x08)
```

This explains both failure modes. The client can wait forever for an event
owner in its expected native slot, or dereference the wrong class of object
from a slot that was populated by an unrelated database row. A UI-only patch
can change which failure wins the race, but it cannot make the actor table
valid.

## Trace-confirmed public Canopy layout

The following mapping comes from
`ffxiv_traces/move_out_of_room.pcapng`, destination frames 97 through 117.
Actor class IDs, positions, and source spawn rows agree exactly. The full
wire actor ID is `0x44D80000 | slot`.

| Slot | Role or current spawn | Actor class |
| ---: | --- | ---: |
| `0x01` | area master | `ZoneMasterFstF0` |
| `0x02` | weather director | `80003` |
| `0x04` | spawn 583, `lionnellais` | `1500055` |
| `0x07` | spawn 585, `hida` | `1500056` |
| `0x08` | spawn 587, `miounne` | `1000230` |
| `0x09` | spawn 578, `tierney` | `1000456` |
| `0x0A` | spawn 580, `gontrant` | `1000457` |
| `0x0B` | spawn 576, `vkorolon` | `1000458` |
| `0x0C` | spawn 595, `anene` | `1000427` |
| `0x0D` | spawn 597, `sylbyrt` | `1000428` |
| `0x0E` | spawn 596, `honga_vunga` | `1000429` |
| `0x0F` | spawn 603, `nonco_menanco` | `1000430` |
| `0x10` | spawn 606, `l'tandhaa` | `1000431` |
| `0x11` | spawn 607, `pofufu` | `1000432` |
| `0x12` | spawn 609, `drividot` | `1000433` |
| `0x13` | spawn 608, `odilie` | `1000434` |
| `0x14` | spawn 611, `basewin` | `1000435` |
| `0x15` | spawn 612, `seikfrae` | `1000436` |
| `0x16` | spawn 594, `edasshym` | `1000437` |
| `0x17` | spawn 574, `emoni` | `1001183` |
| `0x18` | spawn 588, `gyles` | `1001184` |
| `0x1D` | spawn 604, `flavielle` | `1001459` |
| `0x1F` | spawn 591, `aeduin` | `1600092` |
| `0x20` | spawn 601, `memama` | `1001706` |
| `0x21` | spawn 599, `pfarahr` | `1001707` |
| `0x22` | spawn 615, `beaudonet` | `1001708` |
| `0x23` | spawn 600, `fryswyde` | `1001709` |
| `0x24` | spawn 602, `willielmus` | `1001710` |
| `0x26` | spawn 577, `zagylhaemr` | `1600100` |
| `0x27` | spawn 586, `naih_khamazom` | `1600119` |
| `0x2A` | spawn 593, `serpent_private_hill` | `1500334` |
| `0x2B` | spawn 592, `torsefers` | `1500393` |
| `0x2C` | spawn 581, `serpent_private_hodder` | `1002090` |
| `0x2D` | spawn 582, `serpent_private_dauremant` | `1002091` |
| `0x30` | spawn 605, unnamed | `1099046` |
| `0x31` | spawn 584, unnamed | `1090490` |
| `0x32` | spawn 579, unnamed | `1099063` |
| `0x33` | spawn 575, `retainerbell_gridania1` | `1200027` |
| `0x34` | spawn 610, `task_board` | `1200195` |
| `0x35`, `0x36` | spawns 589/590, the two ship-port objects | `5900011` |
| `0x37` | after-quest director | `80009` |

The two ship-port rows have identical class and position data. The capture
proves that their native slots are `0x35` and `0x36`, but it cannot distinguish
which database `uniqueId` owns which slot. Do not pretend that pairing is
trace-confirmed; resolve it from client layout data before assigning the two
names.

The same capture does not observe every actor elsewhere in zone 155. In
particular, current public spawns 613, 614, 632, and 721 through 724 are not in
this local destination set. Unobserved gaps are not permission to assign those
rows to convenient holes. Their slots need client-layout or additional
trace evidence.

## Required implementation

### 1. Make native identity a first-class value

Add one checked composer/decomposer for the 1.x non-player actor format:

```text
kind: bits 28..31
zone namespace: bits 19..27
native slot: bits 0..18
```

It must reject a zone above `0x1FF`, a slot above `0x7FFFF`, zero where a
declared native role disallows it, and any duplicate slot in one area scope.
NPCs, area masters, weather directors, and scripted native directors must all
use this one implementation.

Do not fix this by changing only the packet source ID. The actor object's
identity, dictionaries, event owners, event targets, group membership,
instance cache, actor name, spawn packets, and keep-list must agree.

### 2. Split raw zone identity from wire actor identity

`Area.actorId` currently serves two incompatible meanings. The fix needs
separate properties:

- raw zone/territory ID, used by `zoneList`, `GetZone`, player persistence,
  Lua `GetZoneID`, map selection, seamless-boundary logic, and database
  lookups; and
- composite area-master actor ID, used by actor packets, event ownership, the
  client actor table, and the zone-instance keep-list.

If `Area.actorId` becomes the composite actor ID, every existing raw-zone use
must move to the raw property in the same change. In particular, audit
`WorldManager`, `Player.SendZoneInPackets`, `SetMapPacket`,
`Actor.GenerateActorName`, `Director`, `GuildleveDirector`, and all private
area parent-zone assignments.

Private-area database primary keys are not zone namespaces. Private and
content areas must compose actors with the parent retail zone namespace plus
the area's own native slot table. The current private start value `0x700` is
not evidence and must not be promoted into the new model.

### 3. Port slots through every data layer

Add a native-slot field to the canonical actor spawn record and carry it
without reinterpretation through:

- the reviewed seed artifact;
- `StaticActorSpawnRecord` and V1 import/export artifacts;
- normalized `static_actor_spawns`;
- the direct-core `server_spawn_locations` compatibility schema;
- repositories and database loaders;
- `WorldManager.LoadSpawnLocations`;
- `SpawnLocation`; and
- `Npc` construction and actor-name generation.

The canonical uniqueness key must be area scope plus native slot. Do not rely
on a nullable multi-column unique key whose `NULL` values allow duplicates in
MariaDB; use an explicit area-scope key or normalized non-null scope fields.

Keep `spawn_id` as repository identity. It is not, and must never again be
used as, the wire slot.

### 4. Create the system actors at their native slots

For authoritative public Canopy:

- create the area master as `0x44D80001`;
- create and retain a weather director as `0x44D80002`;
- bind it as `/Director/Weather/WeatherDirector` with director class ID
  `80003`;
- create the after-quest director at `0x44D80037`;
- bind it as `/Director/AfterQuestWarpDirector` with director class ID
  `80009`; and
- ensure the area master, weather director, and active after-quest director
  are each spawned once and kept in the final instance snapshot.

`CreateDirector(path, ...)` needs a native-slot-aware path for resident
directors. It must not silently fall back to the transient counter when a
director is declared native.

### 5. Separate resident and transient allocation

Resident actors use imported native slots. Transient battle/content actors
need a separate, explicitly defined allocation domain with collision checks.
The domain must not consume or renumber resident slots.

Before constructing an actor, reserve its full actor ID atomically. Duplicate
native identities must fail with area, role, source row, and slot in the
error. The current behavior—constructing first and merely tracing a duplicate
when adding to `mActorList`—is too late and can still emit inconsistent
references.

For a staged global port, distinguish areas whose slot catalog is
authoritative from legacy-unverified areas. An authoritative area must fail
startup on a missing or duplicate required slot. An unported area may retain
a clearly logged compatibility mode temporarily, but it must not be labeled
native-correct.

### 6. Preserve the settled transition and waiter work

The six-second room-exit delay, bounded actor batches, mass-delete keep-list
transaction, `0x0007(-1)` readiness gate, response-aware event waiter, and
`0xB0` `RunEventFunction` envelope are separate, already-supported protocol
findings. Keep them while replacing the actor identities they carry.

Do not add another UI workaround to compensate for a bad actor table.

## Regression gates

The next pass is not complete until tests prove all of these:

1. Native actor composition for zone 155 produces:
   `0x44D80001`, `0x44D80002`, `0x44D80008`, and `0x44D80037`.
2. The Canopy bootstrap instantiates the exact roles at those four IDs.
3. Miounne's object name is generated from slot `0x08`, matching the captured
   `pplStd_fst0Twn01_07@09B00`.
4. The area master is `_areaMaster@09B00` with source actor
   `0x44D80001`; no area-master spawn or keep-list entry uses
   `0x0000009B`.
5. The weather director is present, has slot `0x02`, class path
   `/Director/Weather/WeatherDirector`, and class ID `80003`.
6. The after-quest director has slot `0x37`, class path
   `/Director/AfterQuestWarpDirector`, and class ID `80009`.
7. Reversing or randomizing database row order does not change any resident
   actor ID or actor name.
8. Inserting an unrelated spawn does not renumber Miounne or either director.
9. Battle-NPC load count and runtime spawn history do not renumber resident
   actors.
10. Duplicate native slots fail before any spawn packet is queued.
11. The final `0x0006 -> 0x000A/0x0008 -> 0x0007` keep-list contains the same
    composite IDs that were instantiated.
12. A clean Gridania run reaches Miounne, completes the room exit, receives
    the ready acknowledgement, starts the slot-`0x37` notice, receives
    `EventUpdate`, and ends the event without a no-reply lock or actor-manager
    fault.

Add a fixture-level assertion against the official room-exit capture rather
than testing only hand-written constants. Also add a schema/seed validation
test so a later migration cannot drop or duplicate native slots while leaving
the C# tests green.

## Non-solutions

Do not:

- add `ORDER BY id` and call the actor IDs stable;
- equate `spawn_id` with native slot;
- fill unobserved slot gaps sequentially;
- hardcode only Miounne while leaving the area master and directors wrong;
- rewrite only the keep-list IDs;
- send the raw zone number as an actor;
- treat the private-area row ID as the actor namespace;
- let a missing native slot fall back silently to a shared counter; or
- reopen the waiter/UI timing question to mask actor identity corruption.

Those approaches preserve the structural defect and guarantee future drift.

# Umbra Map Travel — development milestone

Status: API, framework service registration, plugin UI, server mesh query, geometry-derived height bounds, and session-owned preview-token
bookkeeping are implemented. Native map selection and authenticated server messaging are
not implemented. This is not yet an end-to-end in-game teleport feature.
No client offsets, map calibration, server acknowledgement, or mesh coverage
are fabricated. Nothing here automatically enables unrestricted server travel.

## Components

- `IUmbraMapService`: read-only native map selection converted to world X/Z,
  including zone, map, floor, session and revision.
- `IUmbraTravelService`: asynchronous preview and execution contracts. The
  preview returns candidate world positions, adjustment distance, expiry and an
  opaque server token. Raw navmesh internals remain on the server.
- Framework: capability-scoped lookup, input validation, cancellation and
  unavailable adapters. A preview-only plugin cannot call warp successfully.
- `Aether.Umbra.MapTravel`: plugin window and `/maptravel` registration. A pin
  can be checked, an ambiguous landing requires an explicit selection, and Warp
  is offered only after a valid preview. Pin/session changes and expiry discard
  old previews. Execution is not retried automatically after lost acknowledgement.
- `NavmeshLandingResolver`: destination-only SharpNav surface enumeration,
  height lookup and bounded horizontal snap. Returns all distinct nearby
  surfaces conservatively; a higher layer must group/rank them by map floor.
  No origin-to-destination walking route is required.

## Build and local verification

From the repository root:

```sh
dotnet build "AetherXIV Launcher/Umbra/Aether.Umbra.MapTravel/Aether.Umbra.MapTravel.csproj"
dotnet build "AetherXIV Launcher/Umbra/Aether.Umbra.Framework/Aether.Umbra.Framework.csproj"
dotnet build src/AetherXIV.Core.Map/AetherXIV.Core.Map.csproj
dotnet test "AetherXIV Launcher/Umbra/Aether.Umbra.MapTravel.Tests/Aether.Umbra.MapTravel.Tests.csproj"
```

The test project follows this repository's existing local-only `*Tests*`
convention and is ignored by Git. It exercises missing adapters, capability
restrictions, invalid coordinates, cancellation, lost acknowledgement, late
previews, pin/session changes, floor selection, expiry, and actual height queries
against `Data/navmesh/wil0Field01.snb`. These are not live client tests.

The build produces the plugin DLL and `umbra-plugin.json`; add their output
folder through Umbra's developer plugin discovery for development. Use a rebuilt
framework and PluginApi assembly from this checkout. This milestone is not
added to the official signed repository or release bundle.

## Remaining integration work

1. Verify the native 1.23b map selection binding and per-map calibration. Convert
   map-local coordinates (accounting for pan, zoom, offsets and floor) to world
   X/Z. Publish immutable selections through `UmbraMapService.Publish`; clear
   selections on logout, map invalidation and adapter loss. Never carry a pin
   across character sessions. Implement a landing-marker overlay once its native
   drawing binding is verified; the current plugin shows coordinates as text.
2. Verify game-to-mesh axes using several known player/ground positions. Existing
   `NavmeshUtils` passes game values directly in one path and also contains axis
   conversion helpers. The new resolver deliberately accepts **mesh coordinates**,
   not an unverified claim that these are game coordinates.
3. Connect `UmbraTravelService.SetTransport` to authenticated game/session
   messaging. Do not expose teleport operations on the developer HTTP bridge: its bearer
   credential is not player-session authentication. The wire protocol and handlers still need implementation.
4. On the destination map thread, check travel policy, zone/private-instance
   identity, mesh availability and verified transforms. Derive vertical bounds
   from the mesh/floor metadata, not the character's current height. Resolve,
   group candidates by surface/floor, and check access and landing clearance.
   Navmesh walkability alone does not validate dynamic obstacles or permissions.
5. Issue a short-lived token bound to character, session, destination, mesh
   revision and allowed candidate IDs. Execute by atomically consuming it,
   revalidating the landing, and invoking existing `DoPlayerMoveInZone` or
   `DoZoneChange`. Report completion only after authoritative acknowledgement.
   Store request outcomes so a reconnect can resolve uncertain delivery without
   a second warp. `TravelPreviewStore` now implements bounded, single-use token
   bookkeeping for the current server session; handler/movement wiring and
   reconnect outcome recovery remain unimplemented.
6. Extend mesh coverage. Only `wil0Field01.snb` is bundled. Without valid geometry,
   return unavailable/no landing; do not assume Y=0 or use the origin's height.

## Intended player flow

Place a pin on a detailed zone map, check the landing, choose a level only when
ambiguous, then Warp. Show any snap adjustment before execution. Do not silently
move the destination far from the pin. A world overview should lead into the
appropriate zone map before an exact landing is chosen.

## Installable plugin and repository verification

Map Travel is a third-party plugin. It is not registered with the framework's
system plugin host and is not included in the bundled repository. Its solution
entry is for compilation only. Use the normal custom repository installation
flow to test delivery, rather than developer-folder discovery.

Build an experimental package and schema-1 catalog:

```sh
python3 "AetherXIV Launcher/Umbra/Aether.Umbra.MapTravel/package.py" \
  --base-url http://127.0.0.1:18797 --output "Developer Plugins/Map Travel"
```

This writes `map-travel-0.1.1.zip` and `umbra-repository.json` with actual byte size
and SHA-256, `built_in: false`, and installation disabled pending user enablement.
Only the plugin DLL, manifest and README are packaged. Neither PluginApi nor
SharpNav is privately bundled. The script authors artifacts; it does not install,
serve or publish them. The localhost URL is only usable when those artifacts are
served on that same host. Supply the actual HTTPS base URL when hosting elsewhere.

The exact artifact can be tested through production fetching and installation:

```sh
dotnet test "AetherXIV Launcher/Umbra/Aether.Umbra.Framework.Tests/Aether.Umbra.Framework.Tests.csproj" \
  --filter FullyQualifiedName~UmbraMapTravelRepositoryTests
```

**Development only:** Map Travel now requires API 2.1 and framework 2.1.0.
Older 2.0 bundles must reject it; native adapters are still unavailable.
See [the API and client structure inventory](UMBRA_CLIENT_API_INVENTORY.md) for the
before/after list, confirmed ownership and remaining evidence requirements.

## API 2.1 integration checkpoint

The calibration algorithm is implemented and tested with synthetic measured-point
fixtures; there are no verified per-map measurements yet. On 2026-09-17 the local
Umbra bridge at port 8797 refused connections, so native observation and in-game
end-to-end validation could not proceed. The installed client executable was
located, but string matches alone are not evidence for map structs or callbacks.

The world packet processor currently selects a route from packet target IDs and
the map processor looks up sessions from source IDs. Those observations alone do
not prove connection-bound authorization. Before travel handler wiring, trace the
connection/session ownership checks and reject mismatches. Do not introduce a
separate HTTP travel listener or claim a plugin-provided session ID authenticates
an operation. Server request handlers and live map hooks remain unimplemented.


## Native map investigation, September 17 evening

The installed script is `MapNavigationWidget`, not `MapWidget`. Its asset is
`client/script/n1635q/x9uw9o139q1vwn1635q.le.lpb`; decoded bytecode is retained
locally under `.local-evidence/umbra-map-travel/lpb/MapNavigationWidget.luac`.
The existing LPB decoder was reused; no client assets were modified.

Verified script-level observations:

- `setDisplayLocation`, source lines 1085–1091, writes `Layout` and `Rect` on
  `CustomControl_MapNavigation`, then toggles `Ready` false/true.
- `zoomOut` and `zoomIn`, lines 1099–1108 and 1116–1125, read the control's `Scale`.
  These Lua methods do not implement the scale change themselves.
- `processUICommandEvent`, lines 222–322, handles `MapScreenControl.NaviRowUpdated`
  and reads `NaviRow` to update region/area labels. Its marker buttons select
  existing marker/menu entries; this is not evidence of arbitrary world-pin input.
- The custom control accepts a `Marker` property. Its coordinate units and
  encoding must be traced before using it to display a travel destination.

The next binding target is the native map control's property/input implementation.
Layout/Rect/Scale names alone do not establish coordinate transforms or memory
layouts. Source map IDs must not be treated as authoritative destination zone IDs.

Server routing still looks up a target session from a packet header while the
world connection also has an `owner`. A travel extension must bind its requests
to that connection owner, validate source/target identity, and continue through
the existing world-to-map relay. It must not use the developer bridge as gameplay
authentication or introduce a second server listener.

## September 17 implementation: preview lifecycle and native property evidence

`TravelPreviewStore` is owned by the existing map `Session` and closed by
`BeginEnding`. It issues cryptographically random 256-bit tokens scoped to that
store, immutable candidate snapshots, and a 15-second preview lifetime measured
with monotonic time. Claims compare server-derived zone, private area name/type,
map, floor and geometry revision. Atomic acquisition prevents concurrent or
repeated execution. Completion (including failure/uncertainty) is retained for
same-session duplicate requests; changing the candidate cannot trigger another
execution. Capacity is bounded and cannot evict an in-progress execution.
This component does not authenticate packets, authorize travel, or call movement.
A reconnect creates a different store; persistent reconnect recovery is still absent.

`NavmeshLandingResolver.Resolve(mesh, query, x, z)` derives vertical bounds from
both coarse and detailed mesh vertices, then uses the existing surface query.
It does not borrow the player's current height. Callers must supply the matching
mesh/query and still verify coordinate axes, access and dynamic clearance.

The read-only `tools/Development/inspect-umbra-map-bindings.py` now verifies the
exact executable and map-script SHA-256, walks all 43 native property
initializers, and correlates their indices with getter/setter branches. Run:

```sh
python3 tools/Development/inspect-umbra-map-bindings.py \
  '/Volumes/Dev2/SquareEnix/FINAL FANTASY XIV' \
  --output .local-evidence/umbra-map-travel/native-properties.json
```

Confirmed property-storage offsets include Scale `0x450`, Layout `0x470`,
Rect `0x478`, Ready `0x480`, PlayerPosX `0x4f8`, PlayerPosY `0x500`, and NaviRow
`0x574`. These are internal dependency-property storage offsets, not a public
world-coordinate struct. In particular, PlayerPosY must not be interpreted as
world elevation. The tool never edits client files or live process memory.

Validation: 24 Map Travel tests pass, including concurrent token claims, expired
or stale previews, wrong-session access, immutable candidates, failure replay,
capacity, session termination and the bundled real mesh. The map server build
passes. These checks do not establish live travel functionality.

The live dev bridge was initially offline, then reconnected to the user-opened
client (bridge session `a545b5333e0642d782d7735fab345534`, exact build verified).
Read-only inspection confirmed the loaded `MapNavigationWidget` class and an
instance whose native script entity refers to that class. This does not yet
identify a usable native map-control pointer. The bridge subsequently disconnected;
the cause was not established. The temporary existing tutorial Lua diagnostic
hook was cleared and its original instructions restored before that disconnect.
No guessed map-click binding or new network opcode has been deployed. Next
required evidence is the active native control, observed pan/zoom/player positions,
game-to-mesh axis verification, and a verified authenticated send/receive path
through the existing world/map connection. The installable plugin remains unavailable for actual warp
until those adapters and handlers are connected.

### Bridge recovery and active-control observation

The disconnect was traced to the managed accept loop: every idle 15-second accept
was treated as a stall and the listener was aborted/recreated. At 20:28:06 the
replacement failed three times with an existing-prefix registration conflict.
The game remained responsive. Toggling the bridge restored the native fallback,
which supports memory reads but not the managed `/snapshot` endpoint.

The accept loop now waits normally while idle, recreates a transport on actual
accept failure, and aborts on cancellation. `IsRunning` checks both listener and
pump state; restarting a dead service cancels and disposes its previous state.
Seventeen portable bridge/hook tests pass. The managed DLL is staged in the
launcher bundle and all 205 receipt entries verify. It requires the next client
launch to load; it has not yet passed a live idle-duration check.

Read-only inspection found a candidate MapScreenControl at `0x41286178` in this
process. Its primary and secondary vtables and class descriptor match the native
constructor. Initial Scale was 1.0, Layout/Rect were -1, and NaviRow was 800.
Liveness and units are not yet established; a user-driven zoom comparison is
pending. Addresses are process-specific and are not runtime bindings.
`observe-umbra-map-control.py` records bounded, structurally checked property
snapshots for those comparisons, without memory writes or function invocation.


### Camp movement observation (20:47)

The user's Lower La Noscea / Bearded Rock screenshots show movement from grid
(25, 29) to (24, 29). The visible map menu contains Change Map and Close; no zoom
control is established. The user reports map-background clicks reaching the
world and character movement remaining available. The requested zoom comparison
is therefore replaced by movement observations; native Scale alone does not
prove a player-facing zoom feature.

The previous control pointer failed its structural checks after travelling to
the camp. The Lua MapNavigationWidget class and instance were also recreated.
The observer correctly refused to interpret the stale object. Runtime integration
must track control lifetime and reacquire after zone/map changes. The screenshot
coordinates are displayed grid values, not yet calibrated server X/Z or elevation.
No current camp control pointer or world/grid transform has been verified.

### Data-first map extraction

Extended the existing `research-client-sheets.py` with `--maps`; run using
`/usr/bin/python3`. It extracts ten installed-client tables into
`.local-evidence/umbra-map-travel/sheets`, preserving schema/resource hashes,
types, column indices and row IDs. All 1,823 rows passed the existing byte-offset
and full-block-consumption checks: 98 pieces, 634 markers, 307 map-data rows,
19 actor-map rows, 118 aetheryte-map rows, 61 regions, 427 navigation rows,
111 zone parameters, 6 region parameters and 42 zone-group parameters.

The locally available SeventhUmbral column definitions identify piece columns
1/5/6 as folder/width/height; aetheryte-map column 1 as navigation ID; navigation
column 7 as 2D map ID and column 14 as place ID (indices include ID at column 0).
These are useful reference annotations, not proof of the remaining transform
columns. Navigation row 800 exists and references 2D map 1041, which gives a
concrete data trail for the previously observed native NaviRow=800.

Manual per-map calibration is not the preferred implementation. Next trace the
client's reads of these tables to identify offsets, scale, sections and grid
conversion, then generate the mapping from parsed data. Live observations should
serve as spot checks of the derived transform, not the source of a handwritten
coordinate table. Map registration and terrain-height/mesh coverage remain
separate requirements.

### Native grid conversion recovered from data (2026-09-17)

`tools/Development/verify-umbra-map-grid.py` now reproduces the grid evidence
against the exact executable/script profile and the extracted navigation DAT
bytes. It verified all **427 navigation-row origins** without manual coordinate
calibration. Its generated report is local research evidence, not a runtime map
catalog or permission to travel to those rows.

The native chain (absolute VAs in the supported executable) is:

- `0x6766ba` / `0x6766e1`: navigation columns 3 and 4, zero-based, are converted
  from signed integers to floats, negated, and stored in loader fields +0x20/+0x24.
  The actual indexed column accessor is `0xc9a480`; the preceding row virtual
  call returns its backing data. The extracted schema has identity column indices.
- `0x678131`–`0x67813d`: those origins are copied to MapScreenControl +0x638/+0x63c.
- `0x67964f` calls the actor position getter `0x4cfe30` with parent accumulation
  enabled. `0x679654` and `0x679664` select vector components 0 and 2 (X/Z), then
  call `0x675840`. The middle component is stored separately at +0x618.
- `0x675840`: continuous grid coordinates are `(X - originX) / 100` and
  `(Z - originZ) / 100`. The constant at `0xf55ad0` is double 100. Displayed
  cells use truncation toward zero, not floor. Native mode +0x9f0 == 1 publishes
  -1/-1 instead; an adapter must exclude this mode.

The internal `UmbraMapGrid` implements this grid conversion and its continuous
inverse, preserving fractional cell positions. Creation checks the client hash
and navigation row shape. The source-linked Map Travel suite now passes **31
tests**, including signed offsets, fractional positions, negative-coordinate
truncation, nonfinite inputs, and overflow. Numerical row-800 fixtures are not
live confirmation of which navigation row the camp uses.

This does **not** enable warping yet. The runtime adapter still needs reliable
active-control lifetime/map identity, the rendered map's screen transform and
input capture. Native artwork/marker rendering uses +0x60c/+0x610/+0x614 scale
fields and +0x640/+0x644 center fields; their complete screen-space contract is
not yet established. Server zone/private-area binding, game-to-mesh axes, and
authenticated preview/commit transport are also unresolved. No guessed screen
conversion or destination height was enabled, and this source-only change was
not deployed to the running client.

### Map overlay interaction and managed lifecycle

The intended interaction is **Warp → point selection → landing preview →
Confirm / Cancel**. Map Travel remains a separately installable plugin. Umbra
owns the native map binding and input routing; the plugin owns the travel UI and
calls the existing travel service. Rendering should reuse Umbra's existing DX9
overlay and input handling, without replacing the game's map or adding a server
listener.

Interaction requirements for the native adapter:

- Show a small Warp control inside the verified open map viewport, clear of the
  game's Map Menu and legend. Hide it when the map closes or shows an unsupported
  overview. Screen bounds must come from the active control, not a fixed desktop
  rectangle or screenshot calibration.
- Warp arms one selection. Begin capturing only after its triggering click has
  finished, so that click cannot also pick a destination. Show a neutral purple
  ring/glow beneath the cursor plus “Choose destination · Esc to cancel”. Do not
  imply that a hovered point is walkable before the server resolves it.
- Consume the next destination click before the underlying game sees it. Ignore
  clicks on map menus, the legend or Umbra controls. Convert an accepted point
  with the verified map transform, complete the matching selection request ID,
  then remove the cursor glow and retain a static destination marker.
- Request a server preview immediately. A confirmation modal shows the selected
  map/grid coordinates and exact server-resolved X/Y/Z. If the server adjusts the
  landing, distinguish that destination from the selected point. Multiple valid
  heights require an explicit landing-level choice. No valid landing means no
  Confirm action.
- Escape, Cancel, map close/change, logout and plugin disposal clear capture and
  effects. Confirm submits once. Once submitted, a local Cancel cannot undo the
  server move; show the result or acknowledgement uncertainty without retrying.

Implemented source groundwork: `IUmbraMapService.CurrentView`,
`SelectPinAsync(expectedView, cancellationToken)`, `UmbraMapView`, and optional
`UmbraMapPin.MapPosition` / `UmbraMapGridPosition`. Existing interface implementers
default to an unavailable selection rather than claiming support. The internal
map service tracks one request ID, cancels on view changes and rejects stale or
cross-map completions. Cancellation releases the managed request synchronously.
Map Travel now starts selection from Warp, previews automatically, shows Confirm
and Cancel, and discards cancelled/obsolete results even if transport ignores
cancellation.

**Not yet implemented:** the map-anchored control, native cursor glow/click capture,
static marker and modal rendering. The current confirmation is rendered inside
the existing plugin window. No native control struct was guessed and no runtime
adapter publishes an available map view yet. Authenticated server transport is
still unresolved. These changes are source-only and have not been deployed or
repackaged into the developer plugin download. The portable suite passes 38
tests; the framework Release build has zero warnings/errors.


### September 17 live observation and startup policy checkpoint (22:15)

The exact-build native update callback now observes a copied 0xa70-byte map
control without a heap scan or retaining a pointer across frames. The existing
Lua dispatcher wraps fingerprint-verified MapNavigationWidget init/closing
methods. Live process 1552 reported a successful binding/open and subsequently a
close callback. The last update snapshot becomes stale with the map closed.
Lifecycle counters remain diagnostic: they do not yet associate a particular
widget instance with the copied native control or prove control-to-screen bounds.
The authenticated developer endpoint `/map/observation` requests five seconds of
read-only capture and no longer depends on visiting the Developer settings page.
It does not accept travel requests.

The current Limsa row is 928, with origin (-1216,-320), center/world position
(-457.568,199.48), native extent 1920x1080 and scale factors 1,1,2. The navigation
field at +0x9f0 is a row ID, not a generic mode. Native marker code computes
local coordinates from viewport half-size plus (worldXZ-centerXZ) multiplied by
the three scale factors. The verifier checks these instructions and constants;
the outer control-to-screen transform, hit regions and runtime zone binding are
still unresolved. The 427 data-derived grid origins remain verified.

The bundled wil0Field01.snb is Central Thanalan (server zone 170). Direct game
X/Y/Z agrees with three existing aetheryte destinations (Black Brush, Cactus
Basin, Four Sisters) within 0.6 vertical units and no horizontal adjustment.
This resolves axes for that mesh only; it does not establish mesh coverage in
Limsa or La Noscea. Distinct overlapping surfaces remain separate candidates.

Map Travel 0.1.1 has been packaged in Developer Plugins/Map Travel and tested
through the ordinary repository installer. The older installed copy is not
replaced by framework deployment. Framework-owned startup suppression now also
blocks window rendering for every third-party plugin until its Installed Open
or settings action is used. Update/Draw callbacks continue, plugin state is not
rewritten, and each load/reload starts suppressed. Legacy plugins that attempt
BeginWindow receive an Open action even without IUmbraPluginUi. This policy is
part of Umbra, not Map Travel.

Verification: 42 portable map/travel tests; 8 package/window-policy tests; exact
binary/Lua fingerprints; real Lua callback result/error/GC tests; native and
managed builds. No live selection overlay or authenticated warp is available yet.

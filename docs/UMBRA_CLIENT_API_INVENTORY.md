# Umbra client structures and plugin API inventory

Source audit: 2026-09-17. Scope: `AetherXIV Launcher/Umbra` production source,
excluding vendored ImGui, OS structures and historical reference projects.
“Implemented” means present in source, not a claim of live validation this session.
No live client map discovery or calibration has been performed for Map Travel.

## Public plugin interfaces: before and after Map Travel

Paths below are relative to `AetherXIV Launcher/Umbra/Aether.Umbra.PluginApi`.

| Interface | Before this work | Current implementation / availability |
|---|---|---|
| `IUmbraPlugin` | Existing | Initialize, Update, Draw, Dispose lifecycle |
| `IUmbraPluginContext` | Existing | Identity, versions, config, capabilities, cancellation, logging, service lookup |
| `IUmbraDrawContext` | Existing | Windows, text, controls, panels and framework UI bridge; no map-coordinate drawing API |
| `IUmbraLogger` | Existing | Plugin logging |
| `IUmbraCommandManager` | Existing | Scoped registrations and managed dispatch; native chat interception is a separate binding |
| `IUmbraChat` | Existing | Print/submit contracts; default native transport is unavailable |
| `IUmbraActorAppearanceService` | Existing | Immutable observation cache; defaults to unavailable until verified adapter activation |
| `IUmbraMapService` | **Added** | Read-only `SelectedPin` plus availability; native adapter not implemented |
| `IUmbraTravelService` | **Added** | Preview and Warp task contracts; authenticated transport not implemented |
| `IUmbraNotificationService` | **Added in 2.1** | `Post(message, tone)` through the native toast display; deployed, live UI verification pending |
| `IUmbraPluginUi` | **Added in 2.1** | Optional `OpenMainUi()` callback, called by the manager for running plugins |
| `IUmbraPluginSettingsUi` | **Added in 2.1** | Optional `OpenSettingsUi()` callback, called by the manager for running plugins |

Existing data types include command registrations/invocations/results, chat
availability/results, appearance snapshots/slots, `UmbraGraphicId`, service
availability, and visual enums. Added data types are `UmbraWorldPosition`,
`UmbraMapPin`, `UmbraLandingCandidate`, `UmbraTravelPreview`, `UmbraTravelResult`
and `UmbraTravelStatus`. These are managed DTOs/value types, **not native client
memory layouts**, even where C# uses `record struct`.

New capability names: `client.map.read`, `travel.preview`, `travel.warp`, `notifications.post`.
They use the existing `UmbraPluginContext.GetService<T>()` capability gate.
Warp requires the preview capability too. Capability declarations control
framework service access; they do not authenticate a player or sandbox arbitrary
managed plugin code.

## Native structures present before this work

| Structure | Source | What it represents |
|---|---|---|
| `LegacyMainMenuEntry` | `Aether.Umbra.Bootstrap/dllmain.cpp` | Client-facing packed menu entry: `short command`, `int label`, `short enabled`; source uses pack 1 (8 bytes) |
| `LegacyMainMenuHookState` | Same | Umbra-owned hook bookkeeping, pointers and saved bytes; not a native map object |
| `JumpHook` | Same | Umbra detour state; not game actor/map data |
| `UmbraRenderEventV1` | Same | Umbra-owned native/managed rendering ABI, eight 4-byte fields |
| `UmbraNativeRenderEvent` | `Aether.Umbra.Framework/UmbraRenderBridge.cs` | Managed counterpart, sequential pack 4; not client-memory map data |
| `hostfxr_initialize_parameters` | Bootstrap | .NET hosting ABI |
| `OverlayVertex`, `OverlayRect`, `UmbraTheme`, `UmbraThemeTuning` | Bootstrap | Umbra rendering/style data |

The Lua call hook and breakpoint stub use explicit capture-buffer offsets and
client observations rather than typed general-purpose map structs. Their current
Lua matching is tutorial-specific. Capture layouts are Umbra instrumentation
ABIs; their existence does not establish a map selection callback.

**New native client structures added by Map Travel: none.**

## Relevant existing interop facilities

- `UmbraClientBuildCatalog`: one exact 1.23b executable SHA-256 profile. It is a
  build identity allowlist, not a catalog of verified map field offsets.
- `UmbraReadOnlyMemory`: bounded memory reads/scans and module/build inspection.
- `UmbraMemoryWatchService`, `UmbraBreakpointService`, `UmbraLuaCallHookService`:
  development observation facilities, not public map APIs.
- `UmbraNativeUi` and `UmbraRenderBridge`: Umbra bootstrap UI/render interop.

Do not expose these internals through Map Travel or clone them in the plugin.
Use the existing observation facilities to discover a binding, then add only the
verified typed view and adapter to the framework.

## Still absent / unverified

- Native map object, click handler, viewport/pan/zoom fields and selected-floor layout.
- Map-local to world coordinate transforms and their per-map calibration data.
- Verified game-to-navmesh transform for the bundled mesh.
- General public current-player-position API backed by a verified client binding.
- Authenticated Map Travel request/response envelope and client send/receive adapter.
- Server preview-token store, request handlers, revalidation and completion tracking.
- Map landing-marker overlay binding and robust candidate grouping by floor.

Do not add guessed structures or offsets to fill these gaps. For each native
binding, record exact build hash, address resolution, field offsets/types,
thread/lifetime rules, supporting observations and invalidation behavior. For
coordinates, record calibration points plus independent residual checks under
pan/zoom and floor changes. Unknown builds must remain unavailable.

## Ownership and no-duplicate-system rules

| Concern | Existing owner / extension point |
|---|---|
| Catalog fetching, cache, health | `UmbraRepositoryFetcher` / registry |
| Download, SHA-256/size validation, staging, backup | `UmbraPluginInstaller` |
| Enable, disable, load, unload | `UmbraRuntime` / `UmbraThirdPartyPluginHost` |
| Client memory and native bindings | Framework/bootstrap interop |
| Public map/travel service contracts | `Aether.Umbra.PluginApi` |
| Navmesh data and queries | Existing map-server SharpNav path; new `NavmeshLandingResolver` shares it |
| Actual character movement | Existing `WorldManager.DoPlayerMoveInZone` / `DoZoneChange` |

Map Travel must stay an installable third-party package, not a registered system
plugin, bundled catalog entry, new installer, separate movement engine or parallel
server listener. The repository authoring script only builds a ZIP and schema-1
catalog. Serving static package files for development is not a gameplay service.

The developer HTTP bridge uses a bearer credential. It is inaccurate to call it
unauthenticated; however that credential is **not game-session authentication**,
so it is not the travel transport.

## Release boundary

API **2.1** and framework **2.1.0** now identify the new contracts. Map Travel
requires both in its manifest and generated catalog. SDK/PluginApi packages are
2.1.0; launcher compatibility, framework receipt verification and build defaults
have been updated. Existing API 2.0 plugin manifests remain valid on 2.1.
Native map and authenticated travel adapters remain unavailable regardless of
version until their bindings have been verified.

`UmbraMapCalibration` is now an internal, tested affine-calibration helper. It
requires three non-collinear anchors and at least two distinct independent check
points. It contains no native fields, shipped calibration values or map IDs.


## Validation performed

Local managed tests exercise normal custom repository install/load, update,
cached catalog fallback and restart using Map Travel. A further test fetches the
exact authored 0.1.0 ZIP/catalog over an ephemeral loopback HTTP server and uses
the production fetcher/installer/runtime. Test repositories are temporary; user
repository settings are not changed. No public hosting or live in-game smoke test
has been performed. Tests follow the repository's ignored local `*Tests*` policy.

## Main-menu correction — 2026-09-17 live diagnostic run

The native LegacyMainMenu hook is **not a working main-menu integration**. After
opening the visible menu and selecting Attributes, all diagnostic hook counters
remained zero. The shipped Lua MainMenuWidget script builds 19 rows and dispatches
Attributes to StatusWidget. Automatic installation of the inactive 17-row native
patch has been removed from source. Its retained research structures and offsets
must not be presented as verified active client UI APIs. A replacement Lua binding is implemented and the candidate DLL is installed for
the next launch. Live row display and selection are still pending verification.
No dock changes are involved; the conflicting F10 manager shortcut is removed.


## Main-menu Lua binding — implementation and evidence

`Aether.Umbra.Bootstrap/UmbraLuaMainMenu.inl` owns the integration; it is a
framework entry point, not part of the separately installable Map Travel plugin.
It observes `luaD_call` at RVA `0x9cfc30`, after the original call completes, and
matches MainMenuWidget's root bytecode and every constant. The root match then
uses a protected Lua call to wrap only `init` and
`processUICommandSelectionChanged`. Both original handlers are retained as
Lua-managed closure upvalues. The tutorial observer at `luaD_precall` is untouched.

The init wrapper calls the original first, then uses existing WidgetBaseClass
methods: `getListPropertyCount`, `insertListProperty`, `setListProperty`,
`getListProperty`, `updateListProperty` and `deleteListProperty` for rollback.
The new row is index 19, labeled **Umbra Plugin Manager**. Indices 0–18 retain their
original meanings. A second init recognizes its existing row. Unexpected row
counts or a foreign row 19 are left alone. Selection forwards the original
arguments, then verifies that row 19 still carries the Umbra label, hides the game menu and queues the existing manager to open on
the render thread. It does not alter dock state or add a server/UI transport.

### Private native API addresses

All addresses are **RVAs**, relative to ffxivgame.exe. Function identities were
checked against local x86 disassembly and [Lua 5.1's API source](https://www.lua.org/source/5.1/lapi.c.html).
These are bootstrap-private bindings, not general plugin APIs.

| API | RVA |
|---|---|
| `lua_gettop` / `lua_settop` | `0x9cdaf0` / `0x9cdb00` |
| `lua_checkstack` / `lua_pushvalue` | `0x9cd9d0` / `0x9cdcb0` |
| `lua_type` / `lua_topointer` | `0x9cdce0` / `0x9ce0e0` |
| `lua_tolstring` / `lua_tonumber` | `0x9cdf80` / `0x9cded0` |
| `lua_pushstring` / `lua_pushnumber` | `0x9ce1f0` / `0x9ce170` |
| `lua_pushlightuserdata` / `lua_touserdata` | `0x9ce380` / `0x9ce090` |
| `lua_pushcclosure` | `0x9ce2c0` |
| `lua_getfield` / `lua_setfield` | `0x9ce400` / `0x9ce620` |
| `lua_call` / `lua_pcall` / `lua_cpcall` | `0x9ce8a0` / `0x9ce900` / `0x9ce9e0` |
| `lua_error` | `0x9cebc0` |
| `lua_getupvalue` / `lua_setupvalue` | `0x9ced90` / `0x9cee20` |
| `luaD_call` (detour) | `0x9cfc30` |

The installer checks the PE timestamp/image size, 21 API entry signatures and
the detour instruction bytes before writing a five-byte jump. The standalone
read-only verifier additionally requires the exact executable and script SHA-256.
The trampoline copies two whole, non-relative instructions. No script file is
modified, no Proto is rewritten, and no game allocation is replaced with an OS
allocation. Runtime code/constant fingerprints are FNV-1a consistency checks,
not a cryptographic security boundary.

### Private client views used

| View | Fields used | Evidence / limits |
|---|---|---|
| `TValue` | 16-byte stride; value +0, tag +8 | Existing precall disassembly plus API push/get implementations |
| Lua closure (`LClosure`) | isC +6, Proto pointer +0x10 | Inner menu init closure fingerprint verified in the live session |
| Native method closure (`CClosure`) | isC +6, nupvalues +7, native function +0x10; upvalue 1 +0x18, upvalue 2 +0x28, each a 16-byte TValue | Menu wrapper at RVA 0x907f20; userdata + original Lua method confirmed through live reads |
| `Proto` | constants +8, code +0x0c, sizek +0x28, sizecode +0x2c, line bounds +0x3c/+0x40, parameter count +0x49 | Lua 5.1 layout and client VM/API disassembly; live init fingerprint matched `72f4b5c0` |
| `TString` | length +0x0c, bytes +0x10 | Client `lua_tolstring` disassembly |
| `LuaMainMenu::Api`, `RowAttempt` | Function-pointer table and rollback flag | Umbra-owned state, not client structures |

All VM work runs synchronously on the calling Lua thread. Original closures are
GC-rooted as upvalues; stack restoration uses API indices rather than saved stack
pointers. Only an atomic manager-open request crosses to the render thread. The
bootstrap is pinned so its callbacks remain resident for the game process lifetime.
There is no public raw Lua-state, navmesh, map or server-coordinate API added here.

### Validation boundary

- `tools/Development/verify-umbra-menu-bindings.py <client-root>` verifies exact
  local assets, API signatures, detour bytes, and root/init/selection fingerprints.
  It reuses the existing LPB decoder (override its location with `--decoder`).
- `tools/Development/test-umbra-lua-menu.py --lua-source <Lua-5.1.5-source>`
  exercises the production callback bodies against a real Lua 5.1 VM with a
  simulated widget: stock actions/rows, arguments/results, duplicate prevention,
  error propagation, rollback, unexpected models and GC lifetime. Build
  `src/liblua.a` for the same host architecture first.
- The x86 bootstrap compiles and links successfully.
- These checks do **not** establish live menu layout, mouse/controller activation
  or native row API behavior. Those require the relaunched client.

As of 18:02 local time on September 17, the launcher bundle contains matching
native and managed framework 2.1.0 / API 2.1, including the updated PluginApi DLL.
The receipt verifier passes for all 205 framework files. The currently open game
still uses the earlier diagnostic build until relaunched. All 44 managed framework
tests pass, plus the Lua callback harness and local executable/script verifier.
Live menu display/click, picker, artwork, and notification checks remain pending.
Map Travel's native map and authenticated travel adapters remain unavailable.


## Plugin manager additions (API 2.1, September 17)

- `IUmbraNotificationService.Post(message, tone)`: scoped through `GetService`, requires `notifications.post`, uses the existing native toast display. The plugin status notification toggle controls plugin messages and update notices. Queue length and message length are bounded.
- Optional `IUmbraPluginUi.OpenMainUi()` and `IUmbraPluginSettingsUi.OpenSettingsUi()`: the manager exposes buttons only when a running plugin implements the corresponding interface. Existing API 2.0 plugins remain compatible.
- Local folders, DLLs with adjacent manifests, and ZIPs are staged as catalog packages. Discovery checks the manifest, current API/framework compatibility, and managed entry assembly metadata without executing it. Installation uses the existing archive verification, staging, provenance, and disabled-by-default flow.
- Catalog `icon_url` artwork is fetched asynchronously into a bounded file cache and decoded through WIC into DX9 textures. Missing or invalid images retain the existing generated artwork. Native image rendering is framework-private, not a public plugin API.

### Verified native method wrapper

The live September 17 session exposed `MainMenuWidget.init` as a C closure, not the Lua closure itself. Its function is `ffxivgame.exe + 0x907f20`, with two upvalues: engine userdata (1) and the original Lua method (2). Reading upvalue 2 through the authenticated development bridge verified the existing init fingerprint `72f4b5c0`, lines 63–169, 329 opcodes, 99 constants. The binding preserves the engine wrapper and replaces only its second upvalue using the client's Lua API, including GC write barriers.

Additional private Lua APIs: `lua_getupvalue` RVA `0x9ced90`; `lua_setupvalue` RVA `0x9cee20`. Their entry bytes are checked with the other 19 API signatures before enabling the binding. These addresses and layouts are private to the verified client; plugins do not receive raw access.

Live menu row rendering and clicking still require verification after deployment; a successful fingerprint or build is not UI verification.


### Additional framework-private UI exports

`UmbraUiImage(path, size)` renders a cached local catalog image through WIC/DX9.
`UmbraUiPostNotification(message, tone)` adds a status message to the existing toast
stack. These exports are used by managed framework code; plugins use the public
notification service and optional UI interfaces instead of P/Invoke.


### Live discovery verification, 18:31 September 17

The user's screenshot confirms local folder discovery produces a Map Travel card
in Discover, labeled Local · unreviewed, API 2.1, with an Install button. The user
has not installed it. This verifies discovery without automatic installation.
The earlier 18:25 screenshot exposes a separate `OPENFILENAME` marshalling error
in the file picker. Its `StringBuilder` structure field was replaced with an
explicitly allocated UTF-16 buffer pointer; the ABI layout is regression-tested.
ZIP/DLL picker UI and installation still require live verification.


### Live menu activation and world-rendering correction

The September 17 18:34 screenshot confirms the manager row renders in the logged-in
Main Menu. Native diagnostics record two successful selections with zero menu
errors. The authenticated `/snapshot` reports `plugin_manager_open: true` while
`viewport_width` and `viewport_height` are both 1. The manager request reached the
framework; the presentation hook inherited the game's offscreen viewport.

The presentation path now selects the presented swapchain backbuffer, uses its
actual dimensions, draws in a balanced scene, and restores the game's render
targets, depth surface, and viewport. A mock-DX9 test exercises this production
function, including failure restoration. Live visual validation remains pending.

The menu adds row 19 **Umbra Plugin Manager** and row 20 **Umbra Settings**.
Settings routes to the same managed settings request as the dock; neither menu
action changes the dock. The Lua harness covers both actions, duplicate prevention,
and rollback when either row's properties fail.


### User-confirmed in-game rendering, 19:04 September 17

The user reports the corrected menu and in-game Umbra UI work well. The native
backbuffer correction is shared by all managed plugin Draw callbacks. This does
not independently validate every plugin's own drawing code. The accompanying
screenshot path was unavailable; this confirmation is based on the user's report.
Map Travel itself remains incomplete; native pin selection and authenticated
server travel are not enabled by the rendering fix.


### Map Travel server and native-map investigation checkpoint

No additional public plugin API was added in this pass. API 2.1's map/travel
contracts remain the intended interface; plugins do not receive raw native or
navmesh pointers.

- Existing map `Session` now owns an internal `TravelPreviewStore`, revoked on
  session end. Internal `TravelDestination`, `TravelLanding`, `TravelGrant`,
  `TravelClaim` and `TravelOutcome` are server bookkeeping records, not client
  structs or wire messages. They are not yet connected to request handlers.
- `NavmeshLandingResolver` can derive the mesh's full vertical bounds from coarse
  and detail vertices. Game/mesh axes remain unverified.
- `inspect-umbra-map-bindings.py` independently verifies the exact 1.23b binary and
  script and inventories 43 native MapScreenControl properties. Their storage
  offsets are evidence for further binding work, not a complete or public client
  struct. Full findings and limitations are in `UMBRA_MAP_TRAVEL.md`.
- Internal `UmbraMapGrid` now derives continuous grid coordinates and displayed
  cells from the native navigation-row offsets. It is not a new public API or
  client-memory struct. The data/binary verifier recovered 427 navigation origins;
  runtime screen selection and server travel remain unavailable pending binding.

### API 2.1 map-selection additions (source, not deployed)

- `IUmbraMapService.CurrentView`: nullable verified open-map identity.
- `IUmbraMapService.SelectPinAsync(UmbraMapView, CancellationToken)`: one cancellable
  destination selection; never performs travel. Returns null on normal cancellation
  or unavailable binding. A token cancelled before entry may throw cancellation.
- `UmbraMapView`: session/revision, zone/map/floor IDs and display name.
- `UmbraMapGridPosition` and optional `UmbraMapPin.MapPosition`: continuous grid
  coordinates supplied only when verified by the adapter, separate from world X/Z.

These are managed contracts, not memory layouts. Existing implementations have
default unavailable behavior for the new interface members. Native cursor/input
and map-window binding remain pending; no new public raw-pointer access was added.


### Map observation and framework startup policy, September 17 late evening

No new public plugin API is required for startup suppression. Umbra owns a
per-load window gate around third-party Draw contexts. Installed Open/settings
releases it for that plugin; reload resets it. Background callbacks and service
notifications continue. This also handles older plugins with an initially true
window flag. The Open callback interfaces remain the preferred way to reopen a
plugin's own closed window.

The internal read-only map observation contains four DWORD header fields and a
0xa70-byte copy of a fingerprint-checked MapScreenControl. Its diagnostic fields
include the navigation row, native drawing dimensions, center, grid origins and
three scale factors. It is not a public, fully mapped client struct. Existing
Lua dispatch now also observes verified MapNavigationWidget init/closing methods.
Neither the raw snapshot nor lifecycle counters currently authorize map selection.
Known Thanalan landmark probes validate direct XYZ for wil0Field01.snb only.

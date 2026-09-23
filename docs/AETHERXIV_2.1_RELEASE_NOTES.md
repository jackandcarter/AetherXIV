# AetherXIV 2.1 Release Notes (Release Candidate)

Release identity: **AetherXIV 2.1, build 22042**.

This is the public release-note draft for the future `2.1` branch. It describes
changes present in this worktree, but it is not a claim that every gameplay path
has passed final live-client acceptance. Features whose scope is still being
proven are identified as **release-candidate** or **pending**.

## Highlights

- A unified `2.1.22042` identity is used by the Core, Launcher, package
  manifests, and release automation.
- The release is organized around two build scopes: **core** (services,
  operator, database package, migrations, configuration, and verification) and
  **full** (core plus Launcher, Umbra, client helpers, and the compatibility
  runtime where applicable).
- macOS, Linux, and SteamOS full packages use the project-pinned Aether.3
  compatibility runtime. Windows remains native and does not package Wine.
- AetherXIV Launcher and Umbra have been substantially reworked around a
  bundled runtime, managed plugins, integrity checks, and an in-game Plugin
  Manager.
- Core includes broader diagnostics and ongoing restoration work for opening
  quests, transitions, actor publication, gear handling, battle presentation,
  and database-backed world content.

## Cross-platform Launcher and Aether.3 runtime

### Bundled, project-controlled compatibility runtime

Full packages for macOS, Linux, and SteamOS ship with the compatibility runtime
selected by the build receipt. The Launcher validates that bundled runtime and
does not discover or substitute Wine, CrossOver, Whisky, a PATH Wine command,
or a user-selected runtime. Windows launches the client natively.

The runtime build recipes, checksum inventory, and packaged launch helpers are
part of the release build. The exact upstream revision and third-party component
receipt must accompany each published artifact; the name **Aether.3** does not
by itself identify an upstream Wine revision.

### Launcher workflow and graphics choices

- First use and relevant runtime/configuration changes create or refresh a
  Launcher-owned readiness receipt. Later launches reuse it only when the
  runtime inventory, client, helper, Umbra state, and prefix generation still
  match.
- The old custom-runtime and custom-prefix selection paths have been removed.
  The Launcher keeps one isolated, managed prefix; **Reset Prefix** remains the
  explicit recovery action.
- **Wine default** is the normal graphics target on compatibility-runtime
  hosts. The previous redundant OpenGL-compatibility value is normalized to
  Wine default. **OpenGL threaded** remains experimental. Linux x64 builds may
  show **DXVK / Vulkan (validated)** only after the bundled x86 D3D9 probe
  succeeds and its cached runtime/host fingerprint still matches.
- Runtime validation reports unmet host prerequisites before launch. Dependency
  installation is explicit and platform-specific; builds and normal launches do
  not silently elevate privileges.
- Core and Launcher are packaged as graphical applications. The release checks
  macOS bundle layout, Windows GUI-subsystem metadata, and Linux/SteamOS
  desktop launch integration and icons.

See the [Launcher guide](LAUNCHER_GUIDE.md),
[dependency guide](BUILD_AND_RUNTIME_DEPENDENCIES.md), and per-platform build
guides for exact host requirements and package contents.

## Umbra framework and plugin development

Umbra 2.1 introduces a stronger separation between the native bootstrap and the
managed plugin framework. Third-party plugins target the versioned managed
Plugin API; they do not reference the native bootstrap or Framework assembly.

- The bundled managed framework provides plugin discovery, manifests,
  capability-gated services, load isolation, safe mode, health/status
  reporting, repository-backed installation, and developer-plugin locations.
- The in-game **Umbra Plugin Manager** is a built-in plugin. It provides
  discovery, installed, updates, repository, settings, and diagnostics views,
  with status/badge presentation for installed plugins.
- The Launcher supplies framework-level controls only: enable Umbra, safe mode,
  and update-service status. Repository management and normal plugin
  installation remain in the in-game Plugin Manager, except for the supported
  Discord Rich Presence integration described below.
- The bundled SDK targets the public Umbra API 2.0 contract and includes an
  SDK, API package, and sample plugin. This API version is independent of the
  AetherXIV product version.
- Umbra includes loopback-only developer bridge, breakpoint, Lua-hook, and
  watch support for authorized development observation. They are not required
  to run the Core or Launcher.

### Discord Rich Presence

The Launcher includes a Discord Rich Presence control. When enabled it ensures
the supported Umbra plugin is available and, on compatibility-runtime hosts,
passes the host Discord IPC location to the Wine-side bridge. The feature is
off by default; it requires both the Launcher setting and the plugin's enabled
state. On Windows the plugin uses Discord's native named-pipe path.

The remote Umbra framework update service is represented in the UI but remains
disabled until its signed service endpoint is deployed. A normal launch uses the
bundled verified framework and does not require that service.

Read the [Umbra SDK and plugin guide](UMBRA_SDK.md) for the supported plugin
contract, capabilities, safety limits, developer bridge, and installation
workflow.

## Core, diagnostics, and service reliability

The Core stack continues to run Lobby, World, Map, Operator, Launcher Services,
and the Direct Core database package as distinct release components.

- `--dev-diagnostics` now emits correlated diagnostic run IDs and monotonic
  trace sequence values across session, event, quest, private-area, actor, and
  Linkpearl operations.
- Event diagnostics distinguish lifecycle routing, event functions, clear/end
  boundaries, quest phase, director ownership, Linkpearl queue state, actor
  publication, marker refresh, and transition handling.
- `--wire-diagnostics` remains opt-in and bounded. It is intended for targeted
  packet investigation, not normal play, so ordinary diagnostics do not pay the
  full wire-preview cost.
- Transition and event recovery changes are being validated against server logs
  and client behavior rather than changing runtime solely to satisfy a test
  assertion.

See [Debugging and bug reporting](DEBUGGING_AND_BUG_REPORTING.md) for safe log
collection and redaction guidance.

## Gameplay restoration and correctness work

The following areas contain source and automated-contract work in the 2.1
candidate. Their inclusion here does **not** mean that every quest chain or
battle formula is retail-complete.

### Opening progression, scenes, and linkshells

- Opening-city quest scripts, transition ownership, private-area actors,
  activation markers, journals, and NPC Linkpearl flows have received targeted
  fixes and tests.
- The Limsa opening includes restoration candidates for its push trigger,
  Musketeers' Guild sequence, Echo/private-area presentation, and Zephyr escort
  path. These require fresh-database migration and full in-game acceptance
  before they can be called fully restored.
- Gridania escort and opening paths retain dedicated recovery and lifecycle
  coverage. Assertions are reviewed against runtime evidence; stale tests are
  not grounds to rewrite working server policy.
- Ul'dah opening progression includes source-backed sequence, journal, and
  Linkpearl handling work. Broader post-opening progression remains an active
  acceptance area.

### World, actors, and city services

- Limsa zone 230 contains four client-layout-backed class-`1200288`
  mini-aetheryte spawn rows. Their data is migration-backed and validated by
  database contracts; NPC/service completion and live teleport acceptance are
  still pending where not explicitly verified.
- Actor identity, spawn metadata, public/private area publication, and
  transition reload behavior have been expanded to support restored scenes and
  content without treating reference-port implementation details as binding
  architecture.

### Inventory, equipment, crafting, and battle foundations

- Equipment handling now has explicit ownership, package, slot, item-type, and
  appearance validation, with database/in-memory consistency and persistence
  checks. Invalid requests are rejected rather than partially applied.
- Equipping a weapon updates class-facing state; soul-crystal handling and
  resulting stat/ability restrictions are covered by the current policy work.
- Crafting/class data, Carpenter progression content, starting-tool flows, and
  inventory/material handling have been expanded in the candidate content set.
- Battle NPC casting presentation and MP/cast-state handling have targeted
  implementation and contract coverage. Auto-attack/stat work is foundational;
  it does not claim a complete retail damage formula or complete NPC ability
  catalogue.

## Database, migration, and packages

- Direct Core packages include the canonical baseline, migration manifest,
  setup tools, backup/restore behavior, and startup preflight checks.
- The required-migration preflight includes the Limsa push-trigger and
  mini-aetheryte/escort migrations, in addition to earlier opening-content
  contracts.
- Fresh-install, upgrade, schema compatibility, migration checksum, and package
  verification are release gates. Database repair failures retain the original
  database backup rather than overwriting it.
- A reusable account-character reset tool is supplied for test environments; it
  requires an explicit account ID and confirmation before it mutates data.

Use [Database setup, updates, and recovery](DATABASE_SETUP_AND_MIGRATION.md)
for production-safe migration and recovery procedures.

## Build and release automation

- Platform entry points support `--scope core` and `--scope full` so server-only
  work can be built independently from Launcher/Umbra/runtime packaging.
- Windows excludes Wine/Aether.3. macOS, Linux, and SteamOS full packages add
  the compatibility runtime; core packages do not.
- Each supported build host has a dependency installer and an early preflight
  path. Installation is opt-in; missing prerequisites produce actionable
  remediation instead of an implicit privileged operation.
- CI validates build identity, source verification, package scopes, database
  contents, and platform package behavior. Artifact, tag, and release naming
  derive from the canonical 2.1 build identity.

Unsigned artifacts are appropriate for engineering validation. Apple
notarization and Windows signing require release-owner credentials and remain
separate publication gates.

## Known limitations and release gates

- This is a release-candidate document, not a final public release declaration.
- Complete live acceptance remains required for the three opening cities,
  Linkpearl presentations, scene exits, escorts, zone transitions, Limsa city
  teleport services, and launcher startup on each supported host.
- The Umbra remote update service is intentionally offline in this candidate.
- Restored gameplay is incremental. It must not be read as a claim of complete
  1.23b quest, crafting, battle, NPC, or damage-formula parity.
- Final artifact receipts must identify the exact bundled runtime revision,
  package hashes, signing state, and notarization/signing verification.

## September 23 runtime and patching update

- Linux and SteamOS include pinned DXVK 3.1.1 for D3D9. The launcher exposes the Vulkan renderer after a capability probe, preserves the selected renderer, and retries failed capability checks after their short cache period.
- Linux Wine builds now require Vulkan and PulseAudio support. Package audits check the Vulkan bridge, XAudio2 2.4/reverb-related modules, audio backend, and shared-library dependencies.
- Runtime packaging verifies the DXVK binary archive and separately pinned license. Fixed GNU tar broken-pipe validation and bundled Wine library resolution during audits.
- Client patching no longer rejects terminal deletions because an installed file differs from historical source metadata. Transactional recovery, archive integrity checks, and version checkpoints protect interrupted patching.
- Temporary Windows sharing/lock violations are retried up to five times with one-second delays. Cancellation remains supported; other I/O errors fail immediately.

The reported Linux XAudio2 reverb startup crash and in-game audio/performance still require desktop validation; successful builds and module audits do not establish those issues are resolved.

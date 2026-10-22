# Umbra plugin development reference

Umbra is AetherXIV Launcher's in-game framework for the supported Final Fantasy
XIV 1.23b client. It provides a versioned managed plugin API, native DirectX 9
render integration, plugin isolation, repository-backed installs, safe mode,
diagnostics, and a loopback-only development bridge.

This document describes the development contract and bundled base runtime
distributed with AetherXIV:

- Umbra API: `2.1`; compatible API 2.0 plugins remain supported.
- Framework implementation: `2.1.2`. Previously installed bundles may still
  provide API 2.0; check the package receipt before using new contracts.
- Plugin target framework: `net10.0-windows` (managed IL, x86 client compatible)
- Recognized client: Final Fantasy XIV 1.23b build `2012.09.19.0001`, x86

Unknown client hashes are not granted client-memory adapters. Plugins must check
service availability instead of assuming that chat or appearance bindings work.

## Enable and use Umbra

The AetherXIV Launcher intentionally exposes only framework-level controls:

1. Turn on **Enable Umbra** to add the verified framework to the game launch.
2. Turn on **Safe Mode** when you want Umbra available without loading third-party
   plugins.
3. The disabled **Umbra Updates (Service Offline)** control becomes **Check for
   Umbra Updates** after the signed Demi Dev Unit service is deployed and
   enabled. Normal launch does not require or automatically contact it.

Plugin installation and updates do not happen in the Launcher. After the
framework starts in game, open the Umbra Plugin Manager to add custom repository
URLs, install or update plugins, and set developer-plugin locations. The
official repository will appear there after a future update service is enabled;
custom HTTPS repositories work without that service.

At startup the Plugin Manager uses its last verified repository cache
immediately, then refreshes repositories after the framework is ready. When
installed plugin manifests have newer repository versions, Umbra shows the
number of available updates in a bottom-right notification. Selecting the
notification opens the existing **Updates** tab.

Start with the [SDK quickstart](../UMBRA_SDK.md) for a plugin project that uses
the public API assembly included with Launcher.

## SDK projects

| Project | Purpose |
|---|---|
| `Aether.Umbra.PluginApi` | Stable contracts referenced by third-party plugins |
| `Aether.Umbra.Sdk` | MSBuild SDK that applies and validates the Umbra 2.1 plugin build contract |
| `Aether.Umbra.Framework` | Bundled base runtime, services, plugin manager, repositories, and development tools |
| `Aether.Umbra.Bootstrap` | Native x86 DirectX 9/Win32 bootstrap loaded into the client |

Plugin projects should reference only `Aether.Umbra.PluginApi`. Do not reference
the Framework assembly or native bootstrap from third-party plugin code. The framework supplies its contracts at runtime; keep its implementation
and bootstrap out of third-party plugin packages.

## Create a plugin

The application downloads currently supply the public API DLL rather than a
separate SDK archive. Use the [direct-assembly quickstart](../UMBRA_SDK.md).
When approved local SDK and API NuGet packages are supplied separately, the
MSBuild SDK form is:

```xml
<Project Sdk="Aether.Umbra.Sdk/2.1.2">
  <PropertyGroup>
    <AssemblyName>Example.Umbra.Plugin</AssemblyName>
  </PropertyGroup>
</Project>
```

The SDK sets `net10.0-windows`, references the public API for compilation,
requires `umbra-plugin.json`, and copies that manifest to the build output. It
rejects projects that override the target framework. Plugin projects must not
reference the Umbra Framework or native bootstrap.

Implement `IUmbraPlugin`:

```csharp
using Aether.Umbra.PluginApi;

public sealed class ExamplePlugin : IUmbraPlugin
{
    private IUmbraPluginContext? context;
    private bool windowOpen = true;

    public string Name => "Example Plugin";

    public void Initialize(IUmbraPluginContext context)
    {
        this.context = context;
        Directory.CreateDirectory(context.ConfigDirectory);
        context.Logger.Info("initialized");
    }

    public void Update(TimeSpan delta) { }

    public void Draw(IUmbraDrawContext draw)
    {
        if (!windowOpen)
            return;

        bool visible = draw.BeginWindow("Example###ExamplePlugin", ref windowOpen);
        try
        {
            if (visible)
                draw.Text("Hello from Umbra.", UmbraTextTone.Accent);
        }
        finally
        {
            draw.EndWindow();
        }
    }

    public void Dispose()
    {
        context?.Logger.Info("disposed");
        context = null;
    }
}
```

Lifecycle callbacks are:

- `Initialize`: acquire services, create configuration storage, and register
  commands.
- `Update`: perform short non-render work.
- `Draw`: emit UI during the render callback. Keep it fast and balanced.
- `Dispose`: release registrations and resources during reload, disable,
  quarantine, or shutdown.

## Plugin manifest

Place `umbra-plugin.json` beside the plugin assembly:

```json
{
  "id": "com.example.umbra.plugin",
  "name": "Example Plugin",
  "version": "1.0.0",
  "api_version": "2.1",
  "entry": "Example.Umbra.Plugin.dll",
  "entry_type": "ExamplePlugin",
  "minimum_framework_version": "2.1.2",
  "target_framework": "net10.0-windows",
  "architecture": "x86",
  "language": "CSharp",
  "enabled": false,
  "capabilities": ["commands.register", "chat.print"]
}
```

| Field | Required | Meaning |
|---|---|---|
| `id` | Yes | Stable, globally unique plugin ID; reverse-domain form is recommended |
| `name` | Yes | Public display name |
| `version` | Yes | Plugin version |
| `api_version` | Yes | Umbra API compatibility requested by the plugin |
| `entry` | Yes | Relative path to the managed entry assembly |
| `entry_type` | No | Fully qualified plugin type; required when multiple public implementations exist |
| `minimum_framework_version` | Yes | Oldest Framework implementation accepted |
| `target_framework` | Yes | Must be `net10.0-windows` for API 2.x |
| `architecture` | Yes | Must be `x86` for the 1.23b client |
| `language` | Yes | Must be `CSharp` for the API 2.x developer contract |
| `enabled` | Yes | Whether the runtime should load the plugin automatically |
| `capabilities` | No | Privileged services requested by the plugin |

Absolute entry paths and `..` traversal are rejected. API compatibility requires
the same major version and a requested minor no newer than the runtime. Framework
compatibility requires the installed version to meet the declared minimum.

## Capabilities and services

| Capability | Service | Access |
|---|---|---|
| `commands.register` | `IUmbraCommandManager` | Register and dispatch plugin slash commands |
| `chat.print` | `IUmbraChat` | Print tagged plugin text through a verified adapter |
| `chat.submit` | `IUmbraChat` | Submit chat input through a verified adapter |
| `client.map.read` | `IUmbraMapService` | Read the current verified map pin in world X/Z |
| `travel.preview` | `IUmbraTravelService` | Request server-resolved landing candidates |
| `travel.warp` | `IUmbraTravelService` | Execute a preview candidate; also requires `travel.preview` |
| `client.appearance.read` | `IUmbraActorAppearanceService` | Read immutable observed appearance snapshots |
| `client.resources.plan` | `IUmbraResourcePlanService` | Stage validated plans; exact-build native loading supports bounded resource replacements |
| `client.appearance.refresh` | `IUmbraCharacterRefreshService` | Typed refresh contract; forced redraw remains suspended |
| `ui.file-dialogs` | `IUmbraFileDialogService` | Asynchronous native file picker |
| `ui.media` | `IUmbraMediaService` | Bounded images, GIF clips, sprite sheets and playback |
| `notifications.post` | `IUmbraNotificationService` | Post bounded UI notifications |
| `plugins.messaging` | `IUmbraMessageBus` | Versioned publisher-qualified JSON messages |
| `diagnostics.performance` | `IUmbraPerformanceService` | Performance snapshots and opt-in traces |
| `client.observations.read` | `IUmbraClientObservationService` | Read-only observations with availability metadata |
| `client.appearance.presets` | `IUmbraAppearancePresetService` | Capture, persist and observe presets; native apply/revert unavailable |

`GetService<T>()` returns `null` when the required capability was not declared.
Chat can be partially granted: print without submit, or submit without print.

The values `ui.draw` and `configuration` are accepted as descriptive manifest
capabilities, but drawing and the plugin config directory are supplied through
the base lifecycle rather than a gated service.

### Map travel (development)

The Map Travel plugin and API contracts are implemented, but the native map
selection adapter and authenticated server travel transport are not connected.
Service availability remains false until those bindings are supplied. See
[Map and travel boundaries](MAP_TRAVEL.md) for availability and integration requirements.

`IUmbraMapService.SelectedPin` contains zone/map/floor identity, world X/Z,
a session identifier and a selection revision. It does not invent a height.
`IUmbraTravelService.PreviewAsync` returns server-resolved positions and an
expiring opaque token. `WarpAsync` accepts that token and a candidate ID, never
an arbitrary client-provided elevation. Plugins need no SharpNav dependency.

## Plugin context

`IUmbraPluginContext` exposes plugin, API, and framework identity; a sanitized
plugin-specific `ConfigDirectory`; declared capabilities; a shutdown token;
scoped logging; and capability-aware service lookup. Treat context and service
objects as runtime-owned and do not retain them after `Dispose`.

## Drawing API

`IUmbraDrawContext` supplies frame timing, viewport dimensions, render-thread
state, content widths, device generation, and plugin-manager state.

Available primitives include:

- Windows and sizing: `BeginWindow`, `EndWindow`, `SetNextWindowSize`.
- Layout: `SameLine`, `Separator`, `Spacing`, `BeginChild`, `BeginPanel`, and
  `EndChild`.
- Text and input: `Text`, `InputText`, `InputInt`, `Checkbox`, `Toggle`,
  `SliderInt`, `SliderFloat`, and `Combo`.
- Disclosure and status: `CollapsingHeader` and `ProgressBar`.
- Actions and visuals: styled `Button`, `Icon`, `Badge`, and `Artwork`.
- Framework action: `RequestPluginManagerOpen`.

Always balance begin/end calls, including early-return and exception paths.
Umbra performs render-state recovery after every plugin callback as a safety net.

The third-party draw budget is 4 milliseconds. Umbra tracks last and peak draw
time and counts over-budget frames. It logs the first slow draw and periodic
reminders thereafter.

## Commands

```csharp
IUmbraCommandManager? commands = context.GetService<IUmbraCommandManager>();
IDisposable? registration = commands?.Register(
    new UmbraCommandRegistration("/example", "Shows the example plugin."),
    invocation => context.Logger.Info(invocation.Arguments));
```

Commands are lowercase and contain 1 to 63 letters, digits, underscores, or
hyphens after `/`. Duplicate commands are rejected. Dispose registrations when
done; Umbra also releases all commands owned by an unloaded plugin.

## Chat

```csharp
IUmbraChat? chat = context.GetService<IUmbraChat>();
if (chat?.Availability.CanPrint == true)
    chat.Print("Ready.", "Example", UmbraChatTone.System);
```

Delivery can be delivered, unavailable, denied, rejected, or failed. The legacy
chat buffer accepts at most 511 UTF-8 bytes. Current builds deliberately report
native chat as unavailable until its client binding is verified.

## Actor appearance observations

`IUmbraActorAppearanceService` is read-only. It exposes immutable snapshots with
actor/model identity, revision, timestamp, source, and the legacy 28-value
appearance table. `UmbraGraphicId` decodes compatible equipment slots into
weapon, equipment, variant, and color components. Plugins cannot publish or
mutate snapshots. A matching exact-build native observer can supply known fields;
unverified builds or older bootstraps receive no active adapter. Test each field
with `IsKnown` before interpreting it.

## Discovery, loading, and isolation

Umbra discovers `umbra-plugin.json` or `plugin.json` directly in the configured
plugin folder and one directory below it. Each plugin loads in a collectible
assembly load context so it can be unloaded or reloaded.

Safe mode loads system plugins only. A third-party plugin is quarantined after
three consecutive callback failures. Runtime status includes load state, errors,
callback duration, peak draw time, and slow-draw count.

The built-in Plugin Manager supports discovery, installed plugins, updates,
repositories, enable/disable, reload, install, and recoverable uninstall.
Uninstall moves a plugin into `Cache/PluginTrash` instead of permanently
deleting it.

## Repositories and package security

Repository and download URLs must use HTTPS; HTTP is allowed only for loopback
development. Repository responses are cached for use when a later fetch fails.
Custom repositories are explicitly user-trusted and do not receive a supported
or reviewed trust label. The current 2.1 AetherXIV release does not require a central
service; a future official repository/update service can be added without
changing the custom-repository contract.

The Repositories tab accepts either a GitHub repository homepage or a direct
HTTPS URL for a JSON plugin index. For a homepage such as
`https://github.com/owner/repository`, Umbra reads
`umbra-repository.json` from that repository's default branch. It never clones
or builds source code on the client. Direct raw GitHub, GitHub Pages, GitHub
Release, and other static HTTPS manifest URLs also work. A custom index may use
a top-level plugin array or a named envelope. Named envelopes supply the display
name shown on the repository card:

```json
{
  "schema_version": 1,
  "repository_name": "Example Umbra Repository",
  "plugins": [
    {
      "id": "example.plugin",
      "name": "Example Plugin",
      "version": "1.0.0",
      "api_version": "2.1",
      "author": "Example Developer",
      "description": "An example Umbra plugin.",
      "download_url": "https://example.invalid/releases/example.plugin-1.0.0.zip",
      "size_bytes": 12345,
      "sha256": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
      "minimum_framework_version": "2.1.2",
      "target_framework": "net10.0-windows",
      "architecture": "x86",
      "language": "CSharp",
      "entry": "Example.Plugin.dll"
    }
  ]
}
```

Every package must contain `umbra-plugin.json` or `plugin.json` at the ZIP root.
Its identity, name, version, API version, minimum framework version, and optional
entry path must match the repository entry, and the declared assembly must exist.
Custom repositories are checksum-verified but remain unreviewed.

Installable entries require identity/version fields, API and minimum framework
versions, URL, archive size, and SHA-256. Umbra verifies size and hash before
extraction, bounds archive size and expansion, and rejects rooted or traversal
archive paths. Installation validates in a staging directory before activation.
Updates preserve the previous package in `Cache/PluginBackups`, and a failed
activation restores the prior installation. Hidden and testing-only entries are
not normally installable.

Each configured source shows its current health, manifest-entry and compatible
plugin counts, last check time, and the most recent error. **View plugins** opens
Discover filtered to that source. A failed refresh keeps the last known-good
cache and marks the source **Cached**; it does not erase working catalog data.
The URL field is cleared only after a repository is validated and saved. Removing
a custom source requires confirmation and never removes already installed
plugins.

Developer-plugin files are never installed, updated, or deleted by Umbra. Safe
mode prevents all custom and developer plugins from loading while leaving the
framework and Plugin Manager available for recovery.

## GitHub custom repository workflow

A plugin developer can host both the index and immutable plugin ZIPs on GitHub:

1. Build the plugin and place `umbra-plugin.json` at the ZIP root beside the
   declared entry assembly.
2. Publish that ZIP on a versioned GitHub Release.
3. Record the release asset's exact HTTPS URL, byte size, and SHA-256 in the
   repository JSON.
4. Commit `umbra-repository.json` at the repository root, or host the JSON at a
   stable raw GitHub or GitHub Pages URL.
5. Ask testers to add either the GitHub repository homepage or raw JSON URL in
   Umbra's in-game **Repositories** tab.
6. Testers can select **View plugins**, choose the package in Discover, and
   install it. New installs remain disabled until explicitly enabled from
   **Installed**.

For example, testers can enter `https://github.com/owner/repository`, while a
direct raw index can look like
`https://raw.githubusercontent.com/owner/repository/main/umbra-repository.json`.
When a newer version is published in the same index, installed users see it in
the in-game **Updates** tab. The Launcher is not involved.

## Developer plugins

The in-game Plugin Manager has a **Load Developer Plugins** option and a
configurable developer-plugin path. A location may point to a plugin DLL,
manifest, or directory. Rescan/reload controls allow iteration without creating
an install package. Umbra treats these locations as developer-owned: it does not
copy, update, quarantine, or delete their source files.

## Development bridge

The optional bridge listens only on `127.0.0.1`, defaults to port `8797`, and is
disabled unless enabled through environment or control state. The managed
framework owns the bridge. A smaller native listener is started only when the
managed framework cannot be hosted, so the two implementations never compete
for the same port.

The managed bridge generates a new 256-bit token for every framework session
and stores it in the local `control.json`. Requests must provide that token as
either `Authorization: Bearer <token>` or `X-Aether-Umbra-Token: <token>`.
Browser-origin requests are rejected even when they originate on the same
machine.

| Method | Path | Purpose |
|---|---|---|
| `GET` | `/status` | Runtime, process, and bridge status |
| `GET` | `/capabilities` | Supported operations and verified-adapter availability |
| `GET` | `/modules` | Loaded modules, ranges, and exact main-executable hash |
| `GET` | `/watches` | Active exact-build, module-relative change watches |
| `GET` | `/events?limit=100` | Recent bounded, sequenced development events |
| `GET` | `/events?after=N&wait_ms=30000` | Cursor-based bounded event long poll |
| `GET` | `/logs?limit=120` | Tail the framework log |
| `GET` | `/observations/actor-appearance` | Verified actor-appearance cache, when available |
| `POST` | `/memory/peek` | Bounded read-only process-memory probe |
| `POST` | `/scan/pattern` | Bounded read-only byte-pattern scan |
| `POST` | `/watch/start` | Start a bounded module-relative change watch |
| `POST` | `/watch/stop` | Stop a change watch by ID |

Memory endpoints accept an absolute address or a safer `module` plus `offset`.
Module-relative requests are rejected when the requested range crosses the
loaded module boundary.

Change watches require the exact cataloged client executable hash. At most
eight may run, each may observe at most 64 bytes, and sampling is limited to
250–5000 milliseconds. A watch records only initial state, changes, errors,
and stop state; it does not assign semantic meaning to the bytes.

Every event carries a bridge-session ID, stable sequence number, UTC timestamp,
and process-monotonic timestamp. The bridge is intended for local development
status and bounded watch operations only.

The framework reports unresolved actor-registry, event-receiver, game-UI, and
network observers explicitly as unavailable. It does not populate those
contracts from guessed offsets. An observer becomes available only after its
layout and signatures are verified against the exact cataloged client hash.

The bridge provides no memory writes, packet mutation, or remote function
invocation. Keep it disabled for ordinary play and do not expose or proxy it
outside the local machine.

The private development companion is not included in these downloads. A local
tool using this documented HTTP interface should read the session token from
control state and keep it out of command history and shared logs.

## Runtime environment

| Variable | Meaning |
|---|---|
| `AETHER_UMBRA_LOG` | Framework log path |
| `AETHER_UMBRA_PLUGIN_DIR` | Plugin discovery/install directory |
| `AETHER_UMBRA_CACHE_DIR` | Repository, config, trash, and dev cache root |
| `AETHER_UMBRA_SAFE_MODE` | `1`, `true`, or `yes` disables third-party plugins |
| `AETHER_UMBRA_DEV_BRIDGE` | Enables the bridge initially |
| `AETHER_UMBRA_DEV_BRIDGE_PORT` | Bridge port from 1024 to 65535 |
| `AETHER_UMBRA_DEV_BRIDGE_DIR` | Bridge state directory |
| `AETHER_UMBRA_DEV_BRIDGE_CONTROL` | Bridge control JSON path |

Launcher-controlled injection also supplies bootstrap/framework paths, load
delay, safe mode, and Wine managed-host preference. Plugins should
use the plugin context rather than reading Launcher variables directly.

## Media and animation

Declare `ui.media`, obtain `IUmbraMediaService` and use the optional
`IUmbraMediaDrawContext` inside Draw for images/image buttons, animated GIFs and
sprite sheets. Decode runs off-thread; the framework owns bounded DX9 uploads
and reset/unload cleanup. No extra decoder or graphics package is needed in a
plugin. SDK projects copy nested `Assets/**/*` automatically.

See [UI media API](UI_MEDIA_API.md) for complete loading/playback/packaging
examples and limits. The [first-plugin quickstart](../UMBRA_SDK.md) provides
a complete starter example before you add media or optional services.

## Build and distribute

Follow the [first-plugin quickstart](../UMBRA_SDK.md) to build from a standalone
workspace. The application release supplies the public API DLL; approved SDK
NuGet packages require a separately configured local feed. Do not assume they
are available from nuget.org.

Package the entry DLL, plugin-owned dependencies, nested assets and
`umbra-plugin.json` at the ZIP root. Keep the framework-owned API and runtime
out of the package. Recompute the archive size and SHA-256 after each build.

## Current SDK limitations

- Native chat print and submit bindings remain unresolved.
- Appearance observations require the matching exact-build native observer.
  Applying/reverting presets and forcing a character redraw remain unavailable.
- Client interop is restricted to one recognized 1.23b executable hash.
- Third-party plugins receive no arbitrary memory-write, packet-mutation, or
  general unsafe-native API.
- Remote framework updates and the official repository remain offline until a
  future update service is deployed. Every
  Launcher release includes its current Umbra framework, while user-added
  custom repositories and local developer plugins continue to work in game.

## Diagnostics extensions

See [Diagnostics and plugin messaging](DIAGNOSTICS.md) for the optional draw extension, bounded telemetry, client observations, and versioned plugin messaging. Use the current PluginApi 2.1.2 assembly, `api_version: "2.1"`, and
`minimum_framework_version: "2.1.2"` for new plugins.

### Managed mod-list controls (framework 2.1.2+)

`IUmbraListDrawContext` is an optional draw-context extension for selectable rows,
content height and the native footer action menu. `ListActionMenu` requires Shift
at Delete activation; managed callers must also recheck `IsShiftHeld` before
performing removal. The native popup closes before returning to managed code, and
all controls obey the plugin host's startup window policy. See [Factory](FACTORY.md).

A standalone author workspace can use the normal .NET `bin/Release/` layout
shown in the quickstart. Keep your editable source separate from installable ZIPs.

## Resources and appearance

[Resource loading and appearance](RESOURCE_AND_APPEARANCE.md) covers session
copies, apply generations, exact-build scope, observation identity, and the
unavailable forced-refresh/preset-writer operations. A managed API contract is
not a guarantee that its native adapter is available on a running client.

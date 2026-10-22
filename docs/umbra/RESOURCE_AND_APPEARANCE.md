# Resource loading and character appearance

Umbra 2.1.2 separates resource mappings, appearance observations, saved presets,
and character refresh requests. Each operation has its own capability and
availability. A plugin must check the returned status rather than assume that
an API type implies a working native operation.

## Current operations

| Operation | Availability in this release |
|---|---|
| Stage and inspect a resource plan | Managed validation and resolution |
| Apply supported PC texture replacements | Exact-build native loading adapter; framework-owned session copies |
| Apply supported body model edits | Bounded, fingerprinted stock Highlander topology edits only |
| General meshes, rigs, materials or new UV layouts | Not a general supported native loading contract |
| Observe character appearance | Exact-build observer with known-field flags and session/spawn identity |
| Capture, save, list and delete appearance presets | Plugin-owned managed persistence |
| Observe a normal visual transition | Bounded, cancellable observation |
| Force character redraw | Suspended and reported unavailable |
| Apply or revert an appearance preset | Native writer not qualified; reported unavailable |
| Assign independent resource collections to actors | Not implemented; logical resource paths are shared client-side |

The supported profile is `ffxiv-1.23b-2012.09.19.0001`, with executable SHA-256
`9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9`.
Unknown builds and mismatched/older native payloads receive no assumed adapter.

## Resource plans

Declare `client.resources.plan` and obtain `IUmbraResourcePlanService`.
A plan contains a client profile and replacements identified by logical game
path, kind, source identity, byte size and SHA-256. `StageAsync` validates the
plan and returns its generation. `ResolvePlanned` inspects the staged mapping.
`ApplyAsync` publishes that generation when supported by the active adapter.

The managed contract can describe more operations than this native adapter
accepts. A swap-path entry, arbitrary model, material or skeleton should not be
treated as supported merely because it can be represented in a plan.

The framework copies supported replacement data into its own session cache
outside the client installation and verifies the bytes before native loading.
The client installation is read-only for this workflow. Native loading does not
depend on retaining a plugin's editable source file or collectible managed buffer.
An unloaded plugin's scope expires; do not retain and reuse its services.

## Apply and cached visuals

Apply publishes mappings for future resource loads. It does not guarantee an
immediate change to an actor whose file data or GPU texture is already cached.
A normal equipment change or character reload can trigger a new load, but a
visual transition alone does not prove the requested texture was reread.

Apply an empty/disabled mapping to restore normal resolution for subsequent
loads. Already cached visuals may persist until a normal reload. Do not edit the
original client files, bypass an availability check, or call a native redraw
function to force the change.

`IUmbraCharacterRefreshService` uses actor/session/spawn identity, expected
appearance revision and resource generation. Its forced-refresh requests remain
unavailable in this release; plugins should display that state rather than
present a successful redraw message.

## Observations and presets

Declare `client.appearance.read` for `IUmbraActorAppearanceService` and
`client.appearance.presets` for `IUmbraAppearancePresetService`. Snapshots contain
session/spawn identity, appearance revision, visual generation, sample time,
source and per-field known flags. Test `IsKnown` before interpreting a value.
The first observed actor is not necessarily the local player.

Presets store sparse captured values in the plugin's own configuration
directory. They contain no native pointer and do not create a permanent
assignment to an actor ID. Identity/revision checks and cancellation protect
against an actor despawning, being replaced, or changing while work is pending.

`WaitForReloadAsync` observes a later normal visual generation for the same
actor spawn. Its result is not a GPU-ready acknowledgement or proof that a new
resource generation is active. Native preset apply/revert remains unavailable.

See [Factory usage](FACTORY.md) for the optional user-facing mod workflow.

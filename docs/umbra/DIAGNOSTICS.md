# Diagnostics and plugin messaging

Umbra API 2.1 provides performance snapshots, explicit trace recording,
client observations, and versioned messages between plugins. New plugins should
compile against the 2.1.2 API assembly and declare a 2.1.2 minimum framework.

## Services and capabilities

| Service | Capability | Purpose |
|---|---|---|
| `IUmbraPerformanceService` | `diagnostics.performance` | Bounded performance snapshots and opt-in traces |
| `IUmbraMessageBus` | `plugins.messaging` | Publisher-qualified JSON messages with cursor-based polling |
| `IUmbraClientObservationService` | `client.observations.read` | Available map/appearance observations with their availability metadata |
| `IUmbraNotificationService` | `notifications.post` | Brief shared UI notifications |
| `IUmbraDiagnosticDraw` | Optional draw-context extension | Plots, tables, tooltips and editor previews |

Get services through `context.GetService<T>()` and check for null. Cast the draw
context to the optional drawing interface before calling its extensions.

```csharp
var metrics = context.GetService<IUmbraPerformanceService>();
var bus = context.GetService<IUmbraMessageBus>();

// In Draw, inside a visible window:
if (draw is IUmbraDiagnosticDraw charts && metrics is not null)
{
    charts.Plot("Frame intervals", metrics.Capture().Frames
        .Select(frame => (float)frame.Milliseconds).ToArray());
}
```

## Interpret measurements correctly

Render-callback intervals are not GPU execution time or guaranteed presentation
FPS. Callback durations include instrumentation overhead. Process working set is
the whole game process, not GPU memory. Client observations are sampled caches,
not an authoritative list of every actor or a substitute for server state.

Performance tracing is explicitly started and stopped. The recorder retains at
most 20,000 Chrome Trace events and reports dropped events. Export locally and
open the JSON in a compatible viewer such as Perfetto. Avoid competing recorders
and do not upload captures automatically.

Drawing limits include 4,096 finite plot samples, tables of at most 8 columns
and 256 rows, and 2,048 characters per cell. Gizmo previews are editor transforms;
they do not move a client actor or modify server placements.

## Messages between plugins

Publish a JSON payload under a short topic and version. The publisher's plugin
ID qualifies the topic: publishing `stats` from `example.plugin` produces
`example.plugin/stats`. Subscribers use `ReadAfter` with their last sequence,
the qualified topic, and the supported version; advance the cursor after reading.

The shared buffer holds 512 messages, with at most 8,192 characters per payload.
Messages can be lost when a subscriber falls behind. Publish a current-state
snapshot when consumers need to recover after a gap. The bus is not durable
storage and does not invoke consumer callbacks or retain their delegates.

## Optional diagnostic plugins

Performance and Client Inspector and Session Monitor are optional packages,
not installed automatically by the application release. If you obtain a
compatible package from a trusted source, install and enable it in the in-game
Plugin Manager and use its **Open** action. Do not follow private framework
source-build paths from an older development guide.

## Local development bridge

The optional development bridge is disabled unless enabled. It listens on
loopback, defaults to port 8797, and requires the per-session token stored in
local control state. Browser-origin requests are rejected. It supports runtime
status, logs, capability checks and bounded read-only observations on a
recognized client build.

Keep the bridge token and raw observations local. An unavailable adapter must
be reported as unavailable. Do not infer permissions to mutate native memory
from a read-only observation or a public service contract.

For routine failures, start with the framework log and the
[bug reporting guide](../DEBUGGING_AND_BUG_REPORTING.md).

# UI media and animation

Umbra's optional `ui.media` capability provides PNG/JPEG/BMP/GIF images, animated
GIF clips, and sprite sheets through the current managed UI and DX9 renderer.
The framework supplies a pinned StbImageSharp decoder under its MIT license option.
Plugin-library artwork also uses this shared asynchronous path and its memory
budgets; the legacy WIC export remains for compatibility.
Plugin projects reference **PluginApi only**; do not ship a decoder, ImGui binding,
Direct3D wrapper or another PluginApi assembly to use this service.

## Loading and drawing

Declare `"ui.media"` in `umbra-plugin.json`. Obtain the service once in Initialize:

```csharp
var media = context.GetService<IUmbraMediaService>();
if (media is not null)
    imageTask = media.LoadAssetAsync("Assets/Media/panel.png", stop.Token).AsTask();
```

Initialize is synchronous. Poll/observe the task in Update, retain its result,
and draw only inside an active visible window:

```csharp
if (draw is IUmbraMediaDrawContext images && image is not null)
{
    images.Image(image, new UmbraImageDrawOptions(192, 112)
    {
        Fit = UmbraImageFit.Contain,
        Uv = UmbraUvRect.Full,
        Tint = System.Numerics.Vector4.One
    });
    if (images.ImageButton("unique-image-button", image, new(96, 56)))
        context.Logger.Info("Clicked");
}
```

Image rectangles reserve the requested layout size. Contain centers without
stretching; Cover crops the source; Stretch fills the rectangle. UVs are
normalized and top-left based. Tint is straight-alpha RGBA in [0,1]. Dimensions
must be finite, 1–8192 UI units. Native images are clipped to the rectangle and
the current ImGui clip region. Image buttons support standard navigation.

`LoadImageAsync(ReadOnlyMemory<byte>)` and `LoadGifAsync(...)` accept encoded bytes
for embedded resources, file-picker results or plugin-generated data. Do not
modify the input memory until the returned operation completes. There is no
implicit URL download or game-resource access.

`LoadAssetAsync`/`LoadGifAssetAsync` resolve forward-slash paths against the host's
plugin manifest directory. Absolute paths, traversal, colons and link/reparse
components are rejected. Scope confinement is a service policy, not a plugin
sandbox; plugins execute managed code in the game process.

Load completion means validated CPU pixels are retained. `IUmbraImage.State`
starts PendingUpload and becomes Ready after a successful draw/upload. Failures
expose `Error`: UploadFailed, BudgetExceeded or RendererUnavailable. While an
upload is pending a placeholder reserves layout space. A budget-delayed GIF keeps
its last uploaded frame. Fatal upload failures retry after reset/reload; memory
budget failures retry when capacity becomes available.

## GIFs and independent playback

```csharp
var clip = await media.LoadGifAssetAsync("Assets/Media/spinner.gif", stop.Token);
var player = media.CreatePlayer(clip);
player.Loop = UmbraAnimationLoop.Source;
player.Play();
// In Draw, inside the window:
images.Animation(player, new(64, 64));
```

A clip owns decoded frame content; each player owns independent timing and one
reusable GPU texture. Dispose the original clip when no longer needed; players
retain its content. GIF handling includes frame rectangles, local/global palettes,
interlacing through stb, transparency, restore-background/restore-previous disposal,
frame delays, and Netscape repeat metadata. Minimum frame delay is 20 ms. Source
play count is total plays: 0 means infinite, absent metadata means once.
GIF plain-text rendering blocks and disposal modes above 3 are rejected.

Players expose Play, Pause, Stop, Seek, Speed (0.01–16), Position, Duration, State
and Error. Loop modes are Source, Once, Forever and Repeat; `PlayCount` is total
plays for Repeat. Stop returns to the first frame. Seek is bounded to Duration.
Drawing the same player multiple times advances/uploads only once per framework
frame. Create another player for an independent clock.

The monotonic clock is independent of clamped UI delta. PauseWhenHidden is the
default: a gap in drawn frames does not advance playback. CatchUp accounts for
elapsed wall time when drawn again. ReducedMotion freezes the current frame;
Stop can select the initial frame first. Controls and drawing belong on the
render thread. No per-frame file reads or image decoding occur.

## Sprite clips and easing

```csharp
var sheet = await media.LoadAssetAsync("Assets/Media/spinner.png", stop.Token);
var frames = Enumerable.Range(0, 8).Select(i => new UmbraSpriteFrame(
    new UmbraUvRect(i / 8f, 0, (i + 1) / 8f, 1), TimeSpan.FromMilliseconds(90))).ToArray();
var clip = media.CreateSpriteClip(sheet, frames);
var player = media.CreatePlayer(clip);
player.Play();
```

Sprite frames use normalized UV rectangles and durations of 20–60000 ms. A clip
has 1–256 frames. Sprite playback and static draws of the same decoded sheet
share its GPU texture; playback changes UVs, not texture contents. Source clip
width/height describe the first sprite frame; each draw preserves the selected
frame's actual aspect ratio. Draw-option UVs are relative to the selected frame.

`UmbraEasing.Linear/EaseIn/EaseOut/SmoothStep(progress)` clamp progress to [0,1].
Use the result for image dimensions and tint opacity, or your own UI state.
No separate animation engine is bundled.

## Lifetime, budgets and DX9

- Dispose images, clips and players. Unload/fault expires the plugin's scope
  before plugin Dispose, cancels queued work, and discards late results. Old
  contexts cannot recreate a scope after reload. A player retains clip content;
  disposing a source image does not invalidate a sprite clip.
- Identical encoded content is deduplicated within a plugin scope while leased.
  Unleased content is released immediately; there is no retained idle LRU cache.
- Inputs: at most 4 MiB encoded, 2048 pixels per dimension, 16 MiB/static image,
  256 GIF frames, and 32 MiB decoded/clip. Retained CPU pixels: 32 MiB/plugin,
  64 MiB/global. GPU pixels: 16 MiB/plugin, 32 MiB/global. Limits combine; a
  maximum-size clip may leave no capacity for another image in its scope.
- At most 256 leases/plugin, 16 pending decode jobs globally, one active decoder.
  Native decoder scratch allocations are bounded to 32 MiB. The active job may
  additionally use up to three canvas-sized managed buffers (at most 48 MiB),
  encoded/rectangle copies and small parser metadata. Retained-pixel quotas are
  not a claim that total process memory is capped at 64 MiB.
- Decode and filesystem reads occur on a worker, serialized to bound scratch and
  decoder globals. Cancellation is cooperative between steps, not an interruption
  of unsafe stb execution. Malformed decode exceptions clean native allocations.
- DX9 create/update/release occurs on the render thread. Uploads use dynamic
  default-pool BGRA8 textures, one mip, row-pitch-aware copies and DISCARD updates.
  The frame budget allows 4 MiB uploads and stops additional work after 2 ms;
  one first large upload is allowed to avoid starvation. This is a scheduling
  budget, not a hard upper bound on driver latency.
- Disposal defers GPU release to the next frame before new draw commands. Native
  IDs are opaque and never reused. Reset retires default-pool textures before
  D3D Reset; logical handles rebuild lazily after rendering resumes. Device
  generation changes invalidate managed GPU state.
- Pixels use top-left BGRA8 with straight alpha. JPEG EXIF orientation and full
  ICC color management are not implemented; prepare UI assets in display order.

## Asset packaging and cookbook

The SDK copies `Assets/**/*` to build/publish output. Custom project files that
use Microsoft.NET.Sdk directly should add:

```xml
<None Update="Assets/**/*" CopyToOutputDirectory="PreserveNewest"
      CopyToPublishDirectory="PreserveNewest" />
```

The bundled-plugin producer now preserves nested assets and runtime dependency
folders, excludes the framework-owned PluginApi copy/debug sidecars, and rejects
symlinks/case collisions. Keep Assets, the plugin DLL, dependency metadata and
manifest together. Package-relative paths also work from developer locations.

Use the [SDK quickstart](../UMBRA_SDK.md) for an independent project and add
media only after basic plugin loading works.

## Verification boundary

Portable tests compare decoded GIF frames to independently generated Pillow
references and exercise limits, ownership, cancellation, playback and reset.
The native headless test exercises actual upload/registry/drawing code with a
pitch-aware D3D9 test double and real ImGui. MinGW cross-compilation checks the
Windows x86 bridge; developer/package round trips check nested asset delivery.
Actual Windows/Wine driver latency, device-loss recovery, alpha edges and live
animation appearance still require in-game platform qualification. No rendering
benchmark or live-game result should be inferred from these tests.

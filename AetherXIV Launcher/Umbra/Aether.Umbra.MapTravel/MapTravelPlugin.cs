// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

using Aether.Umbra.PluginApi;

namespace Aether.Umbra.MapTravel;

public sealed class MapTravelPlugin : IUmbraPlugin, IUmbraPluginUi
{
    private readonly object gate = new();
    private IUmbraMapService? maps;
    private IUmbraTravelService? travel;
    private CancellationTokenSource? lifetime, selectionLifetime;
    private IDisposable? command;
    private bool open, disposed;
    private UmbraMapView? selectedMap;
    private UmbraMapPin? pin;
    private Task<UmbraMapPin?>? selectionTask;
    private Task<UmbraTravelPreview>? previewTask;
    private Task<UmbraTravelResult>? warpTask;
    private UmbraTravelPreview? preview;
    private int selected;
    private string message = "Open a zone map, then choose Warp to select a destination.";

    public string Name => "Map Travel";
    public void OpenMainUi() { lock (gate) { if (!disposed) open = true; } }

    public void Initialize(IUmbraPluginContext context)
    {
        maps = context.GetService<IUmbraMapService>();
        travel = context.GetService<IUmbraTravelService>();
        lifetime = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken);
        command = context.GetService<IUmbraCommandManager>()?.Register(
            new UmbraCommandRegistration("/maptravel", "Open the Map Travel window."), _ => OpenMainUi());
    }

    private void CancelSelection(string reason)
    {
        selectionLifetime?.Cancel();
        selectionLifetime?.Dispose();
        selectionLifetime = null;
        Observe(selectionTask); Observe(previewTask);
        selectionTask = null; previewTask = null; preview = null;
        pin = null; selectedMap = null; selected = 0;
        message = reason;
    }

    private void CheckContext()
    {
        // Recheck on both update and draw: a map change must invalidate a confirm
        // click even when it happens between the two callbacks.
        if (warpTask is null && selectedMap is not null &&
            (maps?.Availability.IsAvailable != true || travel?.Availability.IsAvailable != true ||
             maps.CurrentView != selectedMap || (pin is not null && maps.SelectedPin != pin)))
            CancelSelection("The map or destination changed. Choose Warp again.");
    }

    public void Update(TimeSpan delta)
    {
        lock (gate)
        {
            if (disposed) return;
            CheckContext();
            if (selectionTask is { IsCompleted: true })
            {
                try
                {
                    var destination = selectionTask.GetAwaiter().GetResult();
                    selectionTask = null;
                    if (destination is null) CancelSelection("Destination selection cancelled.");
                    else if (selectedMap is null || destination.SessionId != selectedMap.SessionId ||
                             destination.MapId != selectedMap.MapId || destination.ZoneId != selectedMap.ZoneId ||
                             destination.FloorId != selectedMap.FloorId || maps!.SelectedPin != destination)
                        CancelSelection("The map changed. Choose Warp again.");
                    else
                    {
                        pin = destination;
                        message = "Checking the selected landing…";
                        previewTask = RequestPreviewAsync(destination, selectionLifetime!.Token);
                    }
                }
                catch (OperationCanceledException) { CancelSelection("Destination selection cancelled."); }
                catch (Exception) { CancelSelection("Unable to select a destination on this map."); }
            }
            if (previewTask is { IsCompleted: true })
            {
                try
                {
                    var result = previewTask.GetAwaiter().GetResult();
                    bool executable = result.Status is UmbraTravelStatus.Ready or UmbraTravelStatus.Ambiguous;
                    bool valid = !string.IsNullOrWhiteSpace(result.Token) && result.Candidates.Count > 0 &&
                        result.Candidates.All(c => !string.IsNullOrWhiteSpace(c.Id) && c.Position.IsFinite &&
                            float.IsFinite(c.HorizontalAdjustment) && c.HorizontalAdjustment >= 0) &&
                        result.Candidates.Select(c => c.Id).Distinct().Count() == result.Candidates.Count &&
                        (result.Status != UmbraTravelStatus.Ready || result.Candidates.Count == 1);
                    preview = executable && valid ? result : null;
                    selected = 0;
                    message = executable && !valid ? "The server returned an invalid landing preview." : result.Message;
                }
                catch (OperationCanceledException) { message = "Landing preview cancelled."; }
                catch (Exception) { message = "Unable to resolve this destination."; }
                previewTask = null;
            }
            if (warpTask is { IsCompleted: true })
            {
                string outcome;
                try { outcome = warpTask.GetAwaiter().GetResult().Message; }
                catch (OperationCanceledException) { outcome = "Travel request cancelled; check your position."; }
                catch (Exception) { outcome = "Travel acknowledgement failed; check your position."; }
                warpTask = null;
                CancelSelection(outcome);
            }
            if (preview is not null && preview.ExpiresAt <= DateTimeOffset.UtcNow)
                CancelSelection("Landing preview expired. Choose Warp again.");
        }
    }

    public void Draw(IUmbraDrawContext draw)
    {
        lock (gate)
        {
            if (disposed || !open) return;
            CheckContext();
            draw.SetNextWindowSize(440, 320);
            bool visible = draw.BeginWindow("Map Travel###UmbraMapTravel", ref open);
            try
            {
                if (!open) { CancelSelection("Destination selection cancelled."); return; }
                if (!visible) return;
                draw.Text("Map Travel", UmbraTextTone.Normal, UmbraTextStyle.Title);
                draw.Text(message);
                if (warpTask is not null)
                {
                    draw.Text("Waiting for travel confirmation…");
                    return; // A submitted move cannot be undone by a local Cancel.
                }
                if (maps?.Availability.IsAvailable != true || travel?.Availability.IsAvailable != true)
                {
                    draw.Text("Map travel is not available yet.", UmbraTextTone.Warning);
                    draw.Text(maps?.Availability.Reason ?? "", UmbraTextTone.Muted);
                    draw.Text(travel?.Availability.Reason ?? "", UmbraTextTone.Muted);
                    return;
                }
                if (selectedMap is null)
                {
                    if (maps.CurrentView is not { } map)
                    {
                        draw.Text("Open a supported zone map to choose a destination.", UmbraTextTone.Muted);
                        return;
                    }
                    draw.Text(map.DisplayName);
                    if (draw.Button("Warp", UmbraButtonStyle.Primary))
                    {
                        selectedMap = map;
                        selectionLifetime = CancellationTokenSource.CreateLinkedTokenSource(lifetime!.Token);
                        selectionTask = SelectAsync(map, selectionLifetime.Token);
                        message = "Click a destination on the map. Escape or Cancel stops selection.";
                    }
                    return;
                }
                // Cancel remains available during selection, preview and floor choice.
                if (draw.Button("Cancel")) { CancelSelection("Destination selection cancelled."); return; }
                if (selectionTask is not null || previewTask is not null) return;
                if (pin is null) return;
                draw.Text($"{selectedMap.DisplayName} · Zone {pin.ZoneId} · Map {pin.MapId}");
                if (pin.MapPosition is { IsFinite: true } grid)
                    draw.Text($"Selected map coordinates: {grid.X:F2}, {grid.Y:F2}");
                draw.Text($"Selected world position: X {pin.X:F1} · Z {pin.Z:F1}", UmbraTextTone.Muted);
                if (preview is null)
                {
                    if (draw.Button("Check landing again"))
                        previewTask = RequestPreviewAsync(pin, selectionLifetime!.Token);
                    return;
                }
                if (preview.ExpiresAt <= DateTimeOffset.UtcNow)
                {
                    CancelSelection("Landing preview expired. Choose Warp again.");
                    return;
                }
                var candidates = preview.Candidates;
                if (preview.Status == UmbraTravelStatus.Ambiguous)
                {
                    string[] labels = new[] { "Choose a landing level…" }
                        .Concat(candidates.Select(c => $"{c.Label} · height {c.Position.Y:F1}")).ToArray();
                    draw.Combo("Landing level", ref selected, labels);
                    if (selected < 1 || selected > candidates.Count) return;
                }
                var landing = candidates[preview.Status == UmbraTravelStatus.Ambiguous ? selected - 1 : 0];
                draw.Text("Confirm warp?", UmbraTextTone.Normal, UmbraTextStyle.Heading);
                draw.Text(landing.Label);
                draw.Text($"Destination: X {landing.Position.X:F1} · Y {landing.Position.Y:F1} · Z {landing.Position.Z:F1}");
                if (landing.HorizontalAdjustment > 0.1f)
                    draw.Text($"Adjusted {landing.HorizontalAdjustment:F1} world units to walkable ground.", UmbraTextTone.Warning);
                if (draw.Button("Confirm", UmbraButtonStyle.Primary))
                {
                    warpTask = RequestWarpAsync(preview.Token!, landing.Id);
                    preview = null; // No duplicate submission or implicit retry.
                }
            }
            finally { draw.EndWindow(); }
        }
    }

    private async Task<UmbraMapPin?> SelectAsync(UmbraMapView map, CancellationToken token) =>
        await maps!.SelectPinAsync(map, token).ConfigureAwait(false);
    private async Task<UmbraTravelPreview> RequestPreviewAsync(UmbraMapPin destination, CancellationToken token) =>
        await travel!.PreviewAsync(destination, token).ConfigureAwait(false);
    private async Task<UmbraTravelResult> RequestWarpAsync(string token, string candidate) =>
        await travel!.WarpAsync(token, candidate, lifetime!.Token).ConfigureAwait(false);

    public void Dispose()
    {
        lock (gate)
        {
            if (disposed) return;
            disposed = true;
            command?.Dispose();
            CancelSelection("Plugin stopped.");
            lifetime?.Cancel();
            Observe(warpTask);
            lifetime?.Dispose();
        }
    }

    private static void Observe(Task? task)
    {
        if (task is not null)
            _ = task.ContinueWith(t => _ = t.Exception, CancellationToken.None,
                TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                TaskScheduler.Default);
    }
}

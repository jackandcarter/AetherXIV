// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

// Only the framework's verified native adapter publishes selections. Plugins get
// the read-only interface; there is no fabricated pixel-to-world calibration.
internal sealed class UmbraMapService : IUmbraMapService
{
    private readonly object selectionGate = new();
    private UmbraMapView? view;
    private Selection? selection;
    // The future native adapter reads this request on its UI thread, draws the
    // cursor and consumes the click before calling CompleteSelection. No hooks
    // or unverified client offsets are introduced by this managed lifecycle.
    internal sealed record Selection(Guid Id, UmbraMapView View,
        TaskCompletionSource<UmbraMapPin?> Completion);
    internal Selection? PendingSelection { get { lock (selectionGate) return selection; } }
    public UmbraMapView? CurrentView { get { lock (selectionGate) return view; } }

    internal void PublishView(UmbraMapView? current)
    {
        if (current is not null && (string.IsNullOrWhiteSpace(current.SessionId) ||
            current.Revision < 1 || current.ZoneId == 0 || current.MapId == 0))
            throw new ArgumentException("Invalid map view.", nameof(current));
        lock (selectionGate)
        {
            if (view == current) return;
            selection?.Completion.TrySetResult(null);
            selection = null;
            view = current;
        }
    }

    public async Task<UmbraMapPin?> SelectPinAsync(UmbraMapView expectedView,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(expectedView);
        cancellationToken.ThrowIfCancellationRequested();
        Selection request;
        lock (selectionGate)
        {
            if (!Availability.IsAvailable || view != expectedView || selection is not null) return null;
            request = new(Guid.NewGuid(), expectedView, new(TaskCreationOptions.RunContinuationsAsynchronously));
            selection = request;
        }
        using var registration = cancellationToken.Register(() => CancelSelection(request.Id));
        try { return await request.Completion.Task.ConfigureAwait(false); }
        finally { CancelSelection(request.Id); }
    }

    internal void CancelSelection(Guid id)
    {
        lock (selectionGate)
        {
            if (selection?.Id != id) return;
            selection.Completion.TrySetResult(null);
            selection = null;
        }
    }

    internal bool CompleteSelection(Guid id, UmbraMapPin pin)
    {
        lock (selectionGate)
        {
            if (selection?.Id != id || view is null || view != selection.View || !Availability.IsAvailable ||
                pin.SessionId != view.SessionId || pin.ZoneId != view.ZoneId ||
                pin.MapId != view.MapId || pin.FloorId != view.FloorId || pin.Revision < 1 ||
                !float.IsFinite(pin.X) || !float.IsFinite(pin.Z) ||
                pin.MapPosition is { IsFinite: false }) return false;
            Publish(Availability, pin); // Existing pin validation is authoritative.
            selection.Completion.TrySetResult(pin);
            selection = null;
            return true;
        }
    }
    private sealed record State(UmbraServiceAvailability Availability, UmbraMapPin? Pin);
    private State state = new(new(false, "ffxiv-1.23b-map-unresolved",
        Reason: "The native map selection binding has not been verified."), null);

    public UmbraServiceAvailability Availability => Volatile.Read(ref state).Availability;
    public UmbraMapPin? SelectedPin => Volatile.Read(ref state).Pin;

    internal void Publish(UmbraServiceAvailability availability, UmbraMapPin? pin)
    {
        ArgumentNullException.ThrowIfNull(availability);
        if (pin is not null && (!availability.IsAvailable || pin.Revision < 1 ||
            string.IsNullOrWhiteSpace(pin.SessionId) || pin.ZoneId == 0 || pin.MapId == 0 ||
            !float.IsFinite(pin.X) || !float.IsFinite(pin.Z) || pin.MapPosition is { IsFinite: false }))
            throw new ArgumentException("A pin requires an available adapter and valid world coordinates.", nameof(pin));
        Volatile.Write(ref state, new State(availability, pin));
        if (!availability.IsAvailable) PublishView(null);
    }
}

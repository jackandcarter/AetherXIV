// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Aether.Umbra.PluginApi;

/// <summary>World-space position. Y is elevation; X/Z are horizontal.</summary>
public readonly record struct UmbraWorldPosition(float X, float Y, float Z)
{
    public bool IsFinite => float.IsFinite(X) && float.IsFinite(Y) && float.IsFinite(Z);
}

/// <summary>
/// A verified native map selection after conversion to world X/Z. SessionId changes
/// on login; Revision changes on every selection, map/floor change, and selection clear.
/// MapId/FloorId are client map identifiers, not zone or private-instance identifiers.
/// </summary>
public sealed record UmbraMapPin(
    string SessionId, long Revision, uint ZoneId, uint MapId, string? FloorId,
    float X, float Z)
{
    /// <summary>Continuous map-grid coordinates, when the native adapter has verified them.</summary>
    public UmbraMapGridPosition? MapPosition { get; init; }
}

public readonly record struct UmbraMapGridPosition(double X, double Y)
{
    public bool IsFinite => double.IsFinite(X) && double.IsFinite(Y);
}

/// <summary>A verified open map. Revision changes when its identity or lifetime changes.</summary>
public sealed record UmbraMapView(
    string SessionId, long Revision, uint ZoneId, uint MapId, string? FloorId, string DisplayName);

public interface IUmbraMapService
{
    UmbraServiceAvailability Availability { get; }
    /// <summary>Null when no pin exists or the native binding is unavailable.</summary>
    UmbraMapPin? SelectedPin { get; }

    /// <summary>Null when the map is closed, an overview, or its binding is unresolved.</summary>
    UmbraMapView? CurrentView => null;

    /// <summary>
    /// Capture one destination click on the expected map, without travelling.
    /// The framework owns the cursor indicator and consumes the destination click.
    /// Null means cancelled (including Escape, map close/change, or unavailable binding).
    /// Only one selection may be active. Cancellation releases input capture and effects.
    /// </summary>
    Task<UmbraMapPin?> SelectPinAsync(UmbraMapView expectedView,
        CancellationToken cancellationToken = default) => Task.FromResult<UmbraMapPin?>(null);
}

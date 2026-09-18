// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Aether.Umbra.PluginApi;

public enum UmbraTravelStatus
{
    Ready, Completed, Unavailable, Denied, InvalidDestination,
    NoLanding, Ambiguous, Expired, Failed
}

/// <summary>One server-resolved surface. IDs are opaque and scoped to the preview.</summary>
public sealed record UmbraLandingCandidate(
    string Id, string Label, UmbraWorldPosition Position, float HorizontalAdjustment);

/// <summary>
/// Server-owned preview. The token must be bound to the authenticated character,
/// session, destination, mesh revision, candidates and expiry. Never trust client Y.
/// Ambiguous previews require explicit candidate selection before travel.
/// </summary>
public sealed record UmbraTravelPreview(
    UmbraTravelStatus Status, string Message, string? Token,
    DateTimeOffset ExpiresAt, IReadOnlyList<UmbraLandingCandidate> Candidates);

public sealed record UmbraTravelResult(UmbraTravelStatus Status, string Message);

/// <summary>
/// Resolves map pins on the destination server; does not expose raw mesh memory.
/// Implementations must be nonblocking, honor cancellation and return a result
/// only after server acknowledgement. Cancellation is not a rollback of a warp.
/// </summary>
public interface IUmbraTravelService
{
    UmbraServiceAvailability Availability { get; }
    Task<UmbraTravelPreview> PreviewAsync(UmbraMapPin pin, CancellationToken cancellationToken = default);
    /// <summary>Server revalidates and consumes the preview token atomically.</summary>
    Task<UmbraTravelResult> WarpAsync(string previewToken, string candidateId,
        CancellationToken cancellationToken = default);
}

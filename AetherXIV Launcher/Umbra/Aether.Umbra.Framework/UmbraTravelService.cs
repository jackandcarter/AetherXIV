// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

// Future authenticated server transport implements the same contract. It must
// resolve/execute on the map server and never use the development HTTP bridge.
internal sealed class UmbraTravelService
{
    private IUmbraTravelService transport = new UnavailableTransport();

    internal void SetTransport(IUmbraTravelService value) =>
        Volatile.Write(ref transport, value ?? throw new ArgumentNullException(nameof(value)));

    internal IUmbraTravelService CreateScope(bool allowWarp) => new Scope(this, allowWarp);

    private sealed class Scope(UmbraTravelService owner, bool allowWarp) : IUmbraTravelService
    {
        public UmbraServiceAvailability Availability => Volatile.Read(ref owner.transport).Availability;

        public async Task<UmbraTravelPreview> PreviewAsync(UmbraMapPin pin, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pin);
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(pin.SessionId) || pin.Revision < 1 || pin.ZoneId == 0 ||
                pin.MapId == 0 || !float.IsFinite(pin.X) || !float.IsFinite(pin.Z))
                return Empty(UmbraTravelStatus.InvalidDestination, "The map destination is invalid.");
            var current = Volatile.Read(ref owner.transport);
            if (!current.Availability.IsAvailable)
                return Empty(UmbraTravelStatus.Unavailable, current.Availability.Reason ?? "Travel is unavailable.");
            try { return await current.PreviewAsync(pin, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception) { return Empty(UmbraTravelStatus.Failed, "The landing preview request failed."); }
        }

        public async Task<UmbraTravelResult> WarpAsync(string previewToken, string candidateId,
            CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!allowWarp)
                return new(UmbraTravelStatus.Denied, "Plugin manifest does not declare travel.warp.");
            if (string.IsNullOrWhiteSpace(previewToken) || string.IsNullOrWhiteSpace(candidateId))
                return new(UmbraTravelStatus.InvalidDestination, "Select a resolved landing first.");
            var current = Volatile.Read(ref owner.transport);
            if (!current.Availability.IsAvailable)
                return new(UmbraTravelStatus.Unavailable, current.Availability.Reason ?? "Travel is unavailable.");
            try { return await current.WarpAsync(previewToken, candidateId, cancellationToken).ConfigureAwait(false); }
            catch (OperationCanceledException) { throw; }
            catch (Exception)
            {
                // A lost acknowledgement is not proof that the server did not move
                // the player. Do not automatically retry an execution request.
                return new(UmbraTravelStatus.Failed, "Travel acknowledgement failed. Check your position before trying again.");
            }
        }
    }

    private static UmbraTravelPreview Empty(UmbraTravelStatus status, string message) =>
        new(status, message, null, default, Array.Empty<UmbraLandingCandidate>());

    private sealed class UnavailableTransport : IUmbraTravelService
    {
        public UmbraServiceAvailability Availability { get; } = new(false,
            "aetherxiv-travel-unresolved", Reason: "The authenticated map-travel transport is not connected.");
        public Task<UmbraTravelPreview> PreviewAsync(UmbraMapPin pin, CancellationToken cancellationToken = default) =>
            Task.FromResult(Empty(UmbraTravelStatus.Unavailable, Availability.Reason!));
        public Task<UmbraTravelResult> WarpAsync(string previewToken, string candidateId,
            CancellationToken cancellationToken = default) =>
            Task.FromResult(new UmbraTravelResult(UmbraTravelStatus.Unavailable, Availability.Reason!));
    }
}

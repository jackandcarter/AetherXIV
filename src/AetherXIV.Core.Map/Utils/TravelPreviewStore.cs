// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

#nullable enable

using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;

namespace AetherXIV.Core.Map.utils;

// Server-owned identity: never construct this from unvalidated packet fields.
internal sealed record TravelDestination(uint ZoneId, string PrivateArea, uint PrivateAreaType, uint MapId,
    string FloorId, string GeometryRevision);
internal sealed record TravelLanding(string Id, float X, float Y, float Z);
internal sealed record TravelGrant(string Token, DateTimeOffset ExpiresAt,
    TravelDestination Destination, IReadOnlyList<TravelLanding> Candidates);
internal enum TravelClaimStatus { Acquired, Unknown, Expired, Stale, InvalidCandidate, InProgress, Finished }
internal sealed record TravelOutcome(bool Completed, string Message);
internal sealed record TravelClaim(TravelClaimStatus Status, TravelLanding? Landing = null,
    TravelOutcome? Outcome = null);

/// <summary>
/// One store per authoritative map Session. Tokens are meaningless in any other
/// session. This is bookkeeping, not authentication or permission to move: the
/// handler must validate the connection, policy and geometry on the owning map
/// thread before issuing, and again before claiming. No client-provided Y is used.
/// </summary>
internal sealed class TravelPreviewStore
{
    private sealed class Entry
    {
        internal TravelGrant Grant;
        internal string? ClaimedCandidate;
        internal TravelOutcome? Outcome;
        internal long CreatedAt;
        internal long FinishedAt;
        internal Entry(TravelGrant grant, long createdAt) { Grant = grant; CreatedAt = createdAt; }
    }

    private readonly object gate = new();
    private readonly Dictionary<string, Entry> entries = new(StringComparer.Ordinal);
    private readonly TimeProvider clock;
    private readonly TimeSpan lifetime;
    private readonly TimeSpan outcomeRetention = TimeSpan.FromMinutes(2);
    private readonly int capacity;
    private bool closed;

    internal TravelPreviewStore(TimeProvider? clock = null, int capacity = 32,
        TimeSpan? lifetime = null)
    {
        this.clock = clock ?? TimeProvider.System;
        this.capacity = capacity is >= 1 and <= 128 ? capacity : throw new ArgumentOutOfRangeException(nameof(capacity));
        this.lifetime = lifetime ?? TimeSpan.FromSeconds(15);
        if (this.lifetime <= TimeSpan.Zero || this.lifetime > TimeSpan.FromMinutes(1))
            throw new ArgumentOutOfRangeException(nameof(lifetime));
    }

    // Null means capacity exhausted or the session has ended; do not evict an
    // execution in progress or a retained outcome to make room for a preview.
    internal TravelGrant? Issue(TravelDestination destination, IReadOnlyList<TravelLanding> candidates)
    {
        ArgumentNullException.ThrowIfNull(destination);
        ArgumentNullException.ThrowIfNull(candidates);
        if (destination.ZoneId == 0 || destination.MapId == 0 ||
            string.IsNullOrWhiteSpace(destination.GeometryRevision) ||
            destination.PrivateArea == null || destination.FloorId == null ||
            candidates.Count is < 1 or > 64)
            throw new ArgumentException("A verified destination and bounded landing candidates are required.");
        var copy = candidates.ToArray();
        var ids = new HashSet<string>(StringComparer.Ordinal);
        foreach (var candidate in copy)
            if (candidate == null || string.IsNullOrWhiteSpace(candidate.Id) ||
                candidate.Id.Length > 128 || !ids.Add(candidate.Id) ||
                !float.IsFinite(candidate.X) || !float.IsFinite(candidate.Y) || !float.IsFinite(candidate.Z))
                throw new ArgumentException("Landing candidates must have distinct IDs and finite server coordinates.");

        lock (gate)
        {
            Prune();
            if (closed || entries.Count >= capacity) return null;
            string token;
            do { token = Convert.ToHexString(RandomNumberGenerator.GetBytes(32)); }
            while (entries.ContainsKey(token));
            var grant = new TravelGrant(token, clock.GetUtcNow() + lifetime,
                destination, Array.AsReadOnly(copy));
            entries.Add(token, new Entry(grant, clock.GetTimestamp()));
            return grant;
        }
    }

    /// <summary>
    /// Atomically acquires one execution. The current destination is derived
    /// afresh by the server and includes private-instance and geometry identity.
    /// Repeated calls return status/outcome and never acquire a second execution.
    /// </summary>
    internal TravelClaim Claim(string token, string candidateId, TravelDestination currentDestination)
    {
        lock (gate)
        {
            if (closed || token == null || token.Length != 64 || !entries.TryGetValue(token, out var entry))
                return new(TravelClaimStatus.Unknown);
            if (entry.ClaimedCandidate != null)
            {
                if (!StringComparer.Ordinal.Equals(entry.ClaimedCandidate, candidateId))
                    return new(TravelClaimStatus.InvalidCandidate);
                return entry.Outcome == null
                    ? new(TravelClaimStatus.InProgress)
                    : new(TravelClaimStatus.Finished, Outcome: entry.Outcome);
            }
            if (clock.GetElapsedTime(entry.CreatedAt) >= lifetime)
            {
                entries.Remove(token);
                return new(TravelClaimStatus.Expired);
            }
            if (entry.Grant.Destination != currentDestination)
            {
                entries.Remove(token);
                return new(TravelClaimStatus.Stale);
            }
            var landing = entry.Grant.Candidates.FirstOrDefault(c => StringComparer.Ordinal.Equals(c.Id, candidateId));
            if (landing == null) return new(TravelClaimStatus.InvalidCandidate);
            entry.ClaimedCandidate = candidateId;
            return new(TravelClaimStatus.Acquired, landing);
        }
    }

    // Call even when movement throws or delivery is uncertain. Once acquired,
    // the token must never become executable again, including on failure.
    internal bool Finish(string token, string candidateId, TravelOutcome outcome)
    {
        ArgumentNullException.ThrowIfNull(outcome);
        lock (gate)
        {
            if (closed || token == null || !entries.TryGetValue(token, out var entry) ||
                entry.ClaimedCandidate == null || entry.Outcome != null ||
                !StringComparer.Ordinal.Equals(entry.ClaimedCandidate, candidateId)) return false;
            entry.Outcome = outcome;
            entry.FinishedAt = clock.GetTimestamp();
            return true;
        }
    }

    internal void Close()
    {
        lock (gate) { closed = true; entries.Clear(); }
    }

    private void Prune()
    {
        foreach (var pair in entries.ToArray())
        {
            var entry = pair.Value;
            if ((entry.ClaimedCandidate == null && clock.GetElapsedTime(entry.CreatedAt) >= lifetime) ||
                (entry.Outcome != null && clock.GetElapsedTime(entry.FinishedAt) >= outcomeRetention))
                entries.Remove(pair.Key);
        }
    }
}

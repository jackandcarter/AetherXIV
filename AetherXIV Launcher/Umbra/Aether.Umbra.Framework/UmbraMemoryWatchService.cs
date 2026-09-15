/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

namespace Aether.Umbra.Framework;

public sealed class UmbraMemoryWatchService(
    UmbraReadOnlyMemory memory,
    UmbraDevBridgeEvents events,
    UmbraRuntimeLog log) : IDisposable
{
    public const int MaximumWatches = 8;
    public const int MaximumWatchBytes = 64;
    public const int MinimumIntervalMilliseconds = 250;
    public const int MaximumIntervalMilliseconds = 5000;

    private readonly object gate = new();
    private readonly Dictionary<string, WatchEntry> watches = new(StringComparer.Ordinal);
    private bool disposed;

    public IReadOnlyList<UmbraMemoryWatchSnapshot> Snapshots()
    {
        lock (gate)
            return watches.Values.Select(entry => entry.Snapshot()).ToArray();
    }

    public UmbraMemoryWatchSnapshot Start(
        string? name,
        string? module,
        long offset,
        int size,
        int intervalMilliseconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        if (!memory.TryGetVerifiedClientBuild(out UmbraClientBuildProfile? profile))
        {
            throw new InvalidOperationException(
                "Memory watches require an exact cataloged client executable hash.");
        }

        string normalizedName = Normalize(name, "watch", 96);
        string normalizedModule = Normalize(module, profile!.ExecutableFileName, 128);
        if (size is < 1 or > MaximumWatchBytes)
            throw new ArgumentOutOfRangeException(nameof(size), $"Watch size must be 1..{MaximumWatchBytes}.");
        if (intervalMilliseconds is < MinimumIntervalMilliseconds or > MaximumIntervalMilliseconds)
        {
            throw new ArgumentOutOfRangeException(
                nameof(intervalMilliseconds),
                $"Watch interval must be {MinimumIntervalMilliseconds}..{MaximumIntervalMilliseconds} milliseconds.");
        }

        UmbraMemoryPeekResult initial = memory.PeekModuleForWatch(normalizedModule, offset, size);
        if (!initial.Success)
            throw new InvalidOperationException(initial.Error ?? "Initial watch read failed.");

        WatchEntry entry;
        lock (gate)
        {
            if (watches.Count >= MaximumWatches)
                throw new InvalidOperationException($"At most {MaximumWatches} memory watches may run.");

            string id = Guid.NewGuid().ToString("N");
            entry = new WatchEntry(
                id,
                normalizedName,
                normalizedModule,
                offset,
                size,
                intervalMilliseconds,
                profile.Id,
                initial.Hex);
            watches.Add(id, entry);
            entry.Task = Task.Run(() => PollAsync(entry));
        }

        events.Record("watch.start", new
        {
            entry.Id,
            entry.Name,
            entry.Module,
            offset = $"0x{entry.Offset:X}",
            entry.Size,
            interval_ms = entry.IntervalMilliseconds,
            client_build_id = entry.ClientBuildId,
            initial_hex = entry.CurrentHex
        });
        return entry.Snapshot();
    }

    public bool Stop(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return false;

        WatchEntry? entry;
        lock (gate)
        {
            if (!watches.Remove(id.Trim(), out entry))
                return false;
        }

        entry.Stop.Cancel();
        events.Record("watch.stop", new { entry.Id, entry.Name });
        try
        {
            entry.Task.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Cancellation is the expected watch-stop path.
        }
        entry.Stop.Dispose();
        return true;
    }

    public void Dispose()
    {
        List<WatchEntry> entries;
        lock (gate)
        {
            if (disposed)
                return;
            disposed = true;
            entries = watches.Values.ToList();
            watches.Clear();
        }

        foreach (WatchEntry entry in entries)
            entry.Stop.Cancel();
        try
        {
            Task.WaitAll(entries.Select(entry => entry.Task).ToArray(), TimeSpan.FromSeconds(1));
        }
        catch
        {
            // Process teardown or watch cancellation is already underway.
        }
        foreach (WatchEntry entry in entries)
            entry.Stop.Dispose();
    }

    private async Task PollAsync(WatchEntry entry)
    {
        try
        {
            using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(entry.IntervalMilliseconds));
            while (await timer.WaitForNextTickAsync(entry.Stop.Token).ConfigureAwait(false))
            {
                UmbraMemoryPeekResult result = memory.PeekModuleForWatch(
                    entry.Module,
                    entry.Offset,
                    entry.Size);
                if (!result.Success)
                {
                    entry.SetError(result.Error ?? "Watch read failed.");
                    events.Record("watch.error", new
                    {
                        entry.Id,
                        entry.Name,
                        entry.Error
                    });
                    return;
                }

                string? previous = entry.SetValue(result.Hex);
                if (previous is null)
                    continue;

                events.Record("watch.changed", new
                {
                    entry.Id,
                    entry.Name,
                    entry.Module,
                    offset = $"0x{entry.Offset:X}",
                    previous_hex = previous,
                    current_hex = result.Hex
                });
            }
        }
        catch (OperationCanceledException) when (entry.Stop.IsCancellationRequested)
        {
            // Expected watch shutdown.
        }
        catch (Exception ex)
        {
            entry.SetError(ex.Message);
            log.Warning($"umbra_memory_watch_failed id={entry.Id} error={ex.Message}");
            events.Record("watch.error", new { entry.Id, entry.Name, entry.Error });
        }
    }

    private static string Normalize(string? value, string fallback, int maximumLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maximumLength ? normalized : normalized[..maximumLength];
    }

    private sealed class WatchEntry(
        string id,
        string name,
        string module,
        long offset,
        int size,
        int intervalMilliseconds,
        string clientBuildId,
        string currentHex)
    {
        private readonly object gate = new();
        private string currentHex = currentHex;
        private string? error;
        private DateTimeOffset updatedAt = DateTimeOffset.UtcNow;

        public string Id { get; } = id;
        public string Name { get; } = name;
        public string Module { get; } = module;
        public long Offset { get; } = offset;
        public int Size { get; } = size;
        public int IntervalMilliseconds { get; } = intervalMilliseconds;
        public string ClientBuildId { get; } = clientBuildId;
        public CancellationTokenSource Stop { get; } = new();
        public Task Task { get; set; } = Task.CompletedTask;
        public string CurrentHex { get { lock (gate) return currentHex; } }
        public string? Error { get { lock (gate) return error; } }

        public string? SetValue(string value)
        {
            lock (gate)
            {
                updatedAt = DateTimeOffset.UtcNow;
                if (string.Equals(currentHex, value, StringComparison.Ordinal))
                    return null;
                string previous = currentHex;
                currentHex = value;
                return previous;
            }
        }

        public void SetError(string value)
        {
            lock (gate)
            {
                error = value;
                updatedAt = DateTimeOffset.UtcNow;
            }
        }

        public UmbraMemoryWatchSnapshot Snapshot()
        {
            lock (gate)
            {
                return new UmbraMemoryWatchSnapshot(
                    Id,
                    Name,
                    Module,
                    $"0x{Offset:X}",
                    Size,
                    IntervalMilliseconds,
                    ClientBuildId,
                    currentHex,
                    updatedAt,
                    error);
            }
        }
    }
}

public sealed record UmbraMemoryWatchSnapshot(
    string Id,
    string Name,
    string Module,
    string Offset,
    int Size,
    int IntervalMilliseconds,
    string ClientBuildId,
    string CurrentHex,
    DateTimeOffset UpdatedAt,
    string? Error);

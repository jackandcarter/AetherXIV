/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * AetherXIV is free software: you can redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Diagnostics;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aether.Umbra.Framework;

public sealed class UmbraDevBridgeEvents(UmbraRuntimeOptions options, UmbraRuntimeLog log)
{
    public const int Capacity = 4096;
    private readonly Queue<UmbraDevBridgeEvent> events = new();
    private readonly object gate = new();
    private readonly long monotonicOrigin = Stopwatch.GetTimestamp();
    private readonly string bridgeSessionId = Guid.NewGuid().ToString("N");
    private TaskCompletionSource<long> changed = NewSignal();
    private StreamWriter? captureWriter;
    private string? capturePath;
    private string? lastCapturePath;
    private string? captureSessionId;
    private string? lastCaptureSessionId;
    private string? captureCorrelationId;
    private DateTimeOffset? captureStartedAt;
    private bool capturePaused;
    private long nextSequence;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public string BridgeSessionId => bridgeSessionId;

    public long LatestSequence
    {
        get
        {
            lock (gate)
                return nextSequence;
        }
    }

    public IReadOnlyList<UmbraDevBridgeEvent> Recent(int limit)
    {
        lock (gate)
            return events.TakeLast(Math.Clamp(limit, 1, Capacity)).ToArray();
    }

    public IReadOnlyList<UmbraDevBridgeEvent> After(long sequence, int limit)
    {
        lock (gate)
            return AfterLocked(sequence, limit);
    }

    public async Task<IReadOnlyList<UmbraDevBridgeEvent>> WaitAfterAsync(
        long sequence,
        int limit,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Task signal;
        lock (gate)
        {
            IReadOnlyList<UmbraDevBridgeEvent> available = AfterLocked(sequence, limit);
            if (available.Count > 0 || timeout <= TimeSpan.Zero)
                return available;

            signal = changed.Task;
        }

        try
        {
            await signal.WaitAsync(timeout, cancellationToken).ConfigureAwait(false);
        }
        catch (TimeoutException)
        {
            // An empty page is the expected long-poll timeout response.
        }

        return After(sequence, limit);
    }

    public UmbraDevBridgeEvent Record(string category, object? data = null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(category);
        TaskCompletionSource<long> notify;
        UmbraDevBridgeEvent item;
        lock (gate)
        {
            long sequence = ++nextSequence;
            item = new UmbraDevBridgeEvent(
                sequence,
                DateTimeOffset.UtcNow,
                Stopwatch.GetElapsedTime(monotonicOrigin).TotalMilliseconds,
                bridgeSessionId,
                captureSessionId,
                category,
                data);
            events.Enqueue(item);
            while (events.Count > Capacity)
                events.Dequeue();

            if (!capturePaused && captureWriter is not null)
            {
                captureWriter.WriteLine(JsonSerializer.Serialize(item, JsonOptions));
                captureWriter.Flush();
            }

            notify = changed;
            changed = NewSignal();
        }

        notify.TrySetResult(item.Sequence);
        return item;
    }

    public object StartCapture(string? name, string? correlationId = null, object? metadata = null)
    {
        lock (gate)
        {
            StopCaptureLocked();
            Directory.CreateDirectory(Path.Combine(options.DevBridgeDirectory, "Captures"));
            string safeName = Sanitize(string.IsNullOrWhiteSpace(name) ? "capture" : name, 64);
            captureSessionId = Guid.NewGuid().ToString("N");
            captureCorrelationId = NormalizeOptional(correlationId, 128);
            captureStartedAt = DateTimeOffset.UtcNow;
            capturePath = Path.Combine(
                options.DevBridgeDirectory,
                "Captures",
                $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{safeName}-{captureSessionId[..8]}.jsonl");
            captureWriter = new StreamWriter(File.Open(capturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            capturePaused = false;
            log.Info(
                $"umbra_dev_bridge_capture_start session={captureSessionId} correlation={captureCorrelationId ?? "none"} path={capturePath}");
            Record("capture.start", new
            {
                name = safeName,
                correlation_id = captureCorrelationId,
                metadata
            });
            return CaptureStatus();
        }
    }

    public object Mark(string label, string? note = null)
    {
        string normalizedLabel = NormalizeRequired(label, "marker", 96);
        string? normalizedNote = NormalizeOptional(note, 512);
        UmbraDevBridgeEvent item = Record("capture.marker", new
        {
            label = normalizedLabel,
            note = normalizedNote
        });
        return new
        {
            marked = true,
            item.Sequence,
            item.Timestamp,
            item.CaptureSessionId,
            label = normalizedLabel
        };
    }

    public object PauseCapture()
    {
        lock (gate)
        {
            if (captureWriter is null)
                return CaptureStatus();

            Record("capture.pause");
            capturePaused = true;
            log.Info("umbra_dev_bridge_capture_pause=true");
            return CaptureStatus();
        }
    }

    public object StopCapture()
    {
        lock (gate)
        {
            if (captureWriter is not null)
                Record("capture.stop");
            StopCaptureLocked();
            log.Info("umbra_dev_bridge_capture_stop=true");
            return CaptureStatus();
        }
    }

    public object CaptureStatus()
    {
        lock (gate)
        {
            return new
            {
                active = captureWriter is not null,
                paused = capturePaused,
                session_id = captureSessionId,
                correlation_id = captureCorrelationId,
                started_at = captureStartedAt,
                path = capturePath,
                last_session_id = lastCaptureSessionId,
                last_path = lastCapturePath
            };
        }
    }

    public string? GetCapturePathForExport()
    {
        lock (gate)
            return capturePath ?? lastCapturePath;
    }

    private IReadOnlyList<UmbraDevBridgeEvent> AfterLocked(long sequence, int limit)
    {
        return events
            .Where(item => item.Sequence > Math.Max(0, sequence))
            .Take(Math.Clamp(limit, 1, Capacity))
            .ToArray();
    }

    private void StopCaptureLocked()
    {
        captureWriter?.Dispose();
        if (capturePath is not null)
        {
            lastCapturePath = capturePath;
            lastCaptureSessionId = captureSessionId;
        }

        captureWriter = null;
        capturePaused = false;
        capturePath = null;
        captureSessionId = null;
        captureCorrelationId = null;
        captureStartedAt = null;
    }

    private static TaskCompletionSource<long> NewSignal()
    {
        return new(TaskCreationOptions.RunContinuationsAsynchronously);
    }

    private static string Sanitize(string value, int maxLength)
    {
        char[] chars = value
            .Take(maxLength)
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray();
        string sanitized = new(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "capture" : sanitized;
    }

    private static string NormalizeRequired(string? value, string fallback, int maxLength)
    {
        string normalized = string.IsNullOrWhiteSpace(value) ? fallback : value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }

    private static string? NormalizeOptional(string? value, int maxLength)
    {
        if (string.IsNullOrWhiteSpace(value))
            return null;

        string normalized = value.Trim();
        return normalized.Length <= maxLength ? normalized : normalized[..maxLength];
    }
}

public sealed record UmbraDevBridgeEvent(
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("monotonic_ms")] double MonotonicMilliseconds,
    [property: JsonPropertyName("bridge_session_id")] string BridgeSessionId,
    [property: JsonPropertyName("capture_session_id")] string? CaptureSessionId,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("data")] object? Data);

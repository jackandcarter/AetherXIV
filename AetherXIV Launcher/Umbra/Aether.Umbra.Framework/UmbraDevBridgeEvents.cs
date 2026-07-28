/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * AetherXIV is free software: you may redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Text.Json;
using System.Text.Json.Serialization;

namespace Aether.Umbra.Framework;

public sealed class UmbraDevBridgeEvents(UmbraRuntimeOptions options, UmbraRuntimeLog log)
{
    private const int Capacity = 512;
    private readonly Queue<UmbraDevBridgeEvent> events = new();
    private readonly object gate = new();
    private StreamWriter? captureWriter;
    private string? capturePath;
    private bool capturePaused;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = false
    };

    public IReadOnlyList<UmbraDevBridgeEvent> Recent(int limit)
    {
        lock (gate)
            return events.TakeLast(Math.Clamp(limit, 1, Capacity)).ToArray();
    }

    public void Record(string category, object? data = null)
    {
        UmbraDevBridgeEvent item = new(DateTimeOffset.UtcNow, category, data);
        lock (gate)
        {
            events.Enqueue(item);
            while (events.Count > Capacity)
                events.Dequeue();

            if (!capturePaused && captureWriter is not null)
            {
                captureWriter.WriteLine(JsonSerializer.Serialize(item, JsonOptions));
                captureWriter.Flush();
            }
        }
    }

    public object StartCapture(string? name)
    {
        lock (gate)
        {
            StopCaptureLocked();
            Directory.CreateDirectory(Path.Combine(options.DevBridgeDirectory, "Captures"));
            string safeName = Sanitize(string.IsNullOrWhiteSpace(name) ? "capture" : name);
            capturePath = Path.Combine(
                options.DevBridgeDirectory,
                "Captures",
                $"{DateTimeOffset.Now:yyyyMMdd-HHmmss}-{safeName}.jsonl");
            captureWriter = new StreamWriter(File.Open(capturePath, FileMode.CreateNew, FileAccess.Write, FileShare.Read));
            capturePaused = false;
            log.Info($"umbra_dev_bridge_capture_start path={capturePath}");
            Record("capture.start", new { path = capturePath });
            return CaptureStatus();
        }
    }

    public object PauseCapture()
    {
        lock (gate)
        {
            capturePaused = true;
            log.Info("umbra_dev_bridge_capture_pause=true");
            Record("capture.pause");
            return CaptureStatus();
        }
    }

    public object StopCapture()
    {
        lock (gate)
        {
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
                path = capturePath
            };
        }
    }

    private void StopCaptureLocked()
    {
        captureWriter?.Dispose();
        captureWriter = null;
        capturePaused = false;
        capturePath = null;
    }

    private static string Sanitize(string value)
    {
        char[] chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '-' or '_' ? ch : '_')
            .ToArray();
        string sanitized = new(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "capture" : sanitized;
    }
}

public sealed record UmbraDevBridgeEvent(
    [property: JsonPropertyName("timestamp")] DateTimeOffset Timestamp,
    [property: JsonPropertyName("category")] string Category,
    [property: JsonPropertyName("data")] object? Data);

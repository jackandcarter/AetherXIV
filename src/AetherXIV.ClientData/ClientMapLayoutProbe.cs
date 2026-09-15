/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Text;

namespace AetherXIV.ClientData;

/// <summary>
/// Extracts bounded printable-string metadata from a 1.x MapLayoutResourceData blob.
/// It intentionally does not decode transforms or identify NPCs.
/// </summary>
public static class ClientMapLayoutResourceParser
{
    private const int MaximumProbeBytes = 16 * 1024 * 1024;
    private const int MinimumStringLength = 4;

    public static ClientMapLayoutProbe Parse(ReadOnlySpan<byte> bytes)
    {
        if (bytes.Length < 4 || !bytes[..4].SequenceEqual("MapL"u8))
        {
            return new(
                string.Empty,
                null,
                false,
                bytes.Length,
                0,
                [],
                [],
                [],
                [new(0, "MapLayoutResource magic was not found.")]);
        }

        int scannedLength = Math.Min(bytes.Length, MaximumProbeBytes);
        List<ClientMapLayoutStringObservation> observations = [];
        int offset = 0;

        while (offset < scannedLength)
        {
            while (offset < scannedLength && !IsPrintable(bytes[offset]))
                offset++;

            int start = offset;
            while (offset < scannedLength && IsPrintable(bytes[offset]))
                offset++;

            int length = offset - start;
            if (length < MinimumStringLength)
                continue;

            string value = Encoding.ASCII.GetString(bytes.Slice(start, length));
            ClientMapLayoutStringKind kind = Classify(value);
            observations.Add(new(start, kind, value));
        }

        string? version = observations
            .Select(item => item.Value)
            .FirstOrDefault(value => value.Length <= 16 && value.Count(c => c == '.') >= 1);

        IReadOnlyList<ClientMapLayoutStringObservation> schemas = observations
            .Where(item => item.Kind == ClientMapLayoutStringKind.Schema)
            .ToArray();
        IReadOnlyList<ClientMapLayoutStringObservation> resourceReferences = observations
            .Where(item => item.Kind == ClientMapLayoutStringKind.ResourceReference)
            .ToArray();
        bool complete = scannedLength == bytes.Length;
        List<ClientMapLayoutParseIssue> issues = [];
        if (!complete)
            issues.Add(new(scannedLength, $"Probe bounded at {MaximumProbeBytes} bytes; remaining bytes were not scanned."));

        return new(
            "MapLayoutResourceData",
            version,
            complete,
            bytes.Length,
            scannedLength,
            observations,
            schemas,
            resourceReferences,
            issues);
    }

    private static ClientMapLayoutStringKind Classify(string value)
    {
        if (value.StartsWith("Lay", StringComparison.Ordinal)
            || value.Contains("Object", StringComparison.Ordinal)
            || value.Contains("Transform", StringComparison.Ordinal)
            || value.Contains("Position", StringComparison.Ordinal))
            return ClientMapLayoutStringKind.Schema;

        if (value.Contains('/', StringComparison.Ordinal)
            || value.EndsWith(".win32", StringComparison.OrdinalIgnoreCase)
            || value.Contains("reference", StringComparison.OrdinalIgnoreCase))
            return ClientMapLayoutStringKind.ResourceReference;

        if (value.Contains('_', StringComparison.Ordinal)
            && !value.Contains(' ', StringComparison.Ordinal))
            return ClientMapLayoutStringKind.InstanceName;

        return ClientMapLayoutStringKind.Other;
    }

    private static bool IsPrintable(byte value)
    {
        return value is >= 0x20 and <= 0x7E;
    }
}

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

using System.Text;

namespace AetherXIV.ClientData;

public static class ClientDataStringProbeExtractor
{
    public static async Task<IReadOnlyList<ClientDataStringProbe>> ExtractAsync(
        string filePath,
        int minLength,
        int maxProbeCount,
        long maxBytes,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(minLength, 1);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxProbeCount, 0);
        ArgumentOutOfRangeException.ThrowIfLessThan(maxBytes, 1);

        if (maxProbeCount == 0)
            return [];

        FileInfo file = new(filePath);
        int length = (int)Math.Min(file.Length, maxBytes);
        byte[] buffer = new byte[length];

        await using FileStream stream = File.OpenRead(filePath);
        int bytesRead = 0;
        while (bytesRead < length)
        {
            int read = await stream.ReadAsync(buffer.AsMemory(bytesRead, length - bytesRead), cancellationToken);
            if (read == 0)
                break;

            bytesRead += read;
        }

        if (bytesRead != buffer.Length)
            Array.Resize(ref buffer, bytesRead);

        List<ClientDataStringProbe> probes = new(maxProbeCount);
        ExtractAscii(buffer, minLength, maxProbeCount, probes);

        if (probes.Count < maxProbeCount)
            ExtractUtf16LittleEndian(buffer, minLength, maxProbeCount, probes);

        return probes;
    }

    private static void ExtractAscii(
        byte[] buffer,
        int minLength,
        int maxProbeCount,
        List<ClientDataStringProbe> probes)
    {
        int start = -1;

        for (int i = 0; i <= buffer.Length; i++)
        {
            bool isText = i < buffer.Length && IsAsciiProbeByte(buffer[i]);
            if (isText && start < 0)
            {
                start = i;
                continue;
            }

            if (isText || start < 0)
                continue;

            int count = i - start;
            if (count >= minLength)
            {
                probes.Add(new ClientDataStringProbe(start, "ascii", Encoding.ASCII.GetString(buffer, start, count)));
                if (probes.Count >= maxProbeCount)
                    return;
            }

            start = -1;
        }
    }

    private static void ExtractUtf16LittleEndian(
        byte[] buffer,
        int minLength,
        int maxProbeCount,
        List<ClientDataStringProbe> probes)
    {
        int start = -1;
        int charCount = 0;

        for (int i = 0; i + 1 <= buffer.Length; i += 2)
        {
            bool isText = i + 1 < buffer.Length && buffer[i + 1] == 0 && IsAsciiProbeByte(buffer[i]);
            if (isText)
            {
                if (start < 0)
                    start = i;

                charCount++;
                continue;
            }

            if (start >= 0 && charCount >= minLength)
            {
                probes.Add(new ClientDataStringProbe(start, "utf-16le", Encoding.Unicode.GetString(buffer, start, charCount * 2)));
                if (probes.Count >= maxProbeCount)
                    return;
            }

            start = -1;
            charCount = 0;
        }

        if (start >= 0 && charCount >= minLength && probes.Count < maxProbeCount)
            probes.Add(new ClientDataStringProbe(start, "utf-16le", Encoding.Unicode.GetString(buffer, start, charCount * 2)));
    }

    private static bool IsAsciiProbeByte(byte value)
    {
        return value is >= 0x20 and <= 0x7E;
    }
}

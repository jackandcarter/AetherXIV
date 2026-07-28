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

namespace AetherXIV.ClientData;

public static class ClientDataHeaderProbeExtractor
{
    private const int ProbeByteCount = 32;

    public static async Task<ClientDataHeaderProbe> ExtractAsync(
        string filePath,
        CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[ProbeByteCount];
        await using FileStream stream = File.OpenRead(filePath);
        int bytesRead = await stream.ReadAsync(buffer, cancellationToken);

        if (bytesRead != buffer.Length)
            Array.Resize(ref buffer, bytesRead);

        return Create(buffer);
    }

    public static ClientDataHeaderProbe Create(ReadOnlySpan<byte> bytes)
    {
        string hexPrefix = Convert.ToHexString(bytes).ToLowerInvariant();
        IReadOnlyList<string> magicCandidates = FindMagicCandidates(bytes);
        ClientDataHeaderKind headerKind = Classify(bytes, magicCandidates);

        return new ClientDataHeaderProbe(hexPrefix, headerKind, magicCandidates);
    }

    private static ClientDataHeaderKind Classify(
        ReadOnlySpan<byte> bytes,
        IReadOnlyList<string> magicCandidates)
    {
        if (bytes.IsEmpty)
            return ClientDataHeaderKind.Empty;

        if (magicCandidates.Count > 0)
            return ClientDataHeaderKind.ResourceMagic;

        int zeroCount = 0;
        int highBitCount = 0;
        foreach (byte value in bytes)
        {
            if (value == 0)
                zeroCount++;

            if (value >= 0x80)
                highBitCount++;
        }

        double zeroRatio = (double)zeroCount / bytes.Length;
        if (zeroRatio >= 0.75)
            return ClientDataHeaderKind.MostlyZero;

        double highBitRatio = (double)highBitCount / bytes.Length;
        if (highBitRatio >= 0.75)
            return ClientDataHeaderKind.HighBitPacked;

        return ClientDataHeaderKind.Binary;
    }

    private static IReadOnlyList<string> FindMagicCandidates(ReadOnlySpan<byte> bytes)
    {
        List<string> candidates = [];

        for (int offset = 0; offset + 4 <= bytes.Length; offset += 4)
        {
            ReadOnlySpan<byte> word = bytes.Slice(offset, 4);
            if (!IsMagicWord(word))
                continue;

            string candidate = System.Text.Encoding.ASCII.GetString(word);
            if (!candidates.Contains(candidate, StringComparer.Ordinal))
                candidates.Add(candidate);
        }

        return candidates;
    }

    private static bool IsMagicWord(ReadOnlySpan<byte> word)
    {
        foreach (byte value in word)
        {
            bool valid = value is >= (byte)'A' and <= (byte)'Z'
                || value is >= (byte)'0' and <= (byte)'9'
                || value == (byte)'_';
            if (!valid)
                return false;
        }

        return true;
    }
}

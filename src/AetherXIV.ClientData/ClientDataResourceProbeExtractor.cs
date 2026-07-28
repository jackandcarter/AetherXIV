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

using System.Buffers.Binary;
using System.Text;

namespace AetherXIV.ClientData;

public static class ClientDataResourceProbeExtractor
{
    private const int ProbeByteCount = 2048;

    public static async Task<ClientDataResourceProbe?> ExtractAsync(
        string filePath,
        long fileSizeBytes,
        CancellationToken cancellationToken = default)
    {
        byte[] buffer = new byte[ProbeByteCount];
        await using FileStream stream = File.OpenRead(filePath);
        int bytesRead = await stream.ReadAsync(buffer, cancellationToken);

        if (bytesRead != buffer.Length)
            Array.Resize(ref buffer, bytesRead);

        return Create(buffer, fileSizeBytes);
    }

    public static ClientDataResourceProbe? Create(ReadOnlySpan<byte> bytes, long fileSizeBytes)
    {
        if (bytes.Length < 4)
            return null;

        string magic0 = ReadAscii(bytes, 0);
        string? magic4 = bytes.Length >= 8 ? ReadAscii(bytes, 4) : null;

        if (magic0 == "SEDB" && magic4 == "SSCF" && bytes.Length >= 20)
            return SedbSscfResourceParser.Parse(bytes, fileSizeBytes);

        if (magic0 == "GTEX")
        {
            return new ClientDataResourceProbe(
                ClientDataResourceFamily.Gtex,
                magic0,
                null,
                null,
                null,
                null,
                0,
                null,
                []);
        }

        if (magic0 == "VERS" && bytes.Length >= 24)
        {
            string chunkMagic = ReadAscii(bytes, 12);
            string payloadMagic = ReadAscii(bytes, 20);
            if (chunkMagic == "GTEX" && payloadMagic == "GTEX")
            {
                return new ClientDataResourceProbe(
                    ClientDataResourceFamily.VersWrappedGtex,
                    magic0,
                    payloadMagic,
                    BinaryPrimitives.ReadInt32LittleEndian(bytes.Slice(4, 4)),
                    null,
                    null,
                    20,
                    null,
                    []);
            }
        }

        return null;
    }

    private static string ReadAscii(ReadOnlySpan<byte> bytes, int offset)
    {
        return Encoding.ASCII.GetString(bytes.Slice(offset, 4));
    }
}

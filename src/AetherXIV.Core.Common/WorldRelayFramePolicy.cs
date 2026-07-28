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

using System;

namespace AetherXIV.Core.Common;

/// <summary>
/// Bounds for compressed server-to-client World frames recovered from the
/// supplied retail packet captures. The full corpus reaches 50 subpackets,
/// but never exceeds 0xFE0 bytes before zlib compression.
/// </summary>
public static class WorldRelayFramePolicy
{
    public const ushort RunEventFunctionOpcode = 0x0130;
    public const ushort EndEventOpcode = 0x0131;
    public const int MaximumSubpacketsPerFrame = 50;
    public const int MaximumUncompressedBodyBytes = 0xFE0;

    /// <summary>
    /// The supplied retail capture corpus contains 200 RunEventFunction
    /// packets and 100 EndEvent packets, but never places both opcodes in the
    /// same compressed World frame. Preserve that event-lifecycle boundary
    /// while still allowing either packet to share a frame with unrelated
    /// traffic, as the captures do.
    /// </summary>
    public static bool RequiresBoundaryBefore(
        bool currentFrameContainsRunEventFunction,
        ushort candidateSubpacketType,
        ushort candidateOpcode)
    {
        return currentFrameContainsRunEventFunction
            && candidateSubpacketType == 0x0003
            && candidateOpcode == EndEventOpcode;
    }

    public static bool CanAppend(
        int currentSubpacketCount,
        int currentBodyBytes,
        int candidateBytes)
    {
        if (currentSubpacketCount < 0)
            throw new ArgumentOutOfRangeException(
                nameof(currentSubpacketCount));
        if (currentBodyBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(currentBodyBytes));
        if (candidateBytes < 0)
            throw new ArgumentOutOfRangeException(nameof(candidateBytes));

        return currentSubpacketCount < MaximumSubpacketsPerFrame
            && (currentSubpacketCount == 0
                || currentBodyBytes + candidateBytes
                    <= MaximumUncompressedBodyBytes);
    }
}

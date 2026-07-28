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

using AetherXIV.Core.Common;

namespace AetherXIV.Data.Tests;

public sealed class LegacyBasePacketCompressionTests
{
    [Fact]
    public void WorldRelayFrameBoundsMatchTheOfficialCaptureCorpus()
    {
        Assert.Equal(
            50,
            WorldRelayFramePolicy.MaximumSubpacketsPerFrame);
        Assert.Equal(
            0xFE0,
            WorldRelayFramePolicy.MaximumUncompressedBodyBytes);

        Assert.True(WorldRelayFramePolicy.CanAppend(
            currentSubpacketCount: 49,
            currentBodyBytes: 0xFD8,
            candidateBytes: 0x08));
        Assert.False(WorldRelayFramePolicy.CanAppend(
            currentSubpacketCount: 50,
            currentBodyBytes: 0,
            candidateBytes: 0x08));
        Assert.False(WorldRelayFramePolicy.CanAppend(
            currentSubpacketCount: 1,
            currentBodyBytes: 0xFD8,
            candidateBytes: 0x10));
    }

    [Fact]
    public void RunEventFunctionAndEndEventRequireSeparateWorldFrames()
    {
        Assert.True(WorldRelayFramePolicy.RequiresBoundaryBefore(
            currentFrameContainsRunEventFunction: true,
            candidateSubpacketType: 0x0003,
            candidateOpcode: WorldRelayFramePolicy.EndEventOpcode));
        Assert.False(WorldRelayFramePolicy.RequiresBoundaryBefore(
            currentFrameContainsRunEventFunction: false,
            candidateSubpacketType: 0x0003,
            candidateOpcode: WorldRelayFramePolicy.EndEventOpcode));
        Assert.False(WorldRelayFramePolicy.RequiresBoundaryBefore(
            currentFrameContainsRunEventFunction: true,
            candidateSubpacketType: 0x0003,
            candidateOpcode: 0x00CA));
        Assert.False(WorldRelayFramePolicy.RequiresBoundaryBefore(
            currentFrameContainsRunEventFunction: true,
            candidateSubpacketType: 0x0131,
            candidateOpcode: 0));
    }

    [Fact]
    public void CompressedMultiSubpacketFramePreservesEveryRelayRecord()
    {
        List<SubPacket> expected =
        [
            new SubPacket(0x00CA, 0x44D80001, new byte[0x10]),
            new SubPacket(0x0137, 0x44D80001, new byte[0x20]),
            new SubPacket(0x00CC, 0x44D80001, new byte[0x18])
        ];

        BasePacket encoded = BasePacket.CreatePacket(
            expected,
            isAuthed: true,
            isCompressed: true);
        Assert.Equal((byte)1, encoded.header.isCompressed);
        Assert.Equal((ushort)expected.Count, encoded.header.numSubpackets);
        Assert.Equal(encoded.GetPacketBytes().Length, encoded.header.packetSize);

        BasePacket decoded = new(encoded.GetPacketBytes());
        BasePacket.DecompressPacket(ref decoded);
        List<SubPacket> actual = decoded.GetSubpackets();

        Assert.Equal(expected.Count, actual.Count);
        Assert.Equal(
            expected.Select(packet => packet.gameMessage.opcode),
            actual.Select(packet => packet.gameMessage.opcode));
        Assert.Equal(
            expected.Select(packet => packet.header.sourceId),
            actual.Select(packet => packet.header.sourceId));
    }
}

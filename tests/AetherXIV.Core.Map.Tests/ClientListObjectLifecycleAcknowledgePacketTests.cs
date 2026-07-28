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

using AetherXIV.Core.Map.packets.receive;

namespace AetherXIV.Core.Map.Tests;

public sealed class ClientListObjectLifecycleAcknowledgePacketTests
{
    [Fact]
    public void DecodesObservedActorListAcknowledge()
    {
        ClientListObjectLifecycleAcknowledgePacket packet = new(
            Convert.FromHexString("18000000112700000000000000000000"));

        Assert.False(packet.invalidPacket);
        Assert.Equal(0x18u, packet.actorId);
        Assert.Equal(ClientListObjectLifecycleAcknowledgePacket.ACTOR_LIST_TYPE, packet.listType);
        Assert.Equal(0u, packet.reserved0);
        Assert.Equal(0u, packet.reserved1);
        Assert.True(packet.IsCanonicalActorListAcknowledge());
    }

    [Theory]
    [InlineData("")]
    [InlineData("180000001127000000000000")]
    [InlineData("1800000011270000000000000000000000000000")]
    public void RejectsNonCanonicalPayloadLengths(string payloadHex)
    {
        ClientListObjectLifecycleAcknowledgePacket packet =
            new(Convert.FromHexString(payloadHex));

        Assert.True(packet.invalidPacket);
        Assert.False(packet.IsCanonicalActorListAcknowledge());
    }
}

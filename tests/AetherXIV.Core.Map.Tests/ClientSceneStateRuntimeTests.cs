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
using AetherXIV.Core.Map.packets.receive;
using AetherXIV.Core.Map.packets.send.actor;

namespace AetherXIV.Core.Map.Tests;

public sealed class ClientSceneStateRuntimeTests
{
    [Fact]
    public void CutsceneStateUsesTheOfficialDirectionSpecificPayload()
    {
        byte[] payload = new byte[0x28];
        BitConverter.GetBytes(2u).CopyTo(payload, 0);
        Encoding.ASCII.GetBytes("com0g105").CopyTo(payload, 4);
        BitConverter.GetBytes(0x0018EBD0u).CopyTo(payload, 0x24);

        CutsceneStatePacket packet = new(payload);

        Assert.False(packet.invalidPacket);
        Assert.Equal(2u, packet.state);
        Assert.Equal("com0g105", packet.cutsceneName);
        Assert.Equal(0x0018EBD0u, packet.detail);
    }

    [Fact]
    public void EventTargetUsesTheOfficialEightBytePayload()
    {
        var packet = SetActorEventTargetPacket.BuildPacket(0x029B2941, 0x46700082);

        Assert.Equal((ushort)0x00D2, packet.gameMessage.opcode);
        Assert.Equal(0x28, packet.header.subpacketSize);
        Assert.Equal(0x46700082u, BitConverter.ToUInt32(packet.data, 0));
        Assert.Equal(0u, BitConverter.ToUInt32(packet.data, 4));
    }
}

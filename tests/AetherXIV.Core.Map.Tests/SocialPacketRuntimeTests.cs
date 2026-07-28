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
using AetherXIV.Core.Map.packets.send.social;

namespace AetherXIV.Core.Map.Tests;

public sealed class SocialPacketRuntimeTests
{
    [Fact]
    public void BlacklistUsesOfficialPagedFixedWidthLayout()
    {
        string[] names = ["First", "Second"];
        var packet = SendBlacklistPacket.BuildPacket(1, 0, names);

        Assert.Equal(0x2A8, packet.header.subpacketSize);
        Assert.Equal(0x288, packet.data.Length);
        Assert.Equal(0u, BitConverter.ToUInt32(packet.data, 0));
        Assert.Equal(2u, BitConverter.ToUInt32(packet.data, 4));
        Assert.Equal("First", ReadFixedName(packet.data, 0x08));
        Assert.Equal("Second", ReadFixedName(packet.data, 0x28));
    }

    [Fact]
    public void FriendListUsesOfficialNameAndCharacterIdEntries()
    {
        Tuple<long, string>[] friends = [Tuple.Create(0x1122334455667788L, "Louisoix")];
        var packet = SendFriendlistPacket.BuildPacket(1, 0, friends);

        Assert.Equal(0x348, packet.header.subpacketSize);
        Assert.Equal(0x328, packet.data.Length);
        Assert.Equal(1u, BitConverter.ToUInt32(packet.data, 4));
        Assert.Equal("Louisoix", ReadFixedName(packet.data, 0x08));
        Assert.Equal(0x1122334455667788UL, BitConverter.ToUInt64(packet.data, 0x28));
    }

    [Fact]
    public void FriendStatusUsesOfficialHundredEntryPageLayout()
    {
        Tuple<long, bool>[] statuses = [Tuple.Create(42L, true), Tuple.Create(84L, false)];
        var packet = FriendStatusPacket.BuildPacket(1, 0, statuses);

        Assert.Equal(0x668, packet.header.subpacketSize);
        Assert.Equal(0x648, packet.data.Length);
        Assert.Equal(2u, BitConverter.ToUInt32(packet.data, 4));
        Assert.Equal(42UL, BitConverter.ToUInt64(packet.data, 0x08));
        Assert.Equal(1UL, BitConverter.ToUInt64(packet.data, 0x10));
        Assert.Equal(84UL, BitConverter.ToUInt64(packet.data, 0x18));
        Assert.Equal(0UL, BitConverter.ToUInt64(packet.data, 0x20));
    }

    private static string ReadFixedName(byte[] data, int offset)
    {
        int length = Array.IndexOf(data, (byte)0, offset, 0x20) - offset;
        return Encoding.ASCII.GetString(data, offset, length < 0 ? 0x20 : length);
    }
}

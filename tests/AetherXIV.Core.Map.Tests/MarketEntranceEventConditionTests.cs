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

using AetherXIV.Core.Map.actors;
using AetherXIV.Core.Map.packets.send.actor.events;

namespace AetherXIV.Core.Map.Tests;

public sealed class MarketEntranceEventConditionTests
{
    [Fact]
    public void HallExitPushCircleMatchesTheReviewedRuntimeContract()
    {
        const uint actorId = 0x47480002;
        EventList.PushCircleEventCondition condition = new()
        {
            conditionName = "pushDefault",
            radius = 4.0f,
            secondaryRadius = 10.0f,
            outwards = false,
            silent = false,
            isDisabled = false,
            flags = 1,
            unknown2 = 0,
            useSourceActorId = true
        };

        Core.Common.SubPacket packet = SetPushEventConditionWithCircle.BuildPacket(actorId, condition);

        Assert.Equal(0x016F, packet.gameMessage.opcode);
        Assert.Equal(4.0f, BitConverter.ToSingle(packet.data, 0));
        Assert.Equal(actorId, BitConverter.ToUInt32(packet.data, 4));
        Assert.Equal(10.0f, BitConverter.ToSingle(packet.data, 8));
        Assert.Equal(1, packet.data[16]);
        Assert.Equal(0, packet.data[17]);
        Assert.Equal(0, packet.data[18]);
        Assert.Equal("pushDefault", System.Text.Encoding.ASCII.GetString(packet.data, 19, 11));
    }
}

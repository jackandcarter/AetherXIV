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

using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.packets.send.actor;

namespace AetherXIV.Core.Map.Tests;

public sealed class ActorMovementInitializationTests
{
    [Fact]
    public void ShortConstructorPublishesUsableLegacyMovementSpeeds()
    {
        Actor actor = new(0x44D00001);

        Assert.Equal(SetActorSpeedPacket.DEFAULT_STOP, actor.moveSpeeds[0]);
        Assert.Equal(SetActorSpeedPacket.DEFAULT_WALK, actor.moveSpeeds[1]);
        Assert.Equal(SetActorSpeedPacket.DEFAULT_RUN, actor.moveSpeeds[2]);
        Assert.Equal(SetActorSpeedPacket.DEFAULT_ACTIVE, actor.moveSpeeds[3]);
    }
}

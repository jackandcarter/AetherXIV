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

using AetherXIV.Core;

namespace AetherXIV.Core.Tests;

public sealed class CorePrimitiveTests
{
    [Fact]
    public void ActorIdsRenderAsHex()
    {
        Assert.Equal("0x5FF80001", new ActorId(0x5FF80001).ToString());
    }

    [Fact]
    public void ServerEndpointsRenderAsHostAndPort()
    {
        Assert.Equal("127.0.0.1:54992", new ServerEndpoint("127.0.0.1", 54992).ToString());
    }
}

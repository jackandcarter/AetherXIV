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

namespace AetherXIV.Core.Map.Tests;

public sealed class QuestENpcRefreshPolicyTests
{
    private static QuestENpc PushTrigger() =>
        new QuestENpc(1090201, 2, false, true, false, false);

    [Fact]
    public void UnchangedPresentationIsDeduplicatedWithinTheSameArea()
    {
        Assert.False(QuestENpc.ShouldBroadcast(PushTrigger(), PushTrigger(), false));
    }

    [Fact]
    public void UnchangedPresentationIsRebroadcastAfterAnAreaChange()
    {
        Assert.True(QuestENpc.ShouldBroadcast(PushTrigger(), PushTrigger(), true));
    }

    [Fact]
    public void ChangedPresentationBroadcastsWithoutAnAreaChange()
    {
        QuestENpc disabled = new QuestENpc(1090201, 0, false, false, false, false);

        Assert.True(QuestENpc.ShouldBroadcast(disabled, PushTrigger(), false));
    }
}

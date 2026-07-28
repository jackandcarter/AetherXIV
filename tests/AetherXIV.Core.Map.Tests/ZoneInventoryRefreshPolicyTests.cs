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

using AetherXIV.Core.Map.actors.chara.player;
using AetherXIV.Core.Map.actors.area;

namespace AetherXIV.Core.Map.Tests;

public sealed class ZoneInventoryRefreshPolicyTests
{
    [Fact]
    public void LoginAndCrossZoneRefreshesResendItemDefinitions()
    {
        Assert.True(ZoneInventoryRefreshPolicy.ShouldResendItemDefinitions(
            ZoneInventoryRefreshMode.Full));
    }

    [Fact]
    public void SameZoneContentReloadRetainsKnownItemDefinitions()
    {
        Assert.False(ZoneInventoryRefreshPolicy.ShouldResendItemDefinitions(
            ZoneInventoryRefreshMode.RetainKnownItemDefinitions));
    }

    [Theory]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void GridaniaOpeningWolvesUseRetailTutorialExperience(uint battleNpcId)
    {
        Assert.True(GridaniaOpeningTutorialPolicy.IsTutorialWolf(
            GridaniaOpeningTutorialPolicy.ContentAreaName,
            battleNpcId));
        Assert.Equal((ushort)1000, GridaniaOpeningTutorialPolicy.WolfExperience);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(6)]
    public void GridaniaOpeningPolicyRejectsNonWolfActors(uint battleNpcId)
    {
        Assert.False(GridaniaOpeningTutorialPolicy.IsTutorialWolf(
            GridaniaOpeningTutorialPolicy.ContentAreaName,
            battleNpcId));
    }

    [Fact]
    public void BattleCompletionSignalIsScopedToPlayerActor()
    {
        Assert.Equal("battleComplete:1157627909",
            GridaniaOpeningTutorialPolicy.BuildBattleCompleteSignal(0x45000005));
        Assert.Equal("playerAttack:1157627909",
            GridaniaOpeningTutorialPolicy.BuildPlayerSignal("playerAttack", 0x45000005));
        Assert.Equal("playerActive:1157627909",
            GridaniaOpeningTutorialPolicy.BuildPlayerSignal("playerActive", 0x45000005));
    }
}

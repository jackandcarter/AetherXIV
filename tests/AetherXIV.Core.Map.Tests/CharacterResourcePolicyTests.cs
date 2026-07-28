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

public sealed class CharacterResourcePolicyTests
{
    [Theory]
    [InlineData(1, 19, 19, 1)]
    [InlineData(19, 19, 25, 19)]
    [InlineData(19, 19, 10, 10)]
    [InlineData(1900, 1000, 19, 19)]
    [InlineData(0, 0, 80, 80)]
    [InlineData(0, 80, 80, 0)]
    public void RecalculationPreservesExistingPoolAndOnlyInitializesAnEmptyPool(
        short previousCurrent,
        short previousMaximum,
        short newMaximum,
        short expected)
    {
        Assert.Equal(
            expected,
            CharacterResourcePolicy.RestoreCurrent(previousCurrent, previousMaximum, newMaximum));
    }

    [Fact]
    public void SetMaxMpChangesMaximumWithoutOverwritingCurrentMp()
    {
        Program.Random = new Random(1);
        Character character = new(1);
        character.SetMP(40);
        character.SetMaxMP(115);

        Assert.Equal(40, character.GetMP());
        Assert.Equal(115, character.GetMaxMP());
        Assert.Equal(34, character.GetMPP());
    }

    [Fact]
    public void RecalculateStatsDoesNotHealAnExistingCharacter()
    {
        Program.Random = new Random(1);
        RecalculatingCharacter character = new(2);
        character.SetMaxHP(19);
        character.SetHP(1);
        character.SetMaxMP(115);
        character.SetMP(40);

        character.RecalculateStats("status-effect");

        Assert.Equal(1, character.GetHP());
        Assert.Equal(19, character.GetMaxHP());
        Assert.Equal(40, character.GetMP());
        Assert.Equal(115, character.GetMaxMP());
    }

    private sealed class RecalculatingCharacter : Character
    {
        public RecalculatingCharacter(uint actorId)
            : base(actorId)
        {
        }

        public override void CalculateBaseStats()
        {
            SetMaxHP(19);
            SetHP(19);
            SetMaxMP(115);
            SetMP(115);
        }

        public void SetTpForTest(ushort tp)
        {
            tpBase = tp;
        }
    }

    [Fact]
    public void ResourcePercentagesUseFractionalDivision()
    {
        Program.Random = new Random(1);
        RecalculatingCharacter character = new(3);
        character.SetMaxMP(115);
        character.SetMP(40);
        character.SetTpForTest(1500);

        Assert.Equal(34, character.GetMPP());
        Assert.Equal(50, character.GetTPP());
    }
}

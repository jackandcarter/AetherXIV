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

using AetherXIV.Core.Map.actors.chara.ai.utils;

namespace AetherXIV.Core.Map.Tests;

public sealed class AutoAttackPotencyPolicyTests
{
    [Theory]
    [InlineData(6, 4, 14, 10)]
    [InlineData(18, 0, 90, 18)]
    [InlineData(0, 0, 12, 12)]
    [InlineData(0, 0, 1, 1)]
    [InlineData(0, 0, 0, 1)]
    public void UsesClientWeaponDamageOrNpcAttackInsteadOfUniversalPotency(
        int weaponDamage,
        int virtualAmmoDamage,
        double attack,
        ushort expected)
    {
        Assert.Equal(
            expected,
            AutoAttackPotencyPolicy.Calculate(weaponDamage, virtualAmmoDamage, attack));
    }
}

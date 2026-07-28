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

namespace AetherXIV.Core.Map.Tests;

public sealed class ConfigConstantsTests
{
    [Fact]
    public void ApplyLaunchArgsPreservesCaseSensitiveDatabaseValues()
    {
        string originalHost = ConfigConstants.DATABASE_HOST;
        string originalUser = ConfigConstants.DATABASE_USERNAME;
        string originalPassword = ConfigConstants.DATABASE_PASSWORD;

        try
        {
            ConfigConstants.ApplyLaunchArgs(
            [
                "--HOST", "MariaDB",
                "--user", "AetherUser",
                "--p", "MixedCase-Secret"
            ]);

            Assert.Equal("MariaDB", ConfigConstants.DATABASE_HOST);
            Assert.Equal("AetherUser", ConfigConstants.DATABASE_USERNAME);
            Assert.Equal("MixedCase-Secret", ConfigConstants.DATABASE_PASSWORD);
        }
        finally
        {
            ConfigConstants.DATABASE_HOST = originalHost;
            ConfigConstants.DATABASE_USERNAME = originalUser;
            ConfigConstants.DATABASE_PASSWORD = originalPassword;
        }
    }
}

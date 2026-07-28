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

using System.Text.Json;
using AetherXIV.Launcher.Contracts;

namespace AetherXIV.Launcher.Contracts.Tests;

public sealed class LauncherContractTests
{
    [Fact]
    public void LocalAetherXiv2ProfileUsesCanonicalLocalPorts()
    {
        AetherXivLauncherServerProfile profile = AetherXivLauncherDefaults.LocalAetherXiv2;

        Assert.Equal(AetherXivServerGeneration.AetherXiv2, profile.Generation);
        Assert.Equal(8080, profile.LauncherEndpoint.Port);
        Assert.Equal(54994, profile.LobbyEndpoint.Port);
        Assert.Equal(54992, profile.WorldEndpoint.Port);
        Assert.Equal(1989, profile.MapEndpoint.Port);
    }

    [Fact]
    public void LocalConfigSerializesLauncherJsonContract()
    {
        string json = JsonSerializer.Serialize(AetherXivLauncherDefaults.LocalConfig);

        Assert.Contains("\"service_version\":1", json);
        Assert.Contains("\"server_name\":\"AetherXIV 2 Local\"", json);
        Assert.Contains("\"client_login_url\":\"../login/index.php\"", json);
        Assert.Contains("\"target_game_version\":\"2012.09.19.0001\"", json);
        Assert.Contains("\"plugin_catalog_urls\":[\"umbra/plugin-catalog\"]", json);
    }

    [Fact]
    public void AuthResponseUsesSessionIdJsonName()
    {
        LauncherAuthResponse response = new(true, "ok", "tester", "abc123");

        string json = JsonSerializer.Serialize(response);

        Assert.Contains("\"session_id\":\"abc123\"", json);
    }
}

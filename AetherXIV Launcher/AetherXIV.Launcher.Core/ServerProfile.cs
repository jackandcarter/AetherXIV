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

namespace AetherXIV.Launcher.Core;

public sealed record ServerProfile(
    string Name,
    string Host,
    int LobbyPort,
    int WorldPort,
    int MapPort,
    string LoginUrl = "")
{
    public static ServerProfile LocalDefault() => new(
        "Localhost",
        "127.0.0.1",
        54994,
        54992,
        1989,
        "http://127.0.0.1:8080/login/index.php");

    public static ServerProfile DemiDevUnitDefault() => new(
        "Demi Dev Unit Developer Server",
        "game.dev.demidevunit.com",
        54994,
        54992,
        1989);

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(Name))
            throw new InvalidOperationException("Server profile name is required.");

        if (string.IsNullOrWhiteSpace(Host))
            throw new InvalidOperationException("Server host is required.");

        ValidatePort(LobbyPort, nameof(LobbyPort));
        ValidatePort(WorldPort, nameof(WorldPort));
        ValidatePort(MapPort, nameof(MapPort));

        if (!string.IsNullOrWhiteSpace(LoginUrl)
            && !Uri.TryCreate(LoginUrl, UriKind.Absolute, out _))
            throw new InvalidOperationException("Server login URL must be absolute.");
    }

    private static void ValidatePort(int port, string name)
    {
        if (port < 1 || port > 65535)
            throw new InvalidOperationException($"{name} must be between 1 and 65535.");
    }
}

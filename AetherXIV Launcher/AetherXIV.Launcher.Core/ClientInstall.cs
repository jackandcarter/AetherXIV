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

public sealed record ClientInstall(string RootPath)
{
    public string RootPath { get; } = NormalizeRequiredPath(RootPath);

    public string BootVersionPath => Path.Combine(RootPath, "boot.ver");

    public string GameVersionPath => Path.Combine(RootPath, "game.ver");

    public string StaticActorsSourcePath => StaticActorsLocator.FindSource(RootPath)
        ?? Path.Combine(RootPath, "client", "script", StaticActorsLocator.StaticActorsFileName);

    public string GameExecutablePath => ResolveGameExecutable(RootPath);

    public string BootExecutablePath => Path.Combine(RootPath, "ffxivboot.exe");

    public string UpdaterExecutablePath => Path.Combine(RootPath, "ffxivupdater.exe");

    public string ConfigExecutablePath => Path.Combine(RootPath, "ffxivconfig.exe");

    public string DirectGameExecutablePath => Path.Combine(RootPath, "ffxivgame.exe");

    public bool HasStaticActors => StaticActorsLocator.TryFindSource(RootPath, out _);

    public bool HasGameExecutable => File.Exists(GameExecutablePath);

    public static ClientInstall FromPath(string rootPath) => new(rootPath);

    public ClientInstallReport Inspect() => ClientInstallReport.Create(this);

    private static string ResolveGameExecutable(string rootPath)
    {
        string[] candidates =
        {
            Path.Combine(rootPath, "ffxivgame.exe"),
            Path.Combine(rootPath, "ffxivboot.exe"),
            Path.Combine(rootPath, "client", "ffxivgame.exe"),
            Path.Combine(rootPath, "client", "ffxivboot.exe")
        };

        return candidates.FirstOrDefault(File.Exists) ?? candidates[0];
    }

    private static string NormalizeRequiredPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Client root path is required.", nameof(path));

        return Path.GetFullPath(path);
    }
}

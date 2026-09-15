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

public sealed record UmbraSettings
{
    public const int DefaultLoadDelayMilliseconds = 0;
    public const int MaximumLoadDelayMilliseconds = 30000;

    public bool Enabled { get; init; }

    public bool SafeMode { get; init; }

    public int LoadDelayMilliseconds { get; init; } = DefaultLoadDelayMilliseconds;

    public string PluginDirectory { get; init; } = "";

    /// <summary>
    /// Launcher-side intent for the built-in Discord Rich Presence plugin. The
    /// live switch is the plugin manifest's <c>enabled</c> field; this value is
    /// the fallback when the plugin is not yet installed and the checkbox's
    /// persisted state.
    /// </summary>
    public bool DiscordRichPresenceEnabled { get; init; }

    public static UmbraSettings Default => new();

    public UmbraSettings Normalize()
    {
        return this with
        {
            LoadDelayMilliseconds = Math.Clamp(LoadDelayMilliseconds, 0, MaximumLoadDelayMilliseconds),
            PluginDirectory = string.IsNullOrWhiteSpace(PluginDirectory)
                ? UmbraInstallStore.PluginsRoot
                : Path.GetFullPath(PluginDirectory)
        };
    }
}

public sealed record UmbraLaunchOptions(
    bool Enabled,
    bool SafeMode,
    int LoadDelayMilliseconds,
    string BootstrapPath,
    string FrameworkPath,
    string PluginDirectory,
    string LogPath,
    bool EnableManagedOnWine = false,
    string SupportedRepositoryUrl = "",
    string BundledRepositoryPath = "")
{
    public static UmbraLaunchOptions Disabled => new(
        false,
        false,
        0,
        "",
        "",
        "",
        "");

    public bool HasRequiredPaths =>
        !string.IsNullOrWhiteSpace(BootstrapPath)
        && !string.IsNullOrWhiteSpace(FrameworkPath)
        && !string.IsNullOrWhiteSpace(PluginDirectory)
        && !string.IsNullOrWhiteSpace(LogPath);

    public UmbraLaunchOptions Normalize()
    {
        return this with
        {
            LoadDelayMilliseconds = Math.Clamp(LoadDelayMilliseconds, 0, UmbraSettings.MaximumLoadDelayMilliseconds)
        };
    }
}

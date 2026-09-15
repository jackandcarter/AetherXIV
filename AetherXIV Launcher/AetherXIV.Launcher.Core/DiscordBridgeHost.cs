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

/// <summary>
/// Locates the host-side Discord IPC socket directory for the Wine Discord
/// bridge (AetherXIV.DiscordBridge.exe).
///
/// When the FFXIV 1.x client runs under Wine on macOS or Linux, the in-game
/// Umbra Discord Rich Presence plugin can only see the Windows named pipe
/// <c>\\.\pipe\discord-ipc-0</c>; Wine cannot hand it the host's Unix socket.
/// The launcher therefore starts a small byte-forwarding bridge inside the
/// prefix and tells it where Discord's Unix sockets live through
/// <see cref="DiscordIpcDirectoryEnvironmentVariable"/>.
///
/// The directory is resolved here (a native process, so these are host
/// values) with the same fallback chain Discord itself uses:
/// <c>XDG_RUNTIME_DIR</c> (Linux) → <c>TMPDIR</c> (macOS) → <c>TMP</c> →
/// <c>TEMP</c> → <c>/tmp</c>. Native Windows launches never set the variable
/// because there the plugin connects to Discord's named pipe directly.
/// </summary>
public static class DiscordBridgeHost
{
    public const string DiscordIpcDirectoryEnvironmentVariable = "AETHER_DISCORD_IPC_DIR";
    public const string BridgeExecutableFileName = "AetherXIV.DiscordBridge.exe";

    private static readonly string[] DiscordIpcDirectoryCandidates =
    [
        "XDG_RUNTIME_DIR",
        "TMPDIR",
        "TMP",
        "TEMP"
    ];

    public static string? FindDiscordIpcDirectory()
    {
        return FindDiscordIpcDirectory(Environment.GetEnvironmentVariable, OperatingSystem.IsWindows());
    }

    /// <summary>
    /// Resolves the directory that should contain Discord's <c>discord-ipc-N</c>
    /// Unix sockets on the host, or <see langword="null"/> on native Windows
    /// (where no bridge is needed). Injectable for testing.
    /// </summary>
    public static string? FindDiscordIpcDirectory(
        Func<string, string?> getEnvironmentVariable,
        bool isWindows)
    {
        if (isWindows)
            return null;

        foreach (string candidate in DiscordIpcDirectoryCandidates)
        {
            string? value = getEnvironmentVariable(candidate);
            if (!string.IsNullOrWhiteSpace(value))
                return value.Trim();
        }

        return "/tmp";
    }
}

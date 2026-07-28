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

public static class ClientLaunchHelperLocator
{
    private static readonly string[] ProbeCandidateRelativePaths =
    {
        Path.Combine("Helpers", "win-x64", "AetherXIV.Launcher.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-x86", "AetherXIV.Launcher.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-arm64", "AetherXIV.Launcher.ClientLauncher.exe"),
        "AetherXIV.Launcher.ClientLauncher.exe"
    };

    private static readonly string[] LaunchCandidateRelativePaths =
    {
        Path.Combine("Helpers", "win-x64", "AetherXIV.Launcher.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-x86", "AetherXIV.Launcher.ClientLauncher.exe"),
        Path.Combine("Helpers", "win-arm64", "AetherXIV.Launcher.ClientLauncher.exe"),
        "AetherXIV.Launcher.ClientLauncher.exe"
    };

    public static string? Find(string? baseDirectory = null)
    {
        return FindFirstExisting(ProbeCandidateRelativePaths, baseDirectory);
    }

    public static string? FindLaunchHelper(string? baseDirectory = null)
    {
        return FindFirstExisting(LaunchCandidateRelativePaths, baseDirectory);
    }

    public static string? FindLaunchHelper(ClientLaunchHelperMode mode, string? baseDirectory = null)
    {
        return FindFirstExisting(GetLaunchCandidateRelativePaths(mode), baseDirectory);
    }

    public static string FindRequired(string? baseDirectory = null)
    {
        return Find(baseDirectory)
            ?? throw new FileNotFoundException("AetherXIV Launcher client launch helper is missing from the application bundle.");
    }

    public static string FindLaunchHelperRequired(string? baseDirectory = null)
    {
        return FindLaunchHelper(baseDirectory)
            ?? throw new FileNotFoundException("AetherXIV Launcher client launch helper is missing from the application bundle.");
    }

    public static string FindLaunchHelperRequired(ClientLaunchHelperMode mode, string? baseDirectory = null)
    {
        return FindLaunchHelper(mode, baseDirectory)
            ?? throw new FileNotFoundException("AetherXIV Launcher client launch helper is missing from the application bundle.");
    }

    public static ClientLaunchHelperMode ResolveEffectiveMode(
        ClientLaunchHelperMode requestedMode,
        bool requiresCompatibilityRuntime)
    {
        if (requiresCompatibilityRuntime)
        {
            return requestedMode is ClientLaunchHelperMode.X86 or ClientLaunchHelperMode.Automatic
                ? ClientLaunchHelperMode.X64
                : requestedMode;
        }

        return ClientLaunchHelperMode.X86;
    }

    private static IEnumerable<string> GetLaunchCandidateRelativePaths(ClientLaunchHelperMode mode)
    {
        return mode switch
        {
            ClientLaunchHelperMode.X86 => new[] { Path.Combine("Helpers", "win-x86", "AetherXIV.Launcher.ClientLauncher.exe") },
            ClientLaunchHelperMode.X64 => new[] { Path.Combine("Helpers", "win-x64", "AetherXIV.Launcher.ClientLauncher.exe") },
            ClientLaunchHelperMode.Arm64 => new[] { Path.Combine("Helpers", "win-arm64", "AetherXIV.Launcher.ClientLauncher.exe") },
            _ => LaunchCandidateRelativePaths
        };
    }

    private static string? FindFirstExisting(IEnumerable<string> relativePaths, string? baseDirectory)
    {
        string root = baseDirectory ?? AppContext.BaseDirectory;
        foreach (string relativePath in relativePaths)
        {
            string candidate = Path.Combine(root, relativePath);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}

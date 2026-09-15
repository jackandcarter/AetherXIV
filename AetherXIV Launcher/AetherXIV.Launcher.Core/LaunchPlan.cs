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

public sealed record LaunchPlan(
    ClientInstall ClientInstall,
    ServerProfile ServerProfile,
    WineRuntimeProfile RuntimeProfile,
    string WindowsExecutablePath,
    string Arguments,
    string LogPath,
    string? HelperLogPath,
    UmbraLaunchOptions Umbra,
    IReadOnlyDictionary<string, string> Environment)
{
    private const int HelperObservationSeconds = 15;

    public static LaunchPlan Create(
        ClientInstall clientInstall,
        ServerProfile serverProfile,
        WineRuntimeProfile runtimeProfile,
        string? logPath = null)
    {
        ArgumentNullException.ThrowIfNull(clientInstall);
        ArgumentNullException.ThrowIfNull(serverProfile);
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        Dictionary<string, string> environment = CreateServerEnvironment(serverProfile);

        foreach (KeyValuePair<string, string> pair in runtimeProfile.Environment)
            environment[pair.Key] = pair.Value;

        return new LaunchPlan(
            clientInstall,
            serverProfile,
            runtimeProfile,
            clientInstall.GameExecutablePath,
            runtimeProfile.BuildArguments(clientInstall.GameExecutablePath),
            logPath ?? RuntimeLaunchDiagnostics.CreateLogPath(),
            null,
            UmbraLaunchOptions.Disabled,
            environment);
    }

    public static LaunchPlan CreateWithHelper(
        ClientInstall clientInstall,
        ServerProfile serverProfile,
        WineRuntimeProfile runtimeProfile,
        string helperExecutablePath,
        string sessionId,
        bool mapClientPathsForWine,
        string? logPath = null,
        UmbraLaunchOptions? umbraOptions = null)
    {
        ArgumentNullException.ThrowIfNull(clientInstall);
        ArgumentNullException.ThrowIfNull(serverProfile);
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        if (string.IsNullOrWhiteSpace(helperExecutablePath))
            throw new ArgumentException("Helper executable path is required.", nameof(helperExecutablePath));
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("Session id is required.", nameof(sessionId));

        string outputLogPath = logPath ?? RuntimeLaunchDiagnostics.CreateLogPath();
        string helperOutputLogPath = Path.Combine(
            Path.GetDirectoryName(outputLogPath) ?? ".",
            $"{Path.GetFileNameWithoutExtension(outputLogPath)}.helper.log");
        string gamePath = mapClientPathsForWine
            ? WinePathMapper.ToWindowsPath(clientInstall.GameExecutablePath)
            : clientInstall.GameExecutablePath;
        string workingDirectory = mapClientPathsForWine
            ? WinePathMapper.ToWindowsPath(clientInstall.RootPath)
            : clientInstall.RootPath;
        string helperLogPath = mapClientPathsForWine
            ? WinePathMapper.ToWindowsPath(helperOutputLogPath)
            : helperOutputLogPath;

        UmbraLaunchOptions normalizedUmbra = NormalizeUmbraLaunchOptions(umbraOptions, mapClientPathsForWine);
        List<string> helperParts = new()
        {
            "--game",
            CommandLineArguments.Quote(gamePath),
            "--working-directory",
            CommandLineArguments.Quote(workingDirectory),
            "--session",
            CommandLineArguments.Quote(sessionId),
            "--server-host",
            CommandLineArguments.Quote(serverProfile.Host),
            "--log",
            CommandLineArguments.Quote(helperLogPath),
            "--observe-seconds",
            HelperObservationSeconds.ToString(),
            "--compatibility-runtime",
            mapClientPathsForWine ? "true" : "false"
        };
        AppendUmbraArguments(helperParts, normalizedUmbra);
        string helperArguments = string.Join(" ", helperParts);

        Dictionary<string, string> environment = CreateServerEnvironment(serverProfile);

        foreach (KeyValuePair<string, string> pair in runtimeProfile.Environment)
            environment[pair.Key] = pair.Value;

        string arguments;
        if (runtimeProfile.Kind == WineRuntimeKind.NativeWindows)
        {
            arguments = helperArguments;
        }
        else
        {
            string helperLaunchPath = mapClientPathsForWine
                ? WinePathMapper.ToWindowsPath(helperExecutablePath)
                : helperExecutablePath;
            arguments = runtimeProfile.BuildArguments(helperLaunchPath, helperArguments);
        }

        return new LaunchPlan(
            clientInstall,
            serverProfile,
            runtimeProfile,
            helperExecutablePath,
            arguments,
            outputLogPath,
            helperOutputLogPath,
            normalizedUmbra,
            environment);
    }

    private static Dictionary<string, string> CreateServerEnvironment(ServerProfile serverProfile) =>
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["AETHERXIV_SERVER_HOST"] = serverProfile.Host,
            ["AETHERXIV_LOBBY_PORT"] = serverProfile.LobbyPort.ToString(),
            ["AETHERXIV_WORLD_PORT"] = serverProfile.WorldPort.ToString(),
            ["AETHERXIV_MAP_PORT"] = serverProfile.MapPort.ToString()
        };

    private static UmbraLaunchOptions NormalizeUmbraLaunchOptions(
        UmbraLaunchOptions? options,
        bool mapClientPathsForWine)
    {
        if (options is null || !options.Enabled)
            return UmbraLaunchOptions.Disabled;

        UmbraLaunchOptions normalized = options.Normalize();
        if (!mapClientPathsForWine)
            return normalized;

        return normalized with
        {
            BootstrapPath = WinePathMapper.ToWindowsPath(normalized.BootstrapPath),
            FrameworkPath = WinePathMapper.ToWindowsPath(normalized.FrameworkPath),
            PluginDirectory = WinePathMapper.ToWindowsPath(normalized.PluginDirectory),
            LogPath = WinePathMapper.ToWindowsPath(normalized.LogPath),
            BundledRepositoryPath = string.IsNullOrWhiteSpace(normalized.BundledRepositoryPath)
                ? normalized.BundledRepositoryPath
                : WinePathMapper.ToWindowsPath(normalized.BundledRepositoryPath)
        };
    }

    private static void AppendUmbraArguments(List<string> parts, UmbraLaunchOptions options)
    {
        if (!options.Enabled)
            return;

        parts.Add("--umbra-enabled");
        parts.Add("true");
        parts.Add("--umbra-bootstrap");
        parts.Add(CommandLineArguments.Quote(options.BootstrapPath));
        parts.Add("--umbra-framework");
        parts.Add(CommandLineArguments.Quote(options.FrameworkPath));
        parts.Add("--umbra-plugin-dir");
        parts.Add(CommandLineArguments.Quote(options.PluginDirectory));
        parts.Add("--umbra-log");
        parts.Add(CommandLineArguments.Quote(options.LogPath));
        parts.Add("--umbra-safe-mode");
        parts.Add(options.SafeMode ? "true" : "false");
        parts.Add("--umbra-load-delay-ms");
        parts.Add(options.LoadDelayMilliseconds.ToString());
        parts.Add("--umbra-enable-managed-on-wine");
        parts.Add(options.EnableManagedOnWine ? "true" : "false");
        if (!string.IsNullOrWhiteSpace(options.SupportedRepositoryUrl))
        {
            parts.Add("--umbra-supported-repository");
            parts.Add(CommandLineArguments.Quote(options.SupportedRepositoryUrl));
        }
        if (!string.IsNullOrWhiteSpace(options.BundledRepositoryPath))
        {
            parts.Add("--umbra-bundled-repository");
            parts.Add(CommandLineArguments.Quote(options.BundledRepositoryPath));
        }
    }
}

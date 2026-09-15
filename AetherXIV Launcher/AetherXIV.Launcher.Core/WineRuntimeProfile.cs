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

public enum WineRuntimeKind
{
    NativeWindows = 0,
    WinePrefix = 2
}

public enum RuntimeSelectionMode
{
    AutomaticManaged = 0,
    CustomRuntime = 2
}

public sealed record WineRuntimeProfile(
    string Name,
    WineRuntimeKind Kind,
    string Command,
    string? BottleName,
    string? PrefixPath,
    Dictionary<string, string> Environment)
{
    public const string DefaultDirect3DConfig = "renderer=gl";
    public const string OpenGLThreadedDirect3DConfig = "renderer=gl,csmt=1";

    public static WineRuntimeProfile NativeWindows()
    {
        return new WineRuntimeProfile(
            "Windows native",
            WineRuntimeKind.NativeWindows,
            "",
            null,
            null,
            new Dictionary<string, string>());
    }

    public static WineRuntimeProfile WinePrefix(
        string name,
        string prefixPath,
        string command = "wine",
        IReadOnlyDictionary<string, string>? environment = null)
    {
        Dictionary<string, string> variables = environment is null
            ? new Dictionary<string, string>()
            : new Dictionary<string, string>(environment);
        variables.TryAdd("WINE_D3D_CONFIG", DefaultDirect3DConfig);
        variables["WINEPREFIX"] = prefixPath;

        return new WineRuntimeProfile(name, WineRuntimeKind.WinePrefix, command, null, prefixPath, variables);
    }

    public WineRuntimeProfile WithGraphicsTarget(ClientGraphicsTarget graphicsTarget)
    {
        Dictionary<string, string> variables = new(Environment);
        switch (graphicsTarget)
        {
            case ClientGraphicsTarget.OpenGLThreaded:
                variables["WINE_D3D_CONFIG"] = OpenGLThreadedDirect3DConfig;
                break;
            default:
                // Wine default (and the legacy OpenGLCompatibility value, which
                // resolves to the same backend on this pinned runtime) leaves
                // Wine's own renderer selection in place instead of pinning
                // WINE_D3D_CONFIG.
                variables.Remove("WINE_D3D_CONFIG");
                break;
        }

        return this with { Environment = variables };
    }

    public string BuildArguments(string windowsExecutablePath, string? applicationArguments = null)
    {
        if (Kind == WineRuntimeKind.NativeWindows)
            return applicationArguments ?? "";

        if (Kind != WineRuntimeKind.WinePrefix)
            throw new InvalidOperationException("AetherXIV only launches through its bundled Wine runtime and isolated managed prefix.");

        if (string.IsNullOrWhiteSpace(Command))
            throw new InvalidOperationException("Runtime command is required.");

        if (string.IsNullOrWhiteSpace(windowsExecutablePath))
            throw new InvalidOperationException("Windows executable path is required.");

        string arguments = CommandLineArguments.Quote(windowsExecutablePath);
        return string.IsNullOrWhiteSpace(applicationArguments)
            ? arguments
            : $"{arguments} {applicationArguments}";
    }
}

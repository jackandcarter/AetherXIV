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

using System.Runtime.InteropServices;

namespace AetherXIV.Launcher.Core;

public enum LauncherOperatingSystem
{
    Windows,
    MacOS,
    Linux,
    Unknown
}

public sealed record LauncherPlatform(LauncherOperatingSystem OperatingSystem, string RuntimeIdentifier)
{
    public static LauncherPlatform Current => Detect();

    public bool RequiresCompatibilityRuntime => OperatingSystem is LauncherOperatingSystem.MacOS or LauncherOperatingSystem.Linux;

    public bool UsesNativeWindowsClient => OperatingSystem == LauncherOperatingSystem.Windows;

    public static LauncherPlatform Detect()
    {
        Architecture architecture = System.Runtime.InteropServices.RuntimeInformation.OSArchitecture;
        string architectureId = architecture switch
        {
            Architecture.X86 => "x86",
            Architecture.X64 => "x64",
            Architecture.Arm64 => "arm64",
            Architecture.Arm => "arm",
            _ => architecture.ToString().ToLowerInvariant()
        };

        if (System.OperatingSystem.IsWindows())
            return new LauncherPlatform(LauncherOperatingSystem.Windows, $"win-{architectureId}");

        if (System.OperatingSystem.IsMacOS())
            return new LauncherPlatform(LauncherOperatingSystem.MacOS, $"osx-{architectureId}");

        if (System.OperatingSystem.IsLinux())
            return new LauncherPlatform(LauncherOperatingSystem.Linux, $"linux-{architectureId}");

        return new LauncherPlatform(LauncherOperatingSystem.Unknown, $"unknown-{architectureId}");
    }
}

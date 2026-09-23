/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

namespace AetherXIV.Launcher.Core;

public static class DxvkRuntimeConfigurator
{
    public static WineRuntimeProfile ApplyD3D9(
        WineRuntimeProfile profile,
        string prefixPath,
        string dxvkDllPath)
    {
        ArgumentNullException.ThrowIfNull(profile);
        ArgumentException.ThrowIfNullOrWhiteSpace(prefixPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(dxvkDllPath);
        if (!File.Exists(dxvkDllPath))
            throw new FileNotFoundException("The verified DXVK D3D9 DLL was not found.", dxvkDllPath);

        string wow64SystemDirectory = Path.Combine(
            Path.GetFullPath(prefixPath),
            "drive_c",
            "windows",
            "syswow64");
        Directory.CreateDirectory(wow64SystemDirectory);
        File.Copy(dxvkDllPath, Path.Combine(wow64SystemDirectory, "d3d9.dll"), true);

        Dictionary<string, string> environment = new(profile.Environment)
        {
            ["WINEDLLOVERRIDES"] = "d3d9=n,b"
        };
        environment.Remove("WINE_D3D_CONFIG");
        return profile with { Environment = environment };
    }
}

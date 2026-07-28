/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 *
 * AetherXIV is free software: you may redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using Avalonia;
using AetherXIV.Launcher.Core;

namespace AetherXIV.Launcher.App;

internal static class Program
{
    [STAThread]
    public static int Main(string[] args)
    {
        if (OperatingSystem.IsWindows()
            && args.Length == 1
            && string.Equals(
                args[0],
                WindowsFfxivLaunchRedirects.RepairCommand,
                StringComparison.OrdinalIgnoreCase))
        {
            try
            {
                WindowsFfxivLaunchRedirects.RemoveNovumRedirects();
                return WindowsFfxivLaunchRedirects.FindNovumRedirects().Count == 0 ? 0 : 2;
            }
            catch
            {
                return 1;
            }
        }

        return BuildAvaloniaApp().StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        return AppBuilder.Configure<App>()
            .UsePlatformDetect()
            .LogToTrace();
    }
}

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

public static class RuntimeInstallStore
{
    public static string ApplicationDataRoot => GetApplicationDataRoot();

    public static string PrefixesRoot => Path.Combine(ApplicationDataRoot, "Prefixes");

    public static string ManagedPrefixPath => Path.Combine(PrefixesRoot, "ffxiv-1x");

    public static string LogsRoot => Path.Combine(ApplicationDataRoot, "Logs");

    public static string ServerProfilePath => Path.Combine(ApplicationDataRoot, "servers.xml");

    public static string GetApplicationDataRoot()
    {
        string root;
        if (OperatingSystem.IsLinux())
        {
            string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            root = string.IsNullOrWhiteSpace(xdgDataHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : xdgDataHome;
        }
        else
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(root, "Demi Dev Unit", "AetherXIV Launcher");
    }

    public static void ResetManagedPrefix()
    {
        if (Directory.Exists(ManagedPrefixPath))
            Directory.Delete(ManagedPrefixPath, true);
        RuntimeReadinessStore.Invalidate();
    }

    public static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "runtime";

        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new(value.Select(character =>
            invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character).ToArray());

        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);

        return cleaned.Trim('-', '.').ToLowerInvariant();
    }

}

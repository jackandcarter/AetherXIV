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

public static class WinePathMapper
{
    public static string ToWindowsPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path is required.", nameof(path));

        string fullPath = Path.GetFullPath(path);
        if (OperatingSystem.IsWindows())
            return fullPath;

        string normalized = fullPath.Replace('/', '\\');
        return normalized.StartsWith('\\')
            ? $"Z:{normalized}"
            : normalized;
    }
}

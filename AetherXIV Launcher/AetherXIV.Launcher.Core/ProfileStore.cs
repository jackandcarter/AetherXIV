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

using System.Text.Json;

namespace AetherXIV.Launcher.Core;

public static class ProfileStore
{
    private static readonly JsonSerializerOptions Options = new()
    {
        WriteIndented = true
    };

    public static string DefaultProfilePath
    {
        get
        {
            return Path.Combine(RuntimeInstallStore.ApplicationDataRoot, "profile.json");
        }
    }

    private static string LegacyDefaultProfilePath
    {
        get
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);

            return Path.Combine(root, "Demi Dev Unit", "Echo Gate", "profile.json");
        }
    }

    public static void Save(string path, LauncherProfile profile)
    {
        ArgumentNullException.ThrowIfNull(profile);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(profile, Options));
    }

    public static void SaveDefault(LauncherProfile profile)
    {
        Save(DefaultProfilePath, profile);
    }

    public static LauncherProfile Load(string path)
    {
        string json = File.ReadAllText(path);
        LauncherProfile? profile = JsonSerializer.Deserialize<LauncherProfile>(json, Options);
        return profile ?? throw new InvalidOperationException("Launcher profile could not be read.");
    }

    public static LauncherProfile LoadDefaultOrCreate()
    {
        if (File.Exists(DefaultProfilePath))
            return Load(DefaultProfilePath);

        string legacyPath = LegacyDefaultProfilePath;
        if (!string.Equals(legacyPath, DefaultProfilePath, StringComparison.Ordinal)
            && File.Exists(legacyPath))
        {
            return Load(legacyPath);
        }

        return LauncherProfile.LocalDefault();
    }
}

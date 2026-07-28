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

namespace Aether.Umbra.Framework;

public sealed record UmbraDeveloperPluginSettings(
    bool Enabled,
    IReadOnlyList<string> Locations)
{
    public static UmbraDeveloperPluginSettings Default { get; } =
        new(false, Array.Empty<string>());
}

internal static class UmbraDeveloperPluginSettingsStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static UmbraDeveloperPluginSettings Load(string cacheDirectory, UmbraRuntimeLog log)
    {
        string path = GetPath(cacheDirectory);
        if (!File.Exists(path))
            return UmbraDeveloperPluginSettings.Default;

        try
        {
            UmbraDeveloperPluginSettings? settings =
                JsonSerializer.Deserialize<UmbraDeveloperPluginSettings>(
                    File.ReadAllText(path),
                    JsonOptions);
            return Normalize(settings);
        }
        catch (Exception ex)
        {
            log.Warning($"umbra_developer_plugin_settings_invalid error={ex.Message}");
            return UmbraDeveloperPluginSettings.Default;
        }
    }

    public static UmbraDeveloperPluginSettings Save(
        string cacheDirectory,
        UmbraDeveloperPluginSettings settings)
    {
        UmbraDeveloperPluginSettings normalized = Normalize(settings);
        string path = GetPath(cacheDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        string temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(normalized, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }

        return normalized;
    }

    private static UmbraDeveloperPluginSettings Normalize(
        UmbraDeveloperPluginSettings? settings)
    {
        if (settings is null)
            return UmbraDeveloperPluginSettings.Default;

        List<string> locations = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (string candidate in settings.Locations ?? Array.Empty<string>())
        {
            if (string.IsNullOrWhiteSpace(candidate))
                continue;

            string location;
            try
            {
                location = Path.GetFullPath(candidate.Trim());
            }
            catch
            {
                continue;
            }

            if (seen.Add(location))
                locations.Add(location);
        }

        return new UmbraDeveloperPluginSettings(settings.Enabled, locations);
    }

    private static string GetPath(string cacheDirectory) =>
        Path.Combine(cacheDirectory, "DeveloperPlugins", "settings.json");
}

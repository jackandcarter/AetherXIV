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

internal static class UmbraRepositoryRegistry
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static IReadOnlyList<UmbraRepositorySource> Load(
        string cacheDirectory,
        UmbraRuntimeLog log)
    {
        List<UmbraRepositorySource> sources = [];
        string path = GetPath(cacheDirectory);
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                IReadOnlyList<UmbraRepositorySource>? persisted =
                    JsonSerializer.Deserialize<IReadOnlyList<UmbraRepositorySource>>(json, JsonOptions);
                sources.AddRange((persisted ?? Array.Empty<UmbraRepositorySource>())
                    .Where(source => string.Equals(
                        source.Source,
                        UmbraRepositorySource.Custom,
                        StringComparison.OrdinalIgnoreCase)));
            }
            catch (Exception ex)
            {
                log.Warning($"umbra_repository_registry_load_failed error={ex.Message}");
            }
        }
        return UmbraRepositorySource.Normalize(sources);
    }

    public static void SaveCustom(
        string cacheDirectory,
        IEnumerable<UmbraRepositorySource> sources)
    {
        string path = GetPath(cacheDirectory);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        UmbraRepositorySource[] custom = UmbraRepositorySource.Normalize(sources)
            .Where(source => string.Equals(
                source.Source,
                UmbraRepositorySource.Custom,
                StringComparison.OrdinalIgnoreCase))
            .ToArray();
        string temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, JsonSerializer.Serialize(custom, JsonOptions));
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string GetPath(string cacheDirectory) =>
        Path.Combine(cacheDirectory, "Repositories", "custom-sources.json");
}

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

namespace Aether.Umbra.Framework;

internal static class UmbraDeveloperPluginDiscovery
{
    private static readonly string[] ManifestFileNames =
    [
        "umbra-plugin.json",
        "plugin.json"
    ];

    public static IReadOnlyList<UmbraPluginManifest> Discover(
        IEnumerable<string> locations,
        UmbraRuntimeLog log)
    {
        List<UmbraPluginManifest> manifests = new();
        HashSet<string> seenIds = new(StringComparer.OrdinalIgnoreCase);
        foreach (string location in locations)
        {
            try
            {
                UmbraPluginManifest manifest = LoadLocation(location);
                if (!seenIds.Add(manifest.Id))
                {
                    log.Warning(
                        $"umbra_developer_plugin_duplicate id={manifest.Id} location={location}");
                    continue;
                }

                manifests.Add(manifest);
                log.Info(
                    $"umbra_developer_plugin_manifest={manifest.Id}|{manifest.Name}|{manifest.Version}|{location}");
            }
            catch (Exception ex)
            {
                log.Warning($"umbra_developer_plugin_invalid location={location} error={ex.Message}");
            }
        }

        return manifests;
    }

    public static UmbraPluginManifest LoadLocation(string location)
    {
        if (string.IsNullOrWhiteSpace(location))
            throw new InvalidDataException("Enter an absolute plugin DLL, manifest, or directory path.");

        string fullPath = Path.GetFullPath(location.Trim());
        string manifestPath;
        string? expectedAssemblyPath = null;
        if (Directory.Exists(fullPath))
        {
            manifestPath = FindManifest(fullPath)
                ?? throw new FileNotFoundException(
                    "The developer plugin directory does not contain umbra-plugin.json or plugin.json.",
                    fullPath);
        }
        else if (File.Exists(fullPath)
            && string.Equals(Path.GetExtension(fullPath), ".dll", StringComparison.OrdinalIgnoreCase))
        {
            expectedAssemblyPath = fullPath;
            manifestPath = FindManifest(Path.GetDirectoryName(fullPath)!)
                ?? throw new FileNotFoundException(
                    "The developer plugin DLL needs an adjacent umbra-plugin.json or plugin.json.",
                    fullPath);
        }
        else if (File.Exists(fullPath)
            && string.Equals(Path.GetExtension(fullPath), ".json", StringComparison.OrdinalIgnoreCase))
        {
            manifestPath = fullPath;
        }
        else
        {
            throw new FileNotFoundException(
                "The developer plugin location does not exist or is not a supported path.",
                fullPath);
        }

        UmbraPluginManifest manifest = UmbraPluginManifest.Load(manifestPath);
        string manifestDirectory = Path.GetDirectoryName(manifest.ManifestPath)!;
        string entryPath = Path.GetFullPath(Path.Combine(manifestDirectory, manifest.Entry));
        if (!File.Exists(entryPath))
            throw new FileNotFoundException("The developer plugin entry assembly was not found.", entryPath);
        if (expectedAssemblyPath is not null
            && !string.Equals(entryPath, expectedAssemblyPath, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                "The selected DLL does not match the entry declared by its adjacent manifest.");
        }

        return manifest with
        {
            Enabled = true,
            IsDeveloperPlugin = true,
            DeveloperLocation = fullPath
        };
    }

    private static string? FindManifest(string directory)
    {
        foreach (string fileName in ManifestFileNames)
        {
            string candidate = Path.Combine(directory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }
}

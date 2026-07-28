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

public sealed record UmbraPluginCatalogState(
    IReadOnlyList<UmbraPluginManifest> Installed,
    IReadOnlyList<UmbraStoreEntry> Supported,
    IReadOnlyList<UmbraStoreEntry> Available,
    IReadOnlyList<UmbraStoreEntry> Updates)
{
    internal IReadOnlyList<UmbraStoreEntry> StoreEntries { get; init; } =
        Array.Empty<UmbraStoreEntry>();

    public static UmbraPluginCatalogState Build(
        IEnumerable<UmbraPluginManifest> installed,
        IEnumerable<UmbraStoreEntry> storeEntries)
    {
        IReadOnlyList<UmbraPluginManifest> installedList = installed
            .OrderBy(plugin => plugin.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(plugin => plugin.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<UmbraStoreEntry> eligibleEntries = storeEntries
            .Where(entry => !entry.IsHidden
                && !entry.TestingOnly
                && IsCompatible(entry))
            .ToArray();

        IReadOnlyList<UmbraStoreEntry> supported = eligibleEntries
            .Where(entry => string.Equals(
                entry.Source,
                UmbraRepositorySource.Supported,
                StringComparison.OrdinalIgnoreCase))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(SelectLatest)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        HashSet<string> supportedIds = supported
            .Select(entry => entry.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        IReadOnlyList<UmbraStoreEntry> available = eligibleEntries
            .Where(entry => string.Equals(
                    entry.Source,
                    UmbraRepositorySource.Custom,
                    StringComparison.OrdinalIgnoreCase)
                && !supportedIds.Contains(entry.Id))
            .GroupBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .Select(SelectLatest)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
        IReadOnlyList<UmbraStoreEntry> updates = installedList
            .Select(manifest => SelectUpdate(manifest, eligibleEntries))
            .Where(entry => entry is not null)
            .Select(entry => entry!)
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();

        return new UmbraPluginCatalogState(installedList, supported, available, updates)
        {
            StoreEntries = eligibleEntries
        };
    }

    private static UmbraStoreEntry? SelectUpdate(
        UmbraPluginManifest manifest,
        IEnumerable<UmbraStoreEntry> entries)
    {
        if (manifest.IsDeveloperPlugin)
            return null;

        UmbraStoreEntry[] candidates = entries
            .Where(entry => string.Equals(entry.Id, manifest.Id, StringComparison.OrdinalIgnoreCase)
                && IsNewerVersion(entry.Version, manifest.Version))
            .ToArray();
        if (candidates.Length == 0)
            return null;

        if (!string.IsNullOrWhiteSpace(manifest.InstalledFromUrl))
        {
            candidates = candidates
                .Where(entry => string.Equals(
                        entry.RepositoryUrl,
                        manifest.InstalledFromUrl,
                        StringComparison.OrdinalIgnoreCase)
                    && (string.IsNullOrWhiteSpace(manifest.InstalledFromSource)
                        || string.Equals(
                            entry.Source,
                            manifest.InstalledFromSource,
                            StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }
        else
        {
            int repositoryCount = candidates
                .Select(entry => (entry.RepositoryUrl, entry.Source))
                .Distinct(RepositoryIdentityComparer.Instance)
                .Take(2)
                .Count();
            if (repositoryCount != 1)
                return null;
        }

        return candidates.Length == 0 ? null : SelectLatest(candidates);
    }

    private static bool IsNewerVersion(string candidate, string installed)
    {
        if (Version.TryParse(candidate, out Version? candidateVersion)
            && Version.TryParse(installed, out Version? installedVersion))
        {
            return candidateVersion > installedVersion;
        }

        return string.Compare(candidate, installed, StringComparison.OrdinalIgnoreCase) > 0;
    }

    private static UmbraStoreEntry SelectLatest(IEnumerable<UmbraStoreEntry> entries)
    {
        return entries
            .OrderByDescending(entry => ParseVersion(entry.Version))
            .ThenByDescending(entry => entry.Version, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.RepositoryUrl, StringComparer.OrdinalIgnoreCase)
            .First();
    }

    private static Version ParseVersion(string value) =>
        Version.TryParse(value, out Version? parsed) ? parsed : new Version(0, 0);

    private static bool IsCompatible(UmbraStoreEntry entry) =>
        UmbraPluginCompatibility.SupportsApi(entry.ApiVersion)
        && UmbraPluginCompatibility.SupportsFramework(entry.MinimumFrameworkVersion);

    private sealed class RepositoryIdentityComparer
        : IEqualityComparer<(string RepositoryUrl, string Source)>
    {
        public static RepositoryIdentityComparer Instance { get; } = new();

        public bool Equals(
            (string RepositoryUrl, string Source) x,
            (string RepositoryUrl, string Source) y) =>
            string.Equals(x.RepositoryUrl, y.RepositoryUrl, StringComparison.OrdinalIgnoreCase)
            && string.Equals(x.Source, y.Source, StringComparison.OrdinalIgnoreCase);

        public int GetHashCode((string RepositoryUrl, string Source) value) =>
            HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.RepositoryUrl),
                StringComparer.OrdinalIgnoreCase.GetHashCode(value.Source));
    }
}

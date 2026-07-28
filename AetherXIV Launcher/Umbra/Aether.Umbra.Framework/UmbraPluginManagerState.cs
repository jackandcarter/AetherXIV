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

public enum UmbraPluginManagerTab
{
    Installed,
    Supported,
    Available,
    Updates,
    Settings,
    Logs
}

public sealed record UmbraPluginManagerState(
    bool IsOpen,
    UmbraPluginManagerTab ActiveTab,
    UmbraPluginCatalogState Catalog,
    IReadOnlyList<UmbraRepositorySource> RepositorySources,
    bool SafeMode,
    bool DebugLoggingEnabled,
    bool DevUiEnabled,
    bool PluginExecutionEnabled)
{
    internal UmbraThirdPartyPluginHost? RuntimeHost { get; set; }

    public UmbraDeveloperPluginSettings DeveloperPlugins { get; init; } =
        UmbraDeveloperPluginSettings.Default;

    public static UmbraPluginManagerState Default => new(
        false,
        UmbraPluginManagerTab.Installed,
        new UmbraPluginCatalogState(
            Array.Empty<UmbraPluginManifest>(),
            Array.Empty<UmbraStoreEntry>(),
            Array.Empty<UmbraStoreEntry>(),
            Array.Empty<UmbraStoreEntry>()),
        Array.Empty<UmbraRepositorySource>(),
        false,
        false,
        false,
        false);

    public IReadOnlyList<UmbraPluginManifest> InstalledPlugins => Catalog.Installed;

    public IReadOnlyList<UmbraStoreEntry> SupportedPlugins => Catalog.Supported;

    public IReadOnlyList<UmbraStoreEntry> AvailablePlugins => Catalog.Available;

    public IReadOnlyList<UmbraStoreEntry> Updates => Catalog.Updates;

    public IReadOnlyList<UmbraPluginRuntimeStatus> RuntimePlugins =>
        RuntimeHost?.Statuses ?? Array.Empty<UmbraPluginRuntimeStatus>();
}

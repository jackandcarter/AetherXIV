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

using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

public sealed class UmbraRuntime : IDisposable
{
    private readonly CancellationTokenSource shutdown = new();
    private readonly SemaphoreSlim pluginMutationGate = new(1, 1);
    private readonly UmbraSystemPluginHost systemPlugins;
    private readonly Task updateLoop;
    private readonly Task repositoryRefreshTask;
    private bool disposed;

    private UmbraRuntime(
        UmbraRuntimeOptions options,
        UmbraRuntimeLog log,
        UmbraPluginManagerState pluginManager,
        UmbraDevBridgeService devBridge,
        IReadOnlyList<UmbraPluginManifest> manifests)
    {
        Options = options;
        Log = log;
        PluginManager = pluginManager;
        DevBridge = devBridge;
        Commands = new UmbraCommandService(log);
        Chat = new UmbraChatService(log);
        ActorAppearance = new UmbraActorAppearanceService();
        systemPlugins = new UmbraSystemPluginHost(this);
        Plugins = new UmbraThirdPartyPluginHost(this);
        RenderBridge = new UmbraRenderBridge(this);
        PluginManager.RuntimeHost = Plugins;
        systemPlugins.Register(new UmbraPluginManagerPlugin(this));
        systemPlugins.Register(new UmbraDevBridgePlugin());
        systemPlugins.Register(new UmbraTraceCompanionPlugin());
        systemPlugins.Initialize();
        Plugins.LoadEnabled(manifests);
        updateLoop = Task.Run(() => RunUpdateLoopAsync(shutdown.Token));
        repositoryRefreshTask = PluginManager.RepositorySources.Count == 0
            ? Task.CompletedTask
            : Task.Run(() => RunInitialRepositoryRefreshAsync(shutdown.Token));
    }

    public UmbraRuntimeOptions Options { get; }

    public UmbraRuntimeLog Log { get; }

    public UmbraPluginManagerState PluginManager { get; private set; }

    public UmbraDevBridgeService DevBridge { get; }

    internal UmbraCommandService Commands { get; }

    internal UmbraChatService Chat { get; }

    internal UmbraActorAppearanceService ActorAppearance { get; }

    public UmbraThirdPartyPluginHost Plugins { get; }

    public UmbraRenderBridge RenderBridge { get; }

    public CancellationToken ShutdownToken => shutdown.Token;

    public void Draw(IUmbraDrawContext drawContext)
    {
        systemPlugins.Draw(drawContext);
        Plugins.Draw(drawContext);
    }

    public void RequestPluginManagerOpen()
    {
        SetPluginManagerOpen(true);
        Log.Info("umbra_plugin_manager_open_requested=true");
    }

    internal void SynchronizePluginManagerOpen(bool isOpen)
    {
        if (PluginManager.IsOpen != isOpen)
            SetPluginManagerOpen(isOpen);
    }

    internal void SetPluginManagerOpen(bool isOpen)
    {
        PluginManager = PluginManager with { IsOpen = isOpen };
        PluginManager.RuntimeHost = Plugins;
    }

    internal void SetPluginManagerTab(UmbraPluginManagerTab tab)
    {
        PluginManager = PluginManager with { ActiveTab = tab };
        PluginManager.RuntimeHost = Plugins;
    }

    internal void SetPluginManagerPreferences(bool debugLoggingEnabled, bool devUiEnabled)
    {
        PluginManager = PluginManager with
        {
            DebugLoggingEnabled = debugLoggingEnabled,
            DevUiEnabled = devUiEnabled
        };
        PluginManager.RuntimeHost = Plugins;
    }

    internal UmbraPluginActionResult SetPluginEnabled(string pluginId, bool enabled)
    {
        UmbraPluginManifest? manifest = PluginManager.InstalledPlugins.FirstOrDefault(
            candidate => string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
            return UmbraPluginActionResult.Failure($"Plugin not found: {pluginId}");
        if (manifest.IsDeveloperPlugin)
            return UmbraPluginActionResult.Failure(
                "Developer plugins are controlled by the Developer Plugins switch and local location list.");
        if (enabled && Options.SafeMode)
            return UmbraPluginActionResult.Failure("Safe mode blocks third-party plugin activation.");
        if (manifest.Enabled == enabled)
            return UmbraPluginActionResult.Success(enabled ? "Plugin is already enabled." : "Plugin is already disabled.");

        try
        {
            UmbraPluginManifest updated = manifest with { Enabled = enabled };
            updated.Save();
            ReplaceInstalledManifest(updated);

            if (enabled)
            {
                UmbraPluginRuntimeStatus status = Plugins.Load(updated);
                if (status.State != UmbraPluginRuntimeState.Running)
                    return UmbraPluginActionResult.Failure($"Enabled, but loading failed: {status.LastError}");
            }
            else
            {
                Plugins.Unload(pluginId);
            }

            Log.Info($"umbra_plugin_enabled_changed id={pluginId} enabled={enabled}");
            return UmbraPluginActionResult.Success(enabled ? "Plugin enabled." : "Plugin disabled.");
        }
        catch (Exception ex)
        {
            Log.Error($"umbra_plugin_enabled_change_failed id={pluginId} enabled={enabled}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
    }

    internal UmbraPluginActionResult ReloadPlugin(string pluginId)
    {
        UmbraPluginManifest? manifest = PluginManager.InstalledPlugins.FirstOrDefault(
            candidate => string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
            return UmbraPluginActionResult.Failure($"Plugin not found: {pluginId}");
        if (!manifest.Enabled)
            return UmbraPluginActionResult.Failure("Enable the plugin before reloading it.");
        if (Options.SafeMode)
            return UmbraPluginActionResult.Failure("Safe mode blocks third-party plugin reloads.");

        Plugins.Unload(pluginId);
        UmbraPluginRuntimeStatus status = Plugins.Load(manifest);
        if (status.State != UmbraPluginRuntimeState.Running)
            return UmbraPluginActionResult.Failure($"Reload failed: {status.LastError}");

        Log.Info($"umbra_plugin_reloaded id={pluginId}");
        return UmbraPluginActionResult.Success("Plugin reloaded.");
    }

    internal UmbraPluginActionResult UninstallPlugin(string pluginId)
    {
        UmbraPluginManifest? manifest = PluginManager.InstalledPlugins.FirstOrDefault(
            candidate => string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase));
        if (manifest is null)
            return UmbraPluginActionResult.Failure($"Plugin not found: {pluginId}");
        if (manifest.IsDeveloperPlugin)
            return UmbraPluginActionResult.Failure(
                "Remove developer plugins from the local location list; Umbra will not delete development files.");

        string pluginRoot = Path.GetFullPath(Options.PluginDirectory);
        string installRoot = Path.GetFullPath(Path.GetDirectoryName(manifest.ManifestPath) ?? "");
        string relative = Path.GetRelativePath(pluginRoot, installRoot);
        if (string.Equals(installRoot, pluginRoot, StringComparison.OrdinalIgnoreCase)
            || relative.StartsWith("..", StringComparison.Ordinal))
            return UmbraPluginActionResult.Failure("Plugin installation directory is not safely contained in the plugin root.");

        string trashRoot = Path.Combine(Options.CacheDirectory, "PluginTrash");
        string destination = Path.Combine(
            trashRoot,
            $"{SanitizePluginId(pluginId)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");

        Plugins.Unload(pluginId);
        try
        {
            Directory.CreateDirectory(trashRoot);
            Directory.Move(installRoot, destination);
            RemoveInstalledManifest(pluginId);
            Log.Info($"umbra_plugin_uninstalled id={pluginId} archive={destination}");
            return UmbraPluginActionResult.Success("Plugin uninstalled and archived for recovery.");
        }
        catch (Exception ex)
        {
            if (manifest.Enabled && !Options.SafeMode)
                Plugins.Load(manifest);
            Log.Error($"umbra_plugin_uninstall_failed id={pluginId}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
    }

    internal async Task<UmbraPluginActionResult> RefreshRepositoriesAsync()
    {
        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        try
        {
            IReadOnlyList<UmbraStoreEntry> entries = await UmbraRepositoryFetcher.FetchAsync(
                PluginManager.RepositorySources,
                Path.Combine(Options.CacheDirectory, "Repositories"),
                Log,
                shutdown.Token).ConfigureAwait(false);
            ReplaceStoreCatalog(entries);
            Log.Info($"umbra_repository_refresh_success entries={entries.Count}");
            return UmbraPluginActionResult.Success($"Repositories refreshed: {entries.Count} plugin entries.");
        }
        catch (Exception ex)
        {
            Log.Error("umbra_repository_refresh_failed", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    internal async Task<UmbraPluginActionResult> AddCustomRepositoryAsync(string url)
    {
        IReadOnlyList<UmbraRepositorySource> normalized = UmbraRepositorySource.Normalize(
            new[] { new UmbraRepositorySource(url, UmbraRepositorySource.Custom) });
        if (normalized.Count != 1)
            return UmbraPluginActionResult.Failure("Enter an absolute HTTPS repository index URL.");

        UmbraRepositorySource source = normalized[0];
        if (PluginManager.RepositorySources.Any(candidate =>
            string.Equals(candidate.Url, source.Url, StringComparison.OrdinalIgnoreCase)))
        {
            return UmbraPluginActionResult.Failure("That repository is already configured.");
        }

        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        try
        {
            IReadOnlyList<UmbraStoreEntry> fetched = await UmbraRepositoryFetcher.FetchRepositoryAsync(
                source,
                Path.Combine(Options.CacheDirectory, "Repositories"),
                shutdown.Token).ConfigureAwait(false);
            IReadOnlyList<UmbraRepositorySource> sources = UmbraRepositorySource.Normalize(
                PluginManager.RepositorySources.Append(source));
            UmbraRepositoryRegistry.SaveCustom(Options.CacheDirectory, sources);

            IEnumerable<UmbraStoreEntry> retained = PluginManager.Catalog.StoreEntries
                .Where(entry => !string.Equals(entry.RepositoryUrl, source.Url, StringComparison.OrdinalIgnoreCase));
            PluginManager = PluginManager with
            {
                RepositorySources = sources,
                Catalog = UmbraPluginCatalogState.Build(
                    PluginManager.InstalledPlugins,
                    retained.Concat(fetched))
            };
            PluginManager.RuntimeHost = Plugins;
            Log.Info($"umbra_custom_repository_added url={source.Url} entries={fetched.Count}");
            return UmbraPluginActionResult.Success(
                $"Custom repository added: {fetched.Count} plugin entries discovered.");
        }
        catch (Exception ex)
        {
            Log.Error($"umbra_custom_repository_add_failed url={source.Url}", ex);
            return UmbraPluginActionResult.Failure($"Repository validation failed: {ex.Message}");
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    internal async Task<UmbraPluginActionResult> RemoveCustomRepositoryAsync(string url)
    {
        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        try
        {
            UmbraRepositorySource? source = PluginManager.RepositorySources.FirstOrDefault(candidate =>
                string.Equals(candidate.Url, url, StringComparison.OrdinalIgnoreCase));
            if (source is null || !string.Equals(source.Source, UmbraRepositorySource.Custom, StringComparison.OrdinalIgnoreCase))
                return UmbraPluginActionResult.Failure("Only custom repositories can be removed.");

            IReadOnlyList<UmbraRepositorySource> sources = PluginManager.RepositorySources
                .Where(candidate => !string.Equals(candidate.Url, url, StringComparison.OrdinalIgnoreCase))
                .ToArray();
            UmbraRepositoryRegistry.SaveCustom(Options.CacheDirectory, sources);
            IEnumerable<UmbraStoreEntry> retained = PluginManager.Catalog.StoreEntries
                .Where(entry => !string.Equals(entry.RepositoryUrl, url, StringComparison.OrdinalIgnoreCase));
            PluginManager = PluginManager with
            {
                RepositorySources = sources,
                Catalog = UmbraPluginCatalogState.Build(PluginManager.InstalledPlugins, retained)
            };
            PluginManager.RuntimeHost = Plugins;
            Log.Info($"umbra_custom_repository_removed url={url}");
            return UmbraPluginActionResult.Success("Custom repository removed. Installed plugins were left intact.");
        }
        catch (Exception ex)
        {
            Log.Error($"umbra_custom_repository_remove_failed url={url}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    internal async Task<UmbraPluginActionResult> InstallPluginAsync(UmbraStoreEntry entry)
    {
        UmbraPluginManifest? previousSnapshot = PluginManager.InstalledPlugins.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
        if (previousSnapshot?.IsDeveloperPlugin == true)
        {
            return UmbraPluginActionResult.Failure(
                "A developer plugin with this ID is loaded. Remove its local location before installing a repository package.");
        }

        string archivePath;
        try
        {
            archivePath = await UmbraPluginInstaller.DownloadVerifiedArchiveAsync(
                entry,
                Path.Combine(Options.CacheDirectory, "Packages"),
                shutdown.Token).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Log.Error($"umbra_plugin_download_failed id={entry.Id} version={entry.Version}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }

        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        UmbraPluginManifest? previous = PluginManager.InstalledPlugins.FirstOrDefault(candidate =>
            string.Equals(candidate.Id, entry.Id, StringComparison.OrdinalIgnoreCase));
        try
        {
            if (previous?.IsDeveloperPlugin == true)
            {
                return UmbraPluginActionResult.Failure(
                    "A developer plugin with this ID was loaded while the package downloaded. Remove its local location before installing.");
            }

            if (previous is not null)
                Plugins.Unload(previous.Id);

            UmbraPluginInstallResult install = UmbraPluginInstaller.InstallVerifiedArchive(
                entry,
                archivePath,
                Options.PluginDirectory,
                Path.Combine(Options.CacheDirectory, "PluginBackups"));
            UmbraPluginManifest manifest = UmbraPluginManifest.Load(install.ManifestPath);
            if (previous is null)
            {
                ReplaceInstalledCatalog(PluginManager.InstalledPlugins.Append(manifest));
            }
            else
            {
                ReplaceInstalledManifest(manifest);
            }

            if (manifest.Enabled && !Options.SafeMode)
            {
                UmbraPluginRuntimeStatus status = Plugins.Load(manifest);
                if (status.State != UmbraPluginRuntimeState.Running)
                {
                    if (previous is not null
                        && !string.IsNullOrWhiteSpace(install.BackupDirectory)
                        && Directory.Exists(install.BackupDirectory))
                    {
                        Plugins.Unload(manifest.Id);
                        Directory.Delete(install.InstallDirectory, recursive: true);
                        Directory.Move(install.BackupDirectory, install.InstallDirectory);
                        UmbraPluginManifest restored = UmbraPluginManifest.Load(previous.ManifestPath);
                        ReplaceInstalledManifest(restored);
                        Plugins.Load(restored);
                        return UmbraPluginActionResult.Failure(
                            $"Update loading failed and the previous version was restored: {status.LastError}");
                    }

                    return UmbraPluginActionResult.Failure($"Installed, but loading failed: {status.LastError}");
                }
            }

            Log.Info($"umbra_plugin_installed id={entry.Id} version={entry.Version} source={entry.Source}");
            return UmbraPluginActionResult.Success(
                previous is null ? "Plugin installed. Enable it from Installed when ready." : "Plugin updated successfully.");
        }
        catch (Exception ex)
        {
            if (previous is { Enabled: true } && !Options.SafeMode)
                Plugins.Load(previous);
            Log.Error($"umbra_plugin_install_failed id={entry.Id} version={entry.Version}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    public static async Task<UmbraRuntime> StartAsync(UmbraRuntimeOptions options, UmbraRuntimeLog log)
    {
        Directory.CreateDirectory(options.PluginDirectory);
        Directory.CreateDirectory(options.CacheDirectory);
        Directory.CreateDirectory(options.DevBridgeDirectory);

        log.Info("umbra_runtime_starting=true");
        log.Info($"umbra_cache_dir={options.CacheDirectory}");
        log.Info($"umbra_dev_bridge_dir={options.DevBridgeDirectory}");
        log.Info($"umbra_dev_bridge_control={options.DevBridgeControlPath}");
        log.Info($"umbra_dev_bridge_initial_enabled={options.DevBridgeInitiallyEnabled}");

        IReadOnlyList<UmbraPluginManifest> installedManifests =
            UmbraPluginDiscovery.Discover(options.PluginDirectory, log);
        UmbraDeveloperPluginSettings developerPlugins =
            UmbraDeveloperPluginSettingsStore.Load(options.CacheDirectory, log);
        IReadOnlyList<UmbraPluginManifest> developerManifests = developerPlugins.Enabled
            ? UmbraDeveloperPluginDiscovery.Discover(developerPlugins.Locations, log)
            : Array.Empty<UmbraPluginManifest>();
        IReadOnlyList<UmbraPluginManifest> manifests = MergePluginManifests(
            installedManifests,
            developerManifests,
            log);
        IReadOnlyList<UmbraRepositorySource> repositorySources = UmbraRepositoryRegistry.Load(
            options.CacheDirectory,
            options.RepositorySources,
            log);
        IReadOnlyList<UmbraStoreEntry> storeEntries = UmbraRepositoryFetcher.LoadCached(
            repositorySources,
            Path.Combine(options.CacheDirectory, "Repositories"),
            log);

        UmbraPluginCatalogState catalog = UmbraPluginCatalogState.Build(manifests, storeEntries);
        UmbraPluginManagerState pluginManager = new(
            true,
            UmbraPluginManagerTab.Installed,
            catalog,
            repositorySources,
            options.SafeMode,
            DebugLoggingEnabled: false,
            DevUiEnabled: false,
            PluginExecutionEnabled: !options.SafeMode)
        {
            DeveloperPlugins = developerPlugins
        };

        log.Info($"umbra_plugin_manifest_count={pluginManager.InstalledPlugins.Count}");
        log.Info($"umbra_plugin_enabled_count={pluginManager.InstalledPlugins.Count(plugin => plugin.Enabled)}");
        log.Info($"umbra_supported_plugin_count={pluginManager.SupportedPlugins.Count}");
        log.Info($"umbra_available_plugin_count={pluginManager.AvailablePlugins.Count}");
        log.Info($"umbra_plugin_update_count={pluginManager.Updates.Count}");
        log.Info(options.SafeMode
            ? "umbra_plugin_load_mode=system_plugins_only_safe_mode"
            : "umbra_plugin_load_mode=system_and_enabled_third_party_plugins");
        log.Info($"umbra_plugin_execution_enabled={!options.SafeMode}");

        UmbraDevBridgeService devBridge = new(options, log, new UmbraReadOnlyMemory(log));
        UmbraDevBridgeControl.Ensure(options.DevBridgeControlPath, options.DevBridgeInitiallyEnabled, options.DevBridgePort);

        UmbraRuntime runtime = new(options, log, pluginManager, devBridge, manifests);
        log.Info($"umbra_plugin_running_count={runtime.Plugins.Statuses.Count(status => status.State == UmbraPluginRuntimeState.Running)}");
        log.Info("umbra_runtime_started=true");
        return runtime;
    }

    internal async Task<UmbraPluginActionResult> SetDeveloperPluginsEnabledAsync(bool enabled)
    {
        if (enabled && Options.SafeMode)
            return UmbraPluginActionResult.Failure("Safe mode blocks developer plugin loading.");

        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        try
        {
            UmbraDeveloperPluginSettings settings = UmbraDeveloperPluginSettingsStore.Save(
                Options.CacheDirectory,
                PluginManager.DeveloperPlugins with { Enabled = enabled });
            return RescanDeveloperPlugins(settings, enabled
                ? "Developer plugin loading enabled."
                : "Developer plugin loading disabled.");
        }
        catch (Exception ex)
        {
            Log.Error($"umbra_developer_plugins_enabled_change_failed enabled={enabled}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    internal async Task<UmbraPluginActionResult> AddDeveloperPluginLocationAsync(string location)
    {
        UmbraPluginManifest candidate;
        try
        {
            candidate = UmbraDeveloperPluginDiscovery.LoadLocation(location);
        }
        catch (Exception ex)
        {
            return UmbraPluginActionResult.Failure(ex.Message);
        }

        if (PluginManager.InstalledPlugins.Any(manifest =>
            !manifest.IsDeveloperPlugin
            && string.Equals(manifest.Id, candidate.Id, StringComparison.OrdinalIgnoreCase)))
        {
            return UmbraPluginActionResult.Failure(
                $"An installed plugin already uses the ID {candidate.Id}.");
        }

        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        try
        {
            string normalized = Path.GetFullPath(location.Trim());
            if (PluginManager.DeveloperPlugins.Locations.Contains(
                normalized,
                StringComparer.OrdinalIgnoreCase))
            {
                return UmbraPluginActionResult.Failure("That developer plugin location is already configured.");
            }

            UmbraDeveloperPluginSettings settings = UmbraDeveloperPluginSettingsStore.Save(
                Options.CacheDirectory,
                PluginManager.DeveloperPlugins with
                {
                    Locations = PluginManager.DeveloperPlugins.Locations.Append(normalized).ToArray()
                });
            return RescanDeveloperPlugins(settings, $"Developer plugin location added: {candidate.Name}.");
        }
        catch (Exception ex)
        {
            Log.Error($"umbra_developer_plugin_location_add_failed location={location}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    internal async Task<UmbraPluginActionResult> RemoveDeveloperPluginLocationAsync(string location)
    {
        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        try
        {
            string normalized = Path.GetFullPath(location);
            string[] retained = PluginManager.DeveloperPlugins.Locations
                .Where(candidate => !string.Equals(
                    candidate,
                    normalized,
                    StringComparison.OrdinalIgnoreCase))
                .ToArray();
            if (retained.Length == PluginManager.DeveloperPlugins.Locations.Count)
                return UmbraPluginActionResult.Failure("Developer plugin location was not found.");

            UmbraDeveloperPluginSettings settings = UmbraDeveloperPluginSettingsStore.Save(
                Options.CacheDirectory,
                PluginManager.DeveloperPlugins with { Locations = retained });
            return RescanDeveloperPlugins(settings, "Developer plugin location removed. Local files were left intact.");
        }
        catch (Exception ex)
        {
            Log.Error($"umbra_developer_plugin_location_remove_failed location={location}", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    internal async Task<UmbraPluginActionResult> RescanDeveloperPluginsAsync()
    {
        await pluginMutationGate.WaitAsync(shutdown.Token).ConfigureAwait(false);
        try
        {
            return RescanDeveloperPlugins(
                PluginManager.DeveloperPlugins,
                "Developer plugin locations rescanned.");
        }
        catch (Exception ex)
        {
            Log.Error("umbra_developer_plugin_rescan_failed", ex);
            return UmbraPluginActionResult.Failure(ex.Message);
        }
        finally
        {
            pluginMutationGate.Release();
        }
    }

    public TService? GetService<TService>() where TService : class
    {
        if (DevBridge is TService devBridge)
            return devBridge;

        if (Log is TService log)
            return log;

        if (PluginManager is TService pluginManager)
            return pluginManager;

        if (Plugins is TService plugins)
            return plugins;

        if (Options is TService options)
            return options;

        if (ActorAppearance is TService actorAppearance)
            return actorAppearance;

        return null;
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        shutdown.Cancel();
        try
        {
            Task.WaitAll([updateLoop, repositoryRefreshTask], TimeSpan.FromSeconds(2));
        }
        catch
        {
            // Process teardown is already underway.
        }

        Plugins.Dispose();
        systemPlugins.Dispose();
        Commands.Dispose();
        DevBridge.Dispose();
        shutdown.Dispose();
        Log.Info("umbra_runtime_stopped=true");
    }

    private async Task RunInitialRepositoryRefreshAsync(CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(10), cancellationToken).ConfigureAwait(false);
            Log.Info("umbra_initial_repository_refresh_started=true");
            UmbraPluginActionResult result = await RefreshRepositoriesAsync().ConfigureAwait(false);
            Log.Info($"umbra_initial_repository_refresh_succeeded={result.Succeeded}");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            // Runtime shutdown occurred before the deferred refresh was due.
        }
    }

    private async Task RunUpdateLoopAsync(CancellationToken cancellationToken)
    {
        DateTimeOffset last = DateTimeOffset.UtcNow;
        using PeriodicTimer timer = new(TimeSpan.FromMilliseconds(500));
        while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false))
        {
            DateTimeOffset now = DateTimeOffset.UtcNow;
            TimeSpan delta = now - last;
            last = now;
            systemPlugins.Update(delta);
        }
    }

    private void ReplaceInstalledManifest(UmbraPluginManifest manifest)
    {
        IReadOnlyList<UmbraPluginManifest> installed = PluginManager.InstalledPlugins
            .Select(candidate => string.Equals(candidate.Id, manifest.Id, StringComparison.OrdinalIgnoreCase)
                ? manifest
                : candidate)
            .ToArray();
        ReplaceInstalledCatalog(installed);
    }

    private void RemoveInstalledManifest(string pluginId)
    {
        ReplaceInstalledCatalog(PluginManager.InstalledPlugins.Where(
            candidate => !string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase)));
    }

    private void ReplaceInstalledCatalog(IEnumerable<UmbraPluginManifest> installed)
    {
        IEnumerable<UmbraStoreEntry> storeEntries = PluginManager.Catalog.StoreEntries;
        UmbraPluginCatalogState catalog = UmbraPluginCatalogState.Build(installed, storeEntries);
        PluginManager = PluginManager with { Catalog = catalog };
        PluginManager.RuntimeHost = Plugins;
    }

    private UmbraPluginActionResult RescanDeveloperPlugins(
        UmbraDeveloperPluginSettings settings,
        string successMessage)
    {
        UmbraPluginManifest[] regular = PluginManager.InstalledPlugins
            .Where(manifest => !manifest.IsDeveloperPlugin)
            .ToArray();
        UmbraPluginManifest[] previousDeveloper = PluginManager.InstalledPlugins
            .Where(manifest => manifest.IsDeveloperPlugin)
            .ToArray();
        IReadOnlyList<UmbraPluginManifest> discovered = settings.Enabled
            ? UmbraDeveloperPluginDiscovery.Discover(settings.Locations, Log)
            : Array.Empty<UmbraPluginManifest>();
        IReadOnlyList<UmbraPluginManifest> merged = MergePluginManifests(regular, discovered, Log);

        foreach (UmbraPluginManifest manifest in previousDeveloper)
        {
            Plugins.Unload(manifest.Id);
            Plugins.Forget(manifest.Id);
        }

        UmbraPluginCatalogState catalog = UmbraPluginCatalogState.Build(
            merged,
            PluginManager.Catalog.StoreEntries);
        PluginManager = PluginManager with
        {
            Catalog = catalog,
            DeveloperPlugins = settings
        };
        PluginManager.RuntimeHost = Plugins;

        if (settings.Enabled && !Options.SafeMode)
            Plugins.LoadEnabled(merged.Where(manifest => manifest.IsDeveloperPlugin));

        int loaded = merged.Count(manifest => manifest.IsDeveloperPlugin);
        Log.Info(
            $"umbra_developer_plugins_rescanned enabled={settings.Enabled} discovered={loaded}");
        return UmbraPluginActionResult.Success($"{successMessage} {loaded} developer plugin(s) discovered.");
    }

    private static IReadOnlyList<UmbraPluginManifest> MergePluginManifests(
        IEnumerable<UmbraPluginManifest> installed,
        IEnumerable<UmbraPluginManifest> developer,
        UmbraRuntimeLog log)
    {
        List<UmbraPluginManifest> merged = installed.ToList();
        HashSet<string> ids = merged
            .Select(manifest => manifest.Id)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        foreach (UmbraPluginManifest manifest in developer)
        {
            if (!ids.Add(manifest.Id))
            {
                log.Warning(
                    $"umbra_developer_plugin_id_conflict id={manifest.Id} location={manifest.DeveloperLocation}");
                continue;
            }

            merged.Add(manifest);
        }

        return merged;
    }

    private void ReplaceStoreCatalog(IEnumerable<UmbraStoreEntry> storeEntries)
    {
        UmbraPluginCatalogState catalog = UmbraPluginCatalogState.Build(
            PluginManager.InstalledPlugins,
            storeEntries);
        PluginManager = PluginManager with { Catalog = catalog };
        PluginManager.RuntimeHost = Plugins;
    }

    private static string SanitizePluginId(string value)
    {
        char[] chars = value.Select(character =>
            char.IsLetterOrDigit(character) || character is '.' or '-' or '_' ? character : '_').ToArray();
        return new string(chars);
    }
}

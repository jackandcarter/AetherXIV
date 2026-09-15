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

using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace AetherXIV.Launcher.Core;

/// <summary>
/// Installs and toggles AetherXIV-supported Umbra plugins from the launcher,
/// independently of the in-game plugin manager. The install layout and manifest
/// shape mirror what the in-game framework produces, so a plugin installed here
/// appears in the game's Installed list and can be enabled or disabled from
/// either side. The plugin manifest's <c>enabled</c> field is the single source
/// of truth for the toggle.
/// </summary>
public sealed class UmbraPluginInstallService
{
    public const string DiscordRichPresencePluginId = "dev.aetherxiv.umbra.discord-rich-presence";

    private const string ManifestFileName = "umbra-plugin.json";
    private const long MaximumPackageBytes = 512L * 1024 * 1024;
    private const long MaximumExpandedBytes = 1024L * 1024 * 1024;
    private const int MaximumArchiveEntries = 4096;

    private readonly HttpClient httpClient;
    private readonly string serviceUrl;
    private readonly string bundledPluginsRoot;

    /// <summary>
    /// Where the last successful <see cref="EnsureInstalledAsync"/> resolved the
    /// plugin package from: "remote", "bundled", or null when nothing was
    /// installed. Lets callers report the source without coupling to the
    /// repository service being online.
    /// </summary>
    public string? LastInstallSource { get; private set; }

    public UmbraPluginInstallService(
        HttpClient httpClient,
        string? serviceUrl = null,
        string? bundledPluginsRoot = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        serviceUrl = string.IsNullOrWhiteSpace(serviceUrl)
            ? LauncherProfile.DemiDevUnitUmbraServiceUrl
            : serviceUrl.Trim();
        if (!serviceUrl.EndsWith('/'))
            serviceUrl += "/";
        this.serviceUrl = serviceUrl;
        this.bundledPluginsRoot = string.IsNullOrWhiteSpace(bundledPluginsRoot)
            ? UmbraInstallStore.BundledPluginsRoot
            : Path.GetFullPath(bundledPluginsRoot);
    }

    public string OfficialRepositoryUrl => $"{serviceUrl}repository.json";

    public string BundledRepositoryPath => Path.Combine(bundledPluginsRoot, "repository.json");

    public static string PluginDirectoryFor(string pluginsRoot, string pluginId)
    {
        return Path.Combine(pluginsRoot, SanitizePathSegment(pluginId));
    }

    public static string ManifestPathFor(string pluginsRoot, string pluginId)
    {
        return Path.Combine(PluginDirectoryFor(pluginsRoot, pluginId), ManifestFileName);
    }

    public bool IsInstalled(string pluginsRoot, string pluginId)
    {
        string manifestPath = ManifestPathFor(pluginsRoot, pluginId);
        if (!File.Exists(manifestPath))
            return false;

        try
        {
            UmbraPluginManifest manifest = UmbraPluginManifest.Load(manifestPath);
            string entryPath = Path.GetFullPath(Path.Combine(
                Path.GetDirectoryName(manifestPath)!,
                manifest.Entry));
            string installRoot = Path.GetFullPath(PluginDirectoryFor(pluginsRoot, pluginId));
            return entryPath.StartsWith(installRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
                && File.Exists(entryPath);
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Reads the plugin manifest's <c>enabled</c> flag. Returns null when the
    /// plugin is not installed or its manifest cannot be read.
    /// </summary>
    public bool? ReadEnabled(string pluginsRoot, string pluginId)
    {
        string manifestPath = ManifestPathFor(pluginsRoot, pluginId);
        if (!File.Exists(manifestPath))
            return null;

        try
        {
            using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
            return document.RootElement.TryGetProperty("enabled", out JsonElement enabled)
                && enabled.ValueKind == JsonValueKind.True;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Updates the <c>enabled</c> flag in the installed manifest, preserving
    /// every other field (including framework bookkeeping written in-game). No-op
    /// when the plugin is not installed.
    /// </summary>
    public void SetEnabled(string pluginsRoot, string pluginId, bool enabled)
    {
        string manifestPath = ManifestPathFor(pluginsRoot, pluginId);
        if (!File.Exists(manifestPath))
            return;

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(manifestPath));
        }
        catch
        {
            return;
        }

        if (root is not JsonObject obj)
            return;

        bool changed;
        if (obj["enabled"] is null)
        {
            obj["enabled"] = enabled;
            changed = true;
        }
        else
        {
            bool current = obj["enabled"]?.GetValue<bool>() ?? false;
            changed = current != enabled;
            if (changed)
                obj["enabled"] = enabled;
        }

        if (!changed)
            return;

        WriteAtomic(manifestPath, root.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    /// <summary>
    /// Ensures the plugin is installed and current, preferring the signed
    /// official repository and falling back to the bundled catalog that ships
    /// with the launcher when the service is unreachable or does not list the
    /// plugin yet. Returns true when the plugin is installed at (or newer than)
    /// the best available version. Does not change the enabled state.
    /// </summary>
    public async Task<bool> EnsureInstalledAsync(
        string pluginsRoot,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            UmbraOfficialUpdateClient updateClient = new(httpClient, serviceUrl);
            UmbraOfficialRepositoryManifest repository =
                await updateClient.GetOfficialRepositoryAsync(cancellationToken).ConfigureAwait(false);
            bool remoteInstalled = await EnsureInstalledFromRepositoryAsync(
                repository,
                pluginsRoot,
                pluginId,
                cancellationToken).ConfigureAwait(false);
            if (remoteInstalled)
            {
                LastInstallSource = "remote";
                return true;
            }

            // The service is reachable but does not list the plugin yet.
            return await EnsureInstalledFromBundledAsync(
                pluginsRoot,
                pluginId,
                cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex) when (ex is HttpRequestException
            or TaskCanceledException
            or IOException
            or System.Security.Cryptography.CryptographicException
            or System.Text.Json.JsonException)
        {
            // The official service is not online, unreachable, or not trusted;
            // the bundled foundation catalog is the offline source. Absence is
            // not an error.
            return await EnsureInstalledFromBundledAsync(
                pluginsRoot,
                pluginId,
                cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Ensures the plugin is installed and current from the bundled foundation
    /// catalog that ships inside the launcher payload. Returns false when the
    /// bundled catalog does not list the plugin or its package is missing.
    /// </summary>
    public async Task<bool> EnsureInstalledFromBundledAsync(
        string pluginsRoot,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        if (!File.Exists(BundledRepositoryPath))
            return false;

        UmbraOfficialRepositoryManifest repository;
        try
        {
            repository = JsonSerializer.Deserialize<UmbraOfficialRepositoryManifest>(
                await File.ReadAllTextAsync(BundledRepositoryPath, cancellationToken).ConfigureAwait(false))
                ?? throw new InvalidDataException("Bundled Umbra repository is empty.");
        }
        catch
        {
            return false;
        }

        UmbraPluginCatalogEntry? entry = repository.Plugins
            .Where(candidate => candidate.IsActive
                && string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => ParseVersion(candidate.Version))
            .ThenByDescending(candidate => candidate.Version, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (entry is null)
            return false;

        string installDirectory = PluginDirectoryFor(pluginsRoot, pluginId);
        if (IsCurrent(installDirectory, entry))
        {
            LastInstallSource = "bundled";
            return true;
        }

        string archivePath = await CopyBundledArchiveAsync(entry, cancellationToken).ConfigureAwait(false);
        InstallVerifiedArchive(entry, archivePath, pluginsRoot, BundledRepositoryPath);
        LastInstallSource = "bundled";
        return true;
    }

    private async Task<string> CopyBundledArchiveAsync(
        UmbraPluginCatalogEntry entry,
        CancellationToken cancellationToken)
    {
        if (entry.SizeBytes > MaximumPackageBytes)
            throw new InvalidDataException(
                $"Umbra plugin package exceeds the {MaximumPackageBytes} byte limit.");

        string bundledRoot = Path.GetFullPath(bundledPluginsRoot);
        string sourcePath = Path.GetFullPath(Path.Combine(bundledRoot, entry.DownloadUrl));
        if (!sourcePath.StartsWith(bundledRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Umbra bundled plugin path escapes the bundled directory: {entry.DownloadUrl}");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Umbra bundled plugin package was not found.", sourcePath);

        string cacheRoot = Path.Combine(UmbraInstallStore.Root, "PluginCache");
        Directory.CreateDirectory(cacheRoot);
        string archivePath = Path.Combine(cacheRoot, $"{SanitizePathSegment(entry.Id)}-{entry.Version}.zip");
        string temporaryPath = archivePath + $".{Guid.NewGuid():N}.download";
        try
        {
            await using FileStream source = File.OpenRead(sourcePath);
            await using (FileStream destination = File.Create(temporaryPath))
                await CopyExactAsync(source, destination, entry.SizeBytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, archivePath, overwrite: true);
            ValidateArchive(entry, archivePath);
            return archivePath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    /// <summary>
    /// Ensures the plugin is installed and current from an already-fetched
    /// official repository manifest. Does not change the enabled state.
    /// </summary>
    public async Task<bool> EnsureInstalledFromRepositoryAsync(
        UmbraOfficialRepositoryManifest repository,
        string pluginsRoot,
        string pluginId,
        CancellationToken cancellationToken = default)
    {
        UmbraPluginCatalogEntry? entry = repository.Plugins
            .Where(candidate => candidate.IsActive
                && string.Equals(candidate.Id, pluginId, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(candidate => ParseVersion(candidate.Version))
            .ThenByDescending(candidate => candidate.Version, StringComparer.OrdinalIgnoreCase)
            .FirstOrDefault();
        if (entry is null)
            return false;

        string installDirectory = PluginDirectoryFor(pluginsRoot, pluginId);
        if (IsCurrent(installDirectory, entry))
        {
            LastInstallSource = "remote";
            return true;
        }

        string cacheRoot = Path.Combine(UmbraInstallStore.Root, "PluginCache");
        Directory.CreateDirectory(cacheRoot);
        string archivePath = await DownloadVerifiedArchiveAsync(entry, cacheRoot, cancellationToken)
            .ConfigureAwait(false);
        InstallVerifiedArchive(entry, archivePath, pluginsRoot, OfficialRepositoryUrl);
        LastInstallSource = "remote";
        return true;
    }

    private bool IsCurrent(string installDirectory, UmbraPluginCatalogEntry entry)
    {
        string manifestPath = Path.Combine(installDirectory, ManifestFileName);
        if (!File.Exists(manifestPath))
            return false;

        try
        {
            UmbraPluginManifest installed = UmbraPluginManifest.Load(manifestPath);
            return string.Equals(installed.Id, entry.Id, StringComparison.OrdinalIgnoreCase)
                && ParseVersion(installed.Version) >= ParseVersion(entry.Version)
                && File.Exists(Path.Combine(installDirectory, installed.Entry));
        }
        catch
        {
            return false;
        }
    }

    private async Task<string> DownloadVerifiedArchiveAsync(
        UmbraPluginCatalogEntry entry,
        string cacheRoot,
        CancellationToken cancellationToken)
    {
        if (entry.SizeBytes > MaximumPackageBytes)
            throw new InvalidDataException(
                $"Umbra plugin package exceeds the {MaximumPackageBytes} byte limit.");
        if (!Uri.TryCreate(entry.DownloadUrl, UriKind.Absolute, out Uri? downloadUri)
            || !UmbraRepositoryOptions.IsAllowedRepositoryUri(downloadUri))
        {
            throw new InvalidDataException($"Umbra plugin download URL is not allowed: {entry.DownloadUrl}");
        }

        string archivePath = Path.Combine(cacheRoot, $"{SanitizePathSegment(entry.Id)}-{entry.Version}.zip");
        string temporaryPath = archivePath + $".{Guid.NewGuid():N}.download";
        try
        {
            using HttpResponseMessage response = await httpClient.GetAsync(
                downloadUri,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken).ConfigureAwait(false);
            response.EnsureSuccessStatusCode();

            if (response.Content.Headers.ContentLength is long contentLength
                && contentLength != entry.SizeBytes)
            {
                throw new InvalidDataException(
                    $"Umbra plugin archive size mismatch: expected {entry.SizeBytes}, remote {contentLength}.");
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken)
                .ConfigureAwait(false);
            await using (FileStream destination = File.Create(temporaryPath))
                await CopyExactAsync(source, destination, entry.SizeBytes, cancellationToken).ConfigureAwait(false);
            File.Move(temporaryPath, archivePath, overwrite: true);
            ValidateArchive(entry, archivePath);
            return archivePath;
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void InstallVerifiedArchive(
        UmbraPluginCatalogEntry entry,
        string archivePath,
        string pluginsRoot,
        string installedFromUrl)
    {
        ValidateArchive(entry, archivePath);

        string pluginRoot = Path.GetFullPath(pluginsRoot);
        string installDirectory = Path.GetFullPath(Path.Combine(pluginRoot, SanitizePathSegment(entry.Id)));
        if (!installDirectory.StartsWith(pluginRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Umbra plugin install path escapes the plugin directory.");

        Directory.CreateDirectory(pluginRoot);
        string stagingDirectory = Path.Combine(
            pluginRoot,
            $".umbra-stage-{SanitizePathSegment(entry.Id)}-{Guid.NewGuid():N}");
        string rollbackDirectory = Path.Combine(
            pluginRoot,
            $".umbra-rollback-{SanitizePathSegment(entry.Id)}-{Guid.NewGuid():N}");
        bool existingMoved = false;
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory);
            ValidateExtractedPaths(stagingDirectory);

            string stagedManifestPath = Path.Combine(stagingDirectory, ManifestFileName);
            if (!File.Exists(stagedManifestPath))
                throw new InvalidDataException(
                    "Umbra plugin package must contain umbra-plugin.json at its root.");
            UmbraPluginManifest stagedManifest = UmbraPluginManifest.Load(stagedManifestPath);
            ValidateManifestMatchesEntry(entry, stagedManifest);

            bool enabled = false;
            if (Directory.Exists(installDirectory))
            {
                string? previousManifestPath = Path.Combine(installDirectory, ManifestFileName);
                if (File.Exists(previousManifestPath))
                {
                    try
                    {
                        enabled = ReadManifestEnabled(previousManifestPath);
                    }
                    catch
                    {
                        // A corrupt previous manifest is replaced by this install.
                    }
                }

                Directory.Move(installDirectory, rollbackDirectory);
                existingMoved = true;
            }

            Directory.Move(stagingDirectory, installDirectory);
            string manifestPath = Path.Combine(installDirectory, ManifestFileName);
            SetManifestMetadata(manifestPath, entry, enabled, installedFromUrl);
        }
        catch
        {
            if (Directory.Exists(stagingDirectory))
                Directory.Delete(stagingDirectory, recursive: true);
            if (existingMoved && Directory.Exists(rollbackDirectory))
            {
                if (Directory.Exists(installDirectory))
                    Directory.Delete(installDirectory, recursive: true);
                Directory.Move(rollbackDirectory, installDirectory);
            }

            throw;
        }
        finally
        {
            if (Directory.Exists(rollbackDirectory))
                Directory.Delete(rollbackDirectory, recursive: true);
        }
    }

    private static void ValidateManifestMatchesEntry(
        UmbraPluginCatalogEntry entry,
        UmbraPluginManifest manifest)
    {
        RequireMatch(entry.Id, manifest.Id, "id");
        RequireMatch(entry.Name, manifest.Name, "name");
        RequireMatch(entry.Version, manifest.Version, "version");
        RequireMatch(entry.ApiVersion, manifest.ApiVersion, "api_version");
        RequireMatch(entry.MinimumFrameworkVersion, manifest.MinimumFrameworkVersion, "minimum_framework_version");
    }

    private static void SetManifestMetadata(
        string manifestPath,
        UmbraPluginCatalogEntry entry,
        bool enabled,
        string installedFromUrl)
    {
        JsonNode? root = JsonNode.Parse(File.ReadAllText(manifestPath));
        if (root is not JsonObject obj)
            throw new InvalidDataException("Umbra plugin manifest could not be updated after install.");

        // The framework resolves repository sources from the URLs the launcher
        // seeds; the bundled repository is the offline source and the official
        // repository is the update source. Record which one installed this copy
        // so in-game update matching stays consistent.
        obj["installed_from_url"] = installedFromUrl;
        obj["installed_from_source"] = "supported";
        obj["enabled"] = enabled;
        WriteAtomic(manifestPath, obj.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
    }

    private static void ValidateArchive(UmbraPluginCatalogEntry entry, string archivePath)
    {
        FileInfo info = new(archivePath);
        if (!info.Exists)
            throw new FileNotFoundException("Umbra plugin archive was not found.", archivePath);
        if (info.Length != entry.SizeBytes)
            throw new InvalidDataException(
                $"Umbra plugin archive size mismatch: expected {entry.SizeBytes}, actual {info.Length}.");

        using FileStream stream = File.OpenRead(archivePath);
        string sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Umbra plugin archive SHA256 mismatch.");

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaximumArchiveEntries)
            throw new InvalidDataException(
                $"Umbra plugin archive contains more than {MaximumArchiveEntries} entries.");

        long expandedBytes = 0;
        foreach (ZipArchiveEntry zipEntry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(zipEntry.FullName))
                continue;

            if (Path.IsPathRooted(zipEntry.FullName)
                || zipEntry.FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
            {
                throw new InvalidDataException(
                    $"Umbra plugin archive path escapes install root: {zipEntry.FullName}");
            }

            expandedBytes = checked(expandedBytes + zipEntry.Length);
            if (expandedBytes > MaximumExpandedBytes)
                throw new InvalidDataException("Umbra plugin archive expands beyond the allowed size.");
        }
    }

    private static void ValidateExtractedPaths(string installDirectory)
    {
        string root = Path.GetFullPath(installDirectory);
        foreach (string path in Directory.EnumerateFileSystemEntries(root, "*", SearchOption.AllDirectories))
        {
            string fullPath = Path.GetFullPath(path);
            if (!fullPath.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Umbra plugin extracted path escapes install root: {path}");
        }
    }

    private static async Task CopyExactAsync(
        Stream source,
        Stream destination,
        long expectedBytes,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[64 * 1024];
        long total = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            total += read;
            if (total > expectedBytes)
                throw new InvalidDataException("Umbra plugin download exceeded its declared size.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        if (total != expectedBytes)
            throw new InvalidDataException(
                $"Umbra plugin archive size mismatch: expected {expectedBytes}, actual {total}.");
    }

    private static bool ReadManifestEnabled(string manifestPath)
    {
        using JsonDocument document = JsonDocument.Parse(File.ReadAllText(manifestPath));
        return document.RootElement.TryGetProperty("enabled", out JsonElement enabled)
            && enabled.ValueKind == JsonValueKind.True;
    }

    private static void WriteAtomic(string path, string contents)
    {
        string temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, contents);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static void RequireMatch(string expected, string actual, string field)
    {
        if (!string.Equals(expected, actual, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidDataException(
                $"Umbra package manifest {field} does not match repository metadata.");
        }
    }

    private static Version ParseVersion(string value) =>
        Version.TryParse(value, out Version? version) ? version : new Version(0, 0);

    private static string SanitizePathSegment(string value)
    {
        string sanitized = new(value
            .Select(character => char.IsLetterOrDigit(character) || character is '.' or '-' or '_'
                ? character
                : '_')
            .ToArray());
        return string.IsNullOrWhiteSpace(sanitized) ? "plugin" : sanitized;
    }
}

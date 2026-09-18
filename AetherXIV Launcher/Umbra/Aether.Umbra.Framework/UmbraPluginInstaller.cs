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

namespace Aether.Umbra.Framework;

public sealed record UmbraPluginInstallResult(
    string PluginId,
    string InstallDirectory,
    string ManifestPath,
    string? BackupDirectory);

public static class UmbraPluginInstaller
{
    private const long MaximumPackageBytes = 512L * 1024 * 1024;
    private const long MaximumExpandedBytes = 1024L * 1024 * 1024;
    private const int MaximumArchiveEntries = 4096;

    public static async Task<UmbraPluginInstallResult> DownloadAndInstallAsync(
        UmbraStoreEntry entry,
        string pluginDirectory,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        string archivePath = await DownloadVerifiedArchiveAsync(
            entry,
            cacheDirectory,
            cancellationToken).ConfigureAwait(false);
        return InstallVerifiedArchive(
            entry,
            archivePath,
            pluginDirectory,
            Path.Combine(Path.GetDirectoryName(Path.GetFullPath(cacheDirectory))!, "PluginBackups"));
    }

    internal static async Task<string> DownloadVerifiedArchiveAsync(
        UmbraStoreEntry entry,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        entry.ValidateInstallable();
        UmbraPluginCompatibility.Validate(entry.ToManifest());

        Directory.CreateDirectory(cacheDirectory);

        if (entry.SizeBytes > MaximumPackageBytes)
            throw new InvalidDataException($"Umbra plugin package exceeds the {MaximumPackageBytes} byte limit.");

        string archivePath = Path.Combine(cacheDirectory, $"{SanitizePathSegment(entry.Id)}-{entry.Version}.zip");
        if (entry.BuiltIn)
        {
            CopyBundledArchive(entry, archivePath);
            return archivePath;
        }

        string temporaryArchivePath = archivePath + $".{Guid.NewGuid():N}.download";
        try
        {
            if (Uri.TryCreate(entry.DownloadUrl, UriKind.Absolute, out Uri? localPackage) && localPackage.IsFile)
            {
                await using (FileStream localSource = File.OpenRead(localPackage.LocalPath))
                await using (FileStream destination = File.Create(temporaryArchivePath))
                    await CopyExactAsync(localSource, destination, entry.SizeBytes, cancellationToken);
                ValidateArchive(entry, temporaryArchivePath);
                File.Move(temporaryArchivePath, archivePath, overwrite: true);
                return archivePath;
            }
            using HttpClient client = new() { Timeout = TimeSpan.FromMinutes(2) };
            using HttpResponseMessage response = await client.GetAsync(
                entry.DownloadUrl,
                HttpCompletionOption.ResponseHeadersRead,
                cancellationToken);
            response.EnsureSuccessStatusCode();
            if (response.Content.Headers.ContentLength is long contentLength
                && contentLength != entry.SizeBytes)
            {
                throw new InvalidDataException(
                    $"Umbra plugin archive size mismatch: expected {entry.SizeBytes}, remote {contentLength}.");
            }

            await using Stream source = await response.Content.ReadAsStreamAsync(cancellationToken);
            await using (FileStream destination = File.Create(temporaryArchivePath))
                await CopyExactAsync(source, destination, entry.SizeBytes, cancellationToken);
            ValidateArchive(entry, temporaryArchivePath);
            File.Move(temporaryArchivePath, archivePath, overwrite: true);
            return archivePath;
        }
        finally
        {
            if (File.Exists(temporaryArchivePath))
                File.Delete(temporaryArchivePath);
        }
    }

    /// <summary>
    /// Copies a built-in plugin package from the bundled repository directory
    /// (resolved from the entry's repository URL) into the package cache, then
    /// verifies its size and SHA256. The package is the same zip the official
    /// update service will eventually serve, so verification is identical.
    /// </summary>
    private static void CopyBundledArchive(UmbraStoreEntry entry, string archivePath)
    {
        string repositoryDirectory = Path.GetDirectoryName(Path.GetFullPath(entry.RepositoryUrl))
            ?? throw new InvalidDataException($"Umbra built-in entry has no repository directory: {entry.Id}");
        string repositoryRoot = Path.GetFullPath(repositoryDirectory);
        string sourcePath = Path.GetFullPath(Path.Combine(repositoryRoot, entry.DownloadUrl));
        if (!sourcePath.StartsWith(repositoryRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Umbra built-in package path escapes the repository: {entry.DownloadUrl}");
        if (!File.Exists(sourcePath))
            throw new FileNotFoundException("Umbra built-in plugin package was not found.", sourcePath);

        File.Copy(sourcePath, archivePath, overwrite: true);
        ValidateArchive(entry, archivePath);
    }

    public static UmbraPluginInstallResult InstallVerifiedArchive(
        UmbraStoreEntry entry,
        string archivePath,
        string pluginDirectory,
        string? backupDirectory = null)
    {
        entry.ValidateInstallable();
        UmbraPluginCompatibility.Validate(entry.ToManifest());
        ValidateArchive(entry, archivePath);

        string installDirectory = Path.GetFullPath(Path.Combine(pluginDirectory, SanitizePathSegment(entry.Id)));
        string pluginRoot = Path.GetFullPath(pluginDirectory);
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
        string? archivedBackupDirectory = null;
        try
        {
            Directory.CreateDirectory(stagingDirectory);
            ZipFile.ExtractToDirectory(archivePath, stagingDirectory);
            ValidateExtractedPaths(stagingDirectory);

            string stagedManifestPath = FindManifest(stagingDirectory)
                ?? throw new InvalidDataException("Umbra plugin package must contain umbra-plugin.json or plugin.json at its root.");
            UmbraPluginManifest stagedManifest = UmbraPluginManifest.Load(stagedManifestPath);
            ValidateManifestMatchesEntry(entry, stagedManifest, stagingDirectory);
            UmbraManagedPluginValidator.ValidatePackage(stagingDirectory, stagedManifest);
            stagedManifest = stagedManifest with
            {
                InstalledFromUrl = entry.RepositoryUrl,
                InstalledFromSource = entry.Source
            };
            stagedManifest.Save();

            bool enabled = false;
            if (Directory.Exists(installDirectory))
            {
                string? currentManifestPath = FindManifest(installDirectory);
                if (currentManifestPath is not null)
                    enabled = UmbraPluginManifest.Load(currentManifestPath).Enabled;
                Directory.Move(installDirectory, rollbackDirectory);
                existingMoved = true;
            }

            Directory.Move(stagingDirectory, installDirectory);
            string manifestPath = FindManifest(installDirectory)
                ?? throw new InvalidDataException("Installed Umbra plugin manifest disappeared during activation.");
            UmbraPluginManifest installedManifest = UmbraPluginManifest.Load(manifestPath);
            if (installedManifest.Enabled != enabled)
            {
                installedManifest = installedManifest with { Enabled = enabled };
                installedManifest.Save();
            }

            if (existingMoved)
            {
                try
                {
                    archivedBackupDirectory = ArchiveRollback(rollbackDirectory, backupDirectory, entry);
                }
                catch
                {
                    // Activation succeeded. Keep the rollback directory in place if archival fails.
                    archivedBackupDirectory = rollbackDirectory;
                }
            }

            return new UmbraPluginInstallResult(
                entry.Id,
                installDirectory,
                manifestPath,
                archivedBackupDirectory);
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
    }

    private static void ValidateArchive(UmbraStoreEntry entry, string archivePath)
    {
        FileInfo info = new(archivePath);
        if (!info.Exists)
            throw new FileNotFoundException("Umbra plugin archive was not found.", archivePath);
        if (info.Length != entry.SizeBytes)
            throw new InvalidDataException($"Umbra plugin archive size mismatch: expected {entry.SizeBytes}, actual {info.Length}.");

        using FileStream stream = File.OpenRead(archivePath);
        string sha256 = Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        if (!string.Equals(sha256, entry.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Umbra plugin archive SHA256 mismatch.");

        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        if (archive.Entries.Count > MaximumArchiveEntries)
            throw new InvalidDataException($"Umbra plugin archive contains more than {MaximumArchiveEntries} entries.");

        long expandedBytes = 0;
        foreach (ZipArchiveEntry zipEntry in archive.Entries)
        {
            if (string.IsNullOrWhiteSpace(zipEntry.FullName))
                continue;

            if (Path.IsPathRooted(zipEntry.FullName)
                || zipEntry.FullName.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
            {
                throw new InvalidDataException($"Umbra plugin archive path escapes install root: {zipEntry.FullName}");
            }

            expandedBytes = checked(expandedBytes + zipEntry.Length);
            if (expandedBytes > MaximumExpandedBytes)
                throw new InvalidDataException("Umbra plugin archive expands beyond the allowed size.");
        }
    }

    private static void ValidateManifestMatchesEntry(
        UmbraStoreEntry entry,
        UmbraPluginManifest manifest,
        string stagingDirectory)
    {
        RequireMatch(entry.Id, manifest.Id, "id");
        RequireMatch(entry.Name, manifest.Name, "name");
        RequireMatch(entry.Version, manifest.Version, "version");
        RequireMatch(entry.ApiVersion, manifest.ApiVersion, "api_version");
        RequireMatch(entry.MinimumFrameworkVersion, manifest.MinimumFrameworkVersion, "minimum_framework_version");
        if (!string.IsNullOrWhiteSpace(entry.Entry))
            RequireMatch(entry.Entry, manifest.Entry, "entry");

        string assemblyPath = Path.GetFullPath(Path.Combine(stagingDirectory, manifest.Entry));
        string stagingRoot = Path.GetFullPath(stagingDirectory);
        if (!assemblyPath.StartsWith(stagingRoot + Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)
            || !File.Exists(assemblyPath))
        {
            throw new InvalidDataException($"Umbra plugin entry assembly was not found: {manifest.Entry}");
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

    private static string? ArchiveRollback(
        string rollbackDirectory,
        string? backupDirectory,
        UmbraStoreEntry entry)
    {
        if (string.IsNullOrWhiteSpace(backupDirectory))
        {
            Directory.Delete(rollbackDirectory, recursive: true);
            return null;
        }

        Directory.CreateDirectory(backupDirectory);
        string destination = Path.Combine(
            backupDirectory,
            $"{SanitizePathSegment(entry.Id)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");
        Directory.Move(rollbackDirectory, destination);
        return destination;
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
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;
            total += read;
            if (total > expectedBytes)
                throw new InvalidDataException("Umbra plugin download exceeded its declared size.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }

        if (total != expectedBytes)
            throw new InvalidDataException($"Umbra plugin archive size mismatch: expected {expectedBytes}, actual {total}.");
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

    private static string? FindManifest(string installDirectory)
    {
        foreach (string fileName in new[] { "umbra-plugin.json", "plugin.json" })
        {
            string candidate = Path.Combine(installDirectory, fileName);
            if (File.Exists(candidate))
                return candidate;
        }

        return null;
    }

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

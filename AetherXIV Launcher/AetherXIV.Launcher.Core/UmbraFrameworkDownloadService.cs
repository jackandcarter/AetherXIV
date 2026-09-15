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

namespace AetherXIV.Launcher.Core;

public sealed record UmbraFrameworkDownloadProgress(
    string Message,
    long BytesDownloaded,
    long TotalBytes,
    bool LogMessage);

public sealed record UmbraFrameworkDownloadResult(UmbraFrameworkInstall Install, IReadOnlyList<string> Messages);

public static class UmbraFrameworkDownloadService
{
    public static async Task<UmbraFrameworkDownloadResult> DownloadAndInstallAsync(
        UmbraFrameworkArtifact artifact,
        HttpClient httpClient,
        IProgress<UmbraFrameworkDownloadProgress>? progress = null,
        CancellationToken cancellationToken = default,
        string? frameworksRoot = null,
        string? cacheRoot = null,
        long channelSequence = 0,
        string signingKeyId = "")
    {
        ArgumentNullException.ThrowIfNull(artifact);
        ArgumentNullException.ThrowIfNull(httpClient);

        artifact.ValidateOfficial();

        string frameworkRoot = Path.GetFullPath(frameworksRoot ?? UmbraInstallStore.FrameworksRoot);
        string downloadRoot = Path.GetFullPath(cacheRoot ?? UmbraInstallStore.FrameworkCacheRoot);
        Directory.CreateDirectory(frameworkRoot);
        Directory.CreateDirectory(downloadRoot);

        string archivePath = Path.Combine(downloadRoot, $"{artifact.StableId}.zip");
        await DownloadArchiveAsync(artifact, httpClient, archivePath, progress, cancellationToken);
        ValidateArchive(artifact, archivePath);

        string installRoot = UmbraInstallStore.FrameworkInstallRootFor(artifact, frameworkRoot);
        string stagingRoot = $"{installRoot}.staging-{Guid.NewGuid():N}";
        UmbraFrameworkInstall install;
        try
        {
            Directory.CreateDirectory(stagingRoot);
            ExtractZip(archivePath, stagingRoot);
            ValidateExtractedFiles(artifact, stagingRoot);

            string stagedBootstrapPath = ResolveInstalledPath(stagingRoot, artifact.BootstrapRelativePath);
            string stagedFrameworkPath = ResolveInstalledPath(stagingRoot, artifact.FrameworkRelativePath);
            if (!File.Exists(stagedBootstrapPath))
                throw new FileNotFoundException(
                    "Umbra bootstrap DLL was not found after extraction.",
                    stagedBootstrapPath);
            if (!File.Exists(stagedFrameworkPath))
                throw new FileNotFoundException(
                    "Umbra managed framework entrypoint was not found after extraction.",
                    stagedFrameworkPath);

            if (Directory.Exists(installRoot))
            {
                UmbraFrameworkInstall? existing = UmbraInstallStore.TryLoadVerified(installRoot);
                if (existing is not null
                    && string.Equals(existing.ArchiveSha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
                {
                    UmbraFrameworkInstall refreshed = existing with
                    {
                        ChannelSequence = Math.Max(existing.ChannelSequence, channelSequence),
                        SigningKeyId = channelSequence >= existing.ChannelSequence
                            && !string.IsNullOrWhiteSpace(signingKeyId)
                                ? signingKeyId
                                : existing.SigningKeyId
                    };
                    if (refreshed != existing)
                    {
                        UmbraInstallStore.Save(refreshed);
                        refreshed.ValidateIntegrity();
                    }

                    Directory.Delete(stagingRoot, true);
                    return new UmbraFrameworkDownloadResult(refreshed, new[]
                    {
                        $"Umbra framework already installed: {refreshed.Name} {refreshed.Version}"
                    });
                }

                string quarantine = UmbraInstallStore.CreateFrameworkQuarantinePath(installRoot);
                Directory.Move(installRoot, quarantine);
            }

            Directory.Move(stagingRoot, installRoot);
            string bootstrapPath = ResolveInstalledPath(installRoot, artifact.BootstrapRelativePath);
            string frameworkPath = ResolveInstalledPath(installRoot, artifact.FrameworkRelativePath);
            install = new UmbraFrameworkInstall(
                artifact.Name,
                artifact.Version,
                artifact.ApiVersion,
                artifact.PlatformRid,
                installRoot,
                bootstrapPath,
                frameworkPath,
                artifact.SupportedGameSha256,
                DateTimeOffset.UtcNow)
            {
                ArchiveSha256 = artifact.Sha256,
                Files = artifact.VerifiedFiles,
                ChannelSequence = channelSequence,
                SigningKeyId = signingKeyId
            };
            UmbraInstallStore.Save(install);
            install.ValidateIntegrity();
        }
        catch
        {
            if (Directory.Exists(stagingRoot))
                Directory.Delete(stagingRoot, true);
            throw;
        }

        progress?.Report(new UmbraFrameworkDownloadProgress(
            $"Umbra framework installed: {artifact.Name} {artifact.Version}",
            artifact.SizeBytes,
            artifact.SizeBytes,
            true));

        return new UmbraFrameworkDownloadResult(install, new[]
        {
            $"Installed Umbra framework {artifact.Name} {artifact.Version}",
            $"Umbra bootstrap: {install.BootstrapPath}",
            $"Umbra framework: {install.FrameworkPath}"
        });
    }

    private static void ValidateExtractedFiles(UmbraFrameworkArtifact artifact, string installRoot)
    {
        Dictionary<string, UmbraFrameworkFile> expected = artifact.VerifiedFiles.ToDictionary(
            file => file.Path.Replace('\\', '/'),
            StringComparer.OrdinalIgnoreCase);
        string root = Path.GetFullPath(installRoot);
        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!expected.Remove(relative, out UmbraFrameworkFile? file))
                throw new InvalidDataException($"Umbra framework archive contains an unsigned file: {relative}");

            FileInfo info = new(path);
            if (info.Length != file.SizeBytes)
                throw new InvalidDataException($"Umbra framework file size mismatch: {relative}");

            string actual;
            using (FileStream stream = File.OpenRead(path))
                actual = Convert.ToHexString(SHA256.HashData(stream));
            if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Umbra framework file hash mismatch: {relative}");
        }

        if (expected.Count > 0)
            throw new InvalidDataException(
                $"Umbra framework archive is missing signed files: {string.Join(", ", expected.Keys)}");
    }

    private static async Task DownloadArchiveAsync(
        UmbraFrameworkArtifact artifact,
        HttpClient httpClient,
        string archivePath,
        IProgress<UmbraFrameworkDownloadProgress>? progress,
        CancellationToken cancellationToken)
    {
        Uri sourceUri = new(artifact.ArchiveUrl, UriKind.Absolute);
        string tempPath = $"{archivePath}.download";
        if (File.Exists(tempPath))
            File.Delete(tempPath);

        progress?.Report(new UmbraFrameworkDownloadProgress(
            $"Downloading Umbra framework: {artifact.Name} {artifact.Version}",
            0,
            artifact.SizeBytes,
            true));

        using HttpResponseMessage response = await httpClient.GetAsync(
            sourceUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken);
        await using FileStream output = File.Create(tempPath);

        byte[] buffer = new byte[256 * 1024];
        long downloaded = 0;
        long lastReport = -1;
        while (true)
        {
            int read = await input.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                break;

            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
            downloaded += read;

            if (downloaded - lastReport >= 8 * 1024 * 1024 || downloaded == artifact.SizeBytes)
            {
                lastReport = downloaded;
                progress?.Report(new UmbraFrameworkDownloadProgress(
                    $"Downloading Umbra framework: {downloaded}/{artifact.SizeBytes} bytes",
                    downloaded,
                    artifact.SizeBytes,
                    false));
            }
        }

        if (File.Exists(archivePath))
            File.Delete(archivePath);

        File.Move(tempPath, archivePath);
    }

    private static void ValidateArchive(UmbraFrameworkArtifact artifact, string archivePath)
    {
        FileInfo info = new(archivePath);
        if (info.Length != artifact.SizeBytes)
            throw new InvalidDataException($"Umbra framework archive size mismatch: expected {artifact.SizeBytes}, actual {info.Length}.");

        string actualSha256;
        using (FileStream stream = File.OpenRead(archivePath))
            actualSha256 = Convert.ToHexString(SHA256.HashData(stream));

        if (!string.Equals(actualSha256, artifact.Sha256, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("Umbra framework archive SHA256 mismatch.");
    }

    private static void ExtractZip(string archivePath, string installRoot)
    {
        using ZipArchive archive = ZipFile.OpenRead(archivePath);
        foreach (ZipArchiveEntry entry in archive.Entries)
        {
            string targetPath = ResolveInstalledPath(installRoot, entry.FullName);

            if (string.IsNullOrWhiteSpace(entry.Name))
            {
                Directory.CreateDirectory(targetPath);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(targetPath)!);
            entry.ExtractToFile(targetPath, true);
        }
    }

    private static string ResolveInstalledPath(string installRoot, string relativePath)
    {
        string[] parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string root = Path.GetFullPath(installRoot);
        string target = Path.GetFullPath(Path.Combine([root, .. parts]));

        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(target, root, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Umbra framework archive path escapes install root: {relativePath}");
        }

        return target;
    }
}

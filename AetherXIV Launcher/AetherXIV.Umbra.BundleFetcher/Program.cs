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

using AetherXIV.Launcher.Core;
using System.Security.Cryptography;

if (args.Length >= 1 && string.Equals(args[0], "--stamp-local", StringComparison.Ordinal))
{
    if (args.Length is < 2 or > 3 || string.IsNullOrWhiteSpace(args[1]))
    {
        Console.Error.WriteLine(
            "Usage: AetherXIV.Umbra.BundleFetcher --stamp-local <framework-directory> [version]");
        return 2;
    }

    string localFrameworkRoot = Path.GetFullPath(args[1]);
    string localVersion = args.Length == 3 ? args[2].Trim() : "2.0.0";
    try
    {
        StampLocalBundle(localFrameworkRoot, localVersion);
        Console.WriteLine($"Stamped bundled Umbra framework {localVersion} for offline launcher packaging.");
        return 0;
    }
    catch (Exception ex)
    {
        Console.Error.WriteLine($"Umbra local bundle stamping failed: {ex.Message}");
        return 1;
    }
}

if (args.Length != 1 || string.IsNullOrWhiteSpace(args[0]))
{
    Console.Error.WriteLine(
        "Usage: AetherXIV.Umbra.BundleFetcher <destination-framework-directory>\n" +
        "   or: AetherXIV.Umbra.BundleFetcher --stamp-local <framework-directory> [version]");
    return 2;
}

string destination = Path.GetFullPath(args[0]);
string serviceUrl = Environment.GetEnvironmentVariable("AETHERXIV_UMBRA_SERVICE_URL")
    ?? LauncherProfile.DemiDevUnitUmbraServiceUrl;
string channel = Environment.GetEnvironmentVariable("AETHERXIV_UMBRA_CHANNEL")
    ?? UmbraFrameworkChannelManifest.StableChannel;
string workRoot = Path.Combine(
    Path.GetTempPath(),
    $"aetherxiv-umbra-bundle-{Guid.NewGuid():N}");

try
{
    string? localManifestPath = Environment.GetEnvironmentVariable("AETHERXIV_UMBRA_SIGNED_MANIFEST");
    string? localArchivePath = Environment.GetEnvironmentVariable("AETHERXIV_UMBRA_ARCHIVE");
    using HttpClient httpClient = string.IsNullOrWhiteSpace(localArchivePath)
        ? new HttpClient() { Timeout = TimeSpan.FromMinutes(3) }
        : new HttpClient(new LocalArchiveHandler(Path.GetFullPath(localArchivePath)));
    UmbraFrameworkChannelManifest manifest;
    if (!string.IsNullOrWhiteSpace(localManifestPath))
    {
        string signedJson = await File.ReadAllTextAsync(Path.GetFullPath(localManifestPath));
        manifest = UmbraSignedDocumentVerifier.VerifyAndDeserialize<UmbraFrameworkChannelManifest>(signedJson);
        if (string.IsNullOrWhiteSpace(localArchivePath))
            throw new InvalidOperationException("A local signed manifest requires AETHERXIV_UMBRA_ARCHIVE.");
    }
    else
    {
        UmbraOfficialUpdateClient updateClient = new(httpClient, serviceUrl);
        manifest = await updateClient.GetFrameworkChannelAsync(channel);
    }
    UmbraFrameworkCatalog catalog = manifest.ValidateAndCreateCatalog();
    UmbraFrameworkArtifact artifact = catalog.SelectDefault()
        ?? throw new InvalidDataException("The signed Umbra channel has no default framework artifact.");

    UmbraFrameworkDownloadResult download = await UmbraFrameworkDownloadService.DownloadAndInstallAsync(
        artifact,
        httpClient,
        frameworksRoot: Path.Combine(workRoot, "frameworks"),
        cacheRoot: Path.Combine(workRoot, "cache"),
        channelSequence: manifest.Sequence,
        signingKeyId: UmbraOfficialTrust.StableSigningKeyId);

    string staging = $"{destination}.staging-{Guid.NewGuid():N}";
    CopyDirectory(download.Install.InstallPath, staging);
    UmbraFrameworkInstall bundled = download.Install with
    {
        InstallPath = staging,
        BootstrapPath = Path.Combine(staging, artifact.BootstrapRelativePath),
        FrameworkPath = Path.Combine(staging, artifact.FrameworkRelativePath),
        IsBundled = true
    };
    UmbraInstallStore.Save(bundled);
    bundled.ValidateIntegrity();

    if (Directory.Exists(destination))
        Directory.Delete(destination, recursive: true);
    Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
    Directory.Move(staging, destination);
    Console.WriteLine($"Bundled signed Umbra framework {artifact.Version} from {channel} sequence {manifest.Sequence}.");
    return 0;
}
catch (Exception ex)
{
    Console.Error.WriteLine($"Umbra bundle fetch failed: {ex.Message}");
    return 1;
}
finally
{
    if (Directory.Exists(workRoot))
        Directory.Delete(workRoot, recursive: true);
}

static void CopyDirectory(string source, string destination)
{
    Directory.CreateDirectory(destination);
    foreach (string directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
        Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
    foreach (string file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
    {
        string target = Path.Combine(destination, Path.GetRelativePath(source, file));
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        File.Copy(file, target, overwrite: true);
    }
}

static void StampLocalBundle(string frameworkRoot, string version)
{
    if (!Version.TryParse(version, out _))
        throw new InvalidDataException($"Umbra framework version is invalid: {version}");

    string bootstrapPath = Path.Combine(frameworkRoot, "Aether.Umbra.Bootstrap.x86.dll");
    string frameworkPath = Path.Combine(frameworkRoot, "Managed", "Aether.Umbra.Framework.dll");
    if (!File.Exists(frameworkPath))
        frameworkPath = Path.Combine(frameworkRoot, "Managed", "Aether.Umbra.Framework.exe");
    if (!File.Exists(bootstrapPath))
        throw new FileNotFoundException("The locally built Umbra bootstrap is missing.", bootstrapPath);
    if (!File.Exists(frameworkPath))
        throw new FileNotFoundException("The locally built Umbra framework is missing.", frameworkPath);

    string receiptPath = UmbraInstallStore.ManifestPathFor(frameworkRoot);
    if (File.Exists(receiptPath))
        File.Delete(receiptPath);

    List<UmbraFrameworkFile> files = [];
    foreach (string filePath in Directory.EnumerateFiles(frameworkRoot, "*", SearchOption.AllDirectories)
                 .OrderBy(path => path, StringComparer.Ordinal))
    {
        string relativePath = Path.GetRelativePath(frameworkRoot, filePath).Replace('\\', '/');
        FileInfo info = new(filePath);
        string sha256;
        using (FileStream input = File.OpenRead(filePath))
            sha256 = Convert.ToHexString(SHA256.HashData(input));
        files.Add(new UmbraFrameworkFile(relativePath, info.Length, sha256));
    }

    UmbraFrameworkInstall bundled = new(
        "Aether Umbra",
        version,
        UmbraCompatibility.CurrentApiVersion,
        "win-x86",
        frameworkRoot,
        bootstrapPath,
        frameworkPath,
        new[] { UmbraCompatibility.Known123bGameSha256 },
        DateTimeOffset.UtcNow)
    {
        Files = files,
        IsBundled = true
    };

    UmbraInstallStore.Save(bundled);
    bundled.ValidateIntegrity();
}

sealed class LocalArchiveHandler(string archivePath) : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        FileStream stream = File.OpenRead(archivePath);
        HttpResponseMessage response = new(System.Net.HttpStatusCode.OK)
        {
            RequestMessage = request,
            Content = new StreamContent(stream)
        };
        response.Content.Headers.ContentLength = stream.Length;
        return Task.FromResult(response);
    }
}

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

using System.Text.Json.Serialization;

namespace AetherXIV.Launcher.Core;

public sealed record UmbraFrameworkCatalog(
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<UmbraFrameworkArtifact> Artifacts)
{
    public UmbraFrameworkArtifact? SelectDefault(string? gameSha256 = null)
    {
        IEnumerable<UmbraFrameworkArtifact> candidates = Artifacts.Where(artifact =>
            artifact.IsActive
            && artifact.UsesAetherEntrypoints);
        if (!string.IsNullOrWhiteSpace(gameSha256))
        {
            candidates = candidates.Where(artifact =>
                artifact.SupportsGameHash(gameSha256)
                || artifact.SupportedGameSha256.Count == 0);
        }

        return candidates
            .OrderByDescending(artifact => artifact.IsDefault)
            .ThenBy(artifact => artifact.SortOrder)
            .FirstOrDefault();
    }
}

public sealed record UmbraFrameworkChannelManifest(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("channel")] string Channel,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("platform")] string Platform,
    [property: JsonPropertyName("artifacts")] IReadOnlyList<UmbraFrameworkArtifact> Artifacts)
{
    public const int CurrentSchemaVersion = 1;
    public const string StableChannel = "stable";
    public const string DevChannel = "dev";

    public UmbraFrameworkCatalog ValidateAndCreateCatalog()
    {
        if (SchemaVersion != CurrentSchemaVersion)
            throw new InvalidDataException($"Unsupported Umbra framework channel schema: {SchemaVersion}");
        if (Sequence <= 0)
            throw new InvalidDataException("Umbra framework channel sequence must be positive.");
        if (GeneratedAt == default)
            throw new InvalidDataException("Umbra framework channel must include a generation time.");

        string normalizedChannel = NormalizeChannel(Channel);
        if (!string.Equals(normalizedChannel, Channel, StringComparison.Ordinal))
            throw new InvalidDataException("Umbra framework channel name is not canonical.");
        if (!string.Equals(Platform, "win-x86", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported Umbra framework platform: {Platform}");
        if (Artifacts.Count == 0)
            throw new InvalidDataException("Umbra framework channel does not contain an artifact.");

        foreach (UmbraFrameworkArtifact artifact in Artifacts)
            artifact.ValidateOfficial();

        return new UmbraFrameworkCatalog(Platform, Artifacts);
    }

    public static string NormalizeChannel(string? value)
    {
        string channel = (value ?? "").Trim().ToLowerInvariant();
        return channel switch
        {
            StableChannel => StableChannel,
            DevChannel => DevChannel,
            _ => throw new ArgumentException($"Unsupported Umbra framework channel: {value}")
        };
    }
}

public sealed record UmbraFrameworkFile(
    [property: JsonPropertyName("path")] string Path,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256);

public sealed record UmbraFrameworkArtifact(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("platform_rid")] string PlatformRid,
    [property: JsonPropertyName("archive_url")] string ArchiveUrl,
    [property: JsonPropertyName("archive_format")] string ArchiveFormat,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("bootstrap_relative_path")] string BootstrapRelativePath,
    [property: JsonPropertyName("framework_relative_path")] string FrameworkRelativePath,
    [property: JsonPropertyName("supported_game_sha256")] IReadOnlyList<string> SupportedGameSha256,
    [property: JsonPropertyName("is_default")] bool IsDefault,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("sort_order")] int SortOrder,
    [property: JsonPropertyName("files")] IReadOnlyList<UmbraFrameworkFile>? Files = null)
{
    public string StableId => RuntimeInstallStore.SanitizePathSegment(
        $"{PlatformRid}-{Name}-{Version}-{ShortArchiveHash}");

    public bool UsesAetherEntrypoints =>
        string.Equals(Path.GetFileName(BootstrapRelativePath), "Aether.Umbra.Bootstrap.x86.dll", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(Path.GetFileName(FrameworkRelativePath), "Aether.Umbra.Framework.dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(FrameworkRelativePath), "Aether.Umbra.Framework.exe", StringComparison.OrdinalIgnoreCase));

    private string ShortArchiveHash => string.IsNullOrWhiteSpace(Sha256)
        ? "nohash"
        : Sha256.Trim()[..Math.Min(12, Sha256.Trim().Length)];

    public bool SupportsGameHash(string sha256)
    {
        return SupportedGameSha256.Count == 0
            || SupportedGameSha256.Any(candidate => string.Equals(candidate, sha256, StringComparison.OrdinalIgnoreCase));
    }

    public IReadOnlyList<UmbraFrameworkFile> VerifiedFiles => Files ?? Array.Empty<UmbraFrameworkFile>();

    public void ValidateOfficial()
    {
        if (string.IsNullOrWhiteSpace(Name)
            || string.IsNullOrWhiteSpace(Version)
            || string.IsNullOrWhiteSpace(ApiVersion))
            throw new InvalidDataException("Umbra framework artifact identity is incomplete.");
        if (!System.Version.TryParse(Version, out _))
            throw new InvalidDataException($"Umbra framework version is invalid: {Version}");
        if (!string.Equals(PlatformRid, "win-x86", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported Umbra framework artifact platform: {PlatformRid}");
        if (!string.Equals(ArchiveFormat, "zip", StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsupported Umbra framework archive format: {ArchiveFormat}");
        if (!Uri.TryCreate(ArchiveUrl, UriKind.Absolute, out Uri? archiveUri)
            || !UmbraRepositoryOptions.IsAllowedRepositoryUri(archiveUri))
            throw new InvalidDataException("Umbra framework archive URL must use HTTPS.");
        if (SizeBytes <= 0)
            throw new InvalidDataException("Umbra framework archive size must be positive.");
        ValidateSha256(Sha256, "archive");
        if (!UsesAetherEntrypoints)
            throw new InvalidDataException("Umbra framework artifact does not use the supported entrypoints.");
        if (SupportedGameSha256.Count == 0)
            throw new InvalidDataException("Umbra framework artifact must name at least one supported game hash.");
        foreach (string gameHash in SupportedGameSha256)
            ValidateSha256(gameHash, "supported game");
        if (VerifiedFiles.Count == 0)
            throw new InvalidDataException("Umbra framework artifact must include signed file integrity metadata.");

        HashSet<string> paths = new(StringComparer.OrdinalIgnoreCase);
        foreach (UmbraFrameworkFile file in VerifiedFiles)
        {
            if (string.IsNullOrWhiteSpace(file.Path) || Path.IsPathRooted(file.Path))
                throw new InvalidDataException("Umbra framework file paths must be relative.");
            string normalized = file.Path.Replace('\\', '/');
            if (normalized.Split('/', StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
                throw new InvalidDataException($"Umbra framework file path escapes the package: {file.Path}");
            if (!paths.Add(normalized))
                throw new InvalidDataException($"Umbra framework file path is duplicated: {file.Path}");
            if (file.SizeBytes < 0)
                throw new InvalidDataException($"Umbra framework file size is invalid: {file.Path}");
            ValidateSha256(file.Sha256, $"file {file.Path}");
        }
    }

    private static void ValidateSha256(string value, string label)
    {
        if (value.Length != 64 || !value.All(Uri.IsHexDigit))
            throw new InvalidDataException($"Umbra {label} SHA256 must contain 64 hexadecimal characters.");
    }
}

public sealed record UmbraOfficialRepositoryManifest(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("repository_name")] string RepositoryName,
    [property: JsonPropertyName("plugins")] IReadOnlyList<UmbraPluginCatalogEntry> Plugins,
    [property: JsonPropertyName("resources")] IReadOnlyList<UmbraDeveloperResourceEntry> Resources);

public sealed record UmbraDeveloperResourceEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("resource_type")] string ResourceType,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("download_url")] string DownloadUrl,
    [property: JsonPropertyName("documentation_url")] string DocumentationUrl,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("is_active")] bool IsActive);

public sealed record UmbraPluginBlocklistManifest(
    [property: JsonPropertyName("schema_version")] int SchemaVersion,
    [property: JsonPropertyName("sequence")] long Sequence,
    [property: JsonPropertyName("generated_at")] DateTimeOffset GeneratedAt,
    [property: JsonPropertyName("blocks")] IReadOnlyList<UmbraPluginBlockRule> Blocks);

public sealed record UmbraPluginBlockRule(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("plugin_id")] string? PluginId,
    [property: JsonPropertyName("repository_url")] string? RepositoryUrl,
    [property: JsonPropertyName("minimum_version")] string? MinimumVersion,
    [property: JsonPropertyName("maximum_version")] string? MaximumVersion,
    [property: JsonPropertyName("package_sha256")] string? PackageSha256,
    [property: JsonPropertyName("reason")] string Reason,
    [property: JsonPropertyName("issued_at")] DateTimeOffset IssuedAt,
    [property: JsonPropertyName("expires_at")] DateTimeOffset? ExpiresAt);

public sealed record UmbraPluginCatalog(
    [property: JsonPropertyName("repository_name")] string RepositoryName,
    [property: JsonPropertyName("plugins")] IReadOnlyList<UmbraPluginCatalogEntry> Plugins);

public sealed record UmbraPluginCatalogEntry(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("author")] string Author,
    [property: JsonPropertyName("description")] string Description,
    [property: JsonPropertyName("download_url")] string DownloadUrl,
    [property: JsonPropertyName("size_bytes")] long SizeBytes,
    [property: JsonPropertyName("sha256")] string Sha256,
    [property: JsonPropertyName("minimum_framework_version")] string MinimumFrameworkVersion,
    [property: JsonPropertyName("is_active")] bool IsActive,
    [property: JsonPropertyName("built_in")] bool BuiltIn = false);

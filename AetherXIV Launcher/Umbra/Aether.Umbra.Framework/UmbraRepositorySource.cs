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
using System.Text.Json.Serialization;

namespace Aether.Umbra.Framework;

public sealed record UmbraRepositorySource(
    [property: JsonPropertyName("url")] string Url,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("name")] string? Name = null)
{
    public const string Supported = "supported";
    public const string Custom = "custom";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = false
    };

    public static IReadOnlyList<UmbraRepositorySource> FromUrls(IEnumerable<string>? urls, string source)
    {
        if (urls is null)
            return Array.Empty<UmbraRepositorySource>();

        return Normalize(urls.Select(url => new UmbraRepositorySource(url, source)));
    }

    public static IReadOnlyList<UmbraRepositorySource> FromJson(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return Array.Empty<UmbraRepositorySource>();

        IReadOnlyList<UmbraRepositorySource>? sources =
            JsonSerializer.Deserialize<IReadOnlyList<UmbraRepositorySource>>(json, JsonOptions);
        return Normalize(sources);
    }

    public static IReadOnlyList<UmbraRepositorySource> Normalize(IEnumerable<UmbraRepositorySource>? sources)
    {
        if (sources is null)
            return Array.Empty<UmbraRepositorySource>();

        List<UmbraRepositorySource> normalized = new();
        HashSet<string> seen = new(StringComparer.OrdinalIgnoreCase);
        foreach (UmbraRepositorySource source in sources)
        {
            string url = (source.Url ?? "").Trim();
            if (string.IsNullOrWhiteSpace(url) || !IsAllowedUri(url))
                continue;

            if (!seen.Add(url))
                continue;

            string sourceKind = string.Equals(source.Source, Supported, StringComparison.OrdinalIgnoreCase)
                ? Supported
                : Custom;
            string? name = string.IsNullOrWhiteSpace(source.Name) ? null : source.Name.Trim();
            normalized.Add(new UmbraRepositorySource(url, sourceKind, name));
        }

        return normalized;
    }

    public static string ToJson(IEnumerable<UmbraRepositorySource>? sources)
    {
        return JsonSerializer.Serialize(Normalize(sources), JsonOptions);
    }

    public static bool IsAllowedUri(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return false;

        if (Uri.TryCreate(value, UriKind.Absolute, out Uri? uri))
        {
            if (uri.IsFile)
                return true;

            if (string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase))
                return true;

            if (!string.Equals(uri.Scheme, Uri.UriSchemeHttp, StringComparison.OrdinalIgnoreCase))
                return false;

            return string.Equals(uri.Host, "localhost", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "127.0.0.1", StringComparison.OrdinalIgnoreCase)
                || string.Equals(uri.Host, "::1", StringComparison.OrdinalIgnoreCase);
        }

        // A rooted filesystem path (for example the Wine drive path of the
        // bundled repository the launcher seeds) is a valid local source.
        return Path.IsPathRooted(value);
    }

    /// <summary>
    /// True when the source points at a repository manifest on the local file
    /// system (the bundled foundation catalog) instead of a remote URL.
    /// </summary>
    public bool IsLocalFileSource =>
        (Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri) && uri.IsFile)
        || Path.IsPathRooted(Url);

    public Uri ResolveManifestUri()
    {
        if (Path.IsPathRooted(Url))
            return new Uri(Path.GetFullPath(Url));

        if (!Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri))
            throw new InvalidDataException($"Invalid Umbra repository URL: {Url}");

        if (!IsGitHubRepositoryUri(uri, out string? owner, out string? repository))
            return uri;

        return new Uri(
            $"https://raw.githubusercontent.com/{Uri.EscapeDataString(owner!)}/" +
            $"{Uri.EscapeDataString(repository!)}/HEAD/umbra-repository.json");
    }

    public bool IsGitHubRepository =>
        Uri.TryCreate(Url, UriKind.Absolute, out Uri? uri)
        && IsGitHubRepositoryUri(uri, out _, out _);

    private static bool IsGitHubRepositoryUri(
        Uri uri,
        out string? owner,
        out string? repository)
    {
        owner = null;
        repository = null;
        if (!string.Equals(uri.Scheme, Uri.UriSchemeHttps, StringComparison.OrdinalIgnoreCase)
            || (!string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase)
                && !string.Equals(uri.Host, "www.github.com", StringComparison.OrdinalIgnoreCase)))
        {
            return false;
        }

        string[] parts = uri.AbsolutePath
            .Split('/', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length != 2)
            return false;

        owner = parts[0];
        repository = parts[1].EndsWith(".git", StringComparison.OrdinalIgnoreCase)
            ? parts[1][..^4]
            : parts[1];
        return !string.IsNullOrWhiteSpace(owner) && !string.IsNullOrWhiteSpace(repository);
    }
}

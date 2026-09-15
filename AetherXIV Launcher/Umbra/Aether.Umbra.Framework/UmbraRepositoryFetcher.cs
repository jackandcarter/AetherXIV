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

using System.Security.Cryptography;
using System.Text;

namespace Aether.Umbra.Framework;

public enum UmbraRepositoryFetchState
{
    Healthy,
    Cached,
    Failed
}

public sealed record UmbraRepositoryFetchResult(
    UmbraRepositorySource Source,
    string? RepositoryName,
    IReadOnlyList<UmbraStoreEntry> Entries,
    UmbraRepositoryFetchState State,
    DateTimeOffset CheckedAt,
    string? Error)
{
    public bool Succeeded => State != UmbraRepositoryFetchState.Failed;
}

public sealed record UmbraRepositoryRefreshResult(
    IReadOnlyList<UmbraStoreEntry> Entries,
    IReadOnlyList<UmbraRepositoryFetchResult> Repositories);

public static class UmbraRepositoryFetcher
{
    private const int MaximumRepositoryBytes = 2 * 1024 * 1024;

    internal static IReadOnlyList<UmbraStoreEntry> LoadCached(
        IEnumerable<UmbraRepositorySource> repositories,
        string cacheDirectory,
        UmbraRuntimeLog log)
    {
        return NormalizeEntries(LoadCachedResults(repositories, cacheDirectory, log)
            .SelectMany(result => result.Entries));
    }

    internal static IReadOnlyList<UmbraRepositoryFetchResult> LoadCachedResults(
        IEnumerable<UmbraRepositorySource> repositories,
        string cacheDirectory,
        UmbraRuntimeLog log)
    {
        List<UmbraRepositoryFetchResult> results = new();
        foreach (UmbraRepositorySource repository in repositories)
        {
            string? cached = ReadCache(cacheDirectory, repository.Url);
            if (cached is null)
            {
                results.Add(new UmbraRepositoryFetchResult(
                    repository,
                    repository.Name,
                    Array.Empty<UmbraStoreEntry>(),
                    UmbraRepositoryFetchState.Failed,
                    DateTimeOffset.UtcNow,
                    "Repository has not been checked yet."));
                continue;
            }

            try
            {
                UmbraRepositoryDocument document = UmbraStoreEntry.ParseRepositoryDocument(cached, repository);
                results.Add(new UmbraRepositoryFetchResult(
                    repository,
                    document.Name ?? repository.Name,
                    document.Entries,
                    UmbraRepositoryFetchState.Cached,
                    GetCacheTimestamp(cacheDirectory, repository.Url),
                    null));
                log.Info($"umbra_repository_cache_loaded url={repository.Url}");
            }
            catch (Exception ex)
            {
                results.Add(new UmbraRepositoryFetchResult(
                    repository,
                    repository.Name,
                    Array.Empty<UmbraStoreEntry>(),
                    UmbraRepositoryFetchState.Failed,
                    DateTimeOffset.UtcNow,
                    ex.Message));
                log.Warning($"umbra_repository_cache_invalid url={repository.Url} error={ex.Message}");
            }
        }

        return results;
    }

    public static async Task<IReadOnlyList<UmbraStoreEntry>> FetchAsync(
        IEnumerable<UmbraRepositorySource> repositories,
        string cacheDirectory,
        UmbraRuntimeLog log,
        CancellationToken cancellationToken = default)
    {
        UmbraRepositoryRefreshResult result = await FetchAllAsync(
            repositories,
            cacheDirectory,
            log,
            cancellationToken).ConfigureAwait(false);
        return result.Entries;
    }

    public static async Task<UmbraRepositoryRefreshResult> FetchAllAsync(
        IEnumerable<UmbraRepositorySource> repositories,
        string cacheDirectory,
        UmbraRuntimeLog log,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheDirectory);
        using HttpClient client = CreateClient();

        List<UmbraRepositoryFetchResult> results = new();
        foreach (UmbraRepositorySource repository in repositories)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                UmbraRepositoryFetchResult fetched = await FetchRepositoryResultAsync(
                    client,
                    repository,
                    cacheDirectory,
                    cancellationToken);
                results.Add(fetched);
                log.Info($"umbra_repository_fetch_success url={repository.Url}");
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception ex)
            {
                string? cached = ReadCache(cacheDirectory, repository.Url);
                if (cached is not null)
                {
                    try
                    {
                        UmbraRepositoryDocument document = UmbraStoreEntry.ParseRepositoryDocument(cached, repository);
                        results.Add(new UmbraRepositoryFetchResult(
                            repository,
                            document.Name ?? repository.Name,
                            document.Entries,
                            UmbraRepositoryFetchState.Cached,
                            DateTimeOffset.UtcNow,
                            ex.Message));
                        log.Warning($"umbra_repository_fetch_failed_cached url={repository.Url} error={ex.Message}");
                        continue;
                    }
                    catch (Exception cacheException)
                    {
                        log.Warning(
                            $"umbra_repository_cache_invalid url={repository.Url} error={cacheException.Message}");
                    }
                }

                results.Add(new UmbraRepositoryFetchResult(
                    repository,
                    repository.Name,
                    Array.Empty<UmbraStoreEntry>(),
                    UmbraRepositoryFetchState.Failed,
                    DateTimeOffset.UtcNow,
                    ex.Message));
                log.Warning($"umbra_repository_fetch_failed url={repository.Url} error={ex.Message}");
            }
        }

        return new UmbraRepositoryRefreshResult(
            NormalizeEntries(results.SelectMany(result => result.Entries)),
            results);
    }

    private static IReadOnlyList<UmbraStoreEntry> NormalizeEntries(
        IEnumerable<UmbraStoreEntry> entries)
    {
        return entries
            .GroupBy(entry => (entry.RepositoryUrl, entry.Id, entry.Version), new StoreEntryKeyComparer())
            .Select(group => group.First())
            .OrderBy(entry => entry.Name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Id, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public static async Task<IReadOnlyList<UmbraStoreEntry>> FetchRepositoryAsync(
        UmbraRepositorySource repository,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        UmbraRepositoryFetchResult result = await FetchRepositoryResultAsync(
            repository,
            cacheDirectory,
            cancellationToken).ConfigureAwait(false);
        return result.Entries;
    }

    public static async Task<UmbraRepositoryFetchResult> FetchRepositoryResultAsync(
        UmbraRepositorySource repository,
        string cacheDirectory,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(cacheDirectory);
        using HttpClient client = CreateClient();
        return await FetchRepositoryResultAsync(
            client,
            repository,
            cacheDirectory,
            cancellationToken).ConfigureAwait(false);
    }

    private static async Task<UmbraRepositoryFetchResult> FetchRepositoryResultAsync(
        HttpClient client,
        UmbraRepositorySource repository,
        string cacheDirectory,
        CancellationToken cancellationToken)
    {
        string json = repository.IsLocalFileSource
            ? ReadLocalRepository(repository, cancellationToken)
            : await FetchRemoteRepositoryAsync(client, repository, cancellationToken).ConfigureAwait(false);

        // Parse before caching so a malformed document can never replace a known-good index.
        UmbraRepositoryDocument document = UmbraStoreEntry.ParseRepositoryDocument(json, repository);
        WriteCache(cacheDirectory, repository.Url, json);
        return new UmbraRepositoryFetchResult(
            repository,
            document.Name ?? repository.Name,
            document.Entries,
            UmbraRepositoryFetchState.Healthy,
            DateTimeOffset.UtcNow,
            null);
    }

    private static string ReadLocalRepository(
        UmbraRepositorySource repository,
        CancellationToken cancellationToken)
    {
        Uri manifestUri = repository.ResolveManifestUri();
        if (!manifestUri.IsFile)
            throw new InvalidDataException($"Umbra repository is not a local file: {repository.Url}");

        string path = manifestUri.LocalPath;
        FileInfo info = new(path);
        if (!info.Exists)
            throw new FileNotFoundException("Umbra repository manifest was not found.", path);
        if (info.Length > MaximumRepositoryBytes)
            throw new InvalidDataException(
                $"Umbra repository exceeds the {MaximumRepositoryBytes} byte limit.");

        using FileStream stream = File.OpenRead(path);
        using MemoryStream buffer = new();
        CopyBounded(stream, buffer, MaximumRepositoryBytes);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static async Task<string> FetchRemoteRepositoryAsync(
        HttpClient client,
        UmbraRepositorySource repository,
        CancellationToken cancellationToken)
    {
        Uri manifestUri = repository.ResolveManifestUri();
        using HttpResponseMessage response = await client.GetAsync(
            manifestUri,
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long contentLength
            && contentLength > MaximumRepositoryBytes)
        {
            throw new InvalidDataException(
                $"Umbra repository exceeds the {MaximumRepositoryBytes} byte limit.");
        }

        await using Stream stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using MemoryStream buffer = new();
        await CopyBoundedAsync(stream, buffer, MaximumRepositoryBytes, cancellationToken);
        return Encoding.UTF8.GetString(buffer.ToArray());
    }

    private static void CopyBounded(
        Stream source,
        Stream destination,
        int maximumBytes)
    {
        byte[] buffer = new byte[16 * 1024];
        int total = 0;
        while (true)
        {
            int read = source.Read(buffer, 0, buffer.Length);
            if (read == 0)
                return;

            total += read;
            if (total > maximumBytes)
                throw new InvalidDataException($"Umbra repository exceeds the {maximumBytes} byte limit.");
            destination.Write(buffer, 0, read);
        }
    }

    private static HttpClient CreateClient() => new()
    {
        Timeout = TimeSpan.FromSeconds(10)
    };

    private static async Task CopyBoundedAsync(
        Stream source,
        Stream destination,
        int maximumBytes,
        CancellationToken cancellationToken)
    {
        byte[] buffer = new byte[16 * 1024];
        int total = 0;
        while (true)
        {
            int read = await source.ReadAsync(buffer, cancellationToken);
            if (read == 0)
                return;

            total += read;
            if (total > maximumBytes)
                throw new InvalidDataException($"Umbra repository exceeds the {maximumBytes} byte limit.");
            await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken);
        }
    }

    private static void WriteCache(string cacheDirectory, string repositoryUrl, string json)
    {
        string path = CachePath(cacheDirectory, repositoryUrl);
        string temporaryPath = path + ".tmp";
        try
        {
            File.WriteAllText(temporaryPath, json);
            File.Move(temporaryPath, path, overwrite: true);
        }
        finally
        {
            if (File.Exists(temporaryPath))
                File.Delete(temporaryPath);
        }
    }

    private static string? ReadCache(string cacheDirectory, string repositoryUrl)
    {
        string path = CachePath(cacheDirectory, repositoryUrl);
        return File.Exists(path) ? File.ReadAllText(path) : null;
    }

    private static string CachePath(string cacheDirectory, string repositoryUrl)
    {
        byte[] hash = SHA256.HashData(Encoding.UTF8.GetBytes(repositoryUrl));
        return Path.Combine(cacheDirectory, $"{Convert.ToHexString(hash).ToLowerInvariant()}.json");
    }

    private static DateTimeOffset GetCacheTimestamp(string cacheDirectory, string repositoryUrl)
    {
        string path = CachePath(cacheDirectory, repositoryUrl);
        return File.Exists(path) ? File.GetLastWriteTimeUtc(path) : DateTimeOffset.UtcNow;
    }

    private sealed class StoreEntryKeyComparer : IEqualityComparer<(string RepositoryUrl, string Id, string Version)>
    {
        public bool Equals(
            (string RepositoryUrl, string Id, string Version) x,
            (string RepositoryUrl, string Id, string Version) y)
        {
            return string.Equals(x.RepositoryUrl, y.RepositoryUrl, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Id, y.Id, StringComparison.OrdinalIgnoreCase)
                && string.Equals(x.Version, y.Version, StringComparison.OrdinalIgnoreCase);
        }

        public int GetHashCode((string RepositoryUrl, string Id, string Version) obj)
        {
            return HashCode.Combine(
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.RepositoryUrl),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Id),
                StringComparer.OrdinalIgnoreCase.GetHashCode(obj.Version));
        }
    }
}

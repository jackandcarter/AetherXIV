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

using System.Text;

namespace AetherXIV.Launcher.Core;

public sealed class UmbraOfficialUpdateClient
{
    private const int MaximumSignedDocumentBytes = 6 * 1024 * 1024;

    private readonly HttpClient httpClient;
    private readonly Uri baseUri;
    private readonly IReadOnlyDictionary<string, string> trustedKeys;

    public UmbraOfficialUpdateClient(
        HttpClient httpClient,
        string? baseUrl = null,
        IReadOnlyDictionary<string, string>? trustedKeys = null)
    {
        this.httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        string normalized = string.IsNullOrWhiteSpace(baseUrl)
            ? LauncherProfile.DemiDevUnitUmbraServiceUrl
            : baseUrl.Trim();
        if (!normalized.EndsWith('/'))
            normalized += "/";

        baseUri = new Uri(normalized, UriKind.Absolute);
        if (!UmbraRepositoryOptions.IsAllowedRepositoryUri(baseUri))
            throw new ArgumentException("Umbra update service must use HTTPS, except for localhost development.");

        this.trustedKeys = trustedKeys ?? UmbraOfficialTrust.SigningKeys;
    }

    public Task<UmbraFrameworkChannelManifest> GetFrameworkChannelAsync(
        string channel = UmbraFrameworkChannelManifest.StableChannel,
        CancellationToken cancellationToken = default)
    {
        string normalizedChannel = UmbraFrameworkChannelManifest.NormalizeChannel(channel);
        return GetSignedAsync<UmbraFrameworkChannelManifest>(
            $"channels/{normalizedChannel}/framework-win-x86.json",
            cancellationToken);
    }

    public Task<UmbraOfficialRepositoryManifest> GetOfficialRepositoryAsync(
        CancellationToken cancellationToken = default)
    {
        return GetSignedAsync<UmbraOfficialRepositoryManifest>("repository.json", cancellationToken);
    }

    public Task<UmbraPluginBlocklistManifest> GetBlocklistAsync(
        CancellationToken cancellationToken = default)
    {
        return GetSignedAsync<UmbraPluginBlocklistManifest>("blocklist.json", cancellationToken);
    }

    private async Task<T> GetSignedAsync<T>(
        string relativePath,
        CancellationToken cancellationToken)
    {
        using HttpResponseMessage response = await httpClient.GetAsync(
            new Uri(baseUri, relativePath),
            HttpCompletionOption.ResponseHeadersRead,
            cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        if (response.Content.Headers.ContentLength is long length
            && length > MaximumSignedDocumentBytes)
        {
            throw new InvalidDataException(
                $"Umbra signed document exceeds the {MaximumSignedDocumentBytes} byte limit.");
        }

        await using Stream input = await response.Content.ReadAsStreamAsync(cancellationToken)
            .ConfigureAwait(false);
        using MemoryStream buffer = new();
        byte[] chunk = new byte[16 * 1024];
        while (true)
        {
            int read = await input.ReadAsync(chunk, cancellationToken).ConfigureAwait(false);
            if (read == 0)
                break;
            if (buffer.Length + read > MaximumSignedDocumentBytes)
                throw new InvalidDataException(
                    $"Umbra signed document exceeds the {MaximumSignedDocumentBytes} byte limit.");
            await buffer.WriteAsync(chunk.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
        }

        string json = Encoding.UTF8.GetString(buffer.ToArray());
        return UmbraSignedDocumentVerifier.VerifyAndDeserialize<T>(json, trustedKeys);
    }
}

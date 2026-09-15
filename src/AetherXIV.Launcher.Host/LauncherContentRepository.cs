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
using AetherXIV.Data;
using AetherXIV.Launcher.Contracts;
using MySqlConnector;

namespace AetherXIV.Launcher.Host;

public sealed class MariaDbLauncherContentRepository : ILauncherContentRepository
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbLauncherContentRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async ValueTask<LauncherConfig?> GetActiveConfigAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        LauncherConfigRow? row = await ReadActiveConfigRowAsync(connection, cancellationToken).ConfigureAwait(false);
        if (row is null)
            return null;

        return new LauncherConfig(
            row.ServiceVersion,
            row.ServerName,
            row.ServerStatusUrl,
            row.NewsUrl,
            row.PatchManifestUrl,
            row.RuntimeCatalogUrl,
            row.LoginUrl,
            row.AccountCreateUrl,
            row.ClientLoginUrl,
            row.PatchBaseUrl,
            row.TargetBootVersion,
            row.TargetGameVersion,
            null,
            Array.Empty<string>(),
            null);
    }

    public async ValueTask<LauncherStatusRecord?> GetStatusAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT state, message
FROM launcher_status
WHERE status_key = 'default'
LIMIT 1;
""";

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new LauncherStatusRecord(
            reader.GetString("state"),
            reader.GetString("message"));
    }

    public async ValueTask<IReadOnlyList<LauncherNewsItem>> GetNewsItemsAsync(CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT news_id, title, summary, body, banner_url, link_url, published_at,
       title_color, summary_color, body_color
FROM launcher_news
WHERE is_active = 1
  AND published_at <= UTC_TIMESTAMP()
ORDER BY sort_order ASC, published_at DESC, news_id DESC
LIMIT 20;
""";

        List<LauncherNewsItem> items = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new LauncherNewsItem(
                reader.GetInt32("news_id"),
                reader.GetString("title"),
                reader.GetString("summary"),
                ReadNullableString(reader, "body"),
                ReadNullableString(reader, "banner_url"),
                ReadNullableString(reader, "link_url"),
                ReadUtcDateTimeOffset(reader, "published_at"),
                reader.GetString("title_color"),
                reader.GetString("summary_color"),
                reader.GetString("body_color")));
        }

        return items;
    }

    public async ValueTask<LauncherReelPresentation> GetReelPresentationAsync(
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);

        bool enabled = false;
        await using (MySqlCommand settingsCommand = connection.CreateCommand())
        {
            settingsCommand.CommandText = """
SELECT reel_text_enabled
FROM launcher_presentation
WHERE presentation_key = 'default'
LIMIT 1;
""";
            object? value = await settingsCommand.ExecuteScalarAsync(cancellationToken).ConfigureAwait(false);
            enabled = value is not null && value is not DBNull && Convert.ToBoolean(value);
        }

        List<LauncherReelText> items = [];
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT image_file, header_text, sub_text, header_size, sub_text_size,
       header_color, sub_text_color, is_enabled
FROM launcher_reel_text
ORDER BY image_file ASC;
""";
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            items.Add(new LauncherReelText(
                reader.GetString("image_file"),
                reader.GetString("header_text"),
                reader.GetString("sub_text"),
                reader.GetDouble("header_size"),
                reader.GetDouble("sub_text_size"),
                reader.GetString("header_color"),
                reader.GetString("sub_text_color"),
                reader.GetBoolean("is_enabled")));
        }

        return new LauncherReelPresentation(enabled, items);
    }

    public async ValueTask<IReadOnlyList<LauncherPatchFile>> GetPatchFilesAsync(
        string targetBootVersion,
        string targetGameVersion,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(targetBootVersion);
        ArgumentException.ThrowIfNullOrWhiteSpace(targetGameVersion);

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT relative_path, size_bytes, crc32, sha256
FROM launcher_patch_files
WHERE is_active = 1
  AND target_boot_version = @target_boot_version
  AND target_game_version = @target_game_version
ORDER BY sort_order ASC, relative_path ASC;
""";
        command.Parameters.AddWithValue("@target_boot_version", targetBootVersion);
        command.Parameters.AddWithValue("@target_game_version", targetGameVersion);

        List<LauncherPatchFile> files = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            files.Add(new LauncherPatchFile(
                reader.GetString("relative_path"),
                reader.GetInt64("size_bytes"),
                reader.GetString("crc32"),
                ReadNullableString(reader, "sha256")));
        }

        return files;
    }

    public async ValueTask<IReadOnlyList<RuntimeArtifact>> GetRuntimeArtifactsAsync(
        string platformRid,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT name, version, platform_rid, runtime_kind, archive_url, archive_format, size_bytes, sha256,
       executable_relative_path, prefix_arch, environment_json, is_default, is_active, sort_order
FROM launcher_runtime_artifacts
WHERE is_active = 1
  AND (@platform_rid = '' OR platform_rid = @platform_rid)
ORDER BY is_default DESC, sort_order ASC, name ASC, version ASC;
""";
        command.Parameters.AddWithValue("@platform_rid", platformRid ?? "");

        List<RuntimeArtifact> artifacts = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            artifacts.Add(new RuntimeArtifact(
                reader.GetString("name"),
                reader.GetString("version"),
                reader.GetString("platform_rid"),
                reader.GetString("runtime_kind"),
                reader.GetString("archive_url"),
                reader.GetString("archive_format"),
                reader.GetInt64("size_bytes"),
                reader.GetString("sha256"),
                reader.GetString("executable_relative_path"),
                reader.GetString("prefix_arch"),
                ReadStringDictionary(reader, "environment_json"),
                reader.GetBoolean("is_default"),
                reader.GetBoolean("is_active"),
                reader.GetInt32("sort_order")));
        }

        return artifacts;
    }

    private static async Task<LauncherConfigRow?> ReadActiveConfigRowAsync(
        MySqlConnection connection,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT config_key, service_version, server_name, server_status_url, news_url, patch_manifest_url,
       runtime_catalog_url, login_url, account_create_url, client_login_url, patch_base_url,
       target_boot_version, target_game_version
FROM launcher_config
WHERE is_active = 1
ORDER BY config_key ASC
LIMIT 1;
""";

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new LauncherConfigRow(
            reader.GetString("config_key"),
            reader.GetInt32("service_version"),
            reader.GetString("server_name"),
            ReadNullableString(reader, "server_status_url"),
            reader.GetString("news_url"),
            reader.GetString("patch_manifest_url"),
            ReadNullableString(reader, "runtime_catalog_url"),
            ReadNullableString(reader, "login_url"),
            ReadNullableString(reader, "account_create_url"),
            ReadNullableString(reader, "client_login_url"),
            ReadNullableString(reader, "patch_base_url"),
            reader.GetString("target_boot_version"),
            reader.GetString("target_game_version"));
    }

    private static IReadOnlyDictionary<string, string> ReadStringDictionary(MySqlDataReader reader, string name)
    {
        string? json = ReadNullableString(reader, name);
        if (String.IsNullOrWhiteSpace(json))
            return new Dictionary<string, string>();

        return JsonSerializer.Deserialize<Dictionary<string, string>>(json, JsonOptions)
            ?? new Dictionary<string, string>();
    }

    private static IReadOnlyList<string> ReadStringList(MySqlDataReader reader, string name)
    {
        string? json = ReadNullableString(reader, name);
        if (String.IsNullOrWhiteSpace(json))
            return [];

        return JsonSerializer.Deserialize<string[]>(json, JsonOptions) ?? [];
    }

    private static string? ReadNullableString(MySqlDataReader reader, string name)
    {
        int ordinal = reader.GetOrdinal(name);
        return reader.IsDBNull(ordinal) ? null : reader.GetString(ordinal);
    }

    private static DateTimeOffset ReadUtcDateTimeOffset(MySqlDataReader reader, string name)
    {
        DateTime value = reader.GetDateTime(name);
        return new DateTimeOffset(DateTime.SpecifyKind(value, DateTimeKind.Utc));
    }

    private sealed record LauncherConfigRow(
        string ConfigKey,
        int ServiceVersion,
        string ServerName,
        string? ServerStatusUrl,
        string NewsUrl,
        string PatchManifestUrl,
        string? RuntimeCatalogUrl,
        string? LoginUrl,
        string? AccountCreateUrl,
        string? ClientLoginUrl,
        string? PatchBaseUrl,
        string TargetBootVersion,
        string TargetGameVersion);
}

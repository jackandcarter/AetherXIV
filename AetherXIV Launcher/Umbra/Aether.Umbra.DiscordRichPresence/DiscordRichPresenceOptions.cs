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

namespace Aether.Umbra.DiscordRichPresence;

/// <summary>
/// Persistent configuration for the Discord Rich Presence plugin.
/// Written to the plugin config directory on first start so users can
/// override the application id, status text and artwork without rebuilding.
/// </summary>
public sealed record DiscordRichPresenceOptions(
    [property: JsonPropertyName("client_id")] string ClientId,
    [property: JsonPropertyName("details")] string? Details,
    [property: JsonPropertyName("state")] string? State,
    [property: JsonPropertyName("large_image_key")] string? LargeImageKey,
    [property: JsonPropertyName("large_image_text")] string? LargeImageText)
{
    public const string FileName = "discord-rich-presence.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static DiscordRichPresenceOptions Default { get; } = new(
        "1537597448803975208",
        null,
        null,
        "logo",
        "Final Fantasy XIV 1.X");

    public static DiscordRichPresenceOptions Load(string path)
    {
        if (File.Exists(path))
        {
            try
            {
                string json = File.ReadAllText(path);
                DiscordRichPresenceOptions? options =
                    JsonSerializer.Deserialize<DiscordRichPresenceOptions>(json, JsonOptions);
                if (options is not null && !string.IsNullOrWhiteSpace(options.ClientId))
                    return options;
            }
            catch (Exception ex)
            {
                File.AppendAllText(
                    path + ".error.log",
                    $"{DateTimeOffset.Now:O} {ex}{Environment.NewLine}");
            }
        }

        DiscordRichPresenceOptions defaults = Default;
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
            File.WriteAllText(path, JsonSerializer.Serialize(defaults, JsonOptions));
        }
        catch
        {
            // The plugin still works with in-memory defaults if config cannot be written.
        }

        return defaults;
    }
}

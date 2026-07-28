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

namespace AetherXIV.Launcher.Core;

public sealed record UmbraPluginManifest(
    [property: JsonPropertyName("id")] string Id,
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("version")] string Version,
    [property: JsonPropertyName("api_version")] string ApiVersion,
    [property: JsonPropertyName("entry")] string Entry,
    [property: JsonPropertyName("minimum_framework_version")] string MinimumFrameworkVersion,
    [property: JsonPropertyName("enabled")] bool Enabled)
{
    [JsonPropertyName("entry_type")]
    public string? EntryType { get; init; }

    [JsonPropertyName("capabilities")]
    public IReadOnlyList<string> Capabilities { get; init; } = Array.Empty<string>();

    public static UmbraPluginManifest Load(string path)
    {
        string json = File.ReadAllText(path);
        UmbraPluginManifest? manifest = JsonSerializer.Deserialize<UmbraPluginManifest>(json);
        if (manifest is null)
            throw new InvalidDataException("Umbra plugin manifest could not be read.");

        manifest.Validate();
        return manifest;
    }

    public void Validate()
    {
        Require(Id, "id");
        Require(Name, "name");
        Require(Version, "version");
        Require(ApiVersion, "api_version");
        Require(Entry, "entry");
        Require(MinimumFrameworkVersion, "minimum_framework_version");

        if (Path.IsPathRooted(Entry)
            || Entry.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries).Any(part => part == ".."))
        {
            throw new InvalidDataException("Umbra plugin entry must be a relative assembly path.");
        }

        if (EntryType is not null && string.IsNullOrWhiteSpace(EntryType))
            throw new InvalidDataException("Umbra plugin entry_type must be omitted or contain a type name.");
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new InvalidDataException($"Umbra plugin manifest is missing {name}.");
    }
}

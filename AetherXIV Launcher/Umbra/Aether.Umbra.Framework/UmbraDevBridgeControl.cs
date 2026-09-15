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
using System.Security.Cryptography;

namespace Aether.Umbra.Framework;

public sealed record UmbraDevBridgeControl(
    [property: JsonPropertyName("enabled")] bool Enabled,
    [property: JsonPropertyName("port")] int Port,
    [property: JsonPropertyName("updated_at")] DateTimeOffset UpdatedAt,
    [property: JsonPropertyName("token")] string? Token = null)
{
    public const int TokenBytes = 32;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public static UmbraDevBridgeControl Ensure(string path, bool enabled, int port)
    {
        if (TryRead(path) is { } existing)
        {
            if (IsValidToken(existing.Token))
                return existing;

            UmbraDevBridgeControl repaired = existing with
            {
                Token = CreateToken(),
                UpdatedAt = DateTimeOffset.UtcNow
            };
            Write(path, repaired);
            return repaired;
        }

        UmbraDevBridgeControl control = new(enabled, port, DateTimeOffset.UtcNow, CreateToken());
        Write(path, control);
        return control;
    }

    public static UmbraDevBridgeControl BeginSession(string path, bool enabled, int port)
    {
        UmbraDevBridgeControl? existing = TryRead(path);
        UmbraDevBridgeControl control = new(
            existing?.Enabled ?? enabled,
            existing?.Port ?? port,
            DateTimeOffset.UtcNow,
            CreateToken());
        Write(path, control);
        return control;
    }

    public static UmbraDevBridgeControl? TryRead(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;

            string json = File.ReadAllText(path);
            UmbraDevBridgeControl? control = JsonSerializer.Deserialize<UmbraDevBridgeControl>(json, JsonOptions);
            if (control is null)
                return null;

            return control with
            {
                Port = control.Port is >= 1024 and <= 65535
                    ? control.Port
                    : UmbraRuntimeOptions.DefaultDevBridgePort
            };
        }
        catch
        {
            return null;
        }
    }

    public static void Write(string path, UmbraDevBridgeControl control)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        File.WriteAllText(path, JsonSerializer.Serialize(control, JsonOptions));
    }

    internal static bool IsValidToken(string? token)
    {
        return token is { Length: TokenBytes * 2 }
            && token.All(Uri.IsHexDigit);
    }

    private static string CreateToken()
    {
        return Convert.ToHexString(RandomNumberGenerator.GetBytes(TokenBytes)).ToLowerInvariant();
    }
}

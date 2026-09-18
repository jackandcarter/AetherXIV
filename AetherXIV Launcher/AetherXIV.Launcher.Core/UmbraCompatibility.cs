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

namespace AetherXIV.Launcher.Core;

public static class UmbraCompatibility
{
    public const string CurrentApiVersion = "2.1";

    public const string TargetGameVersion = ClientVersionInfo.TargetGameVersion;

    public const string Known123bGameSha256 = "9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9";

    public static IReadOnlySet<string> KnownGameSha256 { get; } =
        new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Known123bGameSha256
        };

    public static bool IsKnownGameHash(string? sha256)
    {
        return !string.IsNullOrWhiteSpace(sha256) && KnownGameSha256.Contains(sha256.Trim());
    }

    public static string ComputeSha256(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
    }
}

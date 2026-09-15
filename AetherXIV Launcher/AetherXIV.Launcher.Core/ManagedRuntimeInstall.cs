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

namespace AetherXIV.Launcher.Core;

public sealed record ManagedRuntimeInstall(
    string Name,
    string Version,
    string PlatformRid,
    string RuntimeKind,
    string InstallPath,
    string ExecutablePath,
    string PrefixArch,
    IReadOnlyDictionary<string, string> Environment,
    DateTimeOffset InstalledAt)
{
    public WineRuntimeProfile ToWineRuntimeProfile(string prefixPath)
    {
        WineRuntimeProfile profile = WineRuntimeProfile.WinePrefix(
            $"{Name} {Version}",
            prefixPath,
            ExecutablePath,
            Environment);
        if (!String.IsNullOrWhiteSpace(PrefixArch))
            profile.Environment["WINEARCH"] = PrefixArch;

        return profile;
    }
}

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

namespace AetherXIV.Data.Tests;

public sealed class FoundationDayNpcScriptTests
{
    [Theory]
    [InlineData("flame_lieutenant_somber_meadow.lua", "processEventSOMBER")]
    [InlineData("flame_sergeant_mimio_mio.lua", "processEventMIMIO")]
    [InlineData("flame_private_sisimuza_tetemuza.lua", "processEventSISIMUZA")]
    public void UldahFoundationDayNpcDelegatesToSpl000(string fileName, string clientFunction)
    {
        string path = Path.Combine(
            FindDataRoot(),
            "scripts",
            "unique",
            "wil0Town01",
            "PopulaceStandard",
            fileName);
        string script = File.ReadAllText(path);

        Assert.Contains("GetStaticActor(\"Spl000\")", script, StringComparison.Ordinal);
        Assert.Contains($"\"{clientFunction}\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("defaultTalkWithSomber_001", script, StringComparison.Ordinal);
    }

    private static string FindDataRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            string candidate = Path.Combine(directory.FullName, "Data");
            if (Directory.Exists(Path.Combine(candidate, "scripts")))
                return candidate;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository Data directory.");
    }
}

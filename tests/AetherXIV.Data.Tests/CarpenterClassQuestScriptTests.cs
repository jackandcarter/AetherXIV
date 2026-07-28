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

public sealed class CarpenterClassQuestScriptTests
{
    [Fact]
    public void AnaidjaaOffersOnlyTheConfirmedRetailCarpenterChain()
    {
        string script = ReadScript(
            "unique",
            "fst0Town01a",
            "PopulaceStandard",
            "anaidjaa.lua");

        Assert.Contains("{ id = 110300, name = \"Wdk200\" }", script, StringComparison.Ordinal);
        Assert.Contains("{ id = 110301, name = \"Wdk300\" }", script, StringComparison.Ordinal);
        Assert.Contains("{ id = 110302, name = \"Wdk306\" }", script, StringComparison.Ordinal);
        Assert.Contains("player:CanAcceptClassQuest(quest.id)", script, StringComparison.Ordinal);
        Assert.Contains("\"processEventStart\"", script, StringComparison.Ordinal);
        Assert.DoesNotContain("Wdk100", script, StringComparison.Ordinal);
        Assert.DoesNotContain("110299", script, StringComparison.Ordinal);
    }

    [Fact]
    public void CarpenterOfferRetainsOrdinaryDialogueWhenNoQuestIsEligible()
    {
        string script = ReadScript(
            "unique",
            "fst0Town01a",
            "PopulaceStandard",
            "anaidjaa.lua");

        Assert.Contains("\"defaultTalkWithAnaidjaa_001\"", script, StringComparison.Ordinal);
        Assert.Contains("npc:SetQuestGraphic(player, 0x2)", script, StringComparison.Ordinal);
        Assert.Contains("npc:SetQuestGraphic(player, 0x0)", script, StringComparison.Ordinal);
        Assert.Contains("player:EndEvent()", script, StringComparison.Ordinal);
    }

    private static string ReadScript(params string[] path)
    {
        string root = FindRepositoryRoot();
        return File.ReadAllText(Path.Combine(
            new[] { root, "Data", "scripts" }.Concat(path).ToArray()));
    }

    private static string FindRepositoryRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, "Data"))
                && Directory.Exists(Path.Combine(directory.FullName, "src")))
                return directory.FullName;
            directory = directory.Parent;
        }

        throw new DirectoryNotFoundException("Could not locate the repository root.");
    }
}

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

using System.Xml.Linq;

namespace AetherXIV.Launcher.Core;

public static class ServerXmlWriter
{
    public static XDocument CreateDocument(IEnumerable<ServerProfile> profiles)
    {
        ArgumentNullException.ThrowIfNull(profiles);

        XElement root = new("Servers");
        foreach (ServerProfile profile in profiles)
        {
            profile.Validate();
            root.Add(new XElement("Server",
                new XAttribute("Name", profile.Name),
                new XAttribute("Address", profile.Host),
                new XAttribute("LoginUrl", profile.LoginUrl)));
        }

        return new XDocument(new XDeclaration("1.0", "utf-8", "yes"), root);
    }

    public static string ToXml(IEnumerable<ServerProfile> profiles)
    {
        return CreateDocument(profiles).ToString(SaveOptions.DisableFormatting);
    }

    public static void Write(string path, IEnumerable<ServerProfile> profiles)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        CreateDocument(profiles).Save(path);
    }
}

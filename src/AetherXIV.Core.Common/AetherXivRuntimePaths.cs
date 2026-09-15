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

using System;
using System.IO;

namespace AetherXIV.Core.Common
{
    /// <summary>
    /// Resolves writable, user-owned locations for service runtime data.
    /// Packaged services must never write into their published directory because
    /// doing so mutates the signed application bundle.
    /// </summary>
    public static class AetherXivRuntimePaths
    {
        public const string LogDirectoryEnvironmentVariable = "AETHERXIV_LOG_DIRECTORY";

        public static string ConfigureLogDirectory(string serviceName)
        {
            string configured = Environment.GetEnvironmentVariable(LogDirectoryEnvironmentVariable);
            string directory = String.IsNullOrWhiteSpace(configured)
                ? GetDefaultLogDirectory(serviceName)
                : configured;

            Directory.CreateDirectory(directory);
            Environment.SetEnvironmentVariable(LogDirectoryEnvironmentVariable, directory);
            return directory;
        }

        public static string GetDefaultLogDirectory(string serviceName)
        {
            string root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (String.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (String.IsNullOrWhiteSpace(root))
                root = Path.GetTempPath();

            return Path.Combine(root, "AetherXIV", "Core", "logs", NormalizeServiceName(serviceName));
        }

        private static string NormalizeServiceName(string serviceName)
        {
            if (String.IsNullOrWhiteSpace(serviceName))
                return "service";

            foreach (char invalid in Path.GetInvalidFileNameChars())
                serviceName = serviceName.Replace(invalid, '_');

            return serviceName.Trim();
        }
    }
}

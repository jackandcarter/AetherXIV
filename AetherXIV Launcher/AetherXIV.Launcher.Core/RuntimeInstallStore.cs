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

namespace AetherXIV.Launcher.Core;

public static class RuntimeInstallStore
{
    private const string ManifestFileName = "aetherxiv-launcher-runtime.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string ApplicationDataRoot => GetApplicationDataRoot();

    public static string RuntimesRoot => Path.Combine(ApplicationDataRoot, "Runtimes");

    public static string RuntimeCacheRoot => Path.Combine(ApplicationDataRoot, "RuntimeCache");

    public static string PrefixesRoot => Path.Combine(ApplicationDataRoot, "Prefixes");

    public static string ManagedPrefixPath => Path.Combine(PrefixesRoot, "ffxiv-1x");

    public static string LogsRoot => Path.Combine(ApplicationDataRoot, "Logs");

    public static string ServerProfilePath => Path.Combine(ApplicationDataRoot, "servers.xml");

    public static string GetApplicationDataRoot()
    {
        string root;
        if (OperatingSystem.IsLinux())
        {
            string? xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
            root = string.IsNullOrWhiteSpace(xdgDataHome)
                ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".local", "share")
                : xdgDataHome;
        }
        else
        {
            root = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            if (string.IsNullOrWhiteSpace(root))
                root = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        }

        return Path.Combine(root, "Demi Dev Unit", "AetherXIV Launcher");
    }

    public static string InstallRootFor(RuntimeArtifact artifact, string? runtimesRoot = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return Path.Combine(runtimesRoot ?? RuntimesRoot, artifact.StableId);
    }

    public static string ManifestPathFor(string installRoot)
    {
        return Path.Combine(Path.GetFullPath(installRoot), ManifestFileName);
    }

    public static void Save(ManagedRuntimeInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        Directory.CreateDirectory(install.InstallPath);
        File.WriteAllText(ManifestPathFor(install.InstallPath), JsonSerializer.Serialize(install, JsonOptions));
    }

    public static ManagedRuntimeInstall Load(string installRoot)
    {
        string json = File.ReadAllText(ManifestPathFor(installRoot));
        ManagedRuntimeInstall? install = JsonSerializer.Deserialize<ManagedRuntimeInstall>(json, JsonOptions);
        return install ?? throw new InvalidOperationException("Managed runtime install manifest could not be read.");
    }

    public static ManagedRuntimeInstall? FindInstalled(RuntimeArtifact artifact, string? runtimesRoot = null)
    {
        string installRoot = InstallRootFor(artifact, runtimesRoot);
        string manifestPath = ManifestPathFor(installRoot);
        if (!File.Exists(manifestPath))
            return null;

        ManagedRuntimeInstall install = Load(installRoot);
        return File.Exists(install.ExecutablePath) ? install : null;
    }

    public static void ResetManagedPrefix()
    {
        if (Directory.Exists(ManagedPrefixPath))
            Directory.Delete(ManagedPrefixPath, true);
    }

    public static string SanitizePathSegment(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            return "runtime";

        char[] invalid = Path.GetInvalidFileNameChars();
        string cleaned = new(value.Select(character =>
            invalid.Contains(character) || char.IsWhiteSpace(character) ? '-' : character).ToArray());

        while (cleaned.Contains("--", StringComparison.Ordinal))
            cleaned = cleaned.Replace("--", "-", StringComparison.Ordinal);

        return cleaned.Trim('-', '.').ToLowerInvariant();
    }
}

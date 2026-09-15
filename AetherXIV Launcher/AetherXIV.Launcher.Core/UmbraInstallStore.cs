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

public static class UmbraInstallStore
{
    private const string ManifestFileName = "umbra-framework.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    public static string Root => Path.Combine(RuntimeInstallStore.ApplicationDataRoot, "Umbra");

    public static string FrameworksRoot => Path.Combine(Root, "Frameworks");

    public static string FrameworkCacheRoot => Path.Combine(Root, "FrameworkCache");

    public static string PluginsRoot => Path.Combine(Root, "Plugins");

    /// <summary>
    /// The built-in plugin catalog and packages that ship inside the launcher
    /// payload next to the bundled framework, as opposed to the user data root
    /// used for installed plugins. The bundled <c>repository.json</c> is the
    /// foundation catalog: built-in entries resolve their packages here.
    /// </summary>
    public static string BundledPluginsRoot => Path.Combine(AppContext.BaseDirectory, "Umbra", "BundledPlugins");

    public static string BundledRepositoryPath => Path.Combine(BundledPluginsRoot, "repository.json");

    public static string LogsRoot => Path.Combine(Root, "Logs");

    public static string FrameworkQuarantineRoot => Path.Combine(Root, "FrameworkQuarantine");

    public static string FrameworkInstallRootFor(UmbraFrameworkArtifact artifact, string? frameworksRoot = null)
    {
        ArgumentNullException.ThrowIfNull(artifact);
        return Path.Combine(frameworksRoot ?? FrameworksRoot, artifact.StableId);
    }

    public static string ManifestPathFor(string installRoot)
    {
        return Path.Combine(Path.GetFullPath(installRoot), ManifestFileName);
    }

    public static void Save(UmbraFrameworkInstall install)
    {
        ArgumentNullException.ThrowIfNull(install);
        Directory.CreateDirectory(install.InstallPath);
        UmbraFrameworkInstall persisted = install.IsBundled
            ? install with
            {
                InstallPath = ".",
                BootstrapPath = "Aether.Umbra.Bootstrap.x86.dll",
                FrameworkPath = "Managed/Aether.Umbra.Framework.dll"
            }
            : install;
        File.WriteAllText(
            ManifestPathFor(install.InstallPath),
            JsonSerializer.Serialize(persisted, JsonOptions));
    }

    public static UmbraFrameworkInstall Load(string installRoot)
    {
        string json = File.ReadAllText(ManifestPathFor(installRoot));
        UmbraFrameworkInstall? install = JsonSerializer.Deserialize<UmbraFrameworkInstall>(json, JsonOptions);
        return install ?? throw new InvalidOperationException("Umbra framework install manifest could not be read.");
    }

    public static UmbraFrameworkInstall? FindInstalled(UmbraFrameworkArtifact artifact, string? frameworksRoot = null)
    {
        string installRoot = FrameworkInstallRootFor(artifact, frameworksRoot);
        string manifestPath = ManifestPathFor(installRoot);
        if (!File.Exists(manifestPath))
            return null;

        UmbraFrameworkInstall install = Load(installRoot);
        return IsUsableAndVerified(install) ? install : null;
    }

    public static UmbraFrameworkInstall? FindLatestInstalled(string? frameworksRoot = null)
    {
        string root = frameworksRoot ?? FrameworksRoot;
        if (!Directory.Exists(root))
            return null;

        return Directory.EnumerateDirectories(root)
            .Select(TryLoad)
            .Where(install => install is not null && IsUsableAndVerified(install))
            .OrderByDescending(install => install!.InstalledAt)
            .FirstOrDefault();
    }

    public static UmbraFrameworkInstall? FindBestAvailable(
        string? gameSha256 = null,
        string? frameworksRoot = null,
        string? bundledBaseDirectory = null)
    {
        List<UmbraFrameworkInstall> candidates = [];
        string root = frameworksRoot ?? FrameworksRoot;
        if (Directory.Exists(root))
        {
            foreach (string installRoot in Directory.EnumerateDirectories(root))
            {
                UmbraFrameworkInstall? install = TryLoadVerified(installRoot);
                if (install is not null)
                    candidates.Add(install);
            }
        }

        UmbraFrameworkInstall? bundled = FindBundled(bundledBaseDirectory);
        if (bundled is not null)
            candidates.Add(bundled);

        return candidates
            .Where(install => string.IsNullOrWhiteSpace(gameSha256) || install.SupportsGameHash(gameSha256))
            .OrderByDescending(install => ParseVersion(install.Version))
            .ThenByDescending(install => install.ChannelSequence)
            .ThenByDescending(install => install.InstalledAt)
            .FirstOrDefault();
    }

    public static long GetHighestInstalledChannelSequence(
        string? frameworksRoot = null,
        string? bundledBaseDirectory = null)
    {
        long highest = FindBundled(bundledBaseDirectory)?.ChannelSequence ?? 0;
        string root = frameworksRoot ?? FrameworksRoot;
        if (!Directory.Exists(root))
            return highest;

        foreach (string installRoot in Directory.EnumerateDirectories(root))
        {
            UmbraFrameworkInstall? install = TryLoadVerified(installRoot);
            if (install is not null)
                highest = Math.Max(highest, install.ChannelSequence);
        }

        return highest;
    }

    public static UmbraFrameworkInstall? FindBundled(string? baseDirectory = null)
    {
        string root = Path.Combine(baseDirectory ?? AppContext.BaseDirectory, "Umbra", "Framework");
        string bootstrapPath = Path.Combine(root, "Aether.Umbra.Bootstrap.x86.dll");
        string frameworkPath = Path.Combine(root, "Managed", "Aether.Umbra.Framework.dll");

        if (!File.Exists(frameworkPath))
            frameworkPath = Path.Combine(root, "Managed", "Aether.Umbra.Framework.exe");

        string receiptPath = ManifestPathFor(root);
        if (!File.Exists(bootstrapPath) || !File.Exists(frameworkPath) || !File.Exists(receiptPath))
            return null;

        UmbraFrameworkInstall install = Load(root) with
        {
            InstallPath = root,
            BootstrapPath = bootstrapPath,
            FrameworkPath = frameworkPath,
            IsBundled = true
        };
        return IsUsableAndVerified(install) ? install : null;
    }

    public static string CreateLogPath(string prefix = "umbra")
    {
        Directory.CreateDirectory(LogsRoot);
        string timestamp = DateTimeOffset.Now.ToString("yyyyMMdd-HHmmss");
        return Path.Combine(LogsRoot, $"{prefix}-{timestamp}.log");
    }

    public static void ResetFrameworks()
    {
        if (Directory.Exists(FrameworksRoot))
            Directory.Delete(FrameworksRoot, true);
    }

    public static UmbraFrameworkInstall? TryLoadVerified(string installRoot)
    {
        try
        {
            UmbraFrameworkInstall install = Load(installRoot);
            return IsUsableAndVerified(install) ? install : null;
        }
        catch
        {
            return null;
        }
    }

    public static string CreateFrameworkQuarantinePath(string installRoot)
    {
        Directory.CreateDirectory(FrameworkQuarantineRoot);
        string name = Path.GetFileName(Path.TrimEndingDirectorySeparator(installRoot));
        return Path.Combine(
            FrameworkQuarantineRoot,
            $"{RuntimeInstallStore.SanitizePathSegment(name)}-{DateTimeOffset.UtcNow:yyyyMMddHHmmssfff}");
    }

    private static UmbraFrameworkInstall? TryLoad(string installRoot)
    {
        try
        {
            return Load(installRoot);
        }
        catch
        {
            return null;
        }
    }

    private static Version ParseVersion(string value) =>
        Version.TryParse(value, out Version? version) ? version : new Version(0, 0);

    private static bool IsUsableAndVerified(UmbraFrameworkInstall? install)
    {
        if (install is null
            || !install.UsesAetherEntrypoints
            || !File.Exists(install.BootstrapPath)
            || !File.Exists(install.FrameworkPath))
            return false;

        try
        {
            install.ValidateIntegrity();
            return true;
        }
        catch
        {
            return false;
        }
    }
}

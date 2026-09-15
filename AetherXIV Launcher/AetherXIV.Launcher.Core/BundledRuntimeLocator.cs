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

public static class BundledRuntimeLocator
{
    public const string DirectoryName = "CompatibilityRuntime";
    public const string ManifestFileName = "aetherxiv-runtime.json";
    public const string ChecksumFileName = "aetherxiv-runtime.sha256";
    private const string ManifestSchema = "aetherxiv.compatibility-runtime.v1";

    private sealed record RuntimeManifest(
        string Schema,
        string Name,
        string Version,
        string PlatformRid,
        string RuntimeKind,
        string ExecutableRelativePath,
        string WineserverRelativePath,
        string? HostLibrariesRelativePath,
        string PrefixArch,
        string SourceUrl,
        string SourceSha256,
        Dictionary<string, string>? Environment);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static string CurrentRuntimeRoot => ResolveRuntimeRoot(AppContext.BaseDirectory, LauncherPlatform.Current);

    public static string ResolveRuntimeRoot(string applicationBaseDirectory, LauncherPlatform platform)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(applicationBaseDirectory);
        ArgumentNullException.ThrowIfNull(platform);

        string baseDirectory = Path.GetFullPath(applicationBaseDirectory);
        return platform.OperatingSystem switch
        {
            LauncherOperatingSystem.MacOS => Path.GetFullPath(
                Path.Combine(baseDirectory, "..", "Resources", DirectoryName)),
            LauncherOperatingSystem.Linux => Path.Combine(baseDirectory, DirectoryName),
            _ => ""
        };
    }

    public static ManagedRuntimeInstall? FindCurrent(out string error)
    {
        if (!LauncherPlatform.Current.RequiresCompatibilityRuntime)
        {
            error = "Windows launches the client natively.";
            return null;
        }

        return TryLoad(CurrentRuntimeRoot, LauncherPlatform.Current, out ManagedRuntimeInstall? install, out error)
            ? install
            : null;
    }

    public static bool TryLoad(
        string runtimeRoot,
        LauncherPlatform platform,
        out ManagedRuntimeInstall? install,
        out string error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(runtimeRoot);
        ArgumentNullException.ThrowIfNull(platform);

        install = null;
        string normalizedRoot = Path.GetFullPath(runtimeRoot);
        string manifestPath = Path.Combine(normalizedRoot, ManifestFileName);
        if (!File.Exists(manifestPath))
        {
            error = $"The AetherXIV compatibility runtime is missing ({manifestPath}). Reinstall or repair this AetherXIV build.";
            return false;
        }

        RuntimeManifest manifest;
        try
        {
            string json = File.ReadAllText(manifestPath);
            manifest = JsonSerializer.Deserialize<RuntimeManifest>(json, JsonOptions)
                ?? throw new InvalidDataException("The runtime manifest is empty.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException or InvalidDataException)
        {
            error = $"The bundled AetherXIV runtime manifest is invalid: {ex.Message}";
            return false;
        }

        if (!String.Equals(manifest.Schema, ManifestSchema, StringComparison.Ordinal))
        {
            error = $"Unsupported AetherXIV runtime manifest schema: {manifest.Schema}";
            return false;
        }

        string expectedPlatform = platform.OperatingSystem switch
        {
            LauncherOperatingSystem.MacOS => "osx-x64-wow64",
            LauncherOperatingSystem.Linux => "linux-x64-wow64",
            _ => ""
        };
        if (!String.Equals(manifest.PlatformRid, expectedPlatform, StringComparison.OrdinalIgnoreCase))
        {
            error = $"The bundled runtime targets {manifest.PlatformRid}, not {expectedPlatform}. Reinstall the package for this platform.";
            return false;
        }

        if (!String.Equals(manifest.RuntimeKind, "wine", StringComparison.OrdinalIgnoreCase))
        {
            error = $"Unsupported bundled runtime kind: {manifest.RuntimeKind}";
            return false;
        }

        if (!TryResolveBundledPath(normalizedRoot, manifest.ExecutableRelativePath, out string executablePath)
            || !File.Exists(executablePath))
        {
            error = "The bundled AetherXIV Wine executable is missing or outside its runtime directory.";
            return false;
        }

        if (!TryResolveBundledPath(normalizedRoot, manifest.WineserverRelativePath, out string wineserverPath)
            || !File.Exists(wineserverPath))
        {
            error = "The bundled AetherXIV wineserver executable is missing or outside its runtime directory.";
            return false;
        }

        if (!File.Exists(Path.Combine(normalizedRoot, ChecksumFileName)))
        {
            error = "The bundled AetherXIV runtime checksum inventory is missing. Reinstall or repair this AetherXIV build.";
            return false;
        }

        Dictionary<string, string> environment = manifest.Environment is null
            ? new Dictionary<string, string>(StringComparer.Ordinal)
            : new Dictionary<string, string>(manifest.Environment, StringComparer.Ordinal);
        if (platform.OperatingSystem == LauncherOperatingSystem.MacOS)
        {
            if (String.IsNullOrWhiteSpace(manifest.HostLibrariesRelativePath)
                || !TryResolveBundledPath(normalizedRoot, manifest.HostLibrariesRelativePath, out string hostLibrariesPath)
                || !Directory.Exists(hostLibrariesPath))
            {
                error = "The bundled AetherXIV macOS host libraries are missing. Reinstall or repair this AetherXIV build.";
                return false;
            }

            // Use the libraries shipped and signed with this exact runtime. Do not
            // inherit Homebrew, CrossOver, PATH Wine, or another provider.
            environment["DYLD_FALLBACK_LIBRARY_PATH"] = hostLibrariesPath;
        }

        install = new ManagedRuntimeInstall(
            manifest.Name,
            manifest.Version,
            manifest.PlatformRid,
            manifest.RuntimeKind,
            normalizedRoot,
            executablePath,
            manifest.PrefixArch,
            environment,
            File.GetLastWriteTimeUtc(manifestPath));
        error = "";
        return true;
    }

    private static bool TryResolveBundledPath(string runtimeRoot, string relativePath, out string resolvedPath)
    {
        resolvedPath = "";
        if (String.IsNullOrWhiteSpace(relativePath) || Path.IsPathRooted(relativePath))
            return false;

        string root = Path.GetFullPath(runtimeRoot);
        string candidate = Path.GetFullPath(Path.Combine(root, relativePath));
        if (!candidate.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            return false;

        resolvedPath = candidate;
        return true;
    }
}

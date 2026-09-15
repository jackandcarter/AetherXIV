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

public sealed record UmbraFrameworkInstall(
    string Name,
    string Version,
    string ApiVersion,
    string PlatformRid,
    string InstallPath,
    string BootstrapPath,
    string FrameworkPath,
    IReadOnlyList<string> SupportedGameSha256,
    DateTimeOffset InstalledAt)
{
    public string ArchiveSha256 { get; init; } = "";

    public IReadOnlyList<UmbraFrameworkFile> Files { get; init; } = Array.Empty<UmbraFrameworkFile>();

    public long ChannelSequence { get; init; }

    public string SigningKeyId { get; init; } = "";

    public bool IsBundled { get; init; }

    public bool UsesAetherEntrypoints =>
        string.Equals(Path.GetFileName(BootstrapPath), "Aether.Umbra.Bootstrap.x86.dll", StringComparison.OrdinalIgnoreCase)
        && (string.Equals(Path.GetFileName(FrameworkPath), "Aether.Umbra.Framework.dll", StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(FrameworkPath), "Aether.Umbra.Framework.exe", StringComparison.OrdinalIgnoreCase));

    public bool SupportsGameHash(string sha256)
    {
        return SupportedGameSha256.Count == 0
            || SupportedGameSha256.Any(candidate => string.Equals(candidate, sha256, StringComparison.OrdinalIgnoreCase));
    }

    public void ValidateIntegrity()
    {
        if (Files.Count == 0)
            throw new InvalidDataException("Umbra framework install does not include signed file integrity metadata.");

        string root = Path.GetFullPath(InstallPath);
        HashSet<string> expectedFiles = new(StringComparer.OrdinalIgnoreCase);
        foreach (UmbraFrameworkFile file in Files)
        {
            string path = ResolveContainedPath(root, file.Path);
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!expectedFiles.Add(relative))
                throw new InvalidDataException($"Umbra framework integrity metadata contains a duplicate path: {file.Path}");

            if (!File.Exists(path))
                throw new FileNotFoundException($"Umbra framework file is missing: {file.Path}", path);

            FileInfo info = new(path);
            if (info.Length != file.SizeBytes)
                throw new InvalidDataException($"Umbra framework file size mismatch: {file.Path}");

            string actual;
            using (FileStream stream = File.OpenRead(path))
                actual = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));
            if (!string.Equals(actual, file.Sha256, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Umbra framework file hash mismatch: {file.Path}");
        }

        foreach (string path in Directory.EnumerateFiles(root, "*", SearchOption.AllDirectories))
        {
            string relative = Path.GetRelativePath(root, path).Replace('\\', '/');
            if (!expectedFiles.Contains(relative)
                && !string.Equals(relative, "umbra-framework.json", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidDataException($"Umbra framework install contains an unsigned file: {relative}");
            }
        }
    }

    private static string ResolveContainedPath(string root, string relativePath)
    {
        string[] parts = relativePath.Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries);
        string target = Path.GetFullPath(Path.Combine([root, .. parts]));
        if (!target.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal)
            && !string.Equals(target, root, StringComparison.Ordinal))
        {
            throw new InvalidDataException($"Umbra framework file path escapes the install root: {relativePath}");
        }

        return target;
    }
}

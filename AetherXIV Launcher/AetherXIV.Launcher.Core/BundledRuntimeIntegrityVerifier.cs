/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Security.Cryptography;

namespace AetherXIV.Launcher.Core;

public sealed record BundledRuntimeIntegrityResult(
    bool IsValid,
    string Message,
    int VerifiedFileCount);

public static class BundledRuntimeIntegrityVerifier
{
    public static async Task<BundledRuntimeIntegrityResult> VerifyAsync(
        ManagedRuntimeInstall install,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);

        string root = Path.GetFullPath(install.InstallPath);
        string inventoryPath = Path.Combine(root, BundledRuntimeLocator.ChecksumFileName);
        if (!File.Exists(inventoryPath))
        {
            return new BundledRuntimeIntegrityResult(
                false,
                "The bundled runtime checksum inventory is missing.",
                0);
        }

        string[] lines;
        try
        {
            lines = await File.ReadAllLinesAsync(inventoryPath, cancellationToken);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            return new BundledRuntimeIntegrityResult(
                false,
                $"The bundled runtime checksum inventory could not be read: {ex.Message}",
                0);
        }

        HashSet<string> seen = new(StringComparer.Ordinal);
        int verified = 0;
        foreach (string line in lines)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (!TryParseInventoryLine(line, out string expectedSha256, out string relativePath))
            {
                return new BundledRuntimeIntegrityResult(
                    false,
                    $"The bundled runtime checksum inventory contains an invalid entry: {line}",
                    verified);
            }

            if (!seen.Add(relativePath))
            {
                return new BundledRuntimeIntegrityResult(
                    false,
                    $"The bundled runtime checksum inventory contains a duplicate path: {relativePath}",
                    verified);
            }

            if (!TryResolveContainedPath(root, relativePath, out string path))
            {
                return new BundledRuntimeIntegrityResult(
                    false,
                    $"The bundled runtime checksum path escapes the runtime directory: {relativePath}",
                    verified);
            }

            if (!File.Exists(path))
            {
                return new BundledRuntimeIntegrityResult(
                    false,
                    $"The bundled runtime file is missing: {relativePath}",
                    verified);
            }

            string actualSha256;
            try
            {
                await using FileStream stream = new(
                    path,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    1024 * 1024,
                    FileOptions.Asynchronous | FileOptions.SequentialScan);
                byte[] hash = await SHA256.HashDataAsync(stream, cancellationToken);
                actualSha256 = Convert.ToHexString(hash);
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                return new BundledRuntimeIntegrityResult(
                    false,
                    $"The bundled runtime file could not be verified ({relativePath}): {ex.Message}",
                    verified);
            }

            if (!string.Equals(actualSha256, expectedSha256, StringComparison.OrdinalIgnoreCase))
            {
                return new BundledRuntimeIntegrityResult(
                    false,
                    $"The bundled runtime file failed its checksum: {relativePath}",
                    verified);
            }

            verified++;
        }

        if (verified == 0)
        {
            return new BundledRuntimeIntegrityResult(
                false,
                "The bundled runtime checksum inventory is empty.",
                0);
        }

        return new BundledRuntimeIntegrityResult(
            true,
            $"Verified {verified} bundled runtime files.",
            verified);
    }

    internal static bool TryParseInventoryLine(
        string line,
        out string sha256,
        out string relativePath)
    {
        sha256 = "";
        relativePath = "";
        if (line.Length < 67 || line[64] != ' ' || line[65] != ' ')
            return false;

        string candidateSha256 = line[..64];
        if (candidateSha256.Any(character => !Uri.IsHexDigit(character)))
            return false;

        string candidatePath = line[66..].Trim();
        if (candidatePath.StartsWith("./", StringComparison.Ordinal))
            candidatePath = candidatePath[2..];
        candidatePath = candidatePath.Replace('\\', '/');
        if (string.IsNullOrWhiteSpace(candidatePath) || Path.IsPathRooted(candidatePath))
            return false;

        sha256 = candidateSha256;
        relativePath = candidatePath;
        return true;
    }

    internal static bool TryResolveContainedPath(
        string root,
        string relativePath,
        out string resolvedPath)
    {
        string normalizedRoot = Path.GetFullPath(root);
        string[] parts = relativePath.Split('/', StringSplitOptions.RemoveEmptyEntries);
        string candidate = Path.GetFullPath(Path.Combine([normalizedRoot, .. parts]));
        if (!candidate.StartsWith(normalizedRoot + Path.DirectorySeparatorChar, StringComparison.Ordinal))
        {
            resolvedPath = "";
            return false;
        }

        resolvedPath = candidate;
        return true;
    }
}

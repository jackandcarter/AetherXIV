/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Security.Cryptography;
using System.Text;

namespace AetherXIV.Launcher.Core;

public sealed record GraphicsCapabilitySnapshot(
    int SchemaVersion,
    string Fingerprint,
    bool DxvkD3D9Available,
    string Summary,
    DateTimeOffset ValidatedAtUtc,
    string RuntimeVersion,
    string? VulkanSummary = null);

public static class GraphicsCapabilityFingerprint
{
    public const int CurrentSchemaVersion = 1;

    public static string Create(
        ManagedRuntimeInstall runtime,
        string dxvkDllPath,
        string probePath,
        string vulkanSummary)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        ArgumentException.ThrowIfNullOrWhiteSpace(dxvkDllPath);
        ArgumentException.ThrowIfNullOrWhiteSpace(probePath);

        StringBuilder input = new();
        input.AppendLine($"schema={CurrentSchemaVersion}");
        input.AppendLine($"platform={runtime.PlatformRid}");
        input.AppendLine($"runtime={runtime.Name}|{runtime.Version}|{runtime.PrefixArch}");
        input.AppendLine($"manifest={FileIdentity(Path.Combine(runtime.InstallPath, BundledRuntimeLocator.ManifestFileName))}");
        input.AppendLine($"dxvk={FileIdentity(dxvkDllPath)}");
        input.AppendLine($"probe={FileIdentity(probePath)}");
        input.AppendLine($"vulkan={vulkanSummary.Trim()}");
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input.ToString())));
    }

    private static string FileIdentity(string path)
    {
        if (!File.Exists(path))
            return $"missing:{Path.GetFullPath(path)}";

        FileInfo file = new(path);
        using FileStream stream = file.OpenRead();
        return $"{file.FullName}|{file.Length}|{Convert.ToHexString(SHA256.HashData(stream))}";
    }
}

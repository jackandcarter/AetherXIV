/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AetherXIV.Launcher.Core;

public sealed record RuntimeReadinessContext(
    ManagedRuntimeInstall RuntimeInstall,
    WineRuntimeProfile RuntimeProfile,
    string PrefixPath,
    UmbraFrameworkInstall? UmbraInstall,
    WineRuntimeConfigurationSettings ConfigurationSettings,
    string? HelperPath = null,
    string? LauncherPath = null,
    string? ReceiptPath = null);

public sealed record RuntimeReadinessAssessment(
    bool ValidationIsCurrent,
    bool ConfigurationIsCurrent,
    string Reason,
    string ValidationFingerprint,
    string ConfigurationFingerprint);

public static class RuntimeReadinessStore
{
    private const int ReceiptSchemaVersion = 1;
    private const int PrefixMarkerSchemaVersion = 1;
    private const string ConfigurationSchema = "aetherxiv-wine-configuration-v2";
    private const string PrefixMarkerFileName = ".aetherxiv-prefix.json";
    private const string ReceiptFileName = "runtime-readiness.json";

    private sealed record RuntimeReadinessReceipt(
        int SchemaVersion,
        string ValidationFingerprint,
        string ConfigurationFingerprint,
        DateTimeOffset ValidatedAtUtc,
        DateTimeOffset? ConfiguredAtUtc);

    private sealed record PrefixMarker(
        int SchemaVersion,
        string GenerationId,
        DateTimeOffset CreatedAtUtc);

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    public static string DefaultReceiptPath =>
        Path.Combine(RuntimeInstallStore.ApplicationDataRoot, ReceiptFileName);

    public static RuntimeReadinessAssessment Assess(RuntimeReadinessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        if (!TryBuildFingerprints(
                context,
                out string validationFingerprint,
                out string configurationFingerprint,
                out string fingerprintError))
        {
            return new RuntimeReadinessAssessment(
                false,
                false,
                fingerprintError,
                validationFingerprint,
                configurationFingerprint);
        }

        RuntimeReadinessReceipt? receipt = TryLoad(context.ReceiptPath ?? DefaultReceiptPath);
        if (receipt is null || receipt.SchemaVersion != ReceiptSchemaVersion)
        {
            return new RuntimeReadinessAssessment(
                false,
                false,
                "No current runtime readiness receipt exists.",
                validationFingerprint,
                configurationFingerprint);
        }

        bool validationCurrent = string.Equals(
            receipt.ValidationFingerprint,
            validationFingerprint,
            StringComparison.Ordinal);
        bool configurationCurrent = validationCurrent
            && string.Equals(
                receipt.ConfigurationFingerprint,
                configurationFingerprint,
                StringComparison.Ordinal);

        string reason = validationCurrent
            ? configurationCurrent
                ? "The bundled runtime, prefix, helper, Umbra payload, and Wine configuration match the verified receipt."
                : "Wine configuration changed after the last verified setup."
            : "The bundled runtime, prefix, helper, Umbra payload, platform, or Launcher changed after the last full validation.";

        return new RuntimeReadinessAssessment(
            validationCurrent,
            configurationCurrent,
            reason,
            validationFingerprint,
            configurationFingerprint);
    }

    public static void RecordValidation(RuntimeReadinessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsurePrefixMarker(context.PrefixPath);
        if (!TryBuildFingerprints(
                context,
                out string validationFingerprint,
                out _,
                out string error))
        {
            throw new InvalidOperationException(error);
        }

        string receiptPath = context.ReceiptPath ?? DefaultReceiptPath;
        RuntimeReadinessReceipt? existing = TryLoad(receiptPath);
        string existingConfiguration = existing is not null
            && string.Equals(existing.ValidationFingerprint, validationFingerprint, StringComparison.Ordinal)
                ? existing.ConfigurationFingerprint
                : "";
        DateTimeOffset? configuredAt = string.IsNullOrWhiteSpace(existingConfiguration)
            ? null
            : existing?.ConfiguredAtUtc;

        Save(
            receiptPath,
            new RuntimeReadinessReceipt(
                ReceiptSchemaVersion,
                validationFingerprint,
                existingConfiguration,
                DateTimeOffset.UtcNow,
                configuredAt));
    }

    public static void RecordConfiguration(RuntimeReadinessContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        EnsurePrefixMarker(context.PrefixPath);
        if (!TryBuildFingerprints(
                context,
                out string validationFingerprint,
                out string configurationFingerprint,
                out string error))
        {
            throw new InvalidOperationException(error);
        }

        string receiptPath = context.ReceiptPath ?? DefaultReceiptPath;
        RuntimeReadinessReceipt? existing = TryLoad(receiptPath);
        if (existing is null
            || !string.Equals(existing.ValidationFingerprint, validationFingerprint, StringComparison.Ordinal))
        {
            throw new InvalidOperationException(
                "Wine configuration cannot be marked ready before the current runtime inputs pass full validation.");
        }

        Save(
            receiptPath,
            existing with
            {
                SchemaVersion = ReceiptSchemaVersion,
                ConfigurationFingerprint = configurationFingerprint,
                ConfiguredAtUtc = DateTimeOffset.UtcNow
            });
    }

    public static void Invalidate(string? receiptPath = null)
    {
        string path = receiptPath ?? DefaultReceiptPath;
        if (File.Exists(path))
            File.Delete(path);
    }

    public static void EnsurePrefixMarker(string prefixPath)
    {
        string normalizedPrefix = Path.GetFullPath(prefixPath);
        if (!IsPrefixInitialized(normalizedPrefix))
            throw new InvalidOperationException("The Wine prefix is not initialized.");

        string markerPath = Path.Combine(normalizedPrefix, PrefixMarkerFileName);
        PrefixMarker? marker = TryLoadPrefixMarker(markerPath);
        if (marker is not null && marker.SchemaVersion == PrefixMarkerSchemaVersion)
            return;

        Save(
            markerPath,
            new PrefixMarker(
                PrefixMarkerSchemaVersion,
                Guid.NewGuid().ToString("N"),
                DateTimeOffset.UtcNow));
    }

    private static bool TryBuildFingerprints(
        RuntimeReadinessContext context,
        out string validationFingerprint,
        out string configurationFingerprint,
        out string error)
    {
        validationFingerprint = "";
        configurationFingerprint = "";
        error = "";

        string prefix = Path.GetFullPath(context.PrefixPath);
        if (!IsPrefixInitialized(prefix))
        {
            error = "The Wine prefix is not initialized.";
            return false;
        }

        string markerPath = Path.Combine(prefix, PrefixMarkerFileName);
        PrefixMarker? marker = TryLoadPrefixMarker(markerPath);
        if (marker is null || marker.SchemaVersion != PrefixMarkerSchemaVersion)
        {
            error = "The Wine prefix has not completed a full AetherXIV validation.";
            return false;
        }

        string helperPath = context.HelperPath
            ?? ClientLaunchHelperLocator.Find()
            ?? "";
        string launcherPath = context.LauncherPath
            ?? Environment.ProcessPath
            ?? AppContext.BaseDirectory;

        StringBuilder validation = new();
        validation.AppendLine($"schema={ReceiptSchemaVersion}");
        validation.AppendLine($"platform={RuntimeInformation.OSDescription}|{RuntimeInformation.OSArchitecture}");
        validation.AppendLine($"launcher={FileIdentity(launcherPath)}");
        validation.AppendLine($"runtime={context.RuntimeInstall.Name}|{context.RuntimeInstall.Version}|{context.RuntimeInstall.PlatformRid}|{context.RuntimeInstall.PrefixArch}");
        validation.AppendLine($"runtime-command={FileIdentity(context.RuntimeProfile.Command)}");
        validation.AppendLine($"runtime-inventory={RuntimeInventoryMetadataFingerprint(context.RuntimeInstall.InstallPath)}");
        validation.AppendLine($"prefix={prefix}|{marker.GenerationId}");
        validation.AppendLine($"helper={FileIdentity(helperPath)}");
        validation.AppendLine($"umbra={UmbraIdentity(context.UmbraInstall)}");
        validationFingerprint = HashText(validation.ToString());

        string? windowsDocumentsPath = null;
        if (context.ConfigurationSettings.UsePrefixLocalDocuments)
        {
            if (!WineRuntimeConfigurator.TryCreatePrefixLocalDocuments(
                    prefix,
                    out WineUserDocumentsTarget documentsTarget,
                    out string documentsError))
            {
                error = documentsError;
                return false;
            }

            windowsDocumentsPath = documentsTarget.WindowsDocumentsPath;
        }

        StringBuilder configuration = new();
        configuration.AppendLine($"schema={ConfigurationSchema}");
        configuration.AppendLine($"prefix={prefix}|{marker.GenerationId}");
        configuration.AppendLine($"os={context.ConfigurationSettings.OperatingSystem}");
        configuration.AppendLine($"prefix-local-documents={context.ConfigurationSettings.UsePrefixLocalDocuments}");
        foreach (WineRegistrySetting setting in WineRuntimeConfigurator.BuildRegistrySettings(
                     context.ConfigurationSettings,
                     windowsDocumentsPath))
        {
            configuration.AppendLine($"registry={setting.Key}|{setting.ValueName}|{setting.Type}|{setting.Data}");
        }

        configurationFingerprint = HashText(configuration.ToString());
        return true;
    }

    private static string RuntimeInventoryMetadataFingerprint(string runtimeRoot)
    {
        string root = Path.GetFullPath(runtimeRoot);
        string manifestPath = Path.Combine(root, BundledRuntimeLocator.ManifestFileName);
        string inventoryPath = Path.Combine(root, BundledRuntimeLocator.ChecksumFileName);
        if (!File.Exists(manifestPath) || !File.Exists(inventoryPath))
            return "missing-runtime-metadata";

        StringBuilder metadata = new();
        metadata.AppendLine($"manifest={HashFile(manifestPath)}");
        metadata.AppendLine($"inventory={HashFile(inventoryPath)}");
        foreach (string line in File.ReadLines(inventoryPath))
        {
            if (string.IsNullOrWhiteSpace(line))
                continue;
            if (!BundledRuntimeIntegrityVerifier.TryParseInventoryLine(
                    line,
                    out string expectedSha256,
                    out string relativePath)
                || !BundledRuntimeIntegrityVerifier.TryResolveContainedPath(root, relativePath, out string path))
            {
                metadata.AppendLine($"invalid={line}");
                continue;
            }

            metadata.AppendLine($"file={expectedSha256}|{relativePath}|{FileIdentity(path)}");
        }

        return HashText(metadata.ToString());
    }

    private static string UmbraIdentity(UmbraFrameworkInstall? install)
    {
        if (install is null)
            return "disabled";

        StringBuilder identity = new();
        identity.AppendLine($"{install.Name}|{install.Version}|{install.ApiVersion}|{install.PlatformRid}|{install.ChannelSequence}|{install.ArchiveSha256}");
        identity.AppendLine($"bootstrap={FileIdentity(install.BootstrapPath)}");
        identity.AppendLine($"framework={FileIdentity(install.FrameworkPath)}");
        foreach (UmbraFrameworkFile file in install.Files.OrderBy(file => file.Path, StringComparer.Ordinal))
            identity.AppendLine($"signed={file.Path}|{file.SizeBytes}|{file.Sha256}");

        string managedDirectory = Path.GetDirectoryName(install.FrameworkPath) ?? "";
        string runtimeRoot = Path.GetFullPath(Path.Combine(managedDirectory, "..", "Runtime"));
        identity.AppendLine($"hostfxr={FileIdentity(Path.Combine(runtimeRoot, "hostfxr.dll"))}");
        identity.AppendLine($"coreclr={FileIdentity(Path.Combine(runtimeRoot, "coreclr.dll"))}");
        return HashText(identity.ToString());
    }

    private static string FileIdentity(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return "missing";

        string normalized = Path.GetFullPath(path);
        if (Directory.Exists(normalized))
        {
            DirectoryInfo directory = new(normalized);
            return $"directory:{normalized}|{directory.LastWriteTimeUtc.Ticks}";
        }
        if (!File.Exists(normalized))
            return $"missing:{normalized}";

        FileInfo info = new(normalized);
        return $"file:{normalized}|{info.Length}|{info.LastWriteTimeUtc.Ticks}";
    }

    private static string HashFile(string path)
    {
        using FileStream stream = File.OpenRead(path);
        return Convert.ToHexString(SHA256.HashData(stream));
    }

    private static string HashText(string value)
    {
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }

    private static bool IsPrefixInitialized(string prefixPath)
    {
        return File.Exists(Path.Combine(prefixPath, "system.reg"))
            && File.Exists(Path.Combine(prefixPath, "user.reg"));
    }

    private static RuntimeReadinessReceipt? TryLoad(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<RuntimeReadinessReceipt>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static PrefixMarker? TryLoadPrefixMarker(string path)
    {
        try
        {
            if (!File.Exists(path))
                return null;
            return JsonSerializer.Deserialize<PrefixMarker>(File.ReadAllText(path), JsonOptions);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or JsonException)
        {
            return null;
        }
    }

    private static void Save<T>(string path, T value)
    {
        string normalized = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(normalized)!);
        string temporary = normalized + $".{Guid.NewGuid():N}.tmp";
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(value, JsonOptions));
            File.Move(temporary, normalized, true);
        }
        finally
        {
            if (File.Exists(temporary))
                File.Delete(temporary);
        }
    }
}

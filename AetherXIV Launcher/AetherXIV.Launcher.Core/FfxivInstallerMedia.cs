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

using System.Buffers.Binary;
using System.Diagnostics;
using System.Text;

namespace AetherXIV.Launcher.Core;

public sealed record FfxivInstallerMediaReport(
    bool IsValid,
    string InstallerPath,
    string MediaRoot,
    IReadOnlyList<string> Errors)
{
    public string DisplayText => IsValid
        ? "Official FFXIV 1.x installer media is ready."
        : string.Join(" ", Errors);
}

public static class FfxivInstallerMedia
{
    public const string BootstrapFileName = "ffxivsetup.exe";
    public const string ProductGuid = "F2C4E6E0-EB78-4824-A212-6DF6AF0E8E82";

    private static readonly string[] RequiredRelativePaths =
    [
        "version.dvm",
        Path.Combine("data", "setup.exe"),
        Path.Combine("data", "setup.ini"),
        Path.Combine("data", "data1.cab"),
        Path.Combine("data", "data2.cab"),
        Path.Combine("data", "data3.cab"),
        Path.Combine("dx_march2009", "DXSETUP.exe")
    ];

    public static FfxivInstallerMediaReport Validate(string installerPath)
    {
        List<string> errors = [];
        if (string.IsNullOrWhiteSpace(installerPath))
        {
            errors.Add("Select the top-level ffxivsetup.exe from the FFXIV 1.x installation media.");
            return new FfxivInstallerMediaReport(false, "", "", errors);
        }

        string normalizedInstallerPath;
        try
        {
            normalizedInstallerPath = Path.GetFullPath(installerPath);
        }
        catch (Exception ex) when (ex is ArgumentException or NotSupportedException or PathTooLongException)
        {
            errors.Add($"The selected installer path is invalid: {ex.Message}");
            return new FfxivInstallerMediaReport(false, installerPath, "", errors);
        }

        string mediaRoot = Path.GetDirectoryName(normalizedInstallerPath) ?? "";
        if (!File.Exists(normalizedInstallerPath))
            errors.Add("The selected installer does not exist.");

        if (!string.Equals(
                Path.GetFileName(normalizedInstallerPath),
                BootstrapFileName,
                StringComparison.OrdinalIgnoreCase))
        {
            errors.Add("Select the top-level ffxivsetup.exe, not data/setup.exe or another executable.");
        }

        if (File.Exists(normalizedInstallerPath)
            && !TryValidatePe32X86(normalizedInstallerPath, out string executableError))
        {
            errors.Add(executableError);
        }

        foreach (string relativePath in RequiredRelativePaths)
        {
            string path = Path.Combine(mediaRoot, relativePath);
            if (!File.Exists(path))
                errors.Add($"Installer media is incomplete: {relativePath.Replace('\\', '/')} is missing.");
        }

        string setupIniPath = Path.Combine(mediaRoot, "data", "setup.ini");
        if (File.Exists(setupIniPath))
            ValidateSetupIni(setupIniPath, errors);

        ValidateSquareEnixMetadata(normalizedInstallerPath, errors);
        return new FfxivInstallerMediaReport(
            errors.Count == 0,
            normalizedInstallerPath,
            mediaRoot,
            errors);
    }

    private static void ValidateSetupIni(string setupIniPath, ICollection<string> errors)
    {
        string setupIni;
        try
        {
            setupIni = File.ReadAllText(setupIniPath);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            errors.Add($"The InstallShield configuration could not be read: {ex.Message}");
            return;
        }

        if (!ContainsSetting(setupIni, "AppName", "FINAL FANTASY XIV"))
            errors.Add("The selected media does not identify itself as the FINAL FANTASY XIV installer.");

        if (!ContainsSetting(setupIni, "ProductGUID", ProductGuid))
            errors.Add("The selected media does not contain the expected FFXIV 1.x product identifier.");

        if (!setupIni.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Any(line => line.StartsWith("EngineVersion=15.", StringComparison.OrdinalIgnoreCase)))
        {
            errors.Add("The selected media does not contain the expected InstallShield 15 engine.");
        }
    }

    private static bool ContainsSetting(string content, string key, string expectedValue)
    {
        string expected = $"{key}={expectedValue}";
        return content.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Any(line => string.Equals(line, expected, StringComparison.OrdinalIgnoreCase));
    }

    private static void ValidateSquareEnixMetadata(string installerPath, ICollection<string> errors)
    {
        if (!File.Exists(installerPath))
            return;

        bool versionResourceMatches = false;
        try
        {
            FileVersionInfo versionInfo = FileVersionInfo.GetVersionInfo(installerPath);
            versionResourceMatches = ContainsSquareEnix(versionInfo.CompanyName)
                && string.Equals(versionInfo.FileDescription, "FINAL FANTASY XIV", StringComparison.OrdinalIgnoreCase);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            // Some non-Windows hosts do not expose PE version resources through
            // FileVersionInfo. The bounded raw resource check below is portable.
        }

        try
        {
            if (!versionResourceMatches && !ContainsPortableSquareEnixMetadata(installerPath))
                errors.Add("The selected bootstrapper does not contain the expected Square Enix FFXIV metadata.");
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException)
        {
            errors.Add($"The selected bootstrapper metadata could not be read: {ex.Message}");
        }
    }

    private static bool ContainsSquareEnix(string? value) =>
        !string.IsNullOrWhiteSpace(value)
        && value.Contains("SQUARE ENIX", StringComparison.OrdinalIgnoreCase);

    private static bool ContainsPortableSquareEnixMetadata(string installerPath)
    {
        const long maximumBootstrapSize = 16L * 1024L * 1024L;
        FileInfo file = new(installerPath);
        if (file.Length <= 0 || file.Length > maximumBootstrapSize)
            return false;

        byte[] executable = File.ReadAllBytes(installerPath);
        return ContainsUtf16(executable, "SQUARE ENIX CO., LTD.")
            && ContainsUtf16(executable, "FINAL FANTASY XIV");
    }

    private static bool ContainsUtf16(ReadOnlySpan<byte> content, string value)
    {
        byte[] encodedValue = Encoding.Unicode.GetBytes(value);
        return content.IndexOf(encodedValue) >= 0;
    }

    private static bool TryValidatePe32X86(string path, out string error)
    {
        error = "";
        try
        {
            using FileStream stream = File.OpenRead(path);
            if (stream.Length < 0x40)
            {
                error = "The selected bootstrapper is not a complete Windows executable.";
                return false;
            }

            Span<byte> dosHeader = stackalloc byte[0x40];
            stream.ReadExactly(dosHeader);
            if (dosHeader[0] != (byte)'M' || dosHeader[1] != (byte)'Z')
            {
                error = "The selected bootstrapper is not a Windows executable.";
                return false;
            }

            int peOffset = BinaryPrimitives.ReadInt32LittleEndian(dosHeader[0x3c..0x40]);
            if (peOffset < 0x40 || peOffset > stream.Length - 26)
            {
                error = "The selected bootstrapper has an invalid Windows executable header.";
                return false;
            }

            stream.Position = peOffset;
            Span<byte> peHeader = stackalloc byte[26];
            stream.ReadExactly(peHeader);
            bool hasSignature = peHeader[0] == (byte)'P'
                && peHeader[1] == (byte)'E'
                && peHeader[2] == 0
                && peHeader[3] == 0;
            ushort machine = BinaryPrimitives.ReadUInt16LittleEndian(peHeader[4..6]);
            ushort optionalHeaderMagic = BinaryPrimitives.ReadUInt16LittleEndian(peHeader[24..26]);
            if (!hasSignature || machine != 0x014c || optionalHeaderMagic != 0x010b)
            {
                error = "The selected bootstrapper is not the expected 32-bit x86 Windows installer.";
                return false;
            }

            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or EndOfStreamException)
        {
            error = $"The selected bootstrapper could not be inspected: {ex.Message}";
            return false;
        }
    }
}

public static class FfxivInstallerLauncher
{
    public static ProcessStartInfo CreateStartInfo(
        FfxivInstallerMediaReport media,
        WineRuntimeProfile runtimeProfile)
    {
        ArgumentNullException.ThrowIfNull(media);
        ArgumentNullException.ThrowIfNull(runtimeProfile);

        if (!media.IsValid)
            throw new InvalidOperationException("FFXIV installer media must pass validation before it can be launched.");

        ProcessStartInfo startInfo;
        if (runtimeProfile.Kind == WineRuntimeKind.NativeWindows)
        {
            startInfo = new ProcessStartInfo
            {
                FileName = media.InstallerPath,
                WorkingDirectory = media.MediaRoot,
                UseShellExecute = false,
                CreateNoWindow = false
            };
        }
        else if (runtimeProfile.Kind == WineRuntimeKind.WinePrefix)
        {
            startInfo = new ProcessStartInfo
            {
                FileName = runtimeProfile.Command,
                Arguments = runtimeProfile.BuildArguments(media.InstallerPath),
                WorkingDirectory = media.MediaRoot,
                UseShellExecute = false,
                CreateNoWindow = false
            };

            foreach (KeyValuePair<string, string> pair in runtimeProfile.Environment)
                startInfo.Environment[pair.Key] = pair.Value;
        }
        else
        {
            throw new InvalidOperationException("The FFXIV installer can only use Windows or AetherXIV's bundled Wine runtime.");
        }

        return startInfo;
    }
}

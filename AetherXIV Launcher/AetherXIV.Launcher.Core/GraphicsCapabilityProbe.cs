/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.ComponentModel;
using System.Diagnostics;

namespace AetherXIV.Launcher.Core;

public sealed record GraphicsCapabilityProbeResult(
    GraphicsCapabilitySnapshot Snapshot,
    string DxvkDllPath,
    string ProbePath,
    string Diagnostic);

public static class GraphicsCapabilityProbe
{
    private const string DxvkDllRelativePath = "dxvk/x32/d3d9.dll";
    private const string ProbeRelativePath = "dxvk/probe/AetherXIV.DxvkProbe.exe";

    public static bool IsSupportedPlatform(LauncherPlatform platform) =>
        platform.OperatingSystem == LauncherOperatingSystem.Linux
        && platform.RuntimeIdentifier.Equals("linux-x64", StringComparison.OrdinalIgnoreCase);

    public static async Task<(string Fingerprint, string VulkanSummary)> GetCurrentFingerprintAsync(
        ManagedRuntimeInstall runtime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);
        string dxvkDllPath = Path.Combine(runtime.InstallPath, DxvkDllRelativePath);
        string probePath = Path.Combine(runtime.InstallPath, ProbeRelativePath);
        string vulkanSummary = await ReadVulkanSummaryAsync(cancellationToken);
        string fingerprint = GraphicsCapabilityFingerprint.Create(
            runtime,
            dxvkDllPath,
            probePath,
            vulkanSummary);
        return (fingerprint, vulkanSummary);
    }

    public static async Task<GraphicsCapabilityProbeResult> ProbeAsync(
        ManagedRuntimeInstall runtime,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(runtime);

        string dxvkDllPath = Path.Combine(runtime.InstallPath, DxvkDllRelativePath);
        string probePath = Path.Combine(runtime.InstallPath, ProbeRelativePath);
        (string fingerprint, string vulkanSummary) = await GetCurrentFingerprintAsync(runtime, cancellationToken);

        if (!File.Exists(dxvkDllPath) || !File.Exists(probePath))
        {
            return Failure(
                runtime,
                fingerprint,
                dxvkDllPath,
                probePath,
                vulkanSummary,
                "The bundled x86 DXVK D3D9 DLL or probe executable is missing.");
        }

        string? winebootPath = ResolveSibling(runtime.ExecutablePath, "wineboot");
        string? wineserverPath = ResolveSibling(runtime.ExecutablePath, "wineserver");
        if (wineserverPath is null)
        {
            return Failure(
                runtime,
                fingerprint,
                dxvkDllPath,
                probePath,
                vulkanSummary,
                "The bundled Wine runtime is missing wineserver.");
        }

        string probePrefix = Path.Combine(
            Path.GetTempPath(),
            "aetherxiv-dxvk-probe",
            Guid.NewGuid().ToString("N"));
        string logPath = Path.Combine(probePrefix, "probe.log");
        try
        {
            Directory.CreateDirectory(probePrefix);
            Dictionary<string, string?> environment = new(StringComparer.Ordinal);
            foreach (KeyValuePair<string, string> pair in runtime.Environment)
                environment[pair.Key] = pair.Value;
            environment["WINEPREFIX"] = probePrefix;
            environment["WINEARCH"] = runtime.PrefixArch;
            environment["WINEDEBUG"] = "-all";
            // A capability test must fail rather than silently load WineD3D.
            environment["WINEDLLOVERRIDES"] = "d3d9=n";

            ProcessResult wineboot = await RunAsync(
                winebootPath ?? runtime.ExecutablePath,
                winebootPath is null ? ["wineboot", "-u"] : ["-u"],
                environment,
                TimeSpan.FromSeconds(90),
                cancellationToken);
            if (wineboot.ExitCode != 0)
            {
                return Failure(
                    runtime,
                    fingerprint,
                    dxvkDllPath,
                    probePath,
                    vulkanSummary,
                    $"Wine prefix initialization failed with exit code {wineboot.ExitCode}: {FirstLine(wineboot.Error, wineboot.Output)}");
            }

            string wow64SystemDirectory = Path.Combine(probePrefix, "drive_c", "windows", "syswow64");
            Directory.CreateDirectory(wow64SystemDirectory);
            File.Copy(dxvkDllPath, Path.Combine(wow64SystemDirectory, "d3d9.dll"), true);

            string windowsProbePath = WinePathMapper.ToWindowsPath(probePath);
            ProcessResult probe = await RunAsync(
                runtime.ExecutablePath,
                [windowsProbePath],
                environment,
                TimeSpan.FromSeconds(90),
                cancellationToken);
            string diagnostic = $"exit_code={probe.ExitCode}; output={FirstLine(probe.Output)}; error={FirstLine(probe.Error)}";
            bool succeeded = probe.ExitCode == 0
                && probe.Output.Contains("AETHERXIV_DXVK_PROBE_OK", StringComparison.Ordinal);
            GraphicsCapabilitySnapshot snapshot = new(
                GraphicsCapabilityFingerprint.CurrentSchemaVersion,
                fingerprint,
                succeeded,
                succeeded
                    ? "DXVK D3D9 is available on the current Linux host."
                    : "The bundled DXVK D3D9 probe could not create a Vulkan-backed D3D9 device.",
                DateTimeOffset.UtcNow,
                runtime.Version,
                vulkanSummary);
            await File.WriteAllTextAsync(logPath, diagnostic + Environment.NewLine, cancellationToken);
            return new GraphicsCapabilityProbeResult(snapshot, dxvkDllPath, probePath, diagnostic);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or Win32Exception)
        {
            return Failure(
                runtime,
                fingerprint,
                dxvkDllPath,
                probePath,
                vulkanSummary,
                $"DXVK probe could not run: {ex.Message}");
        }
        finally
        {
            try
            {
                await RunAsync(
                    wineserverPath,
                    ["-k"],
                    new Dictionary<string, string?> { ["WINEPREFIX"] = probePrefix },
                    TimeSpan.FromSeconds(15),
                    CancellationToken.None);
            }
            catch
            {
                // Probe cleanup must not hide its result.
            }

            try
            {
                if (Directory.Exists(probePrefix))
                    Directory.Delete(probePrefix, true);
            }
            catch
            {
                // The OS may still be releasing a Wine file handle.
            }
        }
    }

    private static GraphicsCapabilityProbeResult Failure(
        ManagedRuntimeInstall runtime,
        string fingerprint,
        string dxvkDllPath,
        string probePath,
        string vulkanSummary,
        string diagnostic)
    {
        return new GraphicsCapabilityProbeResult(
            new GraphicsCapabilitySnapshot(
                GraphicsCapabilityFingerprint.CurrentSchemaVersion,
                fingerprint,
                false,
                diagnostic,
                DateTimeOffset.UtcNow,
                runtime.Version,
                vulkanSummary),
            dxvkDllPath,
            probePath,
            diagnostic);
    }

    private static string? ResolveSibling(string executablePath, string name)
    {
        string? directory = Path.GetDirectoryName(executablePath);
        if (string.IsNullOrWhiteSpace(directory))
            return null;

        string path = Path.Combine(directory, name);
        return File.Exists(path) ? path : null;
    }

    private static async Task<string> ReadVulkanSummaryAsync(CancellationToken cancellationToken)
    {
        ProcessResult result = await RunAsync(
            "vulkaninfo",
            ["--summary"],
            new Dictionary<string, string?>(),
            TimeSpan.FromSeconds(15),
            cancellationToken);
        if (result.ExitCode != 0)
            return "vulkaninfo-unavailable";

        return result.Output.Trim();
    }

    private static async Task<ProcessResult> RunAsync(
        string command,
        IReadOnlyList<string> arguments,
        IReadOnlyDictionary<string, string?> environment,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        ProcessStartInfo startInfo = new()
        {
            FileName = command,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            CreateNoWindow = true
        };
        foreach (string argument in arguments)
            startInfo.ArgumentList.Add(argument);
        foreach (KeyValuePair<string, string?> pair in environment)
            startInfo.Environment[pair.Key] = pair.Value;

        using Process process = new() { StartInfo = startInfo };
        try
        {
            process.Start();
        }
        catch (Win32Exception ex)
        {
            return new ProcessResult(-1, "", ex.Message);
        }

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Task waitTask = process.WaitForExitAsync(cancellationToken);
        Task completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));
        if (completed != waitTask)
        {
            try { process.Kill(true); } catch { }
            return new ProcessResult(-1, await outputTask, await errorTask);
        }

        return new ProcessResult(process.ExitCode, await outputTask, await errorTask);
    }

    private static string FirstLine(params string[] values)
    {
        foreach (string value in values)
        {
            string? line = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries).FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        }

        return "none";
    }

    private sealed record ProcessResult(int ExitCode, string Output, string Error);
}

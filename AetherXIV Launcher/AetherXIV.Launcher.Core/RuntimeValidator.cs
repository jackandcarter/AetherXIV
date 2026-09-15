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

using System.Diagnostics;

namespace AetherXIV.Launcher.Core;

public sealed record RuntimeValidationResult(
    bool IsReady,
    string Message,
    string? VersionText,
    string PrefixPath,
    string LogPath);

public static class RuntimeValidator
{
    public static Task<RuntimeValidationResult> ValidateAsync(
        ManagedRuntimeInstall install,
        string prefixPath,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(install);
        WineRuntimeProfile profile = install.ToWineRuntimeProfile(prefixPath);
        return ValidateAsync(profile, prefixPath, cancellationToken);
    }

    public static async Task<RuntimeValidationResult> ValidateAsync(
        WineRuntimeProfile profile,
        string prefixPath,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(profile, prefixPath, null, cancellationToken);
    }

    public static async Task<RuntimeValidationResult> ValidateAsync(
        WineRuntimeProfile profile,
        string prefixPath,
        UmbraFrameworkInstall? umbraInstall,
        CancellationToken cancellationToken = default)
    {
        return await ValidateAsync(
            profile,
            prefixPath,
            umbraInstall,
            null,
            verifyBundledIntegrity: false,
            cancellationToken);
    }

    public static async Task<RuntimeValidationResult> ValidateAsync(
        WineRuntimeProfile profile,
        string prefixPath,
        UmbraFrameworkInstall? umbraInstall,
        ManagedRuntimeInstall? bundledRuntimeInstall,
        bool verifyBundledIntegrity,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(profile);

        if (profile.Kind == WineRuntimeKind.NativeWindows)
        {
            return new RuntimeValidationResult(
                true,
                "Windows native launch does not require Wine validation.",
                null,
                "",
                RuntimeLaunchDiagnostics.CreateLogPath());
        }

        if (profile.Kind != WineRuntimeKind.WinePrefix)
        {
            return new RuntimeValidationResult(
                false,
                "AetherXIV requires its bundled Wine runtime and isolated managed prefix on this platform.",
                null,
                profile.Name,
                RuntimeLaunchDiagnostics.CreateLogPath("runtime-validate"));
        }

        if (string.IsNullOrWhiteSpace(profile.Command))
            throw new InvalidOperationException("Runtime command is required.");

        if (!File.Exists(profile.Command) && !CommandExistsOnPath(profile.Command))
            throw new FileNotFoundException("Runtime executable was not found.", profile.Command);

        string logPath = RuntimeLaunchDiagnostics.CreateLogPath("runtime-validate");
        RuntimePrerequisiteResult prerequisites = await RuntimePlatformPrerequisites.CheckAsync(
            profile.Command,
            cancellationToken);
        await File.AppendAllTextAsync(
            logPath,
            $"platform_prerequisites={prerequisites.Message}{Environment.NewLine}",
            cancellationToken);
        foreach (string warning in prerequisites.Warnings)
        {
            await File.AppendAllTextAsync(
                logPath,
                $"platform_warning={warning}{Environment.NewLine}",
                cancellationToken);
        }

        if (!prerequisites.IsReady)
        {
            return new RuntimeValidationResult(
                false,
                prerequisites.Message,
                null,
                profile.Name,
                logPath);
        }

        if (verifyBundledIntegrity)
        {
            if (bundledRuntimeInstall is null)
            {
                return new RuntimeValidationResult(
                    false,
                    "The bundled runtime installation metadata is unavailable for integrity verification.",
                    null,
                    profile.Name,
                    logPath);
            }

            BundledRuntimeIntegrityResult integrity = await BundledRuntimeIntegrityVerifier.VerifyAsync(
                bundledRuntimeInstall,
                cancellationToken);
            await File.AppendAllTextAsync(
                logPath,
                $"runtime_integrity={integrity.Message}{Environment.NewLine}",
                cancellationToken);
            if (!integrity.IsValid)
            {
                return new RuntimeValidationResult(
                    false,
                    $"Bundled runtime integrity verification failed. {integrity.Message} Reinstall or repair this AetherXIV build.",
                    null,
                    profile.Name,
                    logPath);
            }
        }

        Dictionary<string, string> environment = new(profile.Environment);
        string runtimeTarget = profile.Name;
        string? normalizedPrefix = null;
        if (profile.Kind == WineRuntimeKind.WinePrefix)
        {
            string selectedPrefix = !string.IsNullOrWhiteSpace(profile.PrefixPath)
                ? profile.PrefixPath
                : prefixPath;
            normalizedPrefix = Path.GetFullPath(selectedPrefix);
            Directory.CreateDirectory(normalizedPrefix);
            environment["WINEPREFIX"] = normalizedPrefix;
            runtimeTarget = normalizedPrefix;
        }

        ProcessRunResult version = await RunAndLogAsync(
            profile.Command,
            "--version",
            environment,
            logPath,
            TimeSpan.FromSeconds(20),
            cancellationToken);
        if (version.ExitCode != 0)
        {
            string detail = FirstUsefulLine(version.Error, version.Output);
            return new RuntimeValidationResult(
                false,
                $"Runtime version check failed with exit code {version.ExitCode}: {detail}",
                version.Output.Trim(),
                runtimeTarget,
                logPath);
        }

        if (normalizedPrefix is null)
        {
            await File.AppendAllTextAsync(
                logPath,
                $"prefix_managed_by_runtime={runtimeTarget}{Environment.NewLine}",
                cancellationToken);
        }
        else if (IsPrefixInitialized(normalizedPrefix))
        {
            await File.AppendAllTextAsync(
                logPath,
                $"prefix_already_initialized={normalizedPrefix}{Environment.NewLine}",
                cancellationToken);
        }
        else
        {
            (string winebootCommand, string winebootArguments) = ResolveWinebootCommand(profile.Command);
            ProcessRunResult winebootResult = await RunAndLogAsync(
                winebootCommand,
                winebootArguments,
                environment,
                logPath,
                TimeSpan.FromSeconds(90),
                cancellationToken);
            if (winebootResult.ExitCode != 0)
            {
                return new RuntimeValidationResult(
                    false,
                    $"Prefix setup failed with exit code {winebootResult.ExitCode}.",
                    version.Output.Trim(),
                    runtimeTarget,
                    logPath);
            }

            string wineserver = ResolveSiblingTool(profile.Command, "wineserver");
            if (File.Exists(wineserver) || CommandExistsOnPath(wineserver))
            {
                await RunAndLogAsync(
                    wineserver,
                    "-w",
                    environment,
                    logPath,
                    TimeSpan.FromSeconds(30),
                    cancellationToken);
            }
        }

        string? helperPath = ClientLaunchHelperLocator.Find();
        if (!string.IsNullOrWhiteSpace(helperPath))
        {
            ProcessRunResult helperProbe = await RunAndLogAsync(
                profile.Command,
                profile.BuildArguments(helperPath, "--probe"),
                environment,
                logPath,
                TimeSpan.FromSeconds(30),
                cancellationToken);

            if (helperProbe.ExitCode != 0)
            {
                return new RuntimeValidationResult(
                    false,
                    "The bundled AetherXIV runtime cannot start the FFXIV 1.x launch helper. Reinstall or repair this AetherXIV build; another Wine provider will not be selected.",
                    version.Output.Trim(),
                    runtimeTarget,
                    logPath);
            }
        }

        if (umbraInstall is not null)
        {
            string? umbraFailure = await ValidateUmbraRuntimeAsync(
                profile,
                umbraInstall,
                environment,
                logPath,
                cancellationToken);
            if (umbraFailure is not null)
            {
                return new RuntimeValidationResult(
                    false,
                    umbraFailure,
                    version.Output.Trim(),
                    runtimeTarget,
                    logPath);
            }
        }

        return new RuntimeValidationResult(
            true,
            BuildReadyMessage(prerequisites.Warnings, umbraInstall is not null),
            version.Output.Trim(),
            runtimeTarget,
            logPath);
    }

    private static string BuildReadyMessage(IReadOnlyList<string> warnings, bool umbraValidated)
    {
        string message = umbraValidated
            ? "Runtime, Wine prefix, client launch helper, and Umbra x86 .NET runtime are ready."
            : "Runtime, Wine prefix, and client launch helper are ready.";
        return warnings.Count == 0 ? message : $"{message} {string.Join(" ", warnings)}";
    }

    private static async Task<string?> ValidateUmbraRuntimeAsync(
        WineRuntimeProfile profile,
        UmbraFrameworkInstall install,
        Dictionary<string, string> environment,
        string logPath,
        CancellationToken cancellationToken)
    {
        string managedDirectory = Path.GetDirectoryName(install.FrameworkPath) ?? "";
        string appHostPath = string.Equals(
            Path.GetExtension(install.FrameworkPath),
            ".exe",
            StringComparison.OrdinalIgnoreCase)
            ? install.FrameworkPath
            : Path.ChangeExtension(install.FrameworkPath, ".exe");
        string runtimeRoot = Path.GetFullPath(Path.Combine(managedDirectory, "..", "Runtime"));

        if (!File.Exists(appHostPath) || !Directory.Exists(runtimeRoot))
        {
            return "The bundled Umbra runtime is incomplete. Reinstall or rebuild the launcher before enabling Umbra.";
        }

        Dictionary<string, string> probeEnvironment = new(environment)
        {
            ["DOTNET_ROOT"] = WinePathMapper.ToWindowsPath(runtimeRoot),
            ["DOTNET_ROOT_X86"] = WinePathMapper.ToWindowsPath(runtimeRoot)
        };

        ProcessRunResult probe;
        try
        {
            probe = await RunAndLogAsync(
                profile.Command,
                profile.BuildArguments(appHostPath, "--runtime-probe"),
                probeEnvironment,
                logPath,
                TimeSpan.FromSeconds(90),
                cancellationToken);
        }
        catch (TimeoutException)
        {
            return UmbraCompatibilityFailureMessage("The managed runtime probe timed out.");
        }

        if (probe.ExitCode != 0)
        {
            string detail = FirstUsefulLine(probe.Error, probe.Output);
            return UmbraCompatibilityFailureMessage(
                $"The managed runtime probe exited with code {probe.ExitCode}: {detail}");
        }

        if (!probe.Output.Contains("AETHER_UMBRA_MANAGED_RUNTIME_OK", StringComparison.Ordinal))
            return UmbraCompatibilityFailureMessage("The managed runtime probe did not complete its worker-thread checks.");

        return null;
    }

    private static string UmbraCompatibilityFailureMessage(string detail)
    {
        return "This runtime can launch the FFXIV 1.x helper, but cannot safely host Umbra's 32-bit .NET 10 worker threads. "
            + $"{detail} Reinstall or repair this AetherXIV build. Umbra launch remains blocked rather than switching to another Wine provider.";
    }

    private static string FirstUsefulLine(params string[] values)
    {
        foreach (string value in values)
        {
            string? line = value.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(line))
                return line;
        }

        return "No additional error detail was reported.";
    }

    private static async Task<ProcessRunResult> RunAndLogAsync(
        string command,
        string arguments,
        IReadOnlyDictionary<string, string> environment,
        string logPath,
        TimeSpan timeout,
        CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(logPath)!);
        await File.AppendAllTextAsync(logPath, $"$ {command} {arguments}{Environment.NewLine}", cancellationToken);

        ProcessStartInfo startInfo = new()
        {
            FileName = command,
            Arguments = arguments,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            CreateNoWindow = true
        };

        foreach (KeyValuePair<string, string> pair in environment)
            startInfo.Environment[pair.Key] = pair.Value;

        using Process process = new() { StartInfo = startInfo };
        process.Start();

        Task<string> outputTask = process.StandardOutput.ReadToEndAsync(cancellationToken);
        Task<string> errorTask = process.StandardError.ReadToEndAsync(cancellationToken);
        Task waitTask = process.WaitForExitAsync(cancellationToken);
        Task completed = await Task.WhenAny(waitTask, Task.Delay(timeout, cancellationToken));
        if (completed != waitTask)
        {
            try
            {
                process.Kill(true);
            }
            catch
            {
                // The process may have exited between timeout and kill.
            }

            throw new TimeoutException($"Runtime command timed out: {command} {arguments}");
        }

        string output = await outputTask;
        string error = await errorTask;
        await File.AppendAllTextAsync(
            logPath,
            output + error + $"exit={process.ExitCode}{Environment.NewLine}",
            cancellationToken);

        return new ProcessRunResult(process.ExitCode, output, error);
    }

    private static string ResolveSiblingTool(string command, string toolName)
    {
        string? directory = Path.GetDirectoryName(command);
        if (string.IsNullOrWhiteSpace(directory))
            return toolName;

        string candidate = Path.Combine(directory, toolName);
        return File.Exists(candidate) ? candidate : toolName;
    }

    private static (string Command, string Arguments) ResolveWinebootCommand(string wineCommand)
    {
        string? directory = Path.GetDirectoryName(wineCommand);
        if (!string.IsNullOrWhiteSpace(directory))
        {
            string sibling = Path.Combine(directory, "wineboot");
            if (File.Exists(sibling))
                return (sibling, "-u");
        }

        if (CommandExistsOnPath("wineboot"))
            return ("wineboot", "-u");

        return (wineCommand, "wineboot -u");
    }

    private static bool IsPrefixInitialized(string prefixPath)
    {
        return File.Exists(Path.Combine(prefixPath, "system.reg"))
            && File.Exists(Path.Combine(prefixPath, "user.reg"));
    }

    private static bool CommandExistsOnPath(string command)
    {
        if (command.Contains(Path.DirectorySeparatorChar)
            || command.Contains(Path.AltDirectorySeparatorChar))
        {
            return false;
        }

        string? path = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrWhiteSpace(path))
            return false;

        return path.Split(Path.PathSeparator).Any(directory =>
            File.Exists(Path.Combine(directory, command)));
    }

    private sealed record ProcessRunResult(int ExitCode, string Output, string Error);
}

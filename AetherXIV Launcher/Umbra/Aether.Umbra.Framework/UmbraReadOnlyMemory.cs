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

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Security.Cryptography;

namespace Aether.Umbra.Framework;

public sealed class UmbraReadOnlyMemory(UmbraRuntimeLog log)
{
    public const int MaxPeekBytes = 4096;
    public const int MaxScanBytes = 1024 * 1024;
    public const int MaxScanMatches = 128;
    private readonly object buildGate = new();
    private UmbraClientBuildStatus? cachedBuildStatus;

    public UmbraMemoryPeekResult Peek(nuint address, int size)
    {
        return PeekCore(address, size, logSuccess: true);
    }

    private UmbraMemoryPeekResult PeekCore(nuint address, int size, bool logSuccess)
    {
        if (address == 0)
            return UmbraMemoryPeekResult.Failed(address, size, "address is zero");

        if (size <= 0 || size > MaxPeekBytes)
            return UmbraMemoryPeekResult.Failed(address, size, $"size must be 1..{MaxPeekBytes}");

        byte[] bytes = new byte[size];
        if (!ReadProcessMemory(GetCurrentProcess(), address, bytes, bytes.Length, out nuint read) || read == 0)
        {
            string error = new Win32Exception(Marshal.GetLastWin32Error()).Message;
            log.Warning($"umbra_memory_peek_failed address=0x{address:X} size={size} error={error}");
            return UmbraMemoryPeekResult.Failed(address, size, error);
        }

        if ((int)read != bytes.Length)
            Array.Resize(ref bytes, (int)read);

        if (logSuccess)
            log.Info($"umbra_memory_peek address=0x{address:X} size={size} read={read}");
        return UmbraMemoryPeekResult.Ok(address, size, bytes);
    }

    public UmbraMemoryPeekResult PeekModule(string? moduleName, long offset, int size)
    {
        return PeekModuleCore(moduleName, offset, size, logSuccess: true);
    }

    internal UmbraMemoryPeekResult PeekModuleForWatch(string? moduleName, long offset, int size)
    {
        return PeekModuleCore(moduleName, offset, size, logSuccess: false);
    }

    private UmbraMemoryPeekResult PeekModuleCore(string? moduleName, long offset, int size, bool logSuccess)
    {
        if (!TryResolveModuleRange(moduleName, offset, size, out UmbraProcessModuleSnapshot? module, out nuint address, out string? error))
            return UmbraMemoryPeekResult.Failed(0, size, error ?? "module range could not be resolved");

        UmbraMemoryPeekResult result = PeekCore(address, size, logSuccess);
        return result with
        {
            Module = module!.Name,
            ModuleOffset = $"0x{offset:X}"
        };
    }

    public UmbraMemoryScanResult Scan(nuint start, int size, UmbraBytePattern pattern)
    {
        if (start == 0)
            return UmbraMemoryScanResult.Failed(start, size, "start address is zero");

        if (size <= 0 || size > MaxScanBytes)
            return UmbraMemoryScanResult.Failed(start, size, $"size must be 1..{MaxScanBytes}");

        if (pattern.Length == 0 || pattern.Length > 64)
            return UmbraMemoryScanResult.Failed(start, size, "pattern length must be 1..64");

        UmbraMemoryPeekResult peek = Peek(start, size);
        if (!peek.Success || peek.Bytes.Length == 0)
            return UmbraMemoryScanResult.Failed(start, size, peek.Error ?? "memory read failed");

        List<string> matches = new();
        byte[] data = peek.Bytes;
        for (int index = 0; index <= data.Length - pattern.Length; index++)
        {
            if (!pattern.Matches(data, index))
                continue;

            matches.Add($"0x{(start + (uint)index):X}");
            if (matches.Count >= MaxScanMatches)
                break;
        }

        log.Info($"umbra_memory_scan start=0x{start:X} size={size} pattern_bytes={pattern.Length} matches={matches.Count}");
        return new UmbraMemoryScanResult(true, $"0x{start:X}", size, matches, null);
    }

    public UmbraMemoryScanResult ScanModule(string? moduleName, long offset, int size, UmbraBytePattern pattern)
    {
        if (!TryResolveModuleRange(moduleName, offset, size, out UmbraProcessModuleSnapshot? module, out nuint address, out string? error))
            return UmbraMemoryScanResult.Failed(0, size, error ?? "module range could not be resolved");

        UmbraMemoryScanResult result = Scan(address, size, pattern);
        return result with
        {
            Module = module!.Name,
            ModuleOffset = $"0x{offset:X}"
        };
    }

    public object ProcessStatus()
    {
        Process process = Process.GetCurrentProcess();
        return new
        {
            process_id = Environment.ProcessId,
            process_name = process.ProcessName,
            module_count = process.Modules.Count,
            main_module = TryGetMainModule(process)
        };
    }

    public IReadOnlyList<UmbraProcessModuleSnapshot> Modules()
    {
        List<UmbraProcessModuleSnapshot> modules = new();
        try
        {
            using Process process = Process.GetCurrentProcess();
            foreach (ProcessModule module in process.Modules)
            {
                try
                {
                    modules.Add(CreateModuleSnapshot(module, includeHash: module == process.MainModule));
                }
                catch
                {
                    // A module may unload while the collection is being read.
                }
            }
        }
        catch (Exception ex)
        {
            log.Warning($"umbra_memory_modules_failed error={ex.Message}");
        }

        return modules
            .OrderBy(module => module.BaseAddressValue)
            .ToArray();
    }

    public UmbraClientBuildStatus ClientBuildStatus()
    {
        lock (buildGate)
        {
            if (cachedBuildStatus is not null)
                return cachedBuildStatus;
        }

        UmbraClientBuildStatus status;
        try
        {
            using Process process = Process.GetCurrentProcess();
            ProcessModule? main = process.MainModule;
            if (main is null)
                status = UnavailableBuild("main process module is unavailable");
            else
            {
                UmbraProcessModuleSnapshot module = CreateModuleSnapshot(main, includeHash: true);
                if (string.IsNullOrWhiteSpace(module.Sha256))
                {
                    status = new UmbraClientBuildStatus(
                        false,
                        null,
                        module,
                        "the main executable could not be hashed");
                }
                else
                {
                    bool verified = UmbraClientBuildCatalog.TryResolveSha256(
                        module.Sha256,
                        out UmbraClientBuildProfile? profile);
                    status = new UmbraClientBuildStatus(
                        verified,
                        profile,
                        module,
                        verified ? null : "no exact Umbra client-build profile matches this executable hash");
                }
            }
        }
        catch (Exception ex)
        {
            status = UnavailableBuild(ex.Message);
        }

        lock (buildGate)
            return cachedBuildStatus ??= status;
    }

    public bool TryGetVerifiedClientBuild(out UmbraClientBuildProfile? profile)
    {
        UmbraClientBuildStatus status = ClientBuildStatus();
        profile = status.Profile;
        return status.Verified && profile is not null;
    }

    private static object? TryGetMainModule(Process process)
    {
        try
        {
            ProcessModule? module = process.MainModule;
            if (module is null)
                return null;

            return new
            {
                name = module.ModuleName,
                base_address = $"0x{module.BaseAddress.ToInt64():X}",
                size = module.ModuleMemorySize
            };
        }
        catch
        {
            return null;
        }
    }

    private bool TryResolveModuleRange(
        string? moduleName,
        long offset,
        int size,
        out UmbraProcessModuleSnapshot? module,
        out nuint address,
        out string? error)
    {
        module = null;
        address = 0;
        error = null;
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            error = "module is required";
            return false;
        }

        if (offset < 0)
        {
            error = "module offset must be non-negative";
            return false;
        }

        if (size <= 0)
        {
            error = "size must be positive";
            return false;
        }

        module = Modules().FirstOrDefault(candidate =>
            string.Equals(candidate.Name, moduleName.Trim(), StringComparison.OrdinalIgnoreCase)
            || string.Equals(Path.GetFileName(candidate.Path), moduleName.Trim(), StringComparison.OrdinalIgnoreCase));
        if (module is null)
        {
            error = $"module is not loaded: {moduleName.Trim()}";
            return false;
        }

        if (offset > module.Size || size > module.Size - offset)
        {
            error = $"module range must remain inside {module.Name} (size 0x{module.Size:X})";
            return false;
        }

        try
        {
            address = checked((nuint)(module.BaseAddressValue + (ulong)offset));
            return true;
        }
        catch (OverflowException)
        {
            error = "module-relative address overflowed";
            return false;
        }
    }

    private static UmbraProcessModuleSnapshot CreateModuleSnapshot(ProcessModule module, bool includeHash)
    {
        string path = module.FileName ?? "";
        return new UmbraProcessModuleSnapshot(
            module.ModuleName ?? Path.GetFileName(path),
            $"0x{module.BaseAddress.ToInt64():X}",
            unchecked((ulong)module.BaseAddress.ToInt64()),
            module.ModuleMemorySize,
            path,
            includeHash ? TryHashFile(path) : null);
    }

    private static string? TryHashFile(string? path)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
                return null;

            using FileStream stream = File.Open(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
            return Convert.ToHexString(SHA256.HashData(stream)).ToLowerInvariant();
        }
        catch
        {
            return null;
        }
    }

    private static UmbraClientBuildStatus UnavailableBuild(string reason)
    {
        return new UmbraClientBuildStatus(false, null, null, reason);
    }

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool ReadProcessMemory(
        nint process,
        nuint baseAddress,
        [Out] byte[] buffer,
        int size,
        out nuint bytesRead);
}

public sealed record UmbraMemoryPeekResult(
    bool Success,
    string Address,
    int RequestedSize,
    int ReadSize,
    string Hex,
    byte[] Bytes,
    string? Error,
    string? Module = null,
    string? ModuleOffset = null)
{
    public static UmbraMemoryPeekResult Ok(nuint address, int requestedSize, byte[] bytes)
    {
        return new(true, $"0x{address:X}", requestedSize, bytes.Length, Convert.ToHexString(bytes), bytes, null);
    }

    public static UmbraMemoryPeekResult Failed(nuint address, int requestedSize, string error)
    {
        return new(false, $"0x{address:X}", requestedSize, 0, "", Array.Empty<byte>(), error);
    }
}

public sealed record UmbraMemoryScanResult(
    bool Success,
    string Start,
    int Size,
    IReadOnlyList<string> Matches,
    string? Error,
    string? Module = null,
    string? ModuleOffset = null)
{
    public static UmbraMemoryScanResult Failed(nuint start, int size, string error)
    {
        return new(false, $"0x{start:X}", size, Array.Empty<string>(), error);
    }
}

public sealed record UmbraProcessModuleSnapshot(
    string Name,
    string BaseAddress,
    [property: System.Text.Json.Serialization.JsonIgnore] ulong BaseAddressValue,
    long Size,
    string Path,
    string? Sha256);

public sealed record UmbraClientBuildStatus(
    bool Verified,
    UmbraClientBuildProfile? Profile,
    UmbraProcessModuleSnapshot? Module,
    string? Reason);

public sealed class UmbraBytePattern
{
    private readonly byte?[] bytes;

    private UmbraBytePattern(byte?[] bytes)
    {
        this.bytes = bytes;
    }

    public int Length => bytes.Length;

    public static bool TryParse(string? value, out UmbraBytePattern pattern, out string? error)
    {
        pattern = new UmbraBytePattern(Array.Empty<byte?>());
        error = null;
        if (string.IsNullOrWhiteSpace(value))
        {
            error = "pattern is required";
            return false;
        }

        string[] parts = value.Split([' ', '\t', '-', ':'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        List<byte?> parsed = new();
        foreach (string part in parts)
        {
            if (part is "?" or "??")
            {
                parsed.Add(null);
                continue;
            }

            if (!byte.TryParse(part, System.Globalization.NumberStyles.HexNumber, null, out byte b))
            {
                error = $"invalid pattern byte: {part}";
                return false;
            }

            parsed.Add(b);
        }

        pattern = new UmbraBytePattern(parsed.ToArray());
        return true;
    }

    public bool Matches(byte[] data, int offset)
    {
        for (int index = 0; index < bytes.Length; index++)
        {
            byte? expected = bytes[index];
            if (expected.HasValue && data[offset + index] != expected.Value)
                return false;
        }

        return true;
    }
}

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
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Aether.Umbra.Framework;

/// <summary>
/// In-process code breakpoints for the development bridge.
///
/// The framework executes inside the client process, so a breakpoint can be
/// installed by writing a relative jump over a known instruction and routing
/// execution through a generated stub that snapshots registers and a hit
/// counter (see <see cref="UmbraBreakpointStubBuilder"/>). The snapshotted
/// values live in a dedicated RWX page that a background poller reads and
/// reports as <c>breakpoint.hit</c> events.
///
/// This is deliberately a single-breakpoint facility: the address, expected
/// bytes, and relocation are explicit (a <see cref="UmbraBreakpointPlan"/>),
/// and installation refuses to patch unless the bytes at the target match
/// exactly — so a changed client build can never be corrupted by a stale
/// relocation.
/// </summary>
public sealed class UmbraBreakpointService : IDisposable
{
    private const uint PageExecuteReadWrite = 0x40;
    private const uint MemCommitReserve = 0x3000;
    private const uint MemRelease = 0x8000;

    // Layout of the single RWX capture page. The stub writes the counter
    // last, so a poller that sees a changed count reads a fully-written
    // register snapshot.
    private const int CaptureBeforeOffset = 0x00;
    private const int CaptureEspOffset = 0x04;
    private const int CaptureAfterOffset = 0x08;
    private const int CaptureCountOffset = 0x0C;
    private const int CaptureEsiOffset = 0x10;
    private const int CaptureEdiOffset = 0x14;
    private const int CaptureEcxOffset = 0x18;
    private const int StubCodeOffset = 0x100;
    private const int PageSize = 0x1000;

    private const int PollIntervalMilliseconds = 8;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly UmbraReadOnlyMemory memory;
    private readonly UmbraRuntimeLog log;
    private readonly UmbraDevBridgeEvents events;
    private readonly string? hitLogDirectory;
    private readonly object gate = new();

    private InstalledBreakpoint? installed;
    private CancellationTokenSource? pollStop;
    private Task? pollTask;

    public UmbraBreakpointService(
        UmbraReadOnlyMemory memory,
        UmbraRuntimeLog log,
        UmbraDevBridgeEvents events,
        string? devBridgeDirectory = null)
    {
        this.memory = memory;
        this.log = log;
        this.events = events;
        hitLogDirectory = devBridgeDirectory;
    }

    /// <summary>
    /// Installs the given plan. Returns the live snapshot. Throws when a
    /// breakpoint is already installed (clear it first), the module is not
    /// loaded, or the bytes at the target do not match the plan.
    /// </summary>
    public object Install(UmbraBreakpointPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.ExpectedBytes.AsSpan().SequenceEqual(plan.HeadBytes.Concat(plan.TailBytes).ToArray()))
            throw new ArgumentException("Breakpoint plan is inconsistent: expected bytes must equal head ++ tail.", nameof(plan));

        lock (gate)
        {
            if (installed is not null)
                throw new InvalidOperationException("A breakpoint is already installed; clear it first.");

            uint moduleBase = ResolveModuleBase(plan.Module);
            uint target = moduleBase + plan.Offset;
            uint jumpBack = target + (uint)(plan.HeadBytes.Length + plan.TailBytes.Length);

            byte[] actual = ReadBytes(target, plan.ExpectedBytes.Length);
            if (!actual.AsSpan().SequenceEqual(plan.ExpectedBytes))
            {
                throw new InvalidDataException(
                    $"Breakpoint signature mismatch at 0x{target:X}; " +
                    $"expected {Convert.ToHexString(plan.ExpectedBytes)} but read {Convert.ToHexString(actual)}.");
            }

            nint page = VirtualAlloc(0, PageSize, MemCommitReserve, PageExecuteReadWrite);
            if (page == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            try
            {
                uint stubAddress = unchecked((uint)(page.ToInt64() + StubCodeOffset));
                uint captureBefore = unchecked((uint)(page.ToInt64() + CaptureBeforeOffset));
                uint captureEsp = unchecked((uint)(page.ToInt64() + CaptureEspOffset));
                uint captureAfter = unchecked((uint)(page.ToInt64() + CaptureAfterOffset));
                uint captureCount = unchecked((uint)(page.ToInt64() + CaptureCountOffset));            uint captureEsi = unchecked((uint)(page.ToInt64() + CaptureEsiOffset));
            uint captureEdi = unchecked((uint)(page.ToInt64() + CaptureEdiOffset));
            uint captureEcx = unchecked((uint)(page.ToInt64() + CaptureEcxOffset));

                byte[] stub = UmbraBreakpointStubBuilder.BuildStub(
                    stubAddress,
                    captureBefore,
                    captureAfter,
                    captureEsp,
                    captureEsi,
                    captureEdi,
                    captureEcx,
                    captureCount,
                    plan.HeadBytes,
                    plan.TailBytes,
                    jumpBack);
                Marshal.Copy(stub, 0, (nint)stubAddress, stub.Length);

                byte[] patch = UmbraBreakpointStubBuilder.BuildPatch(target, stubAddress);
                byte[] original = ReadBytes(target, UmbraBreakpointStubBuilder.PatchLength);
                WriteBytes(target, patch);

                installed = new InstalledBreakpoint(plan, page, target, jumpBack, original);
                StartPolling();

                log.Info(
                    $"umbra_breakpoint_installed name={plan.Name} target=0x{target:X} " +
                    $"stub=0x{stubAddress:X} jump_back=0x{jumpBack:X}");
                events.Record("breakpoint.install", new
                {
                    plan.Name,
                    module = plan.Module ?? "<main>",
                    offset = plan.Offset,
                    target,
                    stub = stubAddress,
                    jump_back = jumpBack
                });

                return BuildStatus(installed);
            }
            catch
            {
                VirtualFree(page, 0, MemRelease);
                throw;
            }
        }
    }

    /// <summary>Removes the installed breakpoint and restores the original bytes.</summary>
    public object Clear()
    {
        lock (gate)
        {
            InstalledBreakpoint? item = installed;
            if (item is null)
                return new { cleared = false, installed = false };

            installed = null;
            StopPolling();

            try
            {
                WriteBytes(item.Target, item.Original);
            }
            catch (Exception ex)
            {
                log.Warning($"umbra_breakpoint_restore_failed target=0x{item.Target:X} error={ex.Message}");
            }

            try
            {
                VirtualFree(item.Page, 0, MemRelease);
            }
            catch
            {
                // The page is already gone; nothing to release.
            }

            log.Info($"umbra_breakpoint_cleared name={item.Plan.Name} target=0x{item.Target:X}");
            events.Record("breakpoint.clear", new { item.Plan.Name, target = item.Target });
            return new { cleared = true, installed = false, name = item.Plan.Name, target = $"0x{item.Target:X}" };
        }
    }

    /// <summary>Live snapshot of the installed breakpoint (or <c>installed = false</c>).</summary>
    public object Status()
    {
        lock (gate)
        {
            if (installed is null)
                return new { installed = false };
            return BuildStatus(installed);
        }
    }

    public void Dispose()
    {
        Clear();
    }

    private object BuildStatus(InstalledBreakpoint item)
    {
        int count = SafeReadInt32(item.Page + CaptureCountOffset);
        uint before = unchecked((uint)SafeReadInt32(item.Page + CaptureBeforeOffset));
        uint esp = unchecked((uint)SafeReadInt32(item.Page + CaptureEspOffset));
        uint after = unchecked((uint)SafeReadInt32(item.Page + CaptureAfterOffset));
        uint esi = unchecked((uint)SafeReadInt32(item.Page + CaptureEsiOffset));
        uint edi = unchecked((uint)SafeReadInt32(item.Page + CaptureEdiOffset));
        uint ecx = unchecked((uint)SafeReadInt32(item.Page + CaptureEcxOffset));
        return new
        {
            installed = true,
            name = item.Plan.Name,
            module = item.Plan.Module ?? "<main>",
            offset = item.Plan.Offset,
            target = $"0x{item.Target:X}",
            jump_back = $"0x{item.JumpBack:X}",
            hits = count,
            eax_before = before,
            eax_after = after,
            mode_byte = after & 0xFF,
            esp,
            x = esi,
            y = edi,
            ecx
        };
    }

    private uint ResolveModuleBase(string? moduleName)
    {
        if (string.IsNullOrWhiteSpace(moduleName))
        {
            using Process process = Process.GetCurrentProcess();
            ProcessModule? main = process.MainModule;
            if (main is not null)
                return unchecked((uint)main.BaseAddress.ToInt64());

            throw new InvalidOperationException("The main process module is unavailable.");
        }

        foreach (UmbraProcessModuleSnapshot module in memory.Modules())
        {
            if (string.Equals(module.Name, moduleName.Trim(), StringComparison.OrdinalIgnoreCase)
                || string.Equals(Path.GetFileName(module.Path), moduleName.Trim(), StringComparison.OrdinalIgnoreCase))
            {
                return unchecked((uint)module.BaseAddressValue);
            }
        }

        throw new InvalidOperationException($"Module is not loaded: {moduleName.Trim()}");
    }

    private byte[] ReadBytes(uint address, int size)
    {
        UmbraMemoryPeekResult result = memory.Peek(address, size);
        if (!result.Success)
            throw new InvalidOperationException($"Could not read memory at 0x{address:X}: {result.Error}");
        return result.Bytes;
    }

    private void WriteBytes(uint address, byte[] bytes)
    {
        uint oldProtect = 0;
        if (!VirtualProtect((nint)address, (nuint)bytes.Length, PageExecuteReadWrite, out oldProtect))
            throw new Win32Exception(Marshal.GetLastWin32Error());

        try
        {
            Marshal.Copy(bytes, 0, (nint)address, bytes.Length);
        }
        finally
        {
            VirtualProtect((nint)address, (nuint)bytes.Length, oldProtect, out _);
        }

        FlushInstructionCache(GetCurrentProcess(), (nint)address, (nuint)bytes.Length);
    }

    private void StartPolling()
    {
        pollStop?.Cancel();
        pollStop = new CancellationTokenSource();
        pollTask = Task.Run(() => PollLoop(pollStop.Token));
    }

    private void StopPolling()
    {
        pollStop?.Cancel();
        try
        {
            pollTask?.Wait(TimeSpan.FromSeconds(1));
        }
        catch
        {
            // The poll loop may already be gone or interrupted mid-delay.
        }

        pollStop?.Dispose();
        pollStop = null;
        pollTask = null;
    }

    private async Task PollLoop(CancellationToken token)
    {
        // A single unexpected exception must never kill the poller silently
        // (that was the 14:45:41 drop-out observed live): the loop body is
        // guarded so every iteration either reports a hit or resumes on the
        // next interval, and only cancellation or a cleared breakpoint ends it.
        while (!token.IsCancellationRequested)
        {
            InstalledBreakpoint? item;
            lock (gate)
                item = installed;

            if (item is null)
                return;

            try
            {
                int count = SafeReadInt32(item.Page + CaptureCountOffset);
                if (count != item.LastSeenCount)
                {
                    item.LastSeenCount = count;
                    uint before = unchecked((uint)SafeReadInt32(item.Page + CaptureBeforeOffset));
                    uint esp = unchecked((uint)SafeReadInt32(item.Page + CaptureEspOffset));
                    uint after = unchecked((uint)SafeReadInt32(item.Page + CaptureAfterOffset));
                    uint esi = unchecked((uint)SafeReadInt32(item.Page + CaptureEsiOffset));
                    uint edi = unchecked((uint)SafeReadInt32(item.Page + CaptureEdiOffset));
                    uint ecx = unchecked((uint)SafeReadInt32(item.Page + CaptureEcxOffset));
                    var hit = new
                    {
                        t = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
                        name = item.Plan.Name,
                        hits = count,
                        eax_before = before,
                        eax_after = after,
                        mode_byte = after & 0xFF,
                        esp,
                        x = esi,
                        y = edi,
                        ecx
                    };
                    // Durably persist each hit to disk so the capture survives
                    // both a wedged dev-bridge poll path and a hard client
                    // crash (the hits otherwise live only in the client's
                    // memory and are lost with the process).
                    AppendHitLog(hit);
                    events.Record("breakpoint.hit", hit);
                }
            }
            catch (Exception ex)
            {
                log.Warning($"umbra_breakpoint_poll_failed error={ex.Message}");
            }

            try
            {
                await Task.Delay(PollIntervalMilliseconds, token).ConfigureAwait(false);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private static int SafeReadInt32(nint address)
    {
        try
        {
            return Marshal.ReadInt32(address);
        }
        catch
        {
            return 0;
        }
    }

    private void AppendHitLog(object hit)
    {
        if (string.IsNullOrWhiteSpace(hitLogDirectory))
            return;

        try
        {
            Directory.CreateDirectory(hitLogDirectory);
            string path = Path.Combine(hitLogDirectory, "breakpoint-hits.jsonl");
            using StreamWriter writer = new(
                File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                System.Text.Encoding.UTF8);
            writer.WriteLine(JsonSerializer.Serialize(hit, JsonOptions));
        }
        catch (Exception ex)
        {
            log.Warning($"umbra_breakpoint_hit_log_failed error={ex.Message}");
        }
    }

    private sealed class InstalledBreakpoint
    {
        public InstalledBreakpoint(
            UmbraBreakpointPlan plan,
            nint page,
            uint target,
            uint jumpBack,
            byte[] original)
        {
            Plan = plan;
            Page = page;
            Target = target;
            JumpBack = jumpBack;
            Original = original;
        }

        public UmbraBreakpointPlan Plan { get; }
        public nint Page { get; }
        public uint Target { get; }
        public uint JumpBack { get; }
        public byte[] Original { get; }
        public int LastSeenCount { get; set; }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern nint VirtualAlloc(nint address, nuint size, uint allocationType, uint protect);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualFree(nint address, nuint size, uint freeType);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool VirtualProtect(nint address, nuint size, uint newProtect, out uint oldProtect);

    [DllImport("kernel32.dll")]
    private static extern bool FlushInstructionCache(nint process, nint address, nuint size);

    [DllImport("kernel32.dll")]
    private static extern nint GetCurrentProcess();
}

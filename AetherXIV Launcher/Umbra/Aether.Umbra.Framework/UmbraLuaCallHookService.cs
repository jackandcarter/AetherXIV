/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * AetherXIV is free software: you can redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;

namespace Aether.Umbra.Framework;

/// <summary>
/// Hooks the client's embedded Lua 5.1 VM at <c>luaD_precall</c> and observes
/// the two verified DesktopWidget tutorial closures by their bytecode source
/// line ranges. The native detour performs only a cheap closure/proto walk on
/// each Lua call; matched calls are written to a fixed native ring and ordinary
/// Lua traffic is deliberately not emitted to the bridge.
/// </summary>
public sealed class UmbraLuaCallHookService : IDisposable
{
    private const uint PageExecuteReadWrite = 0x40;
    private const uint MemCommitReserve = 0x3000;
    private const uint MemRelease = 0x8000;

    // Scalar capture fields at the start of the executable page.
    private const int CaptureEspOffset = 0x00;
    private const int CaptureFuncOffset = 0x04;
    private const int CaptureClosureOffset = 0x08;
    private const int CaptureProtoOffset = 0x0C;
    private const int CaptureLineDefOffset = 0x10;
    private const int CaptureLastLineOffset = 0x14;
    private const int CaptureTagOffset = 0x18;
    private const int CaptureCountIsTutorialModeOffset = 0x1C;
    private const int CaptureCountOrderTutorialModeOffset = 0x20;
    private const int CaptureCountOtherLuaOffset = 0x24; // reserved for ABI compatibility
    private const int CaptureTotalHitsOffset = 0x28;
    private const int CaptureRecordCursorOffset = 0x2C;
    private const int CaptureTimestampLowOffset = 0x30;
    private const int CaptureTimestampHighOffset = 0x34;
    private const int CaptureThreadIdOffset = 0x38;
    private const int CaptureMatchKindOffset = 0x3C;
    private const int StubCodeOffset = 0x100;
    private const int PageSize = 0x1000;

    private const int PollIntervalMilliseconds = 8;
    private const int MaxRecordReadPerPoll = UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower
    };

    private readonly UmbraReadOnlyMemory memory;
    private readonly UmbraRuntimeLog log;
    private readonly UmbraDevBridgeEvents events;
    private readonly string? hitLogDirectory;
    private readonly object gate = new();

    private InstalledHook? installed;
    private CancellationTokenSource? pollStop;
    private Task? pollTask;
    private uint nextRecordSequence;
    private uint recordsEmitted;
    private uint recordsOverrun;
    private uint lastRecordSequence;
    private string? hitLogPath;

    public UmbraLuaCallHookService(
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

    public object Install(UmbraLuaCallHookPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        if (!plan.ExpectedBytes.AsSpan().SequenceEqual(plan.HeadBytes.Concat(plan.TailBytes).ToArray()))
        {
            throw new ArgumentException(
                "Lua call hook plan is inconsistent: expected bytes must equal head ++ tail.",
                nameof(plan));
        }

        lock (gate)
        {
            if (installed is not null)
                throw new InvalidOperationException("A Lua call hook is already installed; clear it first.");

            uint moduleBase = ResolveMainModuleBase();
            uint target = moduleBase + plan.Offset;
            uint jumpBack = target + (uint)(plan.HeadBytes.Length + plan.TailBytes.Length);
            byte[] actual = ReadBytes(target, plan.ExpectedBytes.Length);
            if (!actual.AsSpan().SequenceEqual(plan.ExpectedBytes))
            {
                throw new InvalidDataException(
                    $"Lua call hook signature mismatch at 0x{target:X}; " +
                    $"expected {Convert.ToHexString(plan.ExpectedBytes)} but read {Convert.ToHexString(actual)}.");
            }

            byte[] original = ReadBytes(target, UmbraBreakpointStubBuilder.PatchLength);
            nint page = VirtualAlloc(0, PageSize, MemCommitReserve, PageExecuteReadWrite);
            if (page == 0)
                throw new Win32Exception(Marshal.GetLastWin32Error());

            bool patchWritten = false;
            try
            {
                uint pageAddress = unchecked((uint)page.ToInt64());
                uint stubAddress = pageAddress + StubCodeOffset;
                uint captureEsp = pageAddress + CaptureEspOffset;
                uint captureFunc = pageAddress + CaptureFuncOffset;
                uint captureClosure = pageAddress + CaptureClosureOffset;
                uint captureProto = pageAddress + CaptureProtoOffset;
                uint captureLineDef = pageAddress + CaptureLineDefOffset;
                uint captureLastLine = pageAddress + CaptureLastLineOffset;
                uint captureTag = pageAddress + CaptureTagOffset;
                uint captureCountIsTut = pageAddress + CaptureCountIsTutorialModeOffset;
                uint captureCountOrder = pageAddress + CaptureCountOrderTutorialModeOffset;
                uint captureCountOther = pageAddress + CaptureCountOtherLuaOffset;
                uint captureTotal = pageAddress + CaptureTotalHitsOffset;
                uint captureRecordCursor = pageAddress + CaptureRecordCursorOffset;
                uint captureTimestampLow = pageAddress + CaptureTimestampLowOffset;
                uint captureTimestampHigh = pageAddress + CaptureTimestampHighOffset;
                uint captureThreadId = pageAddress + CaptureThreadIdOffset;
                uint captureMatchKind = pageAddress + CaptureMatchKindOffset;
                uint captureRecordRing = pageAddress + UmbraBreakpointStubBuilder.LuaCallRecordRingOffset;

                byte[] stub = UmbraBreakpointStubBuilder.BuildLuaCallStub(
                    stubAddress,
                    captureEsp,
                    captureFunc,
                    captureClosure,
                    captureProto,
                    captureLineDef,
                    captureLastLine,
                    captureTag,
                    captureCountIsTut,
                    captureCountOrder,
                    captureCountOther,
                    captureTotal,
                    captureRecordCursor,
                    captureTimestampLow,
                    captureTimestampHigh,
                    captureThreadId,
                    captureMatchKind,
                    captureRecordRing,
                    jumpBack);
                Marshal.Copy(stub, 0, (nint)stubAddress, stub.Length);

                WriteBytes(target, UmbraBreakpointStubBuilder.BuildPatch(target, stubAddress));
                patchWritten = true;

                installed = new InstalledHook(plan, page, target, jumpBack, original);
                nextRecordSequence = 0;
                recordsEmitted = 0;
                recordsOverrun = 0;
                lastRecordSequence = 0;
                hitLogPath = CreateHitLogPath();

                log.Info(
                    $"umbra_lua_call_hook_installed name={plan.Name} target=0x{target:X} " +
                    $"stub=0x{stubAddress:X} jump_back=0x{jumpBack:X}");
                events.Record("lua.call.hook.install", new
                {
                    plan.Name,
                    module = "<main>",
                    offset = plan.Offset,
                    target,
                    stub = stubAddress,
                    jump_back = jumpBack,
                    record_capacity = UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity,
                    record_size = UmbraBreakpointStubBuilder.LuaCallRecordSize,
                    is_tutorial_mode_lines = new[]
                    {
                        UmbraLuaCallHookPlan.IsTutorialModeLineDefined,
                        UmbraLuaCallHookPlan.IsTutorialModeLastLineDefined
                    },
                    order_tutorial_mode_lines = new[]
                    {
                        UmbraLuaCallHookPlan.OrderTutorialModeLineDefined,
                        UmbraLuaCallHookPlan.OrderTutorialModeLastLineDefined
                    }
                });

                // Start only after all fallible setup and event publication has
                // succeeded, so an install failure cannot leave a poller behind.
                StartPolling();
                return BuildStatus(installed);
            }
            catch
            {
                installed = null;
                bool pollingStopped = StopPollingUnsafe();
                bool originalRestored = true;
                if (patchWritten)
                {
                    try
                    {
                        WriteBytes(target, original);
                    }
                    catch (Exception restoreException)
                    {
                        originalRestored = false;
                        log.Error($"umbra_lua_call_hook_install_restore_failed target=0x{target:X}", restoreException);
                    }
                }

                if (pollingStopped && originalRestored)
                {
                    VirtualFree(page, 0, MemRelease);
                }
                else
                {
                    // If restoring the target failed, deliberately retain the
                    // page as well: a still-patched jump must never point at
                    // freed memory. This is an exceptional path, but keeping a
                    // live stub is safer than turning an install error into an
                    // immediate client crash.
                    log.Warning($"umbra_lua_call_hook_install_page_retained target=0x{target:X} polling_stopped={pollingStopped} original_restored={originalRestored}");
                }
                throw;
            }
        }
    }

    public object Clear()
    {
        InstalledHook? item;
        lock (gate)
        {
            item = installed;
            if (item is null)
                return new { cleared = false, installed = false };

            // Prevent a new poll iteration from acquiring this page while the
            // caller waits for the current iteration to finish.
            installed = null;
        }

        bool pollingStopped = StopPolling();
        bool originalRestored = true;
        try
        {
            WriteBytes(item.Target, item.Original);
        }
        catch (Exception ex)
        {
            originalRestored = false;
            log.Warning($"umbra_lua_call_hook_restore_failed target=0x{item.Target:X} error={ex.Message}");
        }

        if (!originalRestored)
        {
            // Preserve the item so a later clear can retry. Most importantly,
            // do not claim the hook is cleared or free its page while the
            // target may still contain the detour jump.
            lock (gate)
            {
                if (installed is null)
                    installed = item;
            }

            events.Record("lua.call.hook.clear_failed", new
            {
                item.Plan.Name,
                target = item.Target,
                polling_stopped = pollingStopped
            });
            return new
            {
                cleared = false,
                installed = true,
                name = item.Plan.Name,
                target = $"0x{item.Target:X}",
                polling_stopped = pollingStopped,
                original_restored = false
            };
        }

        if (pollingStopped)
        {
            VirtualFree(item.Page, 0, MemRelease);
        }
        else
        {
                // If a late poll owns the page, the page remains allocated but
                // the target is restored; there is no safe reclamation point
                // in this service instance.
                log.Warning($"umbra_lua_call_hook_page_retained_after_poll_timeout target=0x{item.Target:X}");
        }

        log.Info($"umbra_lua_call_hook_cleared name={item.Plan.Name} target=0x{item.Target:X}");
        events.Record("lua.call.hook.clear", new
        {
            item.Plan.Name,
            target = item.Target,
            polling_stopped = pollingStopped,
            original_restored = true
        });
        return new
        {
            cleared = true,
            installed = false,
            name = item.Plan.Name,
            target = $"0x{item.Target:X}",
            polling_stopped = pollingStopped,
            original_restored = true
        };
    }

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

    private object BuildStatus(InstalledHook item)
    {
        uint matchedHits = unchecked((uint)(
            SafeReadInt32(item.Page + CaptureCountIsTutorialModeOffset)
            + SafeReadInt32(item.Page + CaptureCountOrderTutorialModeOffset)));
        uint totalLuaCalls = unchecked((uint)SafeReadInt32(item.Page + CaptureTotalHitsOffset));
        uint ordinaryLuaCalls = totalLuaCalls >= matchedHits ? totalLuaCalls - matchedHits : 0;
        uint cursor = unchecked((uint)SafeReadInt32(item.Page + CaptureRecordCursorOffset));
        return new
        {
            installed = true,
            name = item.Plan.Name,
            module = "<main>",
            offset = item.Plan.Offset,
            target = $"0x{item.Target:X}",
            jump_back = $"0x{item.JumpBack:X}",
            total_hits = totalLuaCalls,
            total_lua_calls = totalLuaCalls,
            matched_call_hits = matchedHits,
            ordinary_lua_calls = ordinaryLuaCalls,
            other_lua_hits = ordinaryLuaCalls,
            record_cursor = cursor,
            record_capacity = UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity,
            record_size = UmbraBreakpointStubBuilder.LuaCallRecordSize,
            record_stream = "per-call-bridge-events",
            records_emitted = recordsEmitted,
            records_overrun = recordsOverrun,
            last_record_sequence = lastRecordSequence,
            hit_log_path = hitLogPath,
            is_tutorial_mode_hits = SafeReadInt32(item.Page + CaptureCountIsTutorialModeOffset),
            order_tutorial_mode_hits = SafeReadInt32(item.Page + CaptureCountOrderTutorialModeOffset),
            last_func = $"0x{unchecked((uint)SafeReadInt32(item.Page + CaptureFuncOffset)):X}",
            last_tag = SafeReadInt32(item.Page + CaptureTagOffset),
            last_closure = $"0x{unchecked((uint)SafeReadInt32(item.Page + CaptureClosureOffset)):X}",
            last_proto = $"0x{unchecked((uint)SafeReadInt32(item.Page + CaptureProtoOffset)):X}",
            last_linedefined = SafeReadInt32(item.Page + CaptureLineDefOffset),
            last_lastlinedefined = SafeReadInt32(item.Page + CaptureLastLineOffset),
            last_timestamp_tsc_low = unchecked((uint)SafeReadInt32(item.Page + CaptureTimestampLowOffset)),
            last_timestamp_tsc_high = unchecked((uint)SafeReadInt32(item.Page + CaptureTimestampHighOffset)),
            last_thread_id = unchecked((uint)SafeReadInt32(item.Page + CaptureThreadIdOffset)),
            last_match_kind = SafeReadInt32(item.Page + CaptureMatchKindOffset)
        };
    }

    private uint ResolveMainModuleBase()
    {
        using Process process = Process.GetCurrentProcess();
        ProcessModule? main = process.MainModule;
        if (main is not null)
            return unchecked((uint)main.BaseAddress.ToInt64());

        throw new InvalidOperationException("The main process module is unavailable.");
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
        lock (gate)
        {
            pollStop = new CancellationTokenSource();
            CancellationToken token = pollStop.Token;
            pollTask = Task.Run(() => PollLoop(token));
        }
    }

    private bool StopPolling()
    {
        CancellationTokenSource? stop;
        Task? task;
        lock (gate)
        {
            stop = pollStop;
            task = pollTask;
            pollStop = null;
            pollTask = null;
        }

        return WaitForPolling(stop, task);
    }

    // Called only while gate is already held by Install's rollback path.
    // It cannot wait because PollLoop may be waiting for the same gate.
    private bool StopPollingUnsafe()
    {
        CancellationTokenSource? stop = pollStop;
        Task? task = pollTask;
        pollStop = null;
        pollTask = null;
        stop?.Cancel();
        bool completed = task is null || task.IsCompleted;
        if (completed)
            stop?.Dispose();
        return completed;
    }

    private static bool WaitForPolling(
        CancellationTokenSource? stop,
        Task? task)
    {
        if (stop is null && task is null)
            return true;

        stop?.Cancel();
        bool completed = task is null;
        if (task is not null)
        {
            try
            {
                task.Wait(TimeSpan.FromSeconds(5));
                completed = task.IsCompleted;
            }
            catch
            {
                completed = task.IsCompleted;
            }
        }

        if (completed)
            stop?.Dispose();
        return completed;
    }

    private async Task PollLoop(CancellationToken token)
    {
        while (!token.IsCancellationRequested)
        {
            InstalledHook? item;
            lock (gate)
                item = installed;

            if (item is null)
                return;

            try
            {
                EmitPendingRecords(item);
            }
            catch (Exception ex)
            {
                log.Warning($"umbra_lua_call_hook_poll_failed error={ex.Message}");
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

    private void EmitPendingRecords(InstalledHook item)
    {
        uint latest = unchecked((uint)SafeReadInt32(item.Page + CaptureRecordCursorOffset));
        uint distance = unchecked(latest - nextRecordSequence);
        if (distance == 0)
            return;

        if (distance > UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity)
        {
            uint dropped = distance - (uint)UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity;
            recordsOverrun = unchecked(recordsOverrun + dropped);
            nextRecordSequence = unchecked(
                latest - (uint)UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity);
            events.Record("lua.call.records_overrun", new
            {
                dropped,
                latest_sequence = latest,
                capacity = UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity
            });
        }

        int read = 0;
        while (nextRecordSequence != latest && read++ < MaxRecordReadPerPoll)
        {
            uint expected = unchecked(nextRecordSequence + 1);
            int slot = (int)(expected & (UmbraBreakpointStubBuilder.LuaCallRecordRingCapacity - 1));
            nint address = item.Page
                + UmbraBreakpointStubBuilder.LuaCallRecordRingOffset
                + slot * UmbraBreakpointStubBuilder.LuaCallRecordSize;
            uint committed = unchecked((uint)SafeReadInt32(address));
            if (committed != expected)
                return;

            LuaCallRecord record = new(
                Sequence: committed,
                TimestampLow: unchecked((uint)SafeReadInt32(address + 4)),
                TimestampHigh: unchecked((uint)SafeReadInt32(address + 8)),
                ThreadId: unchecked((uint)SafeReadInt32(address + 12)),
                MatchKind: SafeReadInt32(address + 16),
                Func: unchecked((uint)SafeReadInt32(address + 20)),
                Tag: SafeReadInt32(address + 24),
                Closure: unchecked((uint)SafeReadInt32(address + 28)),
                Proto: unchecked((uint)SafeReadInt32(address + 32)),
                LineDefined: SafeReadInt32(address + 36),
                LastLineDefined: SafeReadInt32(address + 40),
                Esp: unchecked((uint)SafeReadInt32(address + 44)));

            string matchedFunction = record.MatchKind switch
            {
                1 => "isTutorialMode",
                2 => "orderTutorialMode",
                _ => "unknown"
            };
            var payload = new
            {
                matched_function = matchedFunction,
                record_sequence = record.Sequence,
                match_kind = record.MatchKind,
                client_timestamp_tsc_low = record.TimestampLow,
                client_timestamp_tsc_high = record.TimestampHigh,
                client_timestamp_tsc = $"0x{((ulong)record.TimestampHigh << 32 | record.TimestampLow):X}",
                client_thread_id = record.ThreadId,
                func = $"0x{record.Func:X}",
                tag = record.Tag,
                closure = $"0x{record.Closure:X}",
                proto = $"0x{record.Proto:X}",
                linedefined = record.LineDefined,
                lastlinedefined = record.LastLineDefined,
                esp = $"0x{record.Esp:X}"
            };

            AppendHitLog(payload);
            events.Record("lua.call", payload);
            recordsEmitted = unchecked(recordsEmitted + 1);
            lastRecordSequence = expected;
            nextRecordSequence = expected;
        }
    }

    private sealed record LuaCallRecord(
        uint Sequence,
        uint TimestampLow,
        uint TimestampHigh,
        uint ThreadId,
        int MatchKind,
        uint Func,
        int Tag,
        uint Closure,
        uint Proto,
        int LineDefined,
        int LastLineDefined,
        uint Esp);

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
            string path = hitLogPath ??= CreateHitLogPath();
            using StreamWriter writer = new(
                File.Open(path, FileMode.Append, FileAccess.Write, FileShare.ReadWrite),
                System.Text.Encoding.UTF8);
            writer.WriteLine(JsonSerializer.Serialize(hit, JsonOptions));
        }
        catch (Exception ex)
        {
            log.Warning($"umbra_lua_call_hook_hit_log_failed error={ex.Message}");
        }
    }

    private string CreateHitLogPath()
    {
        if (string.IsNullOrWhiteSpace(hitLogDirectory))
            throw new InvalidOperationException("Lua hook hit logging is not configured.");

        string directory = Path.Combine(hitLogDirectory, "LuaCallHits");
        Directory.CreateDirectory(directory);
        return Path.Combine(
            directory,
            $"lua-call-hits-{DateTimeOffset.UtcNow:yyyyMMddTHHmmssfffZ}-{Guid.NewGuid():N}.jsonl");
    }

    private sealed class InstalledHook
    {
        public InstalledHook(
            UmbraLuaCallHookPlan plan,
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

        public UmbraLuaCallHookPlan Plan { get; }
        public nint Page { get; }
        public uint Target { get; }
        public uint JumpBack { get; }
        public byte[] Original { get; }
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

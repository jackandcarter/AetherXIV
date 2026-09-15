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

using System.Runtime.InteropServices;

namespace Aether.Umbra.Framework;

/// <summary>
/// Exercises the runtime facilities Umbra depends on after CoreCLR starts.
/// A process-only probe is insufficient because some Wine runtimes can enter
/// managed x86 code but fail when CoreCLR creates a worker thread.
/// </summary>
public static class UmbraRuntimeProbe
{
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(10);

    public static int Run()
    {
        Console.WriteLine(
            $"AETHER_UMBRA_MANAGED_PROBE_BEGIN architecture={RuntimeInformation.ProcessArchitecture} " +
            $"runtime={RuntimeInformation.FrameworkDescription}");

        if (RuntimeInformation.ProcessArchitecture != Architecture.X86 || IntPtr.Size != 4)
            return Fail(10, "Umbra managed runtime probe must execute as Windows x86.");

        Exception? threadError = null;
        int threadId = 0;
        Thread thread = new(() =>
        {
            try
            {
                threadId = Environment.CurrentManagedThreadId;
            }
            catch (Exception ex)
            {
                threadError = ex;
            }
        })
        {
            IsBackground = true,
            Name = "Umbra Runtime Probe"
        };

        thread.Start();
        if (!thread.Join(ProbeTimeout))
            return Fail(11, "CoreCLR did not complete an explicit managed worker thread.");
        if (threadError is not null)
            return Fail(12, $"Explicit managed worker thread failed: {threadError}");
        if (threadId <= 0)
            return Fail(13, "Explicit managed worker thread did not execute.");

        Console.WriteLine($"AETHER_UMBRA_MANAGED_THREAD_OK thread_id={threadId}");

        Task<int> continuation = RunContinuationAsync();
        try
        {
            if (!continuation.Wait(ProbeTimeout))
                return Fail(20, "CoreCLR did not run an asynchronous continuation.");
        }
        catch (Exception ex)
        {
            return Fail(21, $"Asynchronous continuation failed: {ex}");
        }
        if (continuation.Result <= 0)
            return Fail(22, "Asynchronous continuation did not execute.");

        Console.WriteLine($"AETHER_UMBRA_MANAGED_ASYNC_OK thread_id={continuation.Result}");
        Console.WriteLine("AETHER_UMBRA_MANAGED_RUNTIME_OK");
        return 0;
    }

    private static async Task<int> RunContinuationAsync()
    {
        await Task.Yield();
        return Environment.CurrentManagedThreadId;
    }

    private static int Fail(int exitCode, string message)
    {
        Console.Error.WriteLine($"AETHER_UMBRA_MANAGED_RUNTIME_FAILED code={exitCode} message={message}");
        return exitCode;
    }
}

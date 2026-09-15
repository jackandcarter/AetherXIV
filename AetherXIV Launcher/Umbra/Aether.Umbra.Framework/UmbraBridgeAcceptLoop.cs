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

using System;

namespace Aether.Umbra.Framework;

/// <summary>
/// Opaque accepted request handed from the transport to the handler. The
/// Windows-only HttpListener adapter in <see cref="UmbraDevBridgeService"/>
/// derives from this so the accept pump itself stays portable.
/// </summary>
internal abstract class UmbraBridgeRequest
{
}

/// <summary>
/// Single-flight accept pump for the dev bridge HTTP listener.
///
/// The managed HttpListener accept can stall internally (observed live on
/// macOS under Wine: GetContextAsync never returns while new requests queue
/// silently). HttpListener forbids two concurrent accepts on the same
/// listener, so a timed-out accept must never be abandoned and re-issued on
/// the same transport — that is exactly the wedge that killed the bridge
/// mid-observation. The pump therefore:
///
///   1. issues one accept at a time per transport (single-flight);
///   2. when an accept does not complete within the timeout, aborts the
///      transport (unblocking the pending accept) and recreates it;
///   3. observes every pending accept to a terminal state so no fault goes
///      unobserved and no accept is left running on a dead transport.
///
/// The exact same source file is compiled into the portable regression test
/// assembly (Aether.Umbra.Framework.AcceptLoop.Tests) so these recovery
/// paths are exercised on non-Windows hosts where HttpListener cannot run.
/// </summary>
internal sealed class UmbraBridgeAcceptLoop
{
    public delegate Task<UmbraBridgeRequest?> AcceptAsync();
    public delegate bool RestartTransport();
    public delegate void Notify(string eventName, object? payload);

    private readonly Func<Task<UmbraBridgeRequest?>> acceptAsync;
    private readonly Action abort;
    private readonly RestartTransport restartTransport;
    private readonly TimeSpan acceptTimeout;
    private readonly Notify? notify;

    public UmbraBridgeAcceptLoop(
        Func<Task<UmbraBridgeRequest?>> acceptAsync,
        Action abort,
        RestartTransport restartTransport,
        TimeSpan acceptTimeout,
        Notify? notify = null)
    {
        this.acceptAsync = acceptAsync ?? throw new ArgumentNullException(nameof(acceptAsync));
        this.abort = abort ?? throw new ArgumentNullException(nameof(abort));
        this.restartTransport = restartTransport ?? throw new ArgumentNullException(nameof(restartTransport));
        this.acceptTimeout = acceptTimeout > TimeSpan.Zero
            ? acceptTimeout
            : throw new ArgumentOutOfRangeException(nameof(acceptTimeout));
        this.notify = notify;
    }

    public async Task RunAsync(
        Func<UmbraBridgeRequest, CancellationToken, Task> handle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);

        while (!cancellationToken.IsCancellationRequested)
        {
            Task<UmbraBridgeRequest?> pending;
            try
            {
                pending = acceptAsync();
            }
            catch (Exception ex)
            {
                // The transport rejected the accept synchronously (e.g. it
                // was torn down). Retry after a short delay; stop only when
                // the server is stopping.
                notify?.Invoke("bridge.accept.failed", new { type = ex.GetType().Name, message = ex.Message });
                if (cancellationToken.IsCancellationRequested)
                    return;
                try
                {
                    await Task.Delay(TimeSpan.FromMilliseconds(250), cancellationToken).ConfigureAwait(false);
                }
                catch (OperationCanceledException)
                {
                    return;
                }
                continue;
            }

            Task completed = await Task.WhenAny(pending, Task.Delay(acceptTimeout, cancellationToken)).ConfigureAwait(false);
            if (completed == pending)
            {
                UmbraBridgeRequest? request;
                try
                {
                    request = await pending.ConfigureAwait(false);
                }
                catch (Exception ex)
                {
                    // The pending accept faulted (e.g. the listener was
                    // closed under us). Retry; the transport may need a
                    // restart, which the next accept resolves via null.
                    notify?.Invoke("bridge.accept.failed", new { type = ex.GetType().Name, message = ex.Message });
                    if (cancellationToken.IsCancellationRequested)
                        return;
                    continue;
                }

                if (request is null)
                {
                    // The transport went away (e.g. StopAsync raced the
                    // loop). Recreate it unless the server is stopping.
                    if (cancellationToken.IsCancellationRequested || !restartTransport())
                        return;
                    continue;
                }

                _ = Task.Run(() => handle(request, cancellationToken), CancellationToken.None);
                continue;
            }

            // The accept did not complete within the timeout: the transport
            // stalled. Never issue a second accept on the same transport
            // while one is still pending — abort it so the pending accept
            // unblocks, observe it, then recreate the transport and resume.
            notify?.Invoke("bridge.accept.stalled", new { timeout_ms = acceptTimeout.TotalMilliseconds });
            try
            {
                abort();
            }
            catch
            {
                // Best effort; the transport is broken either way.
            }

            try
            {
                // An aborted transport unblocks its pending accept quickly.
                // Bound the wait so a stubborn transport can never wedge the
                // pump again, and observe the task so no fault is unobserved.
                _ = pending.ContinueWith(
                    static t => _ = t.Exception,
                    CancellationToken.None,
                    TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                    TaskScheduler.Default);
                await pending.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // The aborted accept surfaced its fault; expected during
                // stall recovery.
            }

            if (cancellationToken.IsCancellationRequested)
                return;

            if (!restartTransport())
                return;
        }
    }
}

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
/// Single-flight accept pump. A pending accept is normal while idle; only an
/// actual transport failure warrants a restart. Cancellation aborts the listener.
/// </summary>
internal sealed class UmbraBridgeAcceptLoop
{
    private readonly Func<Task<UmbraBridgeRequest?>> acceptAsync;
    private readonly Action abort;
    private readonly Func<bool> restartTransport;
    private readonly Action<string, object?>? notify;

    public UmbraBridgeAcceptLoop(Func<Task<UmbraBridgeRequest?>> acceptAsync,
        Action abort, Func<bool> restartTransport, Action<string, object?>? notify = null)
    {
        this.acceptAsync = acceptAsync ?? throw new ArgumentNullException(nameof(acceptAsync));
        this.abort = abort ?? throw new ArgumentNullException(nameof(abort));
        this.restartTransport = restartTransport ?? throw new ArgumentNullException(nameof(restartTransport));
        this.notify = notify;
    }

    public async Task RunAsync(Func<UmbraBridgeRequest, CancellationToken, Task> handle,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(handle);
        while (!cancellationToken.IsCancellationRequested)
        {
            Task<UmbraBridgeRequest?>? pending = null;
            try
            {
                pending = acceptAsync();
                var request = await pending.WaitAsync(cancellationToken).ConfigureAwait(false);
                if (request is null)
                {
                    if (cancellationToken.IsCancellationRequested || !restartTransport()) return;
                    continue;
                }
                _ = Task.Run(() => handle(request, cancellationToken), CancellationToken.None);
            }
            catch (Exception ex)
            {
                // Observe an accept that completes after cancellation, even if
                // Wine fails to unblock it promptly when the listener is closed.
                if (pending is not null)
                    _ = pending.ContinueWith(static t => _ = t.Exception,
                        CancellationToken.None,
                        TaskContinuationOptions.OnlyOnFaulted | TaskContinuationOptions.ExecuteSynchronously,
                        TaskScheduler.Default);
                try { abort(); } catch { }
                if (cancellationToken.IsCancellationRequested) return;
                notify?.Invoke("bridge.accept.failed", new { type = ex.GetType().Name, message = ex.Message });
                // Retry on a fresh listener, never spin on a faulted transport.
                try { await Task.Delay(250, cancellationToken).ConfigureAwait(false); }
                catch (OperationCanceledException) { return; }
                if (!restartTransport()) return;
            }
        }
    }
}

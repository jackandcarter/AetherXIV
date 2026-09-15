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

using Aether.Umbra.PluginApi;
using DiscordRPC;
using DiscordRPC.Logging;

namespace Aether.Umbra.DiscordRichPresence;

/// <summary>
/// Shows the FFXIV 1.x client in the user's Discord profile while the game runs.
///
/// The status is driven by Discord's local IPC endpoint (<c>\\.\pipe\discord-ipc-0</c>
/// on Windows). The client connects to that endpoint, handshakes with the registered
/// application id, and sends SET_ACTIVITY frames. No OAuth, voice, or guild features
/// are used, so no developer-portal approval is required.
///
/// The "Playing &lt;name&gt;" line shown in Discord is the application name registered
/// in the Discord Developer Portal, not a string set here. The registered app must
/// therefore be named exactly what should appear after "Playing".
/// </summary>
public sealed class DiscordRichPresencePlugin : IUmbraPlugin
{
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly object gate = new();
    private IUmbraPluginContext? context;
    private DiscordRichPresenceOptions options = DiscordRichPresenceOptions.Default;
    private DiscordRpcClient? client;
    private CancellationTokenSource? workerCancellation;
    private Task? worker;
    private bool disposed;

    public string Name => "Discord Rich Presence";

    public void Initialize(IUmbraPluginContext context)
    {
        this.context = context;
        Directory.CreateDirectory(context.ConfigDirectory);
        options = DiscordRichPresenceOptions.Load(Path.Combine(context.ConfigDirectory, DiscordRichPresenceOptions.FileName));

        workerCancellation = CancellationTokenSource.CreateLinkedTokenSource(context.ShutdownToken);
        worker = Task.Run(() => RunWorkerAsync(workerCancellation.Token));
        context.Logger.Info(
            $"initialized client_id={options.ClientId} details={options.Details ?? "none"} state={options.State ?? "none"}");
    }

    public void Update(TimeSpan delta)
    {
        // All work happens on the background worker so callbacks stay within budget.
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
    }

    public void Dispose()
    {
        if (disposed)
            return;

        disposed = true;
        CancellationTokenSource? cancellation;
        Task? task;
        lock (gate)
        {
            cancellation = workerCancellation;
            task = worker;
            workerCancellation = null;
            worker = null;
        }

        if (cancellation is not null)
        {
            cancellation.Cancel();
            try
            {
                task?.Wait(TimeSpan.FromSeconds(2));
            }
            catch (AggregateException ex)
            {
                context?.Logger.Warning($"worker_teardown_error={ex.InnerException?.Message}");
            }
            catch (Exception ex)
            {
                context?.Logger.Warning($"worker_teardown_error={ex.Message}");
            }

            cancellation.Dispose();
        }

        context?.Logger.Info("disposed");
        context = null;
    }

    private void RunWorkerAsync(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                if (client is null)
                {
                    client = CreateClient();
                    if (client is null)
                    {
                        Delay(ReconnectDelay, cancellationToken);
                        continue;
                    }
                }

                if (!client.IsInitialized)
                {
                    if (!TryInitialize(client))
                    {
                        Delay(ReconnectDelay, cancellationToken);
                        continue;
                    }

                    context?.Logger.Info("connected=true");
                }

                SetPresence();
                Delay(TimeSpan.FromSeconds(10), cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                context?.Logger.Error("worker_failed", ex);
                Delay(ReconnectDelay, cancellationToken);
            }
        }

        ClearPresenceAndDispose();
    }

    private DiscordRpcClient? CreateClient()
    {
        try
        {
            DiscordRpcClient rpcClient = new(options.ClientId)
            {
                Logger = new UmbraDiscordLogger(context)
            };
            rpcClient.OnReady += (_, _) =>
            {
                context?.Logger.Info("ready=true");
                SetPresence();
            };
            rpcClient.OnClose += (_, args) =>
            {
                context?.Logger.Warning($"connection_closed code={args.Code} reason={args.Reason}");
            };
            rpcClient.OnError += (_, args) =>
            {
                context?.Logger.Warning($"rpc_error code={args.Code} message={args.Message}");
            };
            return rpcClient;
        }
        catch (Exception ex)
        {
            context?.Logger.Error("client_create_failed", ex);
            return null;
        }
    }

    private bool TryInitialize(DiscordRpcClient rpcClient)
    {
        try
        {
            return rpcClient.Initialize();
        }
        catch (Exception ex)
        {
            // Discord may not be running yet; the worker retries with backoff.
            if (ex.Message.Length > 0)
                context?.Logger.Warning($"connect_failed error={ex.Message}");
            return false;
        }
    }

    private void SetPresence()
    {
        DiscordRpcClient? rpcClient;
        lock (gate)
        {
            if (disposed)
                return;
            rpcClient = client;
        }

        if (rpcClient is null || !rpcClient.IsInitialized)
            return;

        try
        {
            Assets? assets = null;
            if (!string.IsNullOrWhiteSpace(options.LargeImageKey))
            {
                assets = new Assets
                {
                    LargeImageKey = options.LargeImageKey,
                    LargeImageText = options.LargeImageText
                };
            }

            RichPresence presence = new()
            {
                Details = options.Details,
                State = options.State,
                Timestamps = Timestamps.Now,
                Assets = assets
            };
            rpcClient.SetPresence(presence);
        }
        catch (Exception ex)
        {
            context?.Logger.Warning($"set_presence_failed error={ex.Message}");
        }
    }

    private void ClearPresenceAndDispose()
    {
        DiscordRpcClient? rpcClient;
        lock (gate)
        {
            rpcClient = client;
            client = null;
        }

        if (rpcClient is null)
            return;

        try
        {
            if (rpcClient.IsInitialized)
                rpcClient.ClearPresence();
        }
        catch (Exception ex)
        {
            context?.Logger.Warning($"clear_presence_failed error={ex.Message}");
        }

        try
        {
            rpcClient.Deinitialize();
        }
        catch (Exception ex)
        {
            context?.Logger.Warning($"deinitialize_failed error={ex.Message}");
        }

        rpcClient.Dispose();
    }

    private static void Delay(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            Thread.Sleep(delay);
        }
        catch (ThreadInterruptedException)
        {
            // Cancellation below decides whether the worker continues.
        }

        cancellationToken.ThrowIfCancellationRequested();
    }

    private sealed class UmbraDiscordLogger(IUmbraPluginContext? context) : ILogger
    {
        public LogLevel Level { get; set; } = LogLevel.Info;

        public void Trace(string message, params object[] args) => context?.Logger.Info(Format(message, args));

        public void Info(string message, params object[] args) => context?.Logger.Info(Format(message, args));

        public void Warning(string message, params object[] args) => context?.Logger.Warning(Format(message, args));

        public void Error(string message, params object[] args) => context?.Logger.Error(Format(message, args));

        private static string Format(string message, object[] args) =>
            args.Length == 0 ? message : string.Format(message, args);
    }
}

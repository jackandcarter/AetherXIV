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

using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Aether.Umbra.Framework;

public sealed class UmbraDevBridgeService : IDisposable
{
    public const int ProtocolVersion = 2;
    public const int MaximumRequestBytes = 64 * 1024;
    public const int MaximumLongPollMilliseconds = 30_000;
    public const int AcceptTimeoutSeconds = 15;

    private readonly UmbraRuntimeOptions options;
    private readonly UmbraRuntimeLog log;
    private readonly UmbraReadOnlyMemory memory;
    private readonly UmbraDevBridgeEvents events;
    private readonly UmbraMemoryWatchService watches;
    private readonly UmbraBreakpointService breakpoints;
    private readonly UmbraLuaCallHookService luaHook;
    private readonly object gate = new();
    private HttpListener? listener;
    private CancellationTokenSource? serverStop;
    private Task? serverTask;
    private UmbraRuntime? runtime;
    private string? activeToken;
    private int activePort;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true
    };

    public UmbraDevBridgeService(UmbraRuntimeOptions options, UmbraRuntimeLog log, UmbraReadOnlyMemory memory)
    {
        this.options = options;
        this.log = log;
        this.memory = memory;
        events = new UmbraDevBridgeEvents(options, log);
        watches = new UmbraMemoryWatchService(memory, events, log);
        breakpoints = new UmbraBreakpointService(memory, log, events, options.DevBridgeDirectory);
        luaHook = new UmbraLuaCallHookService(memory, log, events, options.DevBridgeDirectory);
    }

    public bool IsRunning
    {
        get
        {
            lock (gate)
                return listener is not null;
        }
    }

    public object Status => new
    {
        running = IsRunning,
        host = "127.0.0.1",
        port = activePort == 0 ? options.DevBridgePort : activePort,
        transport = "managed",
        protocol_version = ProtocolVersion,
        authenticated = true,
        read_only = true,
        bridge_session_id = events.BridgeSessionId,
        latest_sequence = events.LatestSequence,
        control_path = options.DevBridgeControlPath,
        capture = events.CaptureStatus(),
        process = memory.ProcessStatus(),
        capabilities = Capabilities(),
        breakpoints = breakpoints.Status(),
        lua_hook = luaHook.Status()
    };

    internal void AttachRuntime(UmbraRuntime attachedRuntime)
    {
        ArgumentNullException.ThrowIfNull(attachedRuntime);
        lock (gate)
            runtime = attachedRuntime;
    }

    public Task StartAsync(int port, string? token, CancellationToken cancellationToken = default)
    {
        if (!UmbraDevBridgeControl.IsValidToken(token))
            throw new InvalidOperationException("The development bridge requires a valid per-install local token.");

        lock (gate)
        {
            if (listener is not null)
            {
                activeToken = token;
                return Task.CompletedTask;
            }

            activePort = port is >= 1024 and <= 65535 ? port : options.DevBridgePort;
            activeToken = token;
            HttpListener http = new();
            http.Prefixes.Add($"http://127.0.0.1:{activePort}/");
            http.Start();
            listener = http;
            serverStop = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            serverTask = Task.Run(() => ServeAsync(http, serverStop.Token));
            log.Info($"umbra_dev_bridge_started host=127.0.0.1 port={activePort} protocol={ProtocolVersion}");
            events.Record("bridge.start", new
            {
                host = "127.0.0.1",
                port = activePort,
                protocol_version = ProtocolVersion
            });
        }

        return Task.CompletedTask;
    }

    public async Task StopAsync()
    {
        HttpListener? http;
        CancellationTokenSource? stop;
        Task? task;
        lock (gate)
        {
            http = listener;
            stop = serverStop;
            task = serverTask;
            listener = null;
            serverStop = null;
            serverTask = null;
            activeToken = null;
        }

        if (http is null)
            return;

        events.Record("bridge.stop");
        log.Info("umbra_dev_bridge_stopping=true");
        stop?.Cancel();
        http.Close();
        if (task is not null)
        {
            try
            {
                await task.WaitAsync(TimeSpan.FromSeconds(2)).ConfigureAwait(false);
            }
            catch
            {
                // Listener shutdown often interrupts a pending accept.
            }
        }

        stop?.Dispose();
        log.Info("umbra_dev_bridge_stopped=true");
    }

    public void Dispose()
    {
        luaHook.Dispose();
        breakpoints.Dispose();
        watches.Dispose();
        StopAsync().GetAwaiter().GetResult();
    }

    private async Task ServeAsync(HttpListener http, CancellationToken cancellationToken)
    {
        UmbraBridgeAcceptLoop pump = new(
            acceptAsync: AcceptCurrentAsync,
            abort: AbortListener,
            restartTransport: RestartListener,
            acceptTimeout: TimeSpan.FromSeconds(AcceptTimeoutSeconds),
            notify: (name, payload) => events.Record(name, payload));
        await pump.RunAsync(SafeHandleAsync, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Issues the next accept on the currently registered listener. Returns
    /// null when the listener is gone (StopAsync raced the loop); the pump
    /// recreates the listener via <see cref="RestartListener"/> unless the
    /// server is stopping.
    /// </summary>
    private Task<UmbraBridgeRequest?> AcceptCurrentAsync()
    {
        HttpListener? http;
        lock (gate)
            http = listener;
        return http is null
            ? Task.FromResult<UmbraBridgeRequest?>(null)
            : AcceptFromAsync(http);
    }

    private async Task<UmbraBridgeRequest?> AcceptFromAsync(HttpListener http)
    {
        HttpListenerContext context = await http.GetContextAsync().ConfigureAwait(false);
        return new BridgeHttpRequest(context);
    }

    /// <summary>
    /// Aborts the currently registered listener so a stalled pending accept
    /// unblocks (HttpListener.Abort fails in-flight GetContextAsync calls).
    /// The pump then recreates the listener via <see cref="RestartListener"/>.
    /// </summary>
    private void AbortListener()
    {
        HttpListener? http;
        lock (gate)
            http = listener;
        try
        {
            http?.Abort();
        }
        catch
        {
            // The listener may already be closed; the pump recreates it.
        }
    }

    /// <summary>
    /// Recreates the listener on the same loopback port after a stall. The
    /// listener is only republished under the gate when the server is not
    /// stopping; a concurrent StopAsync declines the restart so the pump
    /// exits cleanly.
    /// </summary>
    private bool RestartListener()
    {
        HttpListener? next = null;
        for (int attempt = 0; attempt < 3; attempt++)
        {
            try
            {
                next = new HttpListener();
                next.Prefixes.Add($"http://127.0.0.1:{activePort}/");
                next.Start();
                break;
            }
            catch (Exception ex)
            {
                log.Warning($"umbra_dev_bridge_restart_failed attempt={attempt} type={ex.GetType().Name} message={ex.Message}");
                try
                {
                    next?.Close();
                }
                catch
                {
                }

                next = null;
                if (attempt < 2)
                    Thread.Sleep(250);
            }
        }

        if (next is null)
            return false;

        lock (gate)
        {
            if (serverStop is null)
            {
                // StopAsync raced the restart; the server is going down.
                try
                {
                    next.Close();
                }
                catch
                {
                }

                return false;
            }

            listener = next;
        }

        log.Info($"umbra_dev_bridge_listener_restarted port={activePort}");
        events.Record("bridge.listener.restart", new { port = activePort });
        return true;
    }

    private sealed class BridgeHttpRequest : UmbraBridgeRequest
    {
        public HttpListenerContext Context { get; }

        public BridgeHttpRequest(HttpListenerContext context)
        {
            Context = context;
        }
    }

    private async Task SafeHandleAsync(UmbraBridgeRequest request, CancellationToken cancellationToken)
    {
        HttpListenerContext context = ((BridgeHttpRequest)request).Context;
        try
        {
            // Disable keep-alive so a broken connection can never wedge the
            // listener's internal accept queue (the failure mode seen live:
            // requests queued forever after one aborted request). The dev
            // bridge is per-request tooling; connection reuse buys nothing.
            context.Response.KeepAlive = false;
            await HandleAsync(context, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Client or server cancelled; nothing to respond to.
        }
        catch (Exception ex)
        {
            log.Warning($"umbra_dev_bridge_handle_failed type={ex.GetType().Name} message={ex.Message}");
        }
        finally
        {
            try
            {
                context.Response.Close();
            }
            catch
            {
                // Already closed or the connection is gone; nothing to do.
            }
        }
    }

    private async Task HandleAsync(HttpListenerContext context, CancellationToken cancellationToken)
    {
        string path = context.Request.Url?.AbsolutePath ?? "/";
        string remote = context.Request.RemoteEndPoint?.Address.ToString() ?? "";
        if (!IPAddress.IsLoopback(context.Request.RemoteEndPoint?.Address ?? IPAddress.None))
        {
            log.Warning($"umbra_dev_bridge_rejected_remote address={remote}");
            await WriteJsonAsync(context, 403, new { error = "loopback only" }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!string.IsNullOrWhiteSpace(context.Request.Headers["Origin"]))
        {
            log.Warning("umbra_dev_bridge_rejected_browser_origin=true");
            await WriteJsonAsync(context, 403, new { error = "browser-origin requests are not accepted" }, cancellationToken).ConfigureAwait(false);
            return;
        }

        if (!IsAuthorized(context.Request))
        {
            log.Warning($"umbra_dev_bridge_rejected_unauthorized path={path}");
            context.Response.Headers["WWW-Authenticate"] = "Bearer realm=\"Aether Umbra Dev Bridge\"";
            await WriteJsonAsync(context, 401, new { error = "valid bridge token required" }, cancellationToken).ConfigureAwait(false);
            return;
        }

        try
        {
            log.Info($"umbra_dev_bridge_request method={context.Request.HttpMethod} path={path} remote={remote}");
            if (path != "/events")
                events.Record("bridge.request", new { method = context.Request.HttpMethod, path });

            if (context.Request.HttpMethod == "GET" && path == "/status")
            {
                await WriteJsonAsync(context, 200, Status, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/snapshot")
            {
                await WriteJsonAsync(context, 200, BuildSnapshot(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/capabilities")
            {
                await WriteJsonAsync(context, 200, Capabilities(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/modules")
            {
                await WriteJsonAsync(context, 200, new { modules = memory.Modules() }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/watches")
            {
                await WriteJsonAsync(context, 200, new { watches = watches.Snapshots() }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/events")
            {
                int limit = ParseQueryInt(context, "limit", 100, 1, UmbraDevBridgeEvents.Capacity);
                bool hasAfter = long.TryParse(context.Request.QueryString["after"], out long after);
                int waitMilliseconds = hasAfter
                    ? ParseQueryInt(context, "wait_ms", 0, 0, MaximumLongPollMilliseconds)
                    : 0;
                IReadOnlyList<UmbraDevBridgeEvent> page = hasAfter
                    ? await events.WaitAfterAsync(
                        after,
                        limit,
                        TimeSpan.FromMilliseconds(waitMilliseconds),
                        cancellationToken).ConfigureAwait(false)
                    : events.Recent(limit);
                await WriteJsonAsync(context, 200, new
                {
                    bridge_session_id = events.BridgeSessionId,
                    latest_sequence = events.LatestSequence,
                    events = page
                }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/logs")
            {
                int limit = ParseQueryInt(context, "limit", 120, 1, 1000);
                await WriteJsonAsync(context, 200, new { lines = ReadLastLines(options.LogPath, limit) }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/observations/actor-appearance")
            {
                UmbraRuntime? attached = runtime;
                await WriteJsonAsync(context, 200, attached is null
                    ? new
                    {
                        availability = Unavailable("runtime-not-attached", "The managed runtime is not attached."),
                        snapshots = Array.Empty<object>()
                    }
                    : new
                    {
                        availability = attached.ActorAppearance.Availability,
                        snapshots = attached.ActorAppearance.Snapshots
                    }, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/start")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                string? name = ReadString(body.RootElement, "name");
                string? correlationId = ReadString(body.RootElement, "correlation_id");
                object metadata = new
                {
                    server_run_id = ReadString(body.RootElement, "server_run_id"),
                    test_case = ReadString(body.RootElement, "test_case"),
                    note = ReadString(body.RootElement, "note"),
                    snapshot = BuildSnapshot()
                };
                await WriteJsonAsync(
                    context,
                    200,
                    events.StartCapture(name, correlationId, metadata),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/mark")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                await WriteJsonAsync(
                    context,
                    200,
                    events.Mark(
                        ReadString(body.RootElement, "label") ?? "marker",
                        ReadString(body.RootElement, "note")),
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/pause")
            {
                await WriteJsonAsync(context, 200, events.PauseCapture(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/stop")
            {
                await WriteJsonAsync(context, 200, events.StopCapture(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/capture/export")
            {
                await WriteJsonAsync(context, 200, ExportCapture(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/memory/peek")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                int size = ReadInt(body.RootElement, "size", 64);
                string? module = ReadString(body.RootElement, "module");
                UmbraMemoryPeekResult result = string.IsNullOrWhiteSpace(module)
                    ? memory.Peek(ReadAddress(body.RootElement, "address"), size)
                    : memory.PeekModule(module, ReadOffset(body.RootElement, "offset"), size);
                events.Record("memory.peek", new
                {
                    result.Address,
                    result.Module,
                    result.ModuleOffset,
                    result.RequestedSize,
                    result.ReadSize,
                    result.Success,
                    result.Error
                });
                await WriteJsonAsync(
                    context,
                    result.Success ? 200 : 400,
                    result with { Bytes = Array.Empty<byte>() },
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/scan/pattern")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                int size = ReadInt(body.RootElement, "size", 4096);
                string? module = ReadString(body.RootElement, "module");
                string? patternText = ReadString(body.RootElement, "pattern");
                if (!UmbraBytePattern.TryParse(patternText, out UmbraBytePattern pattern, out string? error))
                {
                    await WriteJsonAsync(context, 400, new { error }, cancellationToken).ConfigureAwait(false);
                    return;
                }

                UmbraMemoryScanResult result = string.IsNullOrWhiteSpace(module)
                    ? memory.Scan(ReadAddress(body.RootElement, "start"), size, pattern)
                    : memory.ScanModule(module, ReadOffset(body.RootElement, "offset"), size, pattern);
                events.Record("memory.scan", new
                {
                    result.Start,
                    result.Module,
                    result.ModuleOffset,
                    result.Size,
                    matches = result.Matches.Count,
                    result.Success,
                    result.Error
                });
                await WriteJsonAsync(context, result.Success ? 200 : 400, result, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/watch/start")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                UmbraMemoryWatchSnapshot watch = watches.Start(
                    ReadString(body.RootElement, "name"),
                    ReadString(body.RootElement, "module"),
                    ReadOffset(body.RootElement, "offset"),
                    ReadInt(body.RootElement, "size", 4),
                    ReadInt(body.RootElement, "interval_ms", 500));
                await WriteJsonAsync(context, 200, watch, cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/watch/stop")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                string? id = ReadString(body.RootElement, "id");
                bool stopped = watches.Stop(id);
                await WriteJsonAsync(
                    context,
                    stopped ? 200 : 404,
                    stopped ? new { stopped = true, id } : new { stopped = false, id, error = "watch not found" },
                    cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/breakpoint/install")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                string? name = ReadString(body.RootElement, "name");
                UmbraBreakpointPlan? plan = name switch
                {
                    "tutorial-mode-confirm" => UmbraBreakpointPlan.TutorialModeConfirm,
                    "tutorial-mode-attach" => UmbraBreakpointPlan.TutorialModeAttach,
                    "tutorial-mode-gate" => UmbraBreakpointPlan.TutorialModeGate,
                    "vtable-fd785c-dispatch" => UmbraBreakpointPlan.VtableFd785cDispatch,
                    _ => null
                };
                if (plan is null)
                {
                    await WriteJsonAsync(
                        context,
                        400,
                        new { error = "unknown breakpoint; supported names: tutorial-mode-confirm, tutorial-mode-attach, tutorial-mode-gate, vtable-fd785c-dispatch" },
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(context, 200, breakpoints.Install(plan), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/breakpoint/clear")
            {
                await WriteJsonAsync(context, 200, breakpoints.Clear(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/breakpoints")
            {
                await WriteJsonAsync(context, 200, breakpoints.Status(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/lua-hook/install")
            {
                using JsonDocument body = await ReadBodyAsync(context, cancellationToken).ConfigureAwait(false);
                string? name = ReadString(body.RootElement, "name");
                UmbraLuaCallHookPlan? plan = name switch
                {
                    "linkpearl-tutorial-mode" => UmbraLuaCallHookPlan.LinkpearlTutorialMode,
                    _ => null
                };
                if (plan is null)
                {
                    await WriteJsonAsync(
                        context,
                        400,
                        new { error = "unknown hook; supported names: linkpearl-tutorial-mode" },
                        cancellationToken).ConfigureAwait(false);
                    return;
                }

                await WriteJsonAsync(context, 200, luaHook.Install(plan), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "POST" && path == "/lua-hook/clear")
            {
                await WriteJsonAsync(context, 200, luaHook.Clear(), cancellationToken).ConfigureAwait(false);
                return;
            }

            if (context.Request.HttpMethod == "GET" && path == "/lua-hook/status")
            {
                await WriteJsonAsync(context, 200, luaHook.Status(), cancellationToken).ConfigureAwait(false);
                return;
            }

            await WriteJsonAsync(context, 404, new { error = "not found" }, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidDataException ex)
        {
            await WriteJsonAsync(context, 400, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
        }
        catch (ArgumentException ex)
        {
            await WriteJsonAsync(context, 400, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await WriteJsonAsync(context, 409, new { error = ex.Message }, cancellationToken).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            log.Error($"umbra_dev_bridge_request_failed path={path}", ex);
            await WriteJsonAsync(context, 500, new { error = "request failed; inspect the Umbra log" }, cancellationToken).ConfigureAwait(false);
        }
    }

    private object BuildSnapshot()
    {
        UmbraRuntime? attached = runtime;
        return new
        {
            schema_version = ProtocolVersion,
            captured_at = DateTimeOffset.UtcNow,
            bridge_session_id = events.BridgeSessionId,
            latest_sequence = events.LatestSequence,
            framework = new
            {
                UmbraFrameworkInfo.Name,
                UmbraFrameworkInfo.Version,
                api_version = UmbraFrameworkInfo.ApiVersion,
                safe_mode = attached?.Options.SafeMode
            },
            process = memory.ProcessStatus(),
            client_build = memory.ClientBuildStatus(),
            render = attached is null
                ? null
                : new
                {
                    attached.RenderBridge.FrameCount,
                    attached.RenderBridge.ViewportWidth,
                    attached.RenderBridge.ViewportHeight,
                    attached.RenderBridge.DeviceGeneration,
                    attached.RenderBridge.RenderThreadId
                },
            framework_ui = attached is null
                ? null
                : new
                {
                    plugin_manager_open = attached.PluginManager.IsOpen,
                    plugin_manager_tab = attached.PluginManager.ActiveTab.ToString(),
                    running_plugins = attached.Plugins.Statuses.Count(status => status.State == UmbraPluginRuntimeState.Running),
                    faulted_plugins = attached.Plugins.Statuses.Count(status => status.State == UmbraPluginRuntimeState.Faulted)
                },
            observations = ObservationStatus(attached),
            watches = watches.Snapshots(),
            capture = events.CaptureStatus()
        };
    }

    private object Capabilities()
    {
        return new
        {
            event_cursor = true,
            long_poll = true,
            capture_markers = true,
            capture_export = true,
            module_inventory = true,
            module_relative_memory = true,
            build_gated_memory_watches = true,
            raw_memory_read = true,
            code_breakpoints = true,
            lua_call_hooks = true,
            memory_write = false,
            packet_mutation = false,
            remote_function_invocation = false,
            screenshot = false,
            semantic_observations = ObservationStatus(runtime)
        };
    }

    private static object ObservationStatus(UmbraRuntime? attached)
    {
        return new
        {
            actor_appearance = attached?.ActorAppearance.Availability
                ?? Unavailable("runtime-not-attached", "The managed runtime is not attached."),
            actor_registry = Unavailable(
                "ffxiv-1.23b-actor-registry-unresolved",
                "No exact-build actor-registry adapter has been verified."),
            event_state = Unavailable(
                "ffxiv-1.23b-event-state-unresolved",
                "No exact-build event-receiver adapter has been verified."),
            ui_state = Unavailable(
                "ffxiv-1.23b-ui-state-unresolved",
                "No exact-build game UI-state adapter has been verified."),
            network_timeline = Unavailable(
                "ffxiv-1.23b-network-observer-unresolved",
                "No passive client packet observer has been verified.")
        };
    }

    private static object Unavailable(string adapter, string reason)
    {
        return new
        {
            is_available = false,
            adapter,
            client_build_id = (string?)null,
            reason
        };
    }

    private object ExportCapture()
    {
        string? capturePath = events.GetCapturePathForExport();
        if (string.IsNullOrWhiteSpace(capturePath) || !File.Exists(capturePath))
            throw new InvalidDataException("No current or completed capture is available to export.");

        string exportDirectory = Path.Combine(options.DevBridgeDirectory, "Exports");
        Directory.CreateDirectory(exportDirectory);
        string exportPath = Path.Combine(
            exportDirectory,
            $"{Path.GetFileNameWithoutExtension(capturePath)}-{DateTimeOffset.Now:yyyyMMdd-HHmmss}.zip");

        using (FileStream stream = File.Open(exportPath, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
        using (ZipArchive archive = new(stream, ZipArchiveMode.Create))
        {
            archive.CreateEntryFromFile(capturePath, Path.Combine("capture", Path.GetFileName(capturePath)));
            WriteArchiveJson(archive, "manifest.json", new
            {
                schema_version = ProtocolVersion,
                exported_at = DateTimeOffset.UtcNow,
                bridge_session_id = events.BridgeSessionId,
                capture = events.CaptureStatus(),
                snapshot = BuildSnapshot()
            });
            WriteArchiveText(
                archive,
                "umbra-framework-tail.log",
                string.Join(Environment.NewLine, ReadLastLines(options.LogPath, 1000)));
        }

        events.Record("capture.export", new { path = exportPath });
        return new
        {
            exported = true,
            path = exportPath,
            source = capturePath
        };
    }

    private bool IsAuthorized(HttpListenerRequest request)
    {
        string? expected;
        lock (gate)
            expected = activeToken;
        if (!UmbraDevBridgeControl.IsValidToken(expected))
            return false;

        string? presented = request.Headers["X-Aether-Umbra-Token"];
        if (string.IsNullOrWhiteSpace(presented))
        {
            string? authorization = request.Headers["Authorization"];
            const string prefix = "Bearer ";
            if (authorization?.StartsWith(prefix, StringComparison.OrdinalIgnoreCase) == true)
                presented = authorization[prefix.Length..].Trim();
        }

        if (presented is null || presented.Length != expected!.Length)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            Encoding.ASCII.GetBytes(presented),
            Encoding.ASCII.GetBytes(expected));
    }

    private static async Task<JsonDocument> ReadBodyAsync(
        HttpListenerContext context,
        CancellationToken cancellationToken)
    {
        if (context.Request.ContentLength64 > MaximumRequestBytes)
            throw new InvalidDataException($"Request bodies are limited to {MaximumRequestBytes} bytes.");

        using MemoryStream buffer = new();
        await context.Request.InputStream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        if (buffer.Length > MaximumRequestBytes)
            throw new InvalidDataException($"Request bodies are limited to {MaximumRequestBytes} bytes.");

        if (buffer.Length == 0)
            return JsonDocument.Parse("{}");

        return JsonDocument.Parse(buffer.ToArray());
    }

    private static async Task WriteJsonAsync(
        HttpListenerContext context,
        int status,
        object payload,
        CancellationToken cancellationToken)
    {
        byte[] bytes = Encoding.UTF8.GetBytes(JsonSerializer.Serialize(payload, JsonOptions));
        context.Response.StatusCode = status;
        context.Response.ContentType = "application/json; charset=utf-8";
        context.Response.Headers["Cache-Control"] = "no-store";
        context.Response.Headers["X-Content-Type-Options"] = "nosniff";
        context.Response.ContentLength64 = bytes.Length;
        await context.Response.OutputStream.WriteAsync(bytes, cancellationToken).ConfigureAwait(false);
        context.Response.Close();
    }

    private static void WriteArchiveJson(ZipArchive archive, string name, object payload)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using Stream stream = entry.Open();
        JsonSerializer.Serialize(stream, payload, JsonOptions);
    }

    private static void WriteArchiveText(ZipArchive archive, string name, string text)
    {
        ZipArchiveEntry entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using StreamWriter writer = new(entry.Open(), Encoding.UTF8);
        writer.Write(text);
    }

    private static int ParseQueryInt(HttpListenerContext context, string key, int fallback, int min, int max)
    {
        string? value = context.Request.QueryString[key];
        return int.TryParse(value, out int parsed) ? Math.Clamp(parsed, min, max) : fallback;
    }

    private static string? ReadString(JsonElement element, string key)
    {
        return element.TryGetProperty(key, out JsonElement value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;
    }

    private static int ReadInt(JsonElement element, string key, int fallback)
    {
        if (!element.TryGetProperty(key, out JsonElement value))
            return fallback;

        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out int number))
            return number;

        if (value.ValueKind == JsonValueKind.String && int.TryParse(value.GetString(), out number))
            return number;

        return fallback;
    }

    private static nuint ReadAddress(JsonElement element, string key)
    {
        ulong value = ReadUnsigned(element, key);
        return value <= nuint.MaxValue ? (nuint)value : 0;
    }

    private static long ReadOffset(JsonElement element, string key)
    {
        ulong value = ReadUnsigned(element, key);
        return value <= long.MaxValue ? (long)value : -1;
    }

    private static ulong ReadUnsigned(JsonElement element, string key)
    {
        if (!element.TryGetProperty(key, out JsonElement value))
            return 0;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetUInt64(out ulong number))
            return number;
        if (value.ValueKind != JsonValueKind.String)
            return 0;

        string? text = value.GetString()?.Trim();
        if (string.IsNullOrWhiteSpace(text))
            return 0;
        if (text.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
        {
            return ulong.TryParse(
                text[2..],
                System.Globalization.NumberStyles.HexNumber,
                null,
                out ulong hex)
                ? hex
                : 0;
        }

        return ulong.TryParse(text, out number) ? number : 0;
    }

    private static IReadOnlyList<string> ReadLastLines(string path, int limit)
    {
        if (!File.Exists(path))
            return Array.Empty<string>();

        Queue<string> lines = new();
        foreach (string line in File.ReadLines(path))
        {
            lines.Enqueue(line);
            while (lines.Count > limit)
                lines.Dequeue();
        }

        return lines.ToArray();
    }
}

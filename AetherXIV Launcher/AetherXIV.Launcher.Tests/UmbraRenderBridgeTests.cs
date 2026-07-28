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
using Aether.Umbra.Framework;

namespace AetherXIV.Launcher.Tests;

public sealed class UmbraRenderBridgeTests
{
    [Fact]
    public void NativeRenderEventAbiIsStable()
    {
        Assert.Equal(1u, UmbraNativeRenderEvent.CurrentAbiVersion);
        Assert.Equal(32, Marshal.SizeOf<UmbraNativeRenderEvent>());
    }

    [Fact]
    public async Task RenderBridgeDispatchesFramesAndDeviceResetEvents()
    {
        string root = CreateTempDirectory();
        using UmbraRuntime runtime = await StartSafeModeRuntimeAsync(root);

        UmbraNativeRenderEvent frame = CreateEvent(UmbraNativeRenderEventKind.Frame) with
        {
            FrameNumber = 42,
            DeltaSeconds = 1.0f / 60.0f,
            ViewportWidth = 1280,
            ViewportHeight = 720,
            Reserved = 3
        };

        Assert.Equal(0, runtime.RenderBridge.Process(frame));
        Assert.Equal(Environment.CurrentManagedThreadId, runtime.RenderBridge.RenderThreadId);
        Assert.Equal(42, runtime.RenderBridge.FrameCount);
        Assert.True(runtime.PluginManager.IsOpen);
        Assert.Equal(UmbraPluginManagerTab.Settings, runtime.PluginManager.ActiveTab);

        runtime.SetPluginManagerTab(UmbraPluginManagerTab.Installed);
        Assert.Equal(0, runtime.RenderBridge.Process(frame with
        {
            FrameNumber = 43,
            Reserved = 1
        }));
        Assert.Equal(UmbraPluginManagerTab.Installed, runtime.PluginManager.ActiveTab);

        Assert.Equal(0, runtime.RenderBridge.Process(frame with
        {
            FrameNumber = 44,
            Reserved = 5
        }));
        Assert.Equal(UmbraPluginManagerTab.Updates, runtime.PluginManager.ActiveTab);

        Assert.Equal(0, runtime.RenderBridge.Process(CreateEvent(UmbraNativeRenderEventKind.BeforeReset)));
        Assert.Equal(0, runtime.RenderBridge.DeviceGeneration);
        Assert.Equal(0, runtime.RenderBridge.Process(CreateEvent(UmbraNativeRenderEventKind.AfterReset)));
        Assert.Equal(1, runtime.RenderBridge.DeviceGeneration);
    }

    [Fact]
    public async Task RenderBridgeRejectsIncompatibleAbiAndThreadChanges()
    {
        string root = CreateTempDirectory();
        using UmbraRuntime runtime = await StartSafeModeRuntimeAsync(root);

        UmbraNativeRenderEvent incompatible = CreateEvent(UmbraNativeRenderEventKind.Frame) with
        {
            AbiVersion = UmbraNativeRenderEvent.CurrentAbiVersion + 1
        };
        Assert.Equal(-2, runtime.RenderBridge.Process(incompatible));

        UmbraNativeRenderEvent frame = CreateEvent(UmbraNativeRenderEventKind.Frame) with { FrameNumber = 1 };
        Assert.Equal(0, runtime.RenderBridge.Process(frame));
        int otherThreadResult = int.MinValue;
        Thread otherThread = new(() => otherThreadResult = runtime.RenderBridge.Process(frame));
        otherThread.Start();
        Assert.True(otherThread.Join(TimeSpan.FromSeconds(5)), "Render-thread rejection did not complete.");
        Assert.Equal(-4, otherThreadResult);
    }

    private static UmbraNativeRenderEvent CreateEvent(UmbraNativeRenderEventKind kind)
    {
        return new UmbraNativeRenderEvent
        {
            Size = (uint)Marshal.SizeOf<UmbraNativeRenderEvent>(),
            AbiVersion = UmbraNativeRenderEvent.CurrentAbiVersion,
            Kind = kind
        };
    }

    private static Task<UmbraRuntime> StartSafeModeRuntimeAsync(string root)
    {
        string cache = Path.Combine(root, "Cache");
        string devBridge = Path.Combine(cache, "DevBridge");
        string logPath = Path.Combine(root, "Logs", "umbra.log");
        UmbraRuntimeOptions options = new(
            logPath,
            Path.Combine(root, "Plugins"),
            cache,
            devBridge,
            Path.Combine(devBridge, "control.json"),
            false,
            UmbraRuntimeOptions.DefaultDevBridgePort,
            true,
            Array.Empty<string>(),
            Array.Empty<UmbraRepositorySource>());
        return UmbraRuntime.StartAsync(options, UmbraRuntimeLog.Open(logPath));
    }

    private static string CreateTempDirectory()
    {
        string path = Path.Combine(Path.GetTempPath(), "aetherxiv-render-bridge-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }
}

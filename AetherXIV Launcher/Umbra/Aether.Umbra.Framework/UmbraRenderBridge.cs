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

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal enum UmbraNativeRenderEventKind : uint
{
    Frame = 1,
    BeforeReset = 2,
    AfterReset = 3
}

[StructLayout(LayoutKind.Sequential, Pack = 4)]
internal struct UmbraNativeRenderEvent
{
    public const uint CurrentAbiVersion = 1;

    public uint Size;
    public uint AbiVersion;
    public UmbraNativeRenderEventKind Kind;
    public uint FrameNumber;
    public float DeltaSeconds;
    public uint ViewportWidth;
    public uint ViewportHeight;
    public uint Reserved;

    public bool IsPluginManagerOpen => (Reserved & 1u) != 0;

    public bool IsPluginManagerSettingsRequested => (Reserved & 2u) != 0;

    public bool IsPluginManagerUpdatesRequested => (Reserved & 4u) != 0;
}

public sealed class UmbraRenderBridge
{
    private readonly UmbraRuntime runtime;
    private int renderThreadId;
    private int deviceGeneration;
    private int viewportWidth;
    private int viewportHeight;
    private int publishedPluginUpdateCount = -1;
    private long frameCount;

    internal UmbraRenderBridge(UmbraRuntime runtime)
    {
        this.runtime = runtime;
    }

    public int RenderThreadId => Volatile.Read(ref renderThreadId);

    public int DeviceGeneration => Volatile.Read(ref deviceGeneration);

    public long FrameCount => Interlocked.Read(ref frameCount);

    public int ViewportWidth => Volatile.Read(ref viewportWidth);

    public int ViewportHeight => Volatile.Read(ref viewportHeight);

    internal int Process(in UmbraNativeRenderEvent renderEvent)
    {
        if (renderEvent.AbiVersion != UmbraNativeRenderEvent.CurrentAbiVersion
            || renderEvent.Size < Marshal.SizeOf<UmbraNativeRenderEvent>())
        {
            runtime.Log.Warning(
                $"umbra_render_bridge_abi_rejected version={renderEvent.AbiVersion} size={renderEvent.Size}");
            return -2;
        }

        switch (renderEvent.Kind)
        {
            case UmbraNativeRenderEventKind.Frame:
                return ProcessFrame(renderEvent);
            case UmbraNativeRenderEventKind.BeforeReset:
                runtime.Log.Info($"umbra_render_device_reset_begin generation={DeviceGeneration}");
                return 0;
            case UmbraNativeRenderEventKind.AfterReset:
                int generation = Interlocked.Increment(ref deviceGeneration);
                runtime.Log.Info($"umbra_render_device_reset_complete generation={generation}");
                return 0;
            default:
                runtime.Log.Warning($"umbra_render_bridge_event_unknown kind={(uint)renderEvent.Kind}");
                return -3;
        }
    }

    private int ProcessFrame(in UmbraNativeRenderEvent renderEvent)
    {
        int currentThread = Environment.CurrentManagedThreadId;
        int knownThread = Volatile.Read(ref renderThreadId);
        if (knownThread == 0)
        {
            Interlocked.CompareExchange(ref renderThreadId, currentThread, 0);
            knownThread = Volatile.Read(ref renderThreadId);
            runtime.Log.Info($"umbra_render_thread_managed_id={knownThread}");
        }

        if (knownThread != currentThread)
        {
            runtime.Log.Warning(
                $"umbra_render_thread_rejected expected={knownThread} actual={currentThread}");
            return -4;
        }

        TimeSpan delta = TimeSpan.FromSeconds(Math.Clamp(renderEvent.DeltaSeconds, 0.0f, 0.25f));
        runtime.SynchronizePluginManagerOpen(renderEvent.IsPluginManagerOpen);
        if (renderEvent.IsPluginManagerUpdatesRequested
            && runtime.PluginManager.ActiveTab != UmbraPluginManagerTab.Updates)
        {
            runtime.SetPluginManagerTab(UmbraPluginManagerTab.Updates);
            runtime.Log.Info("umbra_plugin_manager_updates_requested=true");
        }
        else if (renderEvent.IsPluginManagerSettingsRequested
            && runtime.PluginManager.ActiveTab != UmbraPluginManagerTab.Settings)
        {
            runtime.SetPluginManagerTab(UmbraPluginManagerTab.Settings);
            runtime.Log.Info("umbra_plugin_manager_settings_requested=true");
        }
        PublishPluginUpdateCount();
        runtime.Notifications.PublishFrame();
        ulong frameNumber = renderEvent.FrameNumber;
        Interlocked.Exchange(ref frameCount, renderEvent.FrameNumber);
        Volatile.Write(ref viewportWidth, (int)Math.Min(renderEvent.ViewportWidth, int.MaxValue));
        Volatile.Write(ref viewportHeight, (int)Math.Min(renderEvent.ViewportHeight, int.MaxValue));

        UmbraDrawContext context = new(
            runtime,
            frameNumber,
            delta,
            ViewportWidth,
            ViewportHeight,
            DeviceGeneration,
            knownThread);

        runtime.Plugins.Update(delta);
        runtime.Draw(context);
        return 0;
    }

    private void PublishPluginUpdateCount()
    {
        int updateCount = runtime.PluginManager.Updates.Count;
        if (Interlocked.Exchange(ref publishedPluginUpdateCount, updateCount) == updateCount
            || !OperatingSystem.IsWindows())
        {
            return;
        }

        try
        {
            UmbraNativeUi.SetPluginUpdateCount(updateCount);
        }
        catch (Exception ex) when (ex is DllNotFoundException or EntryPointNotFoundException)
        {
            runtime.Log.Warning($"umbra_plugin_update_notification_unavailable error={ex.Message}");
        }
    }
}

internal interface IUmbraDrawContextRecovery
{
    void RecoverAfterPluginCallback();
}

internal sealed class UmbraDrawContext(
    UmbraRuntime runtime,
    ulong frameNumber,
    TimeSpan deltaTime,
    int viewportWidth,
    int viewportHeight,
    int deviceGeneration,
    int renderThreadId) : IUmbraDrawContext, IUmbraDrawContextRecovery
{
    private int openWindowDepth;
    private int openChildDepth;

    public ulong FrameNumber { get; } = frameNumber;

    public TimeSpan DeltaTime { get; } = deltaTime;

    public int ViewportWidth { get; } = viewportWidth;

    public int ViewportHeight { get; } = viewportHeight;

    public float AvailableContentWidth
    {
        get
        {
            EnsureWindow();
            return Math.Max(0.0f, UmbraNativeUi.GetAvailableContentWidth());
        }
    }

    public float ContentRegionWidth
    {
        get
        {
            EnsureWindow();
            return Math.Max(0.0f, UmbraNativeUi.GetContentRegionWidth());
        }
    }

    public int DeviceGeneration { get; } = deviceGeneration;

    public bool IsRenderThread => Environment.CurrentManagedThreadId == renderThreadId;

    public bool IsPluginManagerOpen => runtime.PluginManager.IsOpen;

    public void RequestPluginManagerOpen()
    {
        EnsureRenderThread();
        runtime.RequestPluginManagerOpen();
        UmbraNativeUi.SetPluginManagerOpen(true);
    }

    public bool BeginWindow(string title, ref bool isOpen)
    {
        EnsureRenderThread();
        ArgumentException.ThrowIfNullOrWhiteSpace(title);
        int nativeOpen = isOpen ? 1 : 0;
        bool visible = UmbraNativeUi.BeginWindow(title, ref nativeOpen);
        isOpen = nativeOpen != 0;
        openWindowDepth++;
        return visible;
    }

    public void EndWindow()
    {
        EnsureRenderThread();
        if (openWindowDepth <= 0)
            throw new InvalidOperationException("Umbra EndWindow was called without a matching BeginWindow.");
        if (openChildDepth > 0)
            throw new InvalidOperationException("Umbra EndWindow was called while a child region is still open.");

        UmbraNativeUi.EndWindow();
        openWindowDepth--;
    }

    public void Text(string text)
    {
        EnsureWindow();
        UmbraNativeUi.Text(text ?? "");
    }

    public void Text(string text, UmbraTextTone tone)
    {
        EnsureWindow();
        if (!Enum.IsDefined(tone))
            throw new ArgumentOutOfRangeException(nameof(tone));
        UmbraNativeUi.Text(text ?? "", tone);
    }

    public void Text(string text, UmbraTextTone tone, UmbraTextStyle style)
    {
        EnsureWindow();
        if (!Enum.IsDefined(tone))
            throw new ArgumentOutOfRangeException(nameof(tone));
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
        UmbraNativeUi.Text(text ?? "", tone, style);
    }

    public bool InputText(string label, ref string value, string hint = "", int maximumLength = 256)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return UmbraNativeUi.InputText(label, ref value, hint, maximumLength);
    }

    public bool Button(string label)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return UmbraNativeUi.Button(label);
    }

    public bool Button(
        string label,
        UmbraButtonStyle style,
        UmbraIcon icon = UmbraIcon.None,
        float width = 0.0f,
        float height = 0.0f)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
        if (!Enum.IsDefined(icon))
            throw new ArgumentOutOfRangeException(nameof(icon));
        return UmbraNativeUi.Button(label, style, icon, Math.Max(0.0f, width), Math.Max(0.0f, height));
    }

    public bool Checkbox(string label, ref bool value)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        int nativeValue = value ? 1 : 0;
        bool changed = UmbraNativeUi.Checkbox(label, ref nativeValue);
        value = nativeValue != 0;
        return changed;
    }

    public bool Toggle(string label, ref bool value)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        int nativeValue = value ? 1 : 0;
        bool changed = UmbraNativeUi.Toggle(label, ref nativeValue);
        value = nativeValue != 0;
        return changed;
    }

    public bool InputInt(string label, ref int value, int step = 1)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return UmbraNativeUi.InputInt(label, ref value, Math.Max(1, step));
    }

    public bool SliderInt(string label, ref int value, int minimum, int maximum)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(minimum, maximum);
        value = Math.Clamp(value, minimum, maximum);
        return UmbraNativeUi.SliderInt(label, ref value, minimum, maximum);
    }

    public bool SliderFloat(string label, ref float value, float minimum, float maximum)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        if (!float.IsFinite(minimum) || !float.IsFinite(maximum) || minimum >= maximum)
            throw new ArgumentOutOfRangeException(nameof(maximum));
        value = Math.Clamp(float.IsFinite(value) ? value : minimum, minimum, maximum);
        return UmbraNativeUi.SliderFloat(label, ref value, minimum, maximum);
    }

    public bool Combo(string label, ref int selectedIndex, IReadOnlyList<string> items)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        ArgumentNullException.ThrowIfNull(items);
        if (items.Count is < 1 or > 256 || items.Any(string.IsNullOrWhiteSpace))
            throw new ArgumentException("Umbra combo boxes require 1 to 256 non-empty items.", nameof(items));
        selectedIndex = Math.Clamp(selectedIndex, 0, items.Count - 1);
        return UmbraNativeUi.Combo(label, ref selectedIndex, items);
    }

    public bool CollapsingHeader(string label, bool defaultOpen = false)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(label);
        return UmbraNativeUi.CollapsingHeader(label, defaultOpen);
    }

    public void ProgressBar(float fraction, string overlay = "")
    {
        EnsureWindow();
        UmbraNativeUi.ProgressBar(Math.Clamp(float.IsFinite(fraction) ? fraction : 0.0f, 0.0f, 1.0f), overlay);
    }

    public void SameLine()
    {
        EnsureWindow();
        UmbraNativeUi.SameLine();
    }

    public void Separator()
    {
        EnsureWindow();
        UmbraNativeUi.Separator();
    }

    public void Spacing(float height = 8.0f)
    {
        EnsureWindow();
        UmbraNativeUi.Spacing(Math.Max(0.0f, height));
    }

    public void Icon(UmbraIcon icon, UmbraTextTone tone = UmbraTextTone.Normal, float size = 20.0f)
    {
        EnsureWindow();
        if (!Enum.IsDefined(icon))
            throw new ArgumentOutOfRangeException(nameof(icon));
        if (!Enum.IsDefined(tone))
            throw new ArgumentOutOfRangeException(nameof(tone));
        UmbraNativeUi.Icon(icon, tone, Math.Clamp(size, 8.0f, 96.0f));
    }

    public void Badge(string text, UmbraTextTone tone, UmbraIcon icon = UmbraIcon.None)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(text);
        UmbraNativeUi.Badge(text, tone, icon);
    }

    public void Artwork(string seed, UmbraIcon icon = UmbraIcon.Plug, float size = 72.0f)
    {
        EnsureWindow();
        UmbraNativeUi.Artwork(seed ?? "", icon, Math.Clamp(size, 32.0f, 160.0f));
    }

    public void SetNextWindowSize(float width, float height, bool firstUseOnly = true)
    {
        EnsureRenderThread();
        UmbraNativeUi.SetNextWindowSize(Math.Max(0.0f, width), Math.Max(0.0f, height), firstUseOnly);
    }

    public bool BeginChild(string id, float height, bool border = true)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        bool visible = UmbraNativeUi.BeginChild(id, Math.Max(0.0f, height), border);
        openChildDepth++;
        return visible;
    }

    public bool BeginPanel(
        string id,
        float width,
        float height,
        UmbraPanelStyle style = UmbraPanelStyle.Card)
    {
        EnsureWindow();
        ArgumentException.ThrowIfNullOrWhiteSpace(id);
        if (!Enum.IsDefined(style))
            throw new ArgumentOutOfRangeException(nameof(style));
        bool visible = UmbraNativeUi.BeginPanel(
            id,
            Math.Max(0.0f, width),
            Math.Max(0.0f, height),
            style);
        openChildDepth++;
        return visible;
    }

    public void EndChild()
    {
        EnsureRenderThread();
        if (openChildDepth <= 0)
            throw new InvalidOperationException("Umbra EndChild was called without a matching BeginChild.");
        UmbraNativeUi.EndChild();
        openChildDepth--;
    }

    public void RecoverAfterPluginCallback()
    {
        while (openChildDepth > 0)
        {
            try
            {
                UmbraNativeUi.EndChild();
            }
            finally
            {
                openChildDepth--;
            }
        }

        while (openWindowDepth > 0)
        {
            try
            {
                UmbraNativeUi.EndWindow();
            }
            finally
            {
                openWindowDepth--;
            }
        }
    }

    private void EnsureWindow()
    {
        EnsureRenderThread();
        if (openWindowDepth <= 0)
            throw new InvalidOperationException("Umbra UI operations require an open window.");
    }

    private void EnsureRenderThread()
    {
        if (!IsRenderThread)
            throw new InvalidOperationException("Umbra draw operations must run on the render thread.");
    }
}

public static class UmbraManagedRenderEntryPoint
{
    [UnmanagedCallersOnly(EntryPoint = "UmbraRenderBridge", CallConvs = [typeof(CallConvStdcall)])]
    public static int UmbraRenderBridge(nint eventPointer, int sizeBytes)
    {
        return Process(eventPointer, sizeBytes);
    }

    public static int UmbraRenderBridgeCoreClr(nint eventPointer, int sizeBytes)
    {
        return Process(eventPointer, sizeBytes);
    }

    private static int Process(nint eventPointer, int sizeBytes)
    {
        try
        {
            if (eventPointer == nint.Zero || sizeBytes < Marshal.SizeOf<UmbraNativeRenderEvent>())
                return -1;

            if (!UmbraRuntimeHost.TryGet(out UmbraRuntime? runtime) || runtime is null)
                return 1;

            UmbraNativeRenderEvent renderEvent = Marshal.PtrToStructure<UmbraNativeRenderEvent>(eventPointer);
            return runtime.RenderBridge.Process(renderEvent);
        }
        catch (Exception ex)
        {
            if (UmbraRuntimeHost.TryGet(out UmbraRuntime? runtime) && runtime is not null)
                runtime.Log.Error("umbra_render_bridge_failed=true", ex);
            return -100;
        }
    }
}

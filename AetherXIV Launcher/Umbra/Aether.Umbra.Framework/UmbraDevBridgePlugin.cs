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

namespace Aether.Umbra.Framework;

public sealed class UmbraDevBridgePlugin : IUmbraPlugin
{
    private IUmbraPluginContext? context;
    private UmbraDevBridgeService? service;
    private UmbraRuntimeOptions? options;
    private DateTimeOffset lastControlCheck;
    private UmbraDevBridgeControl? lastControl;

    public string Name => "Umbra Dev Bridge";

    public void Initialize(IUmbraPluginContext context)
    {
        this.context = context;
        service = context.GetService<UmbraDevBridgeService>();
        options = context.GetService<UmbraRuntimeOptions>();
        context.Logger.Info("initialized read_only=true localhost_only=true");
        ApplyControl(force: true);
    }

    public void Update(TimeSpan delta)
    {
        if (DateTimeOffset.UtcNow - lastControlCheck < TimeSpan.FromSeconds(1))
            return;

        ApplyControl(force: false);
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
    }

    public void Dispose()
    {
        if (service is not null)
            service.StopAsync().GetAwaiter().GetResult();
    }

    private void ApplyControl(bool force)
    {
        if (context is null || service is null || options is null)
            return;

        lastControlCheck = DateTimeOffset.UtcNow;
        UmbraDevBridgeControl control = UmbraDevBridgeControl.Ensure(
            options.DevBridgeControlPath,
            options.DevBridgeInitiallyEnabled,
            options.DevBridgePort);

        if (!force && control == lastControl)
            return;

        lastControl = control;
        if (control.Enabled)
        {
            try
            {
                service.StartAsync(control.Port).GetAwaiter().GetResult();
                context.Logger.Info($"enabled port={control.Port}");
            }
            catch (Exception ex)
            {
                context.Logger.Error("start failed", ex);
            }

            return;
        }

        if (service.IsRunning)
        {
            service.StopAsync().GetAwaiter().GetResult();
            context.Logger.Info("disabled");
        }
    }
}

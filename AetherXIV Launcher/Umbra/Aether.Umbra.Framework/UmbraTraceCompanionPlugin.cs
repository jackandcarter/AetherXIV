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

public sealed class UmbraTraceCompanionPlugin : IUmbraPlugin
{
    private IUmbraLogger? logger;
    private UmbraDevBridgeService? devBridge;
    private TimeSpan elapsed;

    public string Name => "Umbra Trace Companion";

    public void Initialize(IUmbraPluginContext context)
    {
        logger = context.Logger;
        devBridge = context.GetService<UmbraDevBridgeService>();
        logger.Info("initialized panels=lobby,map,world,client bridge_source=umbra_dev_bridge");
    }

    public void Update(TimeSpan delta)
    {
        elapsed += delta;
        if (elapsed < TimeSpan.FromSeconds(10))
            return;

        elapsed = TimeSpan.Zero;
        logger?.Info($"heartbeat dev_bridge_running={devBridge?.IsRunning ?? false}");
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
    }

    public void Dispose()
    {
        logger?.Info("disposed");
    }
}

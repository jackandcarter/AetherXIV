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

namespace Aether.Umbra.SamplePlugin;

/// <summary>
/// Test fixture used to verify that Umbra quarantines repeatedly failing plugins.
/// It is not the entry point selected by the sample manifest.
/// </summary>
public sealed class FaultingSamplePlugin : IUmbraPlugin
{
    public string Name => "Umbra Fault Containment Fixture";

    public void Initialize(IUmbraPluginContext context)
    {
        context.Logger.Info("initialized for fault-containment test");
    }

    public void Update(TimeSpan delta)
    {
        throw new InvalidOperationException("Intentional sample update failure.");
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
    }

    public void Dispose()
    {
    }
}

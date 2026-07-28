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

public sealed class UmbraFrameworkLogger(UmbraRuntimeLog log, string scope) : IUmbraLogger
{
    public void Info(string message)
    {
        log.Info($"{scope}.info={message}");
    }

    public void Warning(string message)
    {
        log.Warning($"{scope}.warning={message}");
    }

    public void Error(string message, Exception? exception = null)
    {
        if (exception is null)
            log.Error($"{scope}.error={message}");
        else
            log.Error($"{scope}.error={message}", exception);
    }
}

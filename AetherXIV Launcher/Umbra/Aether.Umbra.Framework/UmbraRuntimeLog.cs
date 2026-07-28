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

namespace Aether.Umbra.Framework;

public sealed class UmbraRuntimeLog
{
    private readonly string path;
    private readonly object gate = new();

    private UmbraRuntimeLog(string path)
    {
        this.path = path;
    }

    public static UmbraRuntimeLog Open(string path)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path)) ?? ".");
        return new UmbraRuntimeLog(path);
    }

    public void Info(string message)
    {
        Write(message);
    }

    public void Warning(string message)
    {
        Write($"warning={message}");
    }

    public void Error(string message)
    {
        Write(message);
    }

    public void Error(string message, Exception exception)
    {
        Write($"{message} error={exception}");
    }

    private void Write(string message)
    {
        lock (gate)
            File.AppendAllText(path, $"{DateTimeOffset.Now:O} {message}{Environment.NewLine}");
    }
}

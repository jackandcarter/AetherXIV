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

namespace Aether.Umbra.PluginApi;

public delegate void UmbraCommandHandler(UmbraCommandInvocation invocation);

public sealed record UmbraCommandRegistration(
    string Command,
    string HelpMessage = "",
    bool ShowInHelp = true);

public sealed record UmbraCommandInfo(
    string Command,
    string HelpMessage,
    bool ShowInHelp,
    string PluginId);

public sealed record UmbraCommandInvocation(
    string Command,
    string Arguments,
    string RawInput);

public enum UmbraCommandDispatchStatus
{
    Dispatched,
    NotFound,
    Invalid,
    Failed
}

public sealed record UmbraCommandDispatchResult(
    UmbraCommandDispatchStatus Status,
    string Command,
    string? Error = null)
{
    public bool Succeeded => Status == UmbraCommandDispatchStatus.Dispatched;
}

public interface IUmbraCommandManager
{
    IReadOnlyList<UmbraCommandInfo> Commands { get; }

    IDisposable Register(UmbraCommandRegistration registration, UmbraCommandHandler handler);

    bool Unregister(string command);

    UmbraCommandDispatchResult Dispatch(string content);
}

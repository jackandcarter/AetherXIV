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

public enum UmbraChatTone
{
    Normal,
    System,
    Error
}

public enum UmbraChatDeliveryStatus
{
    Delivered,
    Unavailable,
    Denied,
    Rejected,
    Failed
}

public sealed record UmbraChatAvailability(
    bool CanPrint,
    bool CanSubmit,
    string ClientAdapter);

public sealed record UmbraChatDeliveryResult(
    UmbraChatDeliveryStatus Status,
    string? Error = null)
{
    public bool Succeeded => Status == UmbraChatDeliveryStatus.Delivered;
}

public interface IUmbraChat
{
    UmbraChatAvailability Availability { get; }

    UmbraChatDeliveryResult Print(
        string message,
        string? tag = null,
        UmbraChatTone tone = UmbraChatTone.Normal);

    UmbraChatDeliveryResult Submit(string message);
}

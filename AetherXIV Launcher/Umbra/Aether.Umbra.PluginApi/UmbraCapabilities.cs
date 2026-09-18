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

public static class UmbraCapabilities
{
    public const string NotificationsPost = "notifications.post";

    public const string CommandRegistration = "commands.register";

    public const string ChatPrint = "chat.print";

    public const string ChatSubmit = "chat.submit";

    public const string MapRead = "client.map.read";

    public const string TravelPreview = "travel.preview";

    public const string TravelWarp = "travel.warp";

    public const string ActorAppearanceRead = "client.appearance.read";
}

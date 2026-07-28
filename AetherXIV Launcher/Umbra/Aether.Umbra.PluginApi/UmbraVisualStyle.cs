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

public enum UmbraTextStyle
{
    Body,
    Caption,
    Heading,
    Title
}

public enum UmbraButtonStyle
{
    Default,
    Primary,
    Ghost,
    Navigation,
    Danger
}

public enum UmbraPanelStyle
{
    Default,
    Card,
    Sidebar,
    Detail,
    Selected
}

public enum UmbraIcon
{
    None,
    Umbra,
    Discover,
    Installed,
    Updates,
    Repository,
    Settings,
    Info,
    Search,
    Shield,
    Download,
    Folder,
    Plug,
    Grid,
    List,
    Check,
    Warning,
    Error,
    Refresh,
    Trash,
    Power
}

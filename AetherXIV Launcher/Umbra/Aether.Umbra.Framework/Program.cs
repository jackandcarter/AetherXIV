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

using Aether.Umbra.Framework;

if (args.Length == 1 && string.Equals(args[0], "--probe", StringComparison.OrdinalIgnoreCase))
{
    Console.WriteLine(UmbraFrameworkInfo.ProbeText);
    return 0;
}

if (args.Length == 1 && string.Equals(args[0], "--runtime-probe", StringComparison.OrdinalIgnoreCase))
    return UmbraRuntimeProbe.Run();

if (args.Length == 1 && string.Equals(args[0], "--bootstrap", StringComparison.OrdinalIgnoreCase))
{
    return await UmbraBootstrapRunner.RunFromEnvironmentAsync();
}

Console.WriteLine($"{UmbraFrameworkInfo.Name} {UmbraFrameworkInfo.Version}");
return 0;

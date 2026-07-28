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

using System.Reflection;
using System.Runtime.Loader;
using Aether.Umbra.PluginApi;

namespace Aether.Umbra.Framework;

internal sealed class UmbraPluginLoadContext : AssemblyLoadContext
{
    private static readonly string PluginApiAssemblyName = typeof(IUmbraPlugin).Assembly.GetName().Name!;
    private readonly AssemblyDependencyResolver resolver;

    public UmbraPluginLoadContext(string pluginAssemblyPath)
        : base($"Umbra.Plugin.{Path.GetFileNameWithoutExtension(pluginAssemblyPath)}", isCollectible: true)
    {
        resolver = new AssemblyDependencyResolver(pluginAssemblyPath);
    }

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // The SDK contract must retain a single identity in the default context.
        if (string.Equals(assemblyName.Name, PluginApiAssemblyName, StringComparison.OrdinalIgnoreCase))
            return null;

        string? path = resolver.ResolveAssemblyToPath(assemblyName);
        return path is null ? null : LoadFromAssemblyPath(path);
    }

    protected override nint LoadUnmanagedDll(string unmanagedDllName)
    {
        string? path = resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        return path is null ? nint.Zero : LoadUnmanagedDllFromPath(path);
    }
}

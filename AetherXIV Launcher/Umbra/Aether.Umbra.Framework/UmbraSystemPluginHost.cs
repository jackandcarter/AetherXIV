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

public sealed class UmbraSystemPluginHost(UmbraRuntime runtime) : IDisposable
{
    private readonly List<IUmbraPlugin> plugins = new();

    public void Register(IUmbraPlugin plugin)
    {
        plugins.Add(plugin);
    }

    public void Initialize()
    {
        foreach (IUmbraPlugin plugin in plugins)
        {
            try
            {
                runtime.Log.Info($"umbra_system_plugin_initialize id={plugin.GetType().FullName} name={plugin.Name}");
                plugin.Initialize(new UmbraPluginContext(
                    runtime,
                    plugin.GetType().FullName ?? plugin.Name,
                    capabilities: null,
                    isSystemPlugin: true));
                runtime.Log.Info($"umbra_system_plugin_initialized name={plugin.Name}");
            }
            catch (Exception ex)
            {
                runtime.Log.Error($"umbra_system_plugin_initialize_failed name={plugin.Name}", ex);
            }
        }
    }

    public void Update(TimeSpan delta)
    {
        foreach (IUmbraPlugin plugin in plugins)
        {
            try
            {
                plugin.Update(delta);
            }
            catch (Exception ex)
            {
                runtime.Log.Error($"umbra_system_plugin_update_failed name={plugin.Name}", ex);
            }
        }
    }

    public void Draw(IUmbraDrawContext drawContext)
    {
        foreach (IUmbraPlugin plugin in plugins)
        {
            try
            {
                plugin.Draw(drawContext);
            }
            catch (Exception ex)
            {
                runtime.Log.Error($"umbra_system_plugin_draw_failed name={plugin.Name}", ex);
            }
            finally
            {
                if (drawContext is IUmbraDrawContextRecovery recovery)
                    recovery.RecoverAfterPluginCallback();
            }
        }
    }

    public void Dispose()
    {
        for (int index = plugins.Count - 1; index >= 0; index--)
        {
            IUmbraPlugin plugin = plugins[index];
            try
            {
                plugin.Dispose();
                runtime.Log.Info($"umbra_system_plugin_disposed name={plugin.Name}");
            }
            catch (Exception ex)
            {
                runtime.Log.Error($"umbra_system_plugin_dispose_failed name={plugin.Name}", ex);
            }
        }
    }
}

public sealed class UmbraPluginContext(
    UmbraRuntime runtime,
    string pluginId,
    IReadOnlyCollection<string>? capabilities = null,
    bool isSystemPlugin = false) : IUmbraPluginContext
{
    private readonly HashSet<string> declaredCapabilities = capabilities is null
        ? new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        : new HashSet<string>(capabilities.Where(value => !string.IsNullOrWhiteSpace(value)), StringComparer.OrdinalIgnoreCase);
    private IUmbraCommandManager? commandManager;
    private IUmbraChat? chat;
    private IUmbraTravelService? travel;

    public string PluginId { get; } = pluginId;

    public string ApiVersion => UmbraFrameworkInfo.ApiVersion;

    public string FrameworkVersion => UmbraFrameworkInfo.Version;

    public string ConfigDirectory { get; } = Path.Combine(runtime.Options.CacheDirectory, "PluginConfig", Sanitize(pluginId));

    public IReadOnlyCollection<string> Capabilities => declaredCapabilities;

    public CancellationToken ShutdownToken => runtime.ShutdownToken;

    public IUmbraLogger Logger { get; } = new UmbraFrameworkLogger(runtime.Log, Sanitize(pluginId));

    public bool HasCapability(string capability) =>
        isSystemPlugin || (!string.IsNullOrWhiteSpace(capability) && declaredCapabilities.Contains(capability));

    public TService? GetService<TService>() where TService : class
    {
        if (typeof(TService) == typeof(IUmbraNotificationService))
            return HasCapability(UmbraCapabilities.NotificationsPost) ? (TService)(object)runtime.Notifications.CreateScope(PluginId) : null;

        if (typeof(TService) == typeof(IUmbraCommandManager))
        {
            if (!HasCapability(UmbraCapabilities.CommandRegistration))
                return null;
            commandManager ??= runtime.Commands.CreateScope(PluginId);
            return (TService)commandManager;
        }

        if (typeof(TService) == typeof(IUmbraChat))
        {
            bool allowPrint = HasCapability(UmbraCapabilities.ChatPrint);
            bool allowSubmit = HasCapability(UmbraCapabilities.ChatSubmit);
            if (!allowPrint && !allowSubmit)
                return null;
            chat ??= runtime.Chat.CreateScope(PluginId, allowPrint, allowSubmit);
            return (TService)chat;
        }

        if (typeof(TService) == typeof(IUmbraMapService))
            return HasCapability(UmbraCapabilities.MapRead) ? (TService)(object)runtime.Maps : null;

        if (typeof(TService) == typeof(IUmbraTravelService))
        {
            if (!HasCapability(UmbraCapabilities.TravelPreview))
                return null;
            travel ??= runtime.Travel.CreateScope(HasCapability(UmbraCapabilities.TravelWarp));
            return (TService)travel;
        }

        if (typeof(TService) == typeof(IUmbraActorAppearanceService))
        {
            if (!HasCapability(UmbraCapabilities.ActorAppearanceRead))
                return null;
            return (TService)(object)runtime.ActorAppearance;
        }

        return runtime.GetService<TService>();
    }

    private static string Sanitize(string value)
    {
        char[] chars = value
            .Select(ch => char.IsLetterOrDigit(ch) || ch is '.' or '-' or '_' ? ch : '_')
            .ToArray();
        string sanitized = new(chars);
        return string.IsNullOrWhiteSpace(sanitized) ? "plugin" : sanitized;
    }
}

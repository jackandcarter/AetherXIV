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

using AetherXIV.Core;
using AetherXIV.Data;
using AetherXIV.Launcher.Host;
using AetherXIV.Server.Hosting;

LauncherServiceOptions options = LauncherServiceOptions.FromArgs(args);

WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
builder.WebHost.UseUrls($"http://{options.BindEndpoint.Host}:{options.BindEndpoint.Port}");
builder.Services.AddSingleton(options);
builder.Services.AddSingleton<IClock, SystemClock>();
builder.Services.AddSingleton<IDatabaseConnectionFactory>(_ => new MariaDbConnectionFactory(options.Database));
builder.Services.AddSingleton<ILauncherAccountSessionRepository, MariaDbLauncherAccountSessionRepository>();
builder.Services.AddSingleton<ILauncherContentRepository, MariaDbLauncherContentRepository>();
builder.Services.AddSingleton<LauncherAuthService>();
builder.Services.AddSingleton<LauncherContentService>();

WebApplication app = builder.Build();
LauncherEndpoints.Map(app);

using CancellationTokenSource shutdown = new();
using IDisposable shutdownListener = ConsoleShutdownListener.Attach(
    shutdown,
    new ConsoleDiagnosticSink("AetherXIV.Launcher"),
    "AetherXIV.Launcher");
await app.RunAsync(shutdown.Token);

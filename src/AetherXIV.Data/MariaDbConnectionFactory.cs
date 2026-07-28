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

using MySqlConnector;

namespace AetherXIV.Data;

public interface IDatabaseConnectionFactory
{
    ValueTask<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default);
}

public sealed class MariaDbConnectionFactory : IDatabaseConnectionFactory
{
    private readonly MariaDbOptions options;

    public MariaDbConnectionFactory(MariaDbOptions options)
    {
        this.options = options;
    }

    public async ValueTask<MySqlConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
    {
        MySqlConnection connection = new(options.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        return connection;
    }
}

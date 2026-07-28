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
using MySqlConnector;

namespace AetherXIV.Data;

public sealed class MariaDbPrivateAreaRepository : IPrivateAreaRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbPrivateAreaRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<PrivateAreaRecord?> GetAsync(
        ZoneId parentZoneId,
        string name,
        uint level,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT p.area_id, p.zone_id, p.class_path, p.name, p.area_level,
       p.day_music, p.night_music, p.battle_music,
       r.evidence_status, r.source_type, r.source_ref, r.notes
FROM zone_private_areas p
JOIN provenance_refs r ON r.provenance_id = p.provenance_id
WHERE p.zone_id = @zone_id
  AND p.name = @name
  AND p.area_level = @area_level;
""";
        command.Parameters.AddWithValue("@zone_id", parentZoneId.Value);
        command.Parameters.AddWithValue("@name", name);
        command.Parameters.AddWithValue("@area_level", level);

        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        if (!await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            return null;

        return new PrivateAreaRecord(
            reader.GetUInt32("area_id"),
            new ZoneId(reader.GetUInt32("zone_id")),
            reader.GetString("class_path"),
            reader.GetString("name"),
            reader.GetUInt32("area_level"),
            reader.GetUInt16("day_music"),
            reader.GetUInt16("night_music"),
            reader.GetUInt16("battle_music"),
            new ProvenanceRef(
                Enum.Parse<EvidenceStatus>(reader.GetString("evidence_status"), ignoreCase: true),
                reader.GetString("source_type"),
                reader.GetString("source_ref"),
                reader.GetString("notes")));
    }
}

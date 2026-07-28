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

public sealed class MariaDbCharacterNpcLinkshellStateRepository : ICharacterNpcLinkshellStateRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterNpcLinkshellStateRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CharacterNpcLinkshellStateRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT linkshell_id, is_calling, is_extra
FROM character_npc_linkshell_state
WHERE character_id = @character_id
ORDER BY linkshell_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);
        List<CharacterNpcLinkshellStateRecord> rows = [];
        await using MySqlDataReader reader = await command.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterNpcLinkshellStateRecord(
                characterId,
                reader.GetByte("linkshell_id"),
                reader.GetBoolean("is_calling"),
                reader.GetBoolean("is_extra")));
        }

        return rows;
    }

    public async Task SaveAsync(
        CharacterNpcLinkshellStateRecord state,
        CancellationToken cancellationToken = default)
    {
        if (state.LinkshellId >= 64)
            throw new ArgumentOutOfRangeException(nameof(state), "NPC linkshell id must be below 64.");

        await using MySqlConnection connection = await connectionFactory.OpenConnectionAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
INSERT INTO character_npc_linkshell_state (character_id, linkshell_id, is_calling, is_extra)
VALUES (@character_id, @linkshell_id, @is_calling, @is_extra)
ON DUPLICATE KEY UPDATE
  is_calling = VALUES(is_calling),
  is_extra = VALUES(is_extra);
""";
        command.Parameters.AddWithValue("@character_id", state.CharacterId.Value);
        command.Parameters.AddWithValue("@linkshell_id", state.LinkshellId);
        command.Parameters.AddWithValue("@is_calling", state.IsCalling ? 1 : 0);
        command.Parameters.AddWithValue("@is_extra", state.IsExtra ? 1 : 0);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

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

public sealed class MariaDbCharacterFriendRepository : ICharacterFriendRepository
{
    private readonly IDatabaseConnectionFactory connectionFactory;

    public MariaDbCharacterFriendRepository(IDatabaseConnectionFactory connectionFactory)
    {
        this.connectionFactory = connectionFactory;
    }

    public async Task<IReadOnlyList<CharacterFriendRecord>> ListAsync(
        CharacterId characterId,
        CancellationToken cancellationToken = default)
    {
        await using MySqlConnection connection = await connectionFactory
            .OpenConnectionAsync(cancellationToken)
            .ConfigureAwait(false);
        await using MySqlCommand command = connection.CreateCommand();
        command.CommandText = """
SELECT friendship.character_id, friendship.friend_character_id, friendship.slot,
       friend.name AS friend_name
FROM character_friends AS friendship
INNER JOIN characters AS friend
  ON friend.character_id = friendship.friend_character_id
WHERE friendship.character_id = @character_id
  AND friend.creation_state = 'Active'
ORDER BY friendship.slot, friendship.friend_character_id;
""";
        command.Parameters.AddWithValue("@character_id", characterId.Value);

        List<CharacterFriendRecord> rows = [];
        await using MySqlDataReader reader = await command
            .ExecuteReaderAsync(cancellationToken)
            .ConfigureAwait(false);
        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
        {
            rows.Add(new CharacterFriendRecord(
                new CharacterId(reader.GetUInt32("character_id")),
                new CharacterId(reader.GetUInt32("friend_character_id")),
                reader.GetByte("slot"),
                reader.GetString("friend_name")));
        }

        return rows;
    }
}

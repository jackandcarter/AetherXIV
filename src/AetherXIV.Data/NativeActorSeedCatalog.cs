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

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using AetherXIV.Core;
using MySqlConnector;

namespace AetherXIV.Data;

public sealed record NativeActorSeedManifest(
    string Schema,
    string SeedId,
    string Version,
    int ZoneCount,
    int ActorClassCount,
    int ActorAppearanceCount,
    int StaticActorSpawnCount,
    int ExcludedOrphanSpawnCount,
    int ExcludedInvalidActorClassSpawnCount,
    IReadOnlyDictionary<string, string> Files,
    string Notes);

public sealed record NativeActorIdentitySeed(
    string Schema,
    IReadOnlyList<NativeActorTerritorySeed> Territories);

public sealed record NativeActorTerritorySeed(
    uint TerritoryId,
    NativeActorPublicAreaSeed PublicArea,
    IReadOnlyList<NativeStaticActorIdentitySeed> StaticActorAssignments,
    IReadOnlyList<NativeResidentDirectorSeed> ResidentDirectors);

public sealed record NativeActorPublicAreaSeed(
    string PrivateAreaName,
    uint PrivateAreaLevel,
    uint AreaMasterNativeSlot);

public sealed record NativeStaticActorIdentitySeed(
    uint SpawnId,
    uint NativeActorSlot);

public sealed record NativeResidentDirectorSeed(
    string ScriptPath,
    uint NativeActorSlot,
    uint NativeClassId,
    string NativeClassPath);

public sealed record NativeActorSeedCatalog(
    NativeActorSeedManifest Manifest,
    IReadOnlyList<ZoneRecord> Zones,
    IReadOnlyList<ActorClassRecord> ActorClasses,
    IReadOnlyList<ActorAppearanceRecord> ActorAppearances,
    IReadOnlyList<StaticActorSpawnRecord> StaticActorSpawns,
    NativeActorIdentitySeed NativeActorIdentities,
    string ContentHash)
{
    public const string ExpectedSchema = "aetherxiv.native-actor-seed.v1";
    public const string ExpectedIdentitySchema = "aetherxiv.native-actor-identities.v2";

    public static async Task<NativeActorSeedCatalog> LoadAsync(
        string rootPath,
        CancellationToken cancellationToken = default)
    {
        string root = Path.GetFullPath(rootPath);
        NativeActorSeedManifest manifest = await ActorDataDatabaseLoader
            .ReadJsonAsync<NativeActorSeedManifest>(Path.Combine(root, "manifest.json"), cancellationToken)
            .ConfigureAwait(false);
        if (!String.Equals(manifest.Schema, ExpectedSchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported native actor seed schema '{manifest.Schema}'.");
        if (String.IsNullOrWhiteSpace(manifest.SeedId) || String.IsNullOrWhiteSpace(manifest.Version))
            throw new InvalidDataException("Native actor seed identity and version are required.");

        foreach ((string fileName, string expectedHash) in manifest.Files.OrderBy(row => row.Key, StringComparer.Ordinal))
        {
            string path = Path.Combine(root, fileName);
            await using FileStream stream = File.OpenRead(path);
            string actualHash = Convert.ToHexString(await SHA256.HashDataAsync(stream, cancellationToken)).ToLowerInvariant();
            if (!String.Equals(actualHash, expectedHash, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException($"Native actor seed hash mismatch for {fileName}.");
        }

        IReadOnlyList<ZoneRecord> zones = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<ZoneRecord>>(Path.Combine(root, "zones.json"), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ActorClassRecord> actorClasses = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<ActorClassRecord>>(Path.Combine(root, "actor-classes.json"), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<ActorAppearanceRecord> appearances = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<ActorAppearanceRecord>>(Path.Combine(root, "actor-appearances.json"), cancellationToken)
            .ConfigureAwait(false);
        IReadOnlyList<StaticActorSpawnRecord> spawns = await ActorDataDatabaseLoader
            .ReadJsonAsync<IReadOnlyList<StaticActorSpawnRecord>>(Path.Combine(root, "static-actor-spawns.json"), cancellationToken)
            .ConfigureAwait(false);
        NativeActorIdentitySeed identities = await ActorDataDatabaseLoader
            .ReadJsonAsync<NativeActorIdentitySeed>(
                Path.Combine(root, "native-actor-slot-overrides.json"),
                cancellationToken)
            .ConfigureAwait(false);

        if (zones.Count != manifest.ZoneCount
            || actorClasses.Count != manifest.ActorClassCount
            || appearances.Count != manifest.ActorAppearanceCount
            || spawns.Count != manifest.StaticActorSpawnCount)
        {
            throw new InvalidDataException("Native actor seed manifest counts do not match its data files.");
        }
        HashSet<uint> classIds = actorClasses.Select(row => row.ActorClassId).ToHashSet();
        StaticActorSpawnRecord? missingClassSpawn = spawns.FirstOrDefault(row => !classIds.Contains(row.ActorClassId));
        if (missingClassSpawn is not null)
        {
            throw new InvalidDataException(
                $"Native static actor spawn {missingClassSpawn.SpawnId} references missing class {missingClassSpawn.ActorClassId}.");
        }
        if (actorClasses.Any(row => !ActorDataDatabaseLoader.IsValidJson(row.EventConditions)))
            throw new InvalidDataException("Native actor seed contains invalid event-condition JSON.");
        if (actorClasses.Any(row => String.IsNullOrWhiteSpace(row.ClassPath)))
            throw new InvalidDataException("Native actor seed contains an actor class without a runtime class path.");

        StaticActorSpawnRecord? invalidNativeSlot = spawns.FirstOrDefault(
            row => row.NativeActorSlot is 0 or > 0x7FFFF);
        if (invalidNativeSlot is not null)
        {
            throw new InvalidDataException(
                $"Native static actor spawn {invalidNativeSlot.SpawnId} has invalid slot {invalidNativeSlot.NativeActorSlot}.");
        }

        IGrouping<string, StaticActorSpawnRecord>? duplicateNativeSlot = spawns
            .Where(row => row.NativeActorSlot.HasValue)
            .GroupBy(
                row => $"{row.ZoneId.Value}:{row.PrivateAreaName ?? String.Empty}:{row.PrivateAreaLevel}:{row.NativeActorSlot}",
                StringComparer.Ordinal)
            .FirstOrDefault(group => group.Count() > 1);
        if (duplicateNativeSlot is not null)
        {
            throw new InvalidDataException(
                $"Native actor seed contains duplicate area-scope slot {duplicateNativeSlot.Key}.");
        }
        ValidateNativeActorIdentities(identities, spawns);

        string contentHashInput = String.Join(
            "\n",
            manifest.Files.OrderBy(row => row.Key, StringComparer.Ordinal).Select(row => $"{row.Key}:{row.Value}"));
        string contentHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(contentHashInput))).ToLowerInvariant();
        return new NativeActorSeedCatalog(
            manifest,
            zones,
            actorClasses,
            appearances,
            spawns,
            identities,
            contentHash);
    }

    private static void ValidateNativeActorIdentities(
        NativeActorIdentitySeed identities,
        IReadOnlyList<StaticActorSpawnRecord> spawns)
    {
        if (!String.Equals(identities.Schema, ExpectedIdentitySchema, StringComparison.Ordinal))
            throw new InvalidDataException($"Unsupported native actor identity schema '{identities.Schema}'.");
        if (identities.Territories is null || identities.Territories.Count == 0)
            throw new InvalidDataException("Native actor identity catalog contains no territories.");

        Dictionary<uint, StaticActorSpawnRecord> spawnsById = spawns.ToDictionary(row => row.SpawnId);
        HashSet<uint> territoryIds = [];
        foreach (NativeActorTerritorySeed territory in identities.Territories)
        {
            if (territory.TerritoryId is 0 or > 0x1FF || !territoryIds.Add(territory.TerritoryId))
                throw new InvalidDataException("Native actor identity catalog contains an invalid or duplicate territory.");
            if (territory.PublicArea is null
                || !String.IsNullOrEmpty(territory.PublicArea.PrivateAreaName)
                || territory.PublicArea.PrivateAreaLevel != 0)
            {
                throw new InvalidDataException(
                    $"Territory {territory.TerritoryId} does not define the public-area identity scope.");
            }

            Dictionary<uint, string> claimedSlots = [];
            ClaimNativeSlot(
                claimedSlots,
                territory.PublicArea.AreaMasterNativeSlot,
                "area master",
                territory.TerritoryId);

            HashSet<uint> assignmentSpawnIds = [];
            foreach (NativeStaticActorIdentitySeed assignment in
                     territory.StaticActorAssignments ?? [])
            {
                if (assignment.SpawnId == 0 || !assignmentSpawnIds.Add(assignment.SpawnId))
                {
                    throw new InvalidDataException(
                        $"Territory {territory.TerritoryId} contains an invalid or duplicate static actor assignment.");
                }
                ClaimNativeSlot(
                    claimedSlots,
                    assignment.NativeActorSlot,
                    $"static actor {assignment.SpawnId}",
                    territory.TerritoryId);
                if (!spawnsById.TryGetValue(assignment.SpawnId, out StaticActorSpawnRecord? spawn))
                {
                    throw new InvalidDataException(
                        $"Native actor identity catalog references missing spawn {assignment.SpawnId}.");
                }
                if (spawn.ZoneId.Value != territory.TerritoryId
                    || !String.Equals(
                        spawn.PrivateAreaName ?? String.Empty,
                        territory.PublicArea.PrivateAreaName ?? String.Empty,
                        StringComparison.Ordinal)
                    || spawn.PrivateAreaLevel != territory.PublicArea.PrivateAreaLevel
                    || spawn.NativeActorSlot != assignment.NativeActorSlot)
                {
                    throw new InvalidDataException(
                        $"Static actor {assignment.SpawnId} disagrees with the native actor identity catalog.");
                }
            }

            HashSet<string> scriptPaths = new(StringComparer.Ordinal);
            foreach (NativeResidentDirectorSeed resident in territory.ResidentDirectors ?? [])
            {
                if (String.IsNullOrWhiteSpace(resident.ScriptPath)
                    || !scriptPaths.Add(resident.ScriptPath)
                    || resident.NativeClassId == 0
                    || String.IsNullOrWhiteSpace(resident.NativeClassPath)
                    || !resident.NativeClassPath.StartsWith("/", StringComparison.Ordinal))
                {
                    throw new InvalidDataException(
                        $"Territory {territory.TerritoryId} contains an invalid or duplicate resident director.");
                }
                ClaimNativeSlot(
                    claimedSlots,
                    resident.NativeActorSlot,
                    $"resident director {resident.ScriptPath}",
                    territory.TerritoryId);
            }
        }
    }

    private static void ClaimNativeSlot(
        IDictionary<uint, string> claimedSlots,
        uint nativeSlot,
        string owner,
        uint territoryId)
    {
        if (nativeSlot is 0 or > 0x7FFFF)
            throw new InvalidDataException($"Territory {territoryId} has an invalid native slot for {owner}.");
        if (claimedSlots.TryGetValue(nativeSlot, out string? existingOwner))
        {
            throw new InvalidDataException(
                $"Territory {territoryId} native slot {nativeSlot} is claimed by {existingOwner} and {owner}.");
        }
        claimedSlots.Add(nativeSlot, owner);
    }
}

public sealed record NativeActorSeedDatabaseLoadRequest(
    string SeedRootPath,
    MariaDbOptions DatabaseOptions,
    WorldRecord? World = null);

public sealed record NativeActorSeedDatabaseLoadResult(
    int ZoneCount,
    int ActorClassCount,
    int ActorAppearanceCount,
    int StaticActorSpawnCount,
    string SeedId,
    string Version,
    string ContentHash);

public sealed class NativeActorSeedDatabaseLoader
{
    public async Task<NativeActorSeedDatabaseLoadResult> LoadAsync(
        NativeActorSeedDatabaseLoadRequest request,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(request);
        NativeActorSeedCatalog catalog = await NativeActorSeedCatalog
            .LoadAsync(request.SeedRootPath, cancellationToken)
            .ConfigureAwait(false);
        WorldRecord world = request.World ?? new WorldRecord(
            new WorldId(1),
            "AetherXIV 2.1 Local",
            new ServerEndpoint("127.0.0.1", 54992));

        await using MySqlConnection connection = new(request.DatabaseOptions.ToConnectionString());
        await connection.OpenAsync(cancellationToken).ConfigureAwait(false);
        await using MySqlTransaction transaction = await connection.BeginTransactionAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            await ActorDataDatabaseLoader.UpsertWorldAsync(connection, transaction, world, cancellationToken).ConfigureAwait(false);
            foreach (ZoneRecord zone in catalog.Zones.OrderBy(row => row.Id.Value))
                await ActorDataDatabaseLoader.UpsertZoneAsync(connection, transaction, zone, world.Id, cancellationToken).ConfigureAwait(false);

            foreach (ActorClassRecord actorClass in catalog.ActorClasses.OrderBy(row => row.ActorClassId))
            {
                ulong provenanceId = await ActorDataDatabaseLoader
                    .GetOrInsertProvenanceAsync(connection, transaction, actorClass.Provenance, cancellationToken)
                    .ConfigureAwait(false);
                await ActorDataDatabaseLoader
                    .UpsertActorClassAsync(connection, transaction, actorClass, provenanceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            foreach (ActorAppearanceRecord appearance in catalog.ActorAppearances.OrderBy(row => row.ActorClassId))
            {
                ulong provenanceId = await ActorDataDatabaseLoader
                    .GetOrInsertProvenanceAsync(connection, transaction, appearance.Provenance, cancellationToken)
                    .ConfigureAwait(false);
                await ActorDataDatabaseLoader
                    .UpsertActorAppearanceAsync(connection, transaction, appearance, provenanceId, cancellationToken)
                    .ConfigureAwait(false);
            }
            foreach (StaticActorSpawnRecord spawn in catalog.StaticActorSpawns.OrderBy(row => row.SpawnId))
            {
                ulong provenanceId = await ActorDataDatabaseLoader
                    .GetOrInsertProvenanceAsync(connection, transaction, spawn.Provenance, cancellationToken)
                    .ConfigureAwait(false);
                await ActorDataDatabaseLoader
                    .UpsertStaticActorSpawnAsync(connection, transaction, spawn, provenanceId, cancellationToken)
                    .ConfigureAwait(false);
            }

            await DeleteStaleStaticActorSpawnsAsync(
                connection,
                transaction,
                catalog.StaticActorSpawns.Select(row => row.SpawnId).ToArray(),
                cancellationToken).ConfigureAwait(false);
            await UpsertSeedVersionAsync(connection, transaction, catalog, cancellationToken).ConfigureAwait(false);
            await transaction.CommitAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            await transaction.RollbackAsync(cancellationToken).ConfigureAwait(false);
            throw;
        }

        return new NativeActorSeedDatabaseLoadResult(
            catalog.Zones.Count,
            catalog.ActorClasses.Count,
            catalog.ActorAppearances.Count,
            catalog.StaticActorSpawns.Count,
            catalog.Manifest.SeedId,
            catalog.Manifest.Version,
            catalog.ContentHash);
    }

    private static async Task DeleteStaleStaticActorSpawnsAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        IReadOnlyList<uint> retainedSpawnIds,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        string[] parameters = new string[retainedSpawnIds.Count];
        for (int index = 0; index < retainedSpawnIds.Count; index++)
        {
            string parameter = $"@spawn_id_{index}";
            parameters[index] = parameter;
            command.Parameters.AddWithValue(parameter, retainedSpawnIds[index]);
        }

        command.CommandText = $"""
DELETE sas
FROM static_actor_spawns sas
INNER JOIN provenance_refs provenance ON provenance.provenance_id = sas.provenance_id
WHERE provenance.source_type = 'v1-sql'
  AND provenance.source_ref LIKE 'server_spawn_locations:%'
  AND sas.spawn_id NOT IN ({String.Join(",", parameters)});
""";
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }

    private static async Task UpsertSeedVersionAsync(
        MySqlConnection connection,
        MySqlTransaction transaction,
        NativeActorSeedCatalog catalog,
        CancellationToken cancellationToken)
    {
        await using MySqlCommand command = connection.CreateCommand();
        command.Transaction = transaction;
        command.CommandText = """
INSERT INTO runtime_seed_versions (
  seed_id, seed_version, content_hash, zone_count, actor_class_count,
  actor_appearance_count, static_actor_spawn_count)
VALUES (
  @seed_id, @seed_version, @content_hash, @zone_count, @actor_class_count,
  @actor_appearance_count, @static_actor_spawn_count)
ON DUPLICATE KEY UPDATE
  seed_version = VALUES(seed_version),
  content_hash = VALUES(content_hash),
  zone_count = VALUES(zone_count),
  actor_class_count = VALUES(actor_class_count),
  actor_appearance_count = VALUES(actor_appearance_count),
  static_actor_spawn_count = VALUES(static_actor_spawn_count),
  installed_at = CURRENT_TIMESTAMP;
""";
        command.Parameters.AddWithValue("@seed_id", catalog.Manifest.SeedId);
        command.Parameters.AddWithValue("@seed_version", catalog.Manifest.Version);
        command.Parameters.AddWithValue("@content_hash", catalog.ContentHash);
        command.Parameters.AddWithValue("@zone_count", catalog.Zones.Count);
        command.Parameters.AddWithValue("@actor_class_count", catalog.ActorClasses.Count);
        command.Parameters.AddWithValue("@actor_appearance_count", catalog.ActorAppearances.Count);
        command.Parameters.AddWithValue("@static_actor_spawn_count", catalog.StaticActorSpawns.Count);
        await command.ExecuteNonQueryAsync(cancellationToken).ConfigureAwait(false);
    }
}

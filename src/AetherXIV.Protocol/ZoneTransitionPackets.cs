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

namespace AetherXIV.Protocol;

/// <summary>Begins a mass-delete actor transaction.</summary>
public readonly record struct ServerZoneInstanceBeginPacket;

public sealed class ServerZoneInstanceBeginPacketCodec : IPacketCodec<ServerZoneInstanceBeginPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ServerZoneInstanceBegin;

    public Type PacketType => typeof(ServerZoneInstanceBeginPacket);

    public ServerZoneInstanceBeginPacket Decode(SubPacket packet)
    {
        EnsurePacket(packet, Opcode, PayloadSize, "zone-instance begin");
        return new ServerZoneInstanceBeginPacket();
    }

    public SubPacket Encode(uint sourceActorId, ServerZoneInstanceBeginPacket packet) =>
        SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);

    private static void EnsurePacket(SubPacket packet, PacketOpcode opcode, int payloadSize, string name)
    {
        if (packet.Header.Opcode != opcode)
            throw new InvalidDataException($"Expected {name} opcode 0x{(ushort)opcode:X4}.");
        if (packet.Payload.Length != payloadSize)
            throw new InvalidDataException($"{name} payload must be {payloadSize} bytes, got {packet.Payload.Length}.");
    }
}

/// <summary>
/// Counted keep list for up to eight actors in a mass-delete transaction.
/// </summary>
public sealed record ServerZoneInstanceActorsPacket(IReadOnlyList<uint> ActorIds);

public sealed class ServerZoneInstanceActorsPacketCodec : IPacketCodec<ServerZoneInstanceActorsPacket>
{
    public const int MaximumActors = 8;
    public const int PayloadSize = 0x50 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ServerZoneInstanceActors;

    public Type PacketType => typeof(ServerZoneInstanceActorsPacket);

    public ServerZoneInstanceActorsPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected counted mass-delete keep-list opcode 0x{(ushort)Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Counted mass-delete keep-list payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        uint count = PacketBinary.ReadUInt32LittleEndian(payload);
        if (count > MaximumActors)
            throw new InvalidDataException($"Mass-delete keep-list count {count} exceeds {MaximumActors}.");

        uint[] actorIds = new uint[count];
        for (int index = 0; index < actorIds.Length; index++)
            actorIds[index] = PacketBinary.ReadUInt32LittleEndian(payload[(4 + index * sizeof(uint))..]);
        return new ServerZoneInstanceActorsPacket(actorIds);
    }

    public SubPacket Encode(uint sourceActorId, ServerZoneInstanceActorsPacket packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.ActorIds.Count > MaximumActors)
            throw new ArgumentOutOfRangeException(nameof(packet), $"At most {MaximumActors} actor IDs fit in one packet.");

        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, checked((uint)packet.ActorIds.Count));
        for (int index = 0; index < packet.ActorIds.Count; index++)
            PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4 + index * sizeof(uint)), packet.ActorIds[index]);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public sealed record ServerZoneInstanceKeepActorsX32Packet(IReadOnlyList<uint> ActorIds);

public sealed class ServerZoneInstanceKeepActorsX32PacketCodec : IPacketCodec<ServerZoneInstanceKeepActorsX32Packet>
{
    public const int MaximumActors = 32;
    public const int PayloadSize = 0xC0 - 0x20;
    public const int ReservedSize = 0x20;

    public PacketOpcode Opcode => PacketOpcode.ServerZoneInstanceKeepActorsX32;

    public Type PacketType => typeof(ServerZoneInstanceKeepActorsX32Packet);

    public ServerZoneInstanceKeepActorsX32Packet Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected 32-entry mass-delete keep-list opcode 0x{(ushort)Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException(
                $"32-entry mass-delete keep-list payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        uint[] actorIds = new uint[MaximumActors];
        for (int index = 0; index < actorIds.Length; index++)
        {
            actorIds[index] =
                PacketBinary.ReadUInt32LittleEndian(payload[(index * sizeof(uint))..]);
            if (actorIds[index] == 0)
                throw new InvalidDataException("A 32-entry mass-delete keep-list cannot contain an empty actor slot.");
        }

        ReadOnlySpan<byte> reserved = payload.Slice(
            MaximumActors * sizeof(uint),
            ReservedSize);
        if (reserved.IndexOfAnyExcept((byte)0) >= 0)
            throw new InvalidDataException("32-entry mass-delete keep-list reserved bytes must be zero.");

        return new ServerZoneInstanceKeepActorsX32Packet(actorIds);
    }

    public SubPacket Encode(uint sourceActorId, ServerZoneInstanceKeepActorsX32Packet packet)
    {
        ArgumentNullException.ThrowIfNull(packet);
        if (packet.ActorIds.Count != MaximumActors)
            throw new ArgumentOutOfRangeException(
                nameof(packet),
                $"A 32-entry mass-delete keep-list must contain exactly {MaximumActors} actor IDs.");
        if (packet.ActorIds.Any(actorId => actorId == 0))
            throw new ArgumentOutOfRangeException(
                nameof(packet),
                "A 32-entry mass-delete keep-list cannot contain an empty actor slot.");

        byte[] payload = new byte[PayloadSize];
        for (int index = 0; index < packet.ActorIds.Count; index++)
            PacketBinary.WriteUInt32LittleEndian(
                payload.AsSpan(index * sizeof(uint)),
                packet.ActorIds[index]);
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

/// <summary>Commits a mass-delete actor transaction.</summary>
public readonly record struct ServerZoneInstanceEndPacket;

public sealed class ServerZoneInstanceEndPacketCodec : IPacketCodec<ServerZoneInstanceEndPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ServerZoneInstanceEnd;

    public Type PacketType => typeof(ServerZoneInstanceEndPacket);

    public ServerZoneInstanceEndPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected zone-instance end opcode 0x{(ushort)Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Zone-instance end payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");
        return new ServerZoneInstanceEndPacket();
    }

    public SubPacket Encode(uint sourceActorId, ServerZoneInstanceEndPacket packet) =>
        SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
}

public readonly record struct DeleteAllActorsPacket;

public sealed class DeleteAllActorsPacketCodec : IPacketCodec<DeleteAllActorsPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.DeleteAllActors;

    public Type PacketType => typeof(DeleteAllActorsPacket);

    public DeleteAllActorsPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}, got 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Delete-all-actors payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");
        return new DeleteAllActorsPacket();
    }

    public SubPacket Encode(uint sourceActorId, DeleteAllActorsPacket packet) =>
        SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
}

public readonly record struct ZoneTransitionStatePacket(byte State);

public sealed class ZoneTransitionStatePacketCodec : IPacketCodec<ZoneTransitionStatePacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.ZoneTransitionState;

    public Type PacketType => typeof(ZoneTransitionStatePacket);

    public ZoneTransitionStatePacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}, got 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Zone-transition payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");
        return new ZoneTransitionStatePacket(packet.Payload.Span[0]);
    }

    public SubPacket Encode(uint sourceActorId, ZoneTransitionStatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        payload[0] = packet.State;
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

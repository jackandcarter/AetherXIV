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

public readonly record struct PlayerSessionTransitionPacket;

public sealed class PlayerSessionTransitionPacketCodec : IPacketCodec<PlayerSessionTransitionPacket>
{
    public const int PayloadSize = 0x28 - 0x20;

    public PlayerSessionTransitionPacketCodec(PacketOpcode opcode)
    {
        if (opcode is not PacketOpcode.PlayerLogout and not PacketOpcode.PlayerQuit)
            throw new ArgumentOutOfRangeException(nameof(opcode));
        Opcode = opcode;
    }

    public PacketOpcode Opcode { get; }

    public Type PacketType => typeof(PlayerSessionTransitionPacket);

    public PlayerSessionTransitionPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new InvalidDataException($"Expected opcode 0x{(ushort)Opcode:X4}, got 0x{(ushort)packet.Header.Opcode:X4}.");
        if (packet.Payload.Length != PayloadSize)
            throw new InvalidDataException($"Player session transition payload must be {PayloadSize} bytes, got {packet.Payload.Length}.");
        return new PlayerSessionTransitionPacket();
    }

    public SubPacket Encode(uint sourceActorId, PlayerSessionTransitionPacket packet) =>
        SubPacket.Create(Opcode, sourceActorId, new byte[PayloadSize]);
}

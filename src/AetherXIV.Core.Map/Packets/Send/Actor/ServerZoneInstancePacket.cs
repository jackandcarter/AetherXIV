using AetherXIV.Core.Common;
using System;
using System.Collections.Generic;
using System.IO;

namespace AetherXIV.Core.Map.packets.send.actor
{
    /// <summary>Begins a mass-delete actor transaction.</summary>
    class ServerZoneInstanceBeginPacket
    {
        public const ushort OPCODE = 0x0006;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId) =>
            ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.ServerZoneInstanceBeginPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.ServerZoneInstanceBeginPacket());
    }

    /// <summary>
    /// Counted body containing up to eight actor IDs that survive the
    /// surrounding mass-delete transaction.
    /// </summary>
    class ServerZoneInstanceActorsPacket
    {
        public const ushort OPCODE = 0x0008;
        public const uint PACKET_SIZE = 0x50;
        public const int MAXIMUM_ACTORS = 8;

        public static SubPacket BuildPacket(uint sourceActorId, IReadOnlyList<uint> actorIds)
        {
            if (actorIds == null)
                throw new ArgumentNullException(nameof(actorIds));
            if (actorIds.Count < 1 || actorIds.Count > MAXIMUM_ACTORS)
                throw new ArgumentOutOfRangeException(nameof(actorIds), actorIds.Count,
                    "A counted mass-delete keep-list packet must contain between one and eight actor IDs.");

            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.ServerZoneInstanceActorsPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.ServerZoneInstanceActorsPacket(actorIds));
        }
    }

    /// <summary>
    /// Fixed-width body of the retail mass-delete actor keep list. It carries
    /// exactly 32 actor IDs before any counted 0x0008 keep-list chunks.
    /// The final 0x20 bytes are reserved and remain zero.
    /// </summary>
    class ServerZoneInstanceKeepActorsX32Packet
    {
        public const ushort OPCODE = 0x000A;
        public const uint PACKET_SIZE = 0xC0;
        public const int MAXIMUM_ACTORS = 32;

        public static SubPacket BuildPacket(uint sourceActorId, IReadOnlyList<uint> actorIds)
        {
            if (actorIds == null)
                throw new ArgumentNullException(nameof(actorIds));
            if (actorIds.Count != MAXIMUM_ACTORS)
                throw new ArgumentOutOfRangeException(nameof(actorIds), actorIds.Count,
                    "A 32-entry mass-delete keep-list packet must contain exactly 32 actor IDs.");

            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.ServerZoneInstanceKeepActorsX32PacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.ServerZoneInstanceKeepActorsX32Packet(actorIds));
        }
    }

    /// <summary>Commits a mass-delete actor transaction.</summary>
    class ServerZoneInstanceEndPacket
    {
        public const ushort OPCODE = 0x0007;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId) =>
            ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.ServerZoneInstanceEndPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.ServerZoneInstanceEndPacket());
    }
}

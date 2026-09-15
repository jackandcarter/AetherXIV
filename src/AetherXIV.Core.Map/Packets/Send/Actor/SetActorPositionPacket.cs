using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorPositionPacket
    {
        public const ushort OPCODE = 0x00CE;
        public const uint PACKET_SIZE = 0x48;

        public const ushort SPAWNTYPE_FADEIN = 0;
        public const ushort SPAWNTYPE_PLAYERWAKE = 1;
        public const ushort SPAWNTYPE_WARP_DUTY = 2;
        public const ushort SPAWNTYPE_WARP2 = 3;
        public const ushort SPAWNTYPE_WARP3 = 4;
        public const ushort SPAWNTYPE_WARP_YELLOW = 5;
        public const ushort SPAWNTYPE_WARP_DUTY2 = 6;
        public const ushort SPAWNTYPE_WARP_LIGHT = 7;
        
        public static SubPacket BuildPacket(uint sourceActorId, uint actorId, float x, float y, float z, float rotation, ushort spawnType, bool isZoningPlayer)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorPositionPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorPositionPacket(
                    actorId,
                    x,
                    y,
                    z,
                    rotation,
                    spawnType,
                    isZoningPlayer));
        }

    }
}

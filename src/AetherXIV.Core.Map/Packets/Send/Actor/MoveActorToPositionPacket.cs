using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class MoveActorToPositionPacket
    {
        public const ushort OPCODE = 0x00CF;
        public const uint PACKET_SIZE = 0x50;

        public static SubPacket BuildPacket(uint sourceActorId, float x, float y, float z, float rot, ushort moveState)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.MoveActorToPositionPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.MoveActorToPositionPacket(
                    x,
                    y,
                    z,
                    rot,
                    moveState));
        }

    }
}

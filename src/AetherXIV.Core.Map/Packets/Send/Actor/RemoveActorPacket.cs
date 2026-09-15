using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class RemoveActorPacket
    {
        public const ushort OPCODE = 0x00CB;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.RemoveActorPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.RemoveActorPacket(sourceActorId));
        }

    }
}

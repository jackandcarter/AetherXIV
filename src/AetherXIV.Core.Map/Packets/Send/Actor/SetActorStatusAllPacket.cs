using AetherXIV.Core.Common;
using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorStatusAllPacket
    {
        public const ushort OPCODE = 0x0179;
        public const uint PACKET_SIZE = 0x48;

        public static SubPacket BuildPacket(uint sourceActorId, ushort[] statusIds)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorStatusAllPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorStatusAllPacket(statusIds));
        }
    }
}

using System.IO;

using AetherXIV.Core.Common;
using System;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorBGPropertiesPacket
    {
        public const ushort OPCODE = 0x00D8;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, uint val1, uint val2)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorBGPropertiesPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorBGPropertiesPacket(val1, val2));
        }
    }
}

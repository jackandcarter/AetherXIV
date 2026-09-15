using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.events
{
    class EndEventPacket
    {
        public const ushort OPCODE = 0x0131;
        public const uint PACKET_SIZE = 0x50;

        public static SubPacket BuildPacket(uint sourcePlayerActorId, uint eventOwnerActorID, string eventName, byte eventType)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.EndEventPacketCodec(),
                sourcePlayerActorId,
                new AetherXIV.Protocol.EndEventPacket(
                    sourcePlayerActorId,
                    eventType,
                    eventName));
        }
    }
}

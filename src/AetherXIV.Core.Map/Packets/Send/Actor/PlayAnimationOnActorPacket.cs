using AetherXIV.Core.Common;
using System;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class PlayAnimationOnActorPacket
    {
        public const ushort OPCODE = 0x00DA;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, uint animationID)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.PlayAnimationOnActorPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.PlayAnimationOnActorPacket(animationID));
        }
    }
}

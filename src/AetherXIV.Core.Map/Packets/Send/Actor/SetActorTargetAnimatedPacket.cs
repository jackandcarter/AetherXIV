using System;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorTargetAnimatedPacket
    {
        public const ushort OPCODE = 0x00D3;
        public const uint PACKET_SIZE = 0x28;
        
        public static SubPacket BuildPacket(uint sourceActorId, uint targetID)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorTargetAnimatedPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorTargetAnimatedPacket(targetID));
        }
    }
}

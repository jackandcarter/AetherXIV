using AetherXIV.Core.Common;
using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorSpeedPacket
    {
        public const ushort OPCODE = 0x00D0;
        public const uint PACKET_SIZE = 0xA8;

        public const float DEFAULT_STOP = 0.0f;
        public const float DEFAULT_WALK = 2.0f;
        public const float DEFAULT_RUN = 5.0f;
        public const float DEFAULT_ACTIVE = 5.0f;

        public static SubPacket BuildPacket(uint sourceActorId)
        {
            return BuildPacket(
                sourceActorId,
                DEFAULT_STOP,
                DEFAULT_WALK,
                DEFAULT_RUN,
                DEFAULT_ACTIVE);
        }

        public static SubPacket BuildPacket(uint sourceActorId, float stopSpeed, float walkSpeed, float runSpeed, float activeSpeed)
        {               
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorSpeedPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorSpeedPacket(
                    stopSpeed,
                    walkSpeed,
                    runSpeed,
                    activeSpeed));
        }
    }
}

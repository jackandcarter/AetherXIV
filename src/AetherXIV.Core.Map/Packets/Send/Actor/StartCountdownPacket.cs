using AetherXIV.Core.Common;
using System;
using System.IO;
using System.Text;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class StartCountdownPacket
    {
        public const ushort OPCODE = 0xE5;
        public const uint PACKET_SIZE = 0x48;

        public static SubPacket BuildPacket(uint sourceActorId, byte countdownLength, ulong syncTime, string message)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.StartCountdownPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.StartCountdownPacket(
                    countdownLength,
                    syncTime,
                    message));
        }
    }
}

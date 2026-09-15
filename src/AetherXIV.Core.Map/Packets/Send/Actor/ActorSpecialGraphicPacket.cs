using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorQuestGraphicPacket
    {
        public const int NONE            = 0x0;
        public const int QUEST           = 0x2;
        public const int NOGRAPHIC       = 0x3;
        public const int QUEST_IMPORTANT = 0x4;

        public const ushort OPCODE = 0x00E3;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, int iconCode)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorQuestGraphicPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorQuestGraphicPacket(unchecked((uint)iconCode)));
        }
    }
}

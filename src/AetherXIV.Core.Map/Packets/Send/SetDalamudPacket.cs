using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send
{
    class SetDalamudPacket
    {
        public const ushort OPCODE = 0x0010;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint playerActorId, sbyte dalamudLevel)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetDalamudPacketCodec(),
                playerActorId,
                new AetherXIV.Protocol.SetDalamudPacket(dalamudLevel));
        }
    }
}

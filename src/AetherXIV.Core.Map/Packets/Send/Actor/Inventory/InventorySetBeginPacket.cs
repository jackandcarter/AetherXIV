using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.inventory
{
    class InventorySetBeginPacket
    {
        public const ushort OPCODE = 0x0146;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, ushort size, ushort code)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.InventorySetBeginPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.InventorySetBeginPacket(size, code));
        }

    }
}

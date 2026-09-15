using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.inventory
{
    class InventorySetEndPacket
    {
        public const ushort OPCODE = 0x0147;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint playerActorId)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.InventorySetEndPacketCodec(),
                playerActorId,
                new AetherXIV.Protocol.InventorySetEndPacket());
        }
        
    }
}

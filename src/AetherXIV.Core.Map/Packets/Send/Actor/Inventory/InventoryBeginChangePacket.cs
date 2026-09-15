using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.inventory
{
    class InventoryBeginChangePacket
    {
        public const ushort OPCODE = 0x016D;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint playerActorID, bool clearItemPackage = false)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.InventoryBeginChangePacketCodec(),
                playerActorID,
                new AetherXIV.Protocol.InventoryBeginChangePacket(clearItemPackage));
        }
    }
}

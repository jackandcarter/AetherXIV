using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class DeleteAllActorsPacket
    {
        public const ushort OPCODE = 0x0007;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.DeleteAllActorsPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.DeleteAllActorsPacket());
        }
    }
}

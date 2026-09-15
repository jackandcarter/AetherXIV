using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorIsZoningPacket
    {
        public const ushort OPCODE = 0x017B;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, bool isDimmed)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorIsZoningPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorIsZoningPacket(isDimmed));
        }
    }
}

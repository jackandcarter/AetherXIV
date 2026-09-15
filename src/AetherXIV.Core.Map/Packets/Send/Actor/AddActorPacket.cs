using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class AddActorPacket
    {
        public const ushort OPCODE = 0x00CA;
        public const uint PACKET_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, byte val)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.AddActorPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.AddActorPacket(val));
        }

    }
}

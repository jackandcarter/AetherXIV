using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.battle
{
    class CommandResultX00Packet
    {
        public const ushort OPCODE = 0x013C;
        public const uint PACKET_SIZE = 0x48;

        public static SubPacket BuildPacket(uint sourceActorId, uint animationId, ushort commandId)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.CommandResultX00PacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.CommandResultX00Packet(
                    sourceActorId,
                    animationId,
                    commandId,
                    0x0810));
        }
    }
}

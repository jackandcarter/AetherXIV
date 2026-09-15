using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor.battle
{
    // see xtx_command
    enum CommandResultX01PacketCommand : ushort
    {
        Disengage = 12002,
        Attack = 22104,
    }

    class CommandResultX01Packet
    {
        public const ushort OPCODE = 0x0139;
        public const uint PACKET_SIZE = 0x58;

        public static SubPacket BuildPacket(uint sourceActorId, uint animationId, ushort commandId, CommandResult action)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.CommandResultX01PacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.CommandResultX01Packet(
                    sourceActorId,
                    animationId,
                    commandId,
                    0x0810,
                    ToProtocolAction(action)));
        }

        private static AetherXIV.Protocol.CommandResultAction ToProtocolAction(CommandResult action)
        {
            return new AetherXIV.Protocol.CommandResultAction(
                action.targetId,
                action.amount,
                action.worldMasterTextId,
                action.effectId,
                action.param,
                action.hitNum);
        }
    }
}

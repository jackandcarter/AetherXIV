using AetherXIV.Core.Common;
using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.packets.send.actor.battle
{
    class CommandResultX10Packet
    {
        public const ushort OPCODE = 0x013A;
        public const uint PACKET_SIZE = 0xD8;
        
        public static SubPacket BuildPacket(uint sourceActorId, uint animationId, ushort commandId, CommandResult[] actionList, ref int listOffset)
        {
            if (actionList == null)
                throw new ArgumentNullException(nameof(actionList));

            return BuildPacket(sourceActorId, animationId, commandId, (IReadOnlyList<CommandResult>)actionList, ref listOffset);
        }

        public static SubPacket BuildPacket(uint sourceActorId, uint animationId, ushort commandId, List<CommandResult> actionList, ref int listOffset)
        {
            if (actionList == null)
                throw new ArgumentNullException(nameof(actionList));

            return BuildPacket(sourceActorId, animationId, commandId, (IReadOnlyList<CommandResult>)actionList, ref listOffset);
        }

        private static SubPacket BuildPacket(
            uint sourceActorId,
            uint animationId,
            ushort commandId,
            IReadOnlyList<CommandResult> actionList,
            ref int listOffset)
        {
            if (listOffset < 0 || listOffset > actionList.Count)
                throw new ArgumentOutOfRangeException(nameof(listOffset));

            int actionCount = Math.Min(
                AetherXIV.Protocol.CommandResultX10PacketCodec.MaxActions,
                actionList.Count - listOffset);
            List<AetherXIV.Protocol.CommandResultAction> actions =
                new List<AetherXIV.Protocol.CommandResultAction>(actionCount);

            for (int index = 0; index < actionCount; index++)
            {
                CommandResult action = actionList[listOffset + index];
                actions.Add(new AetherXIV.Protocol.CommandResultAction(
                    action.targetId,
                    action.amount,
                    action.worldMasterTextId,
                    action.effectId,
                    action.param,
                    action.hitNum));
            }

            SubPacket packet = ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.CommandResultX10PacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.CommandResultX10Packet(
                    sourceActorId,
                    animationId,
                    commandId,
                    0x0810,
                    actions));
            listOffset += actionCount;
            return packet;
        }
    }
}

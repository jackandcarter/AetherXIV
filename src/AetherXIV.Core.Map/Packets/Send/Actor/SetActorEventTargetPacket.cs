using System;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    /// <summary>
    /// Confirms the player's event/conversation target after the client sends
    /// SetTarget (0x00CD). Retail sends this once before the subsequent
    /// EventStart/RunEventFunction exchange.
    /// </summary>
    class SetActorEventTargetPacket
    {
        public const ushort OPCODE = 0x00D2;
        public const uint PACKET_SIZE = 0x28;
        public const uint INVALID_ACTOR_ID = 0xC0000000;

        public static SubPacket BuildPacket(uint sourceActorId, uint targetActorId)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];
            Buffer.BlockCopy(BitConverter.GetBytes(targetActorId), 0, data, 0, sizeof(uint));
            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}

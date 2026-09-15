using System;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorNamePacket
    {
        public const ushort OPCODE = 0x013D;
        public const uint PACKET_SIZE = 0x48;

        public static SubPacket BuildPacket(uint sourceActorId, uint displayNameID, string customName)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorNamePacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorNamePacket(
                    displayNameID,
                    customName ?? String.Empty));
        }

    }
}

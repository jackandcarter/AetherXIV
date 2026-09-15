using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send
{
    class SetMapPacket
    {
        public const ushort OPCODE = 0x0005;
        public const uint PACKET_SIZE = 0x30;

        public static SubPacket BuildPacket(uint playerActorID, uint mapID, uint regionID)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetMapPacketCodec(),
                playerActorID,
                new AetherXIV.Protocol.SetMapPacket(mapID, regionID));
        }
    }
}

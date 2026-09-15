using AetherXIV.Core.Map.lua;
using System.Collections.Generic;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.player
{
    class GenericDataPacket
    {
        public const ushort OPCODE = 0x0133;
        public const uint PACKET_SIZE = 0xE0;

        public static SubPacket BuildPacket(uint sourceActorId, List<LuaParam> luaParams)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.GenericDataPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.GenericDataPacket(
                    ProtocolPacketAdapter.EncodeLuaParameters(luaParams)));
        }
    }
}

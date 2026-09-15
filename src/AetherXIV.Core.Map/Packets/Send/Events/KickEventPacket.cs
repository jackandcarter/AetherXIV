using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.events
{
    class KickEventPacket
    {
        public const ushort OPCODE = 0x012F;
        public const uint PACKET_SIZE = 0x90;

        public static SubPacket BuildPacket(uint triggerActorId, uint ownerActorId, string eventName, byte eventType, List<LuaParam> luaParams)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.KickEventPacketCodec(),
                triggerActorId,
                new AetherXIV.Protocol.KickEventPacket(
                    triggerActorId,
                    ownerActorId,
                    eventType,
                    eventName,
                    ProtocolPacketAdapter.EncodeLuaParameters(luaParams)));
        }
    }

}

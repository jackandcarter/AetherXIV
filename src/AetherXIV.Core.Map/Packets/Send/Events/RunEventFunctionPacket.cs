using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.events
{
    class RunEventFunctionPacket
    {
        public const ushort OPCODE = 0x0130;
        public const uint PACKET_SIZE = 0xB0;

        public static uint GetPacketSize(byte eventType)
        {
            return PACKET_SIZE;
        }

        public static SubPacket BuildPacket(uint triggerActorID, uint ownerActorID, string eventName, byte eventType, string functionName, List<LuaParam> luaParams)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.RunEventFunctionPacketCodec(),
                triggerActorID,
                new AetherXIV.Protocol.RunEventFunctionPacket(
                    triggerActorID,
                    ownerActorID,
                    eventType,
                    eventName,
                    functionName,
                    ProtocolPacketAdapter.EncodeLuaParameters(luaParams)));
        }
    }
}

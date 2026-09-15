using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class ActorInstantiatePacket
    {
        public const ushort OPCODE = 0x00CC;
        public const uint PACKET_SIZE = 0x128;

        public static SubPacket BuildPacket(
            uint sourceActorId,
            string objectName,
            string className,
            List<LuaParam> initParams,
            ushort areaContainerKey)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.ActorInstantiatePacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.ActorInstantiatePacket(
                    objectName,
                    className,
                    ProtocolPacketAdapter.EncodeLuaParameters(initParams),
                    0,
                    areaContainerKey));
        }

    }
}

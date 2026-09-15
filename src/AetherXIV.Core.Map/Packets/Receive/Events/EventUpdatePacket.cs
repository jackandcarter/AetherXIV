using System;
using System.Collections.Generic;
using System.IO;

using AetherXIV.Core.Map.lua;

namespace AetherXIV.Core.Map.packets.receive.events
{
    class EventUpdatePacket
    {
        public const ushort OPCODE = 0x012E;
        public const uint PACKET_SIZE = 0x78;

        public bool invalidPacket = false;

        public uint triggerActorID;
        public uint serverCodes;
        public uint unknown1;
        public uint unknown2;
        public byte eventType;
        public List<LuaParam> luaParams;

        public EventUpdatePacket(byte[] data)
        {
            try
            {
                AetherXIV.Protocol.EventUpdatePacket decoded =
                    new AetherXIV.Protocol.EventUpdatePacketCodec().Decode(
                        AetherXIV.Protocol.SubPacket.Create(
                            AetherXIV.Protocol.PacketOpcode.EventUpdate,
                            0,
                            data));
                triggerActorID = decoded.TriggerActorId;
                serverCodes = decoded.ServerCodes;
                unknown1 = decoded.Unknown1;
                unknown2 = decoded.Unknown2;
                eventType = decoded.EventType;
                luaParams = ProtocolPacketAdapter.DecodeLuaParameters(decoded.Parameters);
            }
            catch (Exception)
            {
                invalidPacket = true;
                luaParams = new List<LuaParam>();
            }
        }
    }
}

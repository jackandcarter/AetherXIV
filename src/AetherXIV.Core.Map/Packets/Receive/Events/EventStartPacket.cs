using AetherXIV.Core.Common;
using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive.events
{
    class EventStartPacket
    {
        public const ushort OPCODE = 0x012D;
        public const uint PACKET_SIZE = 0xD8;

        public bool invalidPacket = false;

        public uint triggerActorID;
        public uint ownerActorID;
        public uint serverCodes;
        public uint unknown;
        public byte eventType;
        public string eventName;
        public List<LuaParam> luaParams;

        public uint errorIndex;
        public uint errorNum;
        public string error = null;
        
        public EventStartPacket(byte[] data)
        {
            try
            {
                AetherXIV.Protocol.EventStartPacket decoded =
                    new AetherXIV.Protocol.EventStartPacketCodec().Decode(
                        AetherXIV.Protocol.SubPacket.Create(
                            AetherXIV.Protocol.PacketOpcode.EventStart,
                            0,
                            data));
                triggerActorID = decoded.TriggerActorId;
                ownerActorID = decoded.OwnerActorId;
                serverCodes = decoded.ServerCodes;
                unknown = decoded.Unknown;
                eventType = decoded.EventType;
                eventName = decoded.EventName;
                luaParams = ProtocolPacketAdapter.DecodeLuaParameters(decoded.Parameters);
                if (decoded.IsClientScriptError)
                {
                    errorIndex = decoded.ClientScriptErrorIndex;
                    errorNum = decoded.ClientScriptErrorCount;
                    error = decoded.ClientScriptErrorText;
                }
            }
            catch (Exception)
            {
                invalidPacket = true;
                luaParams = new List<LuaParam>();
            }
        }
    }
}

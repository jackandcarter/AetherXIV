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
        private const int PARAMETER_OFFSET = 0x49;

        public static uint GetPacketSize(byte eventType)
        {
            return PACKET_SIZE;
        }

        public static SubPacket BuildPacket(uint triggerActorID, uint ownerActorID, string eventName, byte eventType, string functionName, List<LuaParam> luaParams)
        {
            uint packetSize = GetPacketSize(eventType);
            byte[] data = new byte[packetSize - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write((UInt32)triggerActorID);
                    binWriter.Write((UInt32)ownerActorID);
                    binWriter.Write((Byte)eventType);
                    Utils.WriteNullTermString(binWriter, eventName);
                    binWriter.Seek(0x29, SeekOrigin.Begin);                
                    Utils.WriteNullTermString(binWriter, functionName);
                    binWriter.Seek(PARAMETER_OFFSET, SeekOrigin.Begin);

                    try
                    {
                        LuaUtils.WriteLuaParams(binWriter, luaParams);
                    }
                    catch (NotSupportedException ex)
                    {
                        throw new ArgumentException(
                            $"RunEventFunction parameters exceed the 0x{packetSize:X} packet contract.",
                            nameof(luaParams),
                            ex);
                    }
                }
            }

            return new SubPacket(OPCODE, triggerActorID, data);
        }
    }
}

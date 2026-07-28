using System;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.social
{
    class SendBlacklistPacket
    {
        public const ushort OPCODE = 0x01CB;
        public const uint PACKET_SIZE = 0x2A8;
        public const int MAX_ENTRIES = 20;
        public const int NAME_SIZE = 0x20;

        public static SubPacket BuildPacket(uint sourceActorId, uint pageIndex, string[] blacklistedNames)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write(pageIndex);
                    int start = checked((int)pageIndex * MAX_ENTRIES);
                    int count = Math.Max(0, Math.Min(MAX_ENTRIES, blacklistedNames.Length - start));
                    binWriter.Write((UInt32)count);

                    for (int i = 0; i < count; i++)
                    {
                        byte[] encoded = Encoding.ASCII.GetBytes(blacklistedNames[start + i] ?? "");
                        binWriter.Write(encoded, 0, Math.Min(encoded.Length, NAME_SIZE));
                        binWriter.BaseStream.Position = 0x08 + ((i + 1) * NAME_SIZE);
                    }
                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}

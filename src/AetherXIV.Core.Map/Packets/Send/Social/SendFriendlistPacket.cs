using System;
using System.IO;
using System.Text;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.social
{
    class SendFriendlistPacket
    {
        public const ushort OPCODE = 0x01CE;
        public const uint PACKET_SIZE = 0x348;
        public const int MAX_ENTRIES = 20;
        public const int NAME_SIZE = 0x20;
        public const int ENTRY_SIZE = 0x28;

        public static SubPacket BuildPacket(uint sourceActorId, uint pageIndex, Tuple<long, string>[] friends)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write(pageIndex);
                    int start = checked((int)pageIndex * MAX_ENTRIES);
                    int count = Math.Max(0, Math.Min(MAX_ENTRIES, friends.Length - start));
                    binWriter.Write((UInt32)count);

                    for (int i = 0; i < count; i++)
                    {
                        Tuple<long, string> friend = friends[start + i];
                        byte[] encoded = Encoding.ASCII.GetBytes(friend.Item2 ?? "");
                        binWriter.Write(encoded, 0, Math.Min(encoded.Length, NAME_SIZE));
                        binWriter.BaseStream.Position = 0x08 + (i * ENTRY_SIZE) + NAME_SIZE;
                        binWriter.Write((UInt64)friend.Item1);
                        binWriter.BaseStream.Position = 0x08 + ((i + 1) * ENTRY_SIZE);
                    }
                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}

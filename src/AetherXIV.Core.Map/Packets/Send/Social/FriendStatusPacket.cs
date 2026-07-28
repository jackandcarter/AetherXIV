using System;
using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.social
{
    class FriendStatusPacket
    {
        public const ushort OPCODE = 0x01CF;
        public const uint PACKET_SIZE = 0x668;
        public const int MAX_ENTRIES = 100;

        public static SubPacket BuildPacket(uint sourceActorId, uint pageIndex, Tuple<long, bool>[] friendStatus)
        {
            byte[] data = new byte[PACKET_SIZE - 0x20];

            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryWriter binWriter = new BinaryWriter(mem))
                {
                    binWriter.Write(pageIndex);
                    int start = checked((int)pageIndex * MAX_ENTRIES);
                    int max;

                    if (friendStatus != null)
                    {
                        max = Math.Max(0, Math.Min(MAX_ENTRIES, friendStatus.Length - start));
                    }
                    else
                        max = 0;

                    binWriter.Write((UInt32)max);

                    for (int i = 0; i < max; i++)
                    {
                        binWriter.Write((UInt64)friendStatus[start + i].Item1);
                        binWriter.Write((UInt64)(friendStatus[start + i].Item2 ? 1 : 0));
                    }

                }
            }

            return new SubPacket(OPCODE, sourceActorId, data);
        }
    }
}

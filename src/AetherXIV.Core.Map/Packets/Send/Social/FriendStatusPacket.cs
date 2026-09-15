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
            int start = checked((int)pageIndex * MAX_ENTRIES);
            int count = friendStatus == null
                ? 0
                : Math.Max(0, Math.Min(MAX_ENTRIES, friendStatus.Length - start));
            AetherXIV.Protocol.FriendStatusEntry[] entries =
                new AetherXIV.Protocol.FriendStatusEntry[count];
            for (int index = 0; index < count; index++)
            {
                entries[index] = new AetherXIV.Protocol.FriendStatusEntry(
                    unchecked((ulong)friendStatus[start + index].Item1),
                    friendStatus[start + index].Item2);
            }

            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.FriendStatusPacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.FriendStatusPacket(pageIndex, entries));
        }
    }
}

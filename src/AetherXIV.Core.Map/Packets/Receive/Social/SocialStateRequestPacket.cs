using System.IO;

namespace AetherXIV.Core.Map.packets.receive.social
{
    class SocialStateRequestPacket
    {
        public const int PAYLOAD_SIZE = 0x08;

        public bool invalidPacket;
        public uint pageIndex;
        public uint requestToken;

        public SocialStateRequestPacket(byte[] data)
        {
            if (data == null || data.Length != PAYLOAD_SIZE)
            {
                invalidPacket = true;
                return;
            }

            using (MemoryStream mem = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(mem))
            {
                pageIndex = reader.ReadUInt32();
                requestToken = reader.ReadUInt32();
            }
        }
    }
}

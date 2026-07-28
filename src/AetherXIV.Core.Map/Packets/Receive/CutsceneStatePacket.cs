using System;
using System.IO;
using System.Text;

namespace AetherXIV.Core.Map.packets.receive
{
    /// <summary>
    /// Direction-specific client 0x00CE payload observed in cutscene_book.
    /// The server does not reply; it records the client's cutscene lifecycle.
    /// </summary>
    class CutsceneStatePacket
    {
        public const ushort OPCODE = 0x00CE;
        public const int PAYLOAD_SIZE = 0x28;
        public const int NAME_SIZE = 0x20;

        public bool invalidPacket;
        public uint state;
        public string cutsceneName = "";
        public uint detail;

        public CutsceneStatePacket(byte[] data)
        {
            if (data == null || data.Length != PAYLOAD_SIZE)
            {
                invalidPacket = true;
                return;
            }

            using (MemoryStream mem = new MemoryStream(data))
            using (BinaryReader reader = new BinaryReader(mem))
            {
                state = reader.ReadUInt32();
                byte[] nameBytes = reader.ReadBytes(NAME_SIZE);
                int terminator = Array.IndexOf(nameBytes, (byte)0);
                cutsceneName = Encoding.ASCII.GetString(
                    nameBytes,
                    0,
                    terminator < 0 ? nameBytes.Length : terminator);
                detail = reader.ReadUInt32();
            }
        }
    }
}

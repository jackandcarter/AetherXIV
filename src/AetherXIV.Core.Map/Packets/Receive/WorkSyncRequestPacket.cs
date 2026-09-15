using System;
using System.IO;
using System.Text;

namespace AetherXIV.Core.Map.packets.receive
{
    /// <summary>
    /// Decodes the client's 0x012F work-property synchronization request.
    /// Bitfield requests carry a 0x09 marker and an inclusive bit range before
    /// the property target; ordinary work requests carry the target directly.
    /// </summary>
    class WorkSyncRequestPacket
    {
        public const ushort OPCODE = 0x012F;
        public const uint PACKET_SIZE = 0x48;

        public bool invalidPacket;
        public uint actorID;
        public string propertyName = String.Empty;
        public ushort from;
        public ushort to;
        public bool requestingBitfield;

        public WorkSyncRequestPacket(byte[] data)
        {
            try
            {
                using MemoryStream memory = new(data, writable: false);
                using BinaryReader reader = new(memory, Encoding.ASCII, leaveOpen: false);

                actorID = reader.ReadUInt32();
                if (reader.BaseStream.Position < reader.BaseStream.Length
                    && reader.ReadByte() == 0x09)
                {
                    requestingBitfield = true;
                    from = reader.ReadUInt16();
                    to = reader.ReadUInt16();
                }
                else
                {
                    reader.BaseStream.Seek(-1, SeekOrigin.Current);
                }

                long stringPosition = reader.BaseStream.Position;
                int length = 0;
                while (reader.BaseStream.Position < reader.BaseStream.Length
                    && reader.ReadByte() != 0
                    && length <= 0x20)
                {
                    length++;
                }

                if (length > 0x20)
                    throw new InvalidDataException("Work property name exceeds the protocol limit.");

                reader.BaseStream.Seek(stringPosition, SeekOrigin.Begin);
                propertyName = Encoding.ASCII.GetString(reader.ReadBytes(length));
                if (propertyName.Length == 0)
                    throw new InvalidDataException("Work property name is empty.");
            }
            catch (Exception)
            {
                invalidPacket = true;
                propertyName = String.Empty;
            }
        }
    }
}

using System.IO;

using AetherXIV.Core.Common;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorAppearancePacket
    {
        public const ushort OPCODE = 0x00D6;
        public const uint PACKET_SIZE = 0x128;
        
        public const int SIZE = 0;
        public const int COLORINFO = 1;
        public const int FACEINFO = 2;
        public const int HIGHLIGHT_HAIR = 3;
        public const int VOICE = 4;
        public const int MAINHAND = 5;
        public const int OFFHAND = 6;
        public const int SPMAINHAND = 7;
        public const int SPOFFHAND = 8;
        public const int THROWING = 9;
        public const int PACK = 10;
        public const int POUCH = 11;
        public const int HEADGEAR = 12;
        public const int BODYGEAR = 13;
        public const int LEGSGEAR = 14;
        public const int HANDSGEAR = 15;
        public const int FEETGEAR = 16;
        public const int WAISTGEAR = 17;
        public const int NECKGEAR = 18;
        public const int L_EAR = 19;
        public const int R_EAR = 20;
        public const int R_WRIST = 21;
        public const int L_WRIST = 22;
        public const int R_RINGFINGER = 23;
        public const int L_RINGFINGER = 24;
        public const int R_INDEXFINGER = 25;
        public const int L_INDEXFINGER = 26;
        public const int UNKNOWN = 27;

        public uint modelID;
        public uint[] appearanceIDs;

        public SetActorAppearancePacket(uint modelID)
        {
            this.modelID = modelID;
            appearanceIDs = new uint[28];
        }

        public SetActorAppearancePacket(uint modelID, uint[] appearanceTable)
        {
            this.modelID = modelID;
            appearanceIDs = appearanceTable;
        }

        public SubPacket BuildPacket(uint sourceActorId)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorAppearancePacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorAppearancePacket(modelID, appearanceIDs));
        }       

    }
}

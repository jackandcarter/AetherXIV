using System.IO;

using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.chara;

namespace AetherXIV.Core.Map.packets.send.actor
{
    class SetActorSubStatePacket
    {
        public const ushort OPCODE = 0x144;
        public const uint PACKET_SIZE = 0x28;

        enum SubStat : int
        {
            Breakage          = 0x00, // (index goes high to low, bitflags)
            Chant             = 0x01, // [Nibbles: left / right hand = value]) (AKA SubStatObject)
            Guard             = 0x02, // [left / right hand = true] 0,1,2,3) ||| High byte also defines how many bools to use as flags for byte 0x4. 
            Waste             = 0x03, // (High Nibble)
            Mode              = 0x04, // ???
            Unknown           = 0x05, // ???
            SubStatMotionPack = 0x06,
            Unknown2          = 0x07,
        }
        public static SubPacket BuildPacket(uint sourceActorId, SubState substate)
        {
            return ProtocolPacketAdapter.Encode(
                new AetherXIV.Protocol.SetActorSubStatePacketCodec(),
                sourceActorId,
                new AetherXIV.Protocol.SetActorSubStatePacket(
                    substate.breakage,
                    substate.chantId,
                    (byte)(substate.guard & 0xF),
                    substate.waste,
                    substate.mode,
                    substate.motionPack));
        }
    }
}

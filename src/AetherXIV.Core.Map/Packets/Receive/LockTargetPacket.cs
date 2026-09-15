using System;
using System.IO;

using AetherXIV.Core.Map.Actors;

namespace AetherXIV.Core.Map.packets.receive
{
    class LockTargetPacket
    {
        private const uint ClearLockActorId = 0xC0000000;

        public bool invalidPacket = false;
        public uint actorID;
        public uint otherVal; //Camera related?

        // Client opcode 0x00CC is directionally overloaded. A zero second
        // value with a concrete actor is the trace-observed short
        // ActorInstantiate acknowledgement sent immediately before an NPC
        // EventStart; combat lock state carries a context value, while the
        // clear sentinel remains lock state even when that value is zero.
        public bool IsActorInstantiateAcknowledge =>
            !invalidPacket &&
            otherVal == 0 &&
            actorID != 0 &&
            actorID != ClearLockActorId &&
            actorID != Actor.INVALID_ACTORID &&
            actorID != UInt32.MaxValue;

        public bool IsClearLock =>
            actorID == ClearLockActorId ||
            actorID == Actor.INVALID_ACTORID ||
            actorID == UInt32.MaxValue;

        public LockTargetPacket(byte[] data)
        {
            using (MemoryStream mem = new MemoryStream(data))
            {
                using (BinaryReader binReader = new BinaryReader(mem))
                {
                    try{
                        actorID = binReader.ReadUInt32();
                        otherVal = binReader.ReadUInt32(); 
                    }
                    catch (Exception){
                        invalidPacket = true;
                    }
                }
            }
        }
    }
}

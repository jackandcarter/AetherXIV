namespace AetherXIV.Core.Map.actors.area
{
    class SpawnLocation
    {
        public uint spawnId;
        public uint classId;
        public string uniqueId;
        public uint zoneId;
        public string privAreaName;
        public uint privAreaLevel;
        public float x;
        public float y;
        public float z;
        public float rot;
        public ushort state;
        public uint animId;
        public uint? nativeActorSlot;

        public SpawnLocation(uint spawnId, uint classId, string uniqueId, uint zoneId, string privAreaName, uint privAreaLevel, float x, float y, float z, float rot, ushort state, uint animId, uint? nativeActorSlot)
        {
            this.spawnId = spawnId;
            this.classId = classId;
            this.uniqueId = uniqueId;
            this.zoneId = zoneId;
            this.privAreaName = privAreaName;
            this.privAreaLevel = privAreaLevel;
            this.x = x;
            this.y = y;
            this.z = z;
            this.rot = rot;
            this.state = state;
            this.animId = animId;
            this.nativeActorSlot = nativeActorSlot;
        }
    }
}

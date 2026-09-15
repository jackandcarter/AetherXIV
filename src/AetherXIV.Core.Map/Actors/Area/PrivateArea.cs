using AetherXIV.Core.Common;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.send.actor;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.actors.area
{
    class PrivateArea : Area    
    {
        private Zone parentZone;
        private string privateAreaName;
        private new uint privateAreaType;

        public PrivateArea(Zone parent, uint id, string classPath, string privateAreaName, uint privateAreaType, ushort bgmDay, ushort bgmNight, ushort bgmBattle)
            : base(
                parent.GetTerritoryId(),
                NativeActorId.ComposeNonPlayer(parent.GetTerritoryId(), 1),
                parent.GetTerritoryId(),
                false,
                parent.zoneName,
                parent.regionId,
                classPath,
                bgmDay,
                bgmNight,
                bgmBattle,
                parent.isIsolated,
                parent.isInn,
                parent.canRideChocobo,
                parent.canStealth,
                true)
        {
            this.parentZone = parent;
            this.zoneName = parent.zoneName;
            this.privateAreaName = privateAreaName;
            this.privateAreaType = privateAreaType;
            this.actorName = string.Format(
                "_areaMaster@{0:X3}{1:X2}",
                parent.GetTerritoryId(),
                privateAreaType);
        }

        public override string GetPrivateAreaName()
        {
            return privateAreaName;
        }

        public override uint GetPrivateAreaType()
        {
            return privateAreaType;
        }

        public override bool IsPublic()
        {
            return false;
        }

        public Zone GetParentZone()
        {
            return parentZone;
        }

        internal uint ReserveStaticActorNumber(SpawnLocation spawn)
        {
            uint actorNumber = parentZone.ReservePrivateStaticActorNumber(
                spawn.spawnId,
                privateAreaName,
                privateAreaType,
                spawn.uniqueId);
            return ReserveCompatibilityActorSlot(
                actorNumber,
                System.String.Format(
                    "private-static:{0}:{1}",
                    spawn.spawnId,
                    spawn.uniqueId));
        }

        internal uint AllocateTransientActorNumber(string uniqueId)
        {
            uint actorNumber = parentZone.AllocatePrivateTransientActorNumber(
                privateAreaName,
                privateAreaType,
                uniqueId);
            return ReserveCompatibilityActorSlot(
                actorNumber,
                "private-transient");
        }

        internal void ReleaseTransientActorNumberFromParent(uint actorNumber)
        {
            parentZone.ReleasePrivateTransientActorNumber(actorNumber);
        }

        public override SubPacket CreateScriptBindPacket()
        {
            List<LuaParam> lParams;

            string path = className;

            string realClassName = className.Substring(className.LastIndexOf("/") + 1);

            lParams = LuaUtils.CreateLuaParamList(classPath, false, true, zoneName, privateAreaName, privateAreaType, canRideChocobo ? (byte)1 : (byte)0, canStealth, isInn, false, false, false, false, false, false);
            ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                realClassName,
                lParams,
                GetActorInstantiationAreaKey()).DebugPrintSubPacket();
            return ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                realClassName,
                lParams,
                GetActorInstantiationAreaKey());
        }


        public void AddSpawnLocation(SpawnLocation spawn)
        {
            mSpawnLocations.Add(spawn);
        }

        public void SpawnAllActors()
        {
            foreach (SpawnLocation spawn in mSpawnLocations)
                SpawnActor(spawn);
        }
    }
}

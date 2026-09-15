using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.chara;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.send.actor;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.Actors
{
    class WorldMaster : Actor
    {
        public WorldMaster() : base(0x5FF80001)
        {
            this.displayNameId = 0;
            this.customDisplayName = "worldMaster";

            this.actorName = "worldMaster";
            this.className = "WorldMaster";
        }

        public override SubPacket CreateScriptBindPacket(Player player)
        {
            List<LuaParam> lParams;
            lParams = LuaUtils.CreateLuaParamList("/World/WorldMaster_event", false, false, false, false, false, null);
            return ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                className,
                lParams,
                GetActorInstantiationAreaKey(player));
        }

        public override List<SubPacket> GetSpawnPackets(Player player, ushort spawnType)
        {
            List<SubPacket> subpackets = new List<SubPacket>();
            subpackets.Add(CreateAddActorPacket(0));
            subpackets.Add(CreateSpeedPacket());
            subpackets.Add(CreateSpawnPositonPacket(0));
            subpackets.Add(CreatePositionUpdatePacket());
            subpackets.Add(CreateNamePacket());
            subpackets.Add(CreateStatePacket());
            subpackets.Add(SetActorSubStatePacket.BuildPacket(actorId, currentSubState));
            subpackets.Add(SetActorStatusAllPacket.BuildPacket(actorId, new ushort[20]));
            subpackets.Add(SetActorIconPacket.BuildPacket(actorId, 0));
            subpackets.Add(CreateIsZoneingPacket());
            subpackets.Add(CreateScriptBindPacket(player));
            return subpackets;
        }
    }
}

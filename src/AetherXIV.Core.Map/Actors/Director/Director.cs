using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.area;
using AetherXIV.Core.Map.actors.group;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.send.actor;
using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.actors.director
{
    class Director : Actor
    {
        private uint directorId;
        private string directorScriptPath;
        private List<Actor> members = new List<Actor>();
        protected ContentGroup contentGroup;
        private bool isCreated = false;
        private bool isDeleted = false;
        private bool isDeleting = false;
        private readonly uint nativeClassId;
        private readonly bool hasNativeSlot;

        public Director(uint id, Area zone, string directorPath, bool hasContentGroup, params object[] args)
            : this(id, zone, directorPath, hasContentGroup, 0, null, args)
        {
        }

        public Director(uint id, Area zone, string directorPath, bool hasContentGroup, uint nativeClassId, string nativeClassPath, params object[] args)
            : this(id, zone, directorPath, hasContentGroup, nativeClassId, nativeClassPath, nativeClassId != 0, args)
        {
        }

        public Director(uint id, Area zone, string directorPath, bool hasContentGroup, uint nativeClassId, string nativeClassPath, bool cataloguedSlot, params object[] args)
            : base(NativeActorId.ComposeNonPlayer(zone.GetActorNamespaceId(), id))
        {
            directorId = id;
            this.zone = zone;
            this.zoneId = zone.GetTerritoryId();
            directorScriptPath = directorPath;
            this.nativeClassId = nativeClassId;
            hasNativeSlot = cataloguedSlot;

            if (!String.IsNullOrWhiteSpace(nativeClassPath))
            {
                classPath = nativeClassPath;
                className = nativeClassPath.Substring(nativeClassPath.LastIndexOf("/") + 1);
                GenerateActorName(zone.ResolveObjectNameOrdinal(id, cataloguedSlot));
                isCreated = true;
            }

            if (hasContentGroup)
            {
                contentGroup = this is GuildleveDirector
                    ? Server.GetWorldManager().CreateGLContentGroup(this, GetMembers())
                    : Server.GetWorldManager().CreateContentGroup(this, GetMembers());
            }

            eventConditions = new EventList();
            eventConditions.noticeEventConditions = new List<EventList.NoticeEventCondition>();
            eventConditions.noticeEventConditions.Add(new EventList.NoticeEventCondition("noticeEvent",  0xE,0x0));
            eventConditions.noticeEventConditions.Add(new EventList.NoticeEventCondition("noticeRequest", 0x0, 0x1));
            eventConditions.noticeEventConditions.Add(new EventList.NoticeEventCondition("reqForChild", 0x0, 0x1));            
        }       

        public override SubPacket CreateScriptBindPacket()
        {
            List<LuaParam> actualLParams = new List<LuaParam>();
            actualLParams.Insert(0, new LuaParam(2, classPath));
            actualLParams.Insert(1, new LuaParam(4, 4));
            actualLParams.Insert(2, new LuaParam(4, 4));
            actualLParams.Insert(3, new LuaParam(4, 4));
            actualLParams.Insert(4, new LuaParam(4, 4));
            actualLParams.Insert(5, new LuaParam(4, 4));
            if (nativeClassId != 0)
                actualLParams.Insert(6, new LuaParam(0, (int)nativeClassId));

            // Catalog-backed resident directors already carry the trace-reviewed
            // native class path and class id. Their retail instantiate vector
            // ends at that id, so do not append script-derived compatibility
            // parameters. Non-native directors still obtain their extra
            // construction values from Lua.
            if (nativeClassId == 0)
            {
                List<LuaParam> lparams = LuaEngine.GetInstance()
                    .CallLuaFunctionForReturn(null, this, "init", false);
                for (int i = 1; i < lparams.Count; i++)
                    actualLParams.Add(lparams[i]);
            }

            return ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                className,
                actualLParams,
                GetActorInstantiationAreaKey());
        }

        public override List<SubPacket> GetSpawnPackets(ushort spawnType = 1)
        {
            List<SubPacket> subpackets = new List<SubPacket>();
            subpackets.Add(CreateAddActorPacket(0));
            subpackets.AddRange(GetEventConditionPackets());
            subpackets.Add(CreateSpeedPacket());
            subpackets.Add(CreateSpawnPositonPacket(0));
            subpackets.Add(CreatePositionUpdatePacket());
            subpackets.Add(CreateNamePacket());
            subpackets.Add(CreateStatePacket());
            subpackets.Add(SetActorSubStatePacket.BuildPacket(actorId, currentSubState));
            subpackets.Add(SetActorStatusAllPacket.BuildPacket(actorId, new ushort[20]));
            subpackets.Add(SetActorIconPacket.BuildPacket(actorId, 0));
            subpackets.Add(CreateIsZoneingPacket());
            subpackets.Add(CreateScriptBindPacket());
            return subpackets;
        }

        public override List<SubPacket> GetInitPackets()
        {
            List<SubPacket> subpackets = new List<SubPacket>();
            SetActorPropetyPacket initProperties = new SetActorPropetyPacket("/_init");
            if (directorScriptPath == "WeatherDirector")
                initProperties.AddShort(Utils.MurmurHash2("weatherDirectorWork.weatherId", 0), zone.GetCurrentWeather());
            initProperties.AddTarget();
            subpackets.Add(initProperties.BuildPacket(actorId));
            return subpackets;
        }

        public SubPacket CreateWeatherUpdatePacket(ushort weather)
        {
            // WeatherDirectorBaseClass tags this integer16 as weatherInfo.
            var properties = new SetActorPropetyPacket("weatherDirectorWork/weatherInfo");
            properties.AddShort(Utils.MurmurHash2("weatherDirectorWork.weatherId", 0), weather);
            properties.AddTarget();
            return properties.BuildPacket(actorId);
        }

        public void OnTalkEvent(Player player, Npc npc)
        {
            LuaEngine.GetInstance().CallLuaFunction(player, this, "onTalkEvent", false, npc);
        }

        public void OnCommandEvent(Player player, Command command)
        {
            LuaEngine.GetInstance().CallLuaFunction(player, this, "onCommandEvent", false, command);
        }   

        public void StartDirector(bool spawnImmediate, params object[] args)
        {
            DevDiagnostics.Trace(
                "director.start.begin",
                "area", zone == null ? "" : zone.zoneName,
                "territory", zoneId,
                "path", directorScriptPath ?? "",
                "actorId", String.Format("0x{0:X}", actorId),
                "nativeSlot", NativeActorId.GetNativeSlot(actorId),
                "spawnImmediate", spawnImmediate,
                "argumentCount", args == null ? 0 : args.Length,
                "memberCount", members.Count);

            if (this is GuildleveDirector guildleveDirector)
                guildleveDirector.IncludeEligiblePartyMembers();

            List<LuaParam> lparams = LuaEngine.GetInstance()
                .CallLuaFunctionForReturn(null, this, "init", false, args);
            
            if (lparams != null && lparams.Count >= 1 && lparams[0].value is string)
            {
                classPath = (string)lparams[0].value;
                className = classPath.Substring(classPath.LastIndexOf("/") + 1);
                GenerateActorName(zone.ResolveObjectNameOrdinal(directorId, hasNativeSlot));
                isCreated = true;
            }

            DevDiagnostics.Trace(
                "director.start.init",
                "path", directorScriptPath ?? "",
                "actorId", String.Format("0x{0:X}", actorId),
                "returnCount", lparams == null ? 0 : lparams.Count,
                "classPath", classPath ?? "",
                "className", className ?? "",
                "isCreated", isCreated,
                "memberCount", members.Count);

            if (isCreated && spawnImmediate)
            {
                if (contentGroup != null)
                    contentGroup.Start();

                foreach (Player p in GetPlayerMembers())
                {
                    p.QueuePackets(GetSpawnPackets());
                    p.QueuePackets(GetInitPackets());
                    p.QueuePackets(GetSetEventStatusPackets());
                }
            }

            if (this is GuildleveDirector)
                ((GuildleveDirector)this).LoadGuildleve();

            if (!(this is GuildleveDirector) || !((GuildleveDirector)this).UsesTraceRestoredContent)
                LuaEngine.GetInstance().CallLuaFunction(null, this, "main", true, contentGroup);

            DevDiagnostics.Trace(
                "director.start.complete",
                "path", directorScriptPath ?? "",
                "actorId", String.Format("0x{0:X}", actorId),
                "isCreated", isCreated,
                "spawnImmediate", spawnImmediate,
                "memberCount", members.Count);
        }

        public void StartContentGroup()
        {
            if (contentGroup != null)
                contentGroup.Start();
        }

        public void EndDirector()
        {
            if (isDeleting || isDeleted)
                return;

            isDeleting = true;

            if (contentGroup != null)
                contentGroup.DeleteGroup();

            if (this is GuildleveDirector)
                ((GuildleveDirector)this).EndGuildleveDirector();

            List<Actor> players = GetPlayerMembers();
            foreach (Actor player in players)
                ((Player)player).RemoveDirector(this);
            members.Clear();
            isDeleted = true;
            // Remove this exact logical director from its owning area. Looking
            // the area up by territory can select the public Zone instead of
            // the private/content area that created it, and actor ids are not
            // unique across per-player native director instances.
            zone?.DeleteDirector(this);
        }
        
        public void AddMember(Actor actor)
        {
            if (!members.Contains(actor))
            {
                members.Add(actor);

                if (actor is Player)
                    ((Player)actor).AddDirector(this);

                if (contentGroup != null)
                    contentGroup.AddMember(actor);
            }
        }

        public void RemoveMember(Actor actor)
        {
            if (members.Contains(actor))
                members.Remove(actor);
            if (contentGroup != null)
                contentGroup.RemoveMember(actor.actorId);
            if (GetPlayerMembers().Count == 0 && !isDeleting)
                EndDirector();
        }

        public List<Actor> GetMembers()
        {
            return members;
        }

        public List<Actor> GetPlayerMembers()
        {
            return members.FindAll(s => s is Player);
        }

        public List<Actor> GetNpcMembers()
        {
            return members.FindAll(s => s is Npc);
        }

        public bool IsCreated()
        {
            return isCreated;
        }

        public bool IsDeleted()
        {
            return isDeleted;
        }

        public bool HasContentGroup()
        {
            return contentGroup != null;
        }

        public ContentGroup GetContentGroup()
        {
            return contentGroup;
        }

        public new void GenerateActorName(int actorNumber)
        {            
            //Format Class Name
            string className = this.className;
                
            className = Char.ToLowerInvariant(className[0]) + className.Substring(1);

            //Format Zone Name
            string zoneName = zone.zoneName.Replace("Field", "Fld")
                                           .Replace("Dungeon", "Dgn")
                                           .Replace("Town", "Twn")
                                           .Replace("Battle", "Btl")
                                           .Replace("Test", "Tes")
                                           .Replace("Event", "Evt")
                                           .Replace("Ship", "Shp")
                                           .Replace("Office", "Ofc");
            if (zone is PrivateArea)
            {
                //Check if "normal"
                zoneName = zoneName.Remove(zoneName.Length - 1, 1) + "P";
            }
            zoneName = Char.ToLowerInvariant(zoneName[0]) + zoneName.Substring(1);

            try
            {
                className = className.Substring(0, 20 - zoneName.Length);
            }
            catch (ArgumentOutOfRangeException)
            { }

            //Convert actor number to base 63
            string classNumber = Utils.ToStringBase63(actorNumber);

            //Get stuff after @
            uint zoneId = zone.GetActorNameZoneId();
            uint privLevel = 0;
            if (zone is PrivateArea)
                privLevel = ((PrivateArea)zone).GetPrivateAreaType();

            actorName = String.Format("{0}_{1}_{2}@{3:X3}{4:X2}", className, zoneName, classNumber, zoneId, privLevel);
        }

        public string GetScriptPath()
        {
            return directorScriptPath;
        }

        public uint GetDirectorClassId()
        {
            return nativeClassId;
        }

        public bool HasNativeSlot()
        {
            return hasNativeSlot;
        }

        public uint GetNativeSlot()
        {
            return directorId;
        }

        public void OnEventStart(Player player, object[] args)
        {
            LuaEngine.GetInstance().CallLuaFunction(
                player,
                this,
                "onEventStarted",
                false,
                args);
        }
    }    
}

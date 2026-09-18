using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.area;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.send.actor;
using System;
using System.Collections.Generic;
using System.Linq;
using AetherXIV.Core.Map.packets.send;
using AetherXIV.Core.Map.actors.director;
using AetherXIV.Core.Map.actors.chara;

namespace AetherXIV.Core.Map.Actors
{
    class Area : Actor
    {
        private readonly System.Collections.Concurrent.ConcurrentQueue<Action> travelRequests = new();
        private int travelRequestCount;
        internal bool QueueTravel(Action action)
        {
            if (System.Threading.Interlocked.Increment(ref travelRequestCount) > 32)
            {
                System.Threading.Interlocked.Decrement(ref travelRequestCount);
                return false;
            }
            travelRequests.Enqueue(action);
            return true;
        }
        public string zoneName;        
        public ushort regionId;
        public bool isIsolated, canStealth, isInn, canRideChocobo, isInstanceRaid;
        public ushort weatherNormal, weatherCommon, weatherRare;
        public ushort bgmDay, bgmNight, bgmBattle;

        protected new string classPath;

        public int boundingGridSize = 50;
        public int minX = -5000, minY = -5000, maxX = 5000, maxY = 5000;
        protected int numXBlocks, numYBlocks;
        protected int halfWidth, halfHeight;

        // Legacy Meteor creates a logical director for every request. Native
        // director slots are client-visible identities, not area-wide object
        // singletons, so multiple players may own separate director instances
        // that intentionally use the same stable actor id.
        private List<Director> currentDirectors = new List<Director>();
        private Object directorLock = new Object();

        protected Director mWeatherDirector;

        protected List<SpawnLocation> mSpawnLocations = new List<SpawnLocation>();
        protected Dictionary<uint, Actor> mActorList = new Dictionary<uint, Actor>();
        protected List<Actor>[,] mActorBlock;
        private const uint PublicTransientActorNumberStart = 0x40000;
        private readonly uint territoryId;
        private readonly uint actorNamespaceId;
        private readonly bool usesAuthoritativeNativeSlots;
        private readonly Dictionary<uint, string> reservedActorSlots = new Dictionary<uint, string>();
        private readonly Dictionary<int, string> reservedObjectNameOrdinals = new Dictionary<int, string>();
        private readonly Dictionary<uint, int> transientObjectNameOrdinals = new Dictionary<uint, int>();
        private readonly Object actorIdentityLock = new Object();
        private uint nextPublicAreaActorNumber;
        private int nextPublicTransientObjectNameOrdinal = NativeActorId.MaximumObjectNameOrdinal;

        // Public Lua-facing area contract. The native-slot implementation
        // remains the sole owner of the backing values.
        public string ZoneName { get { return zoneName; } }
        public uint ZoneId { get { return territoryId; } }
        public ushort RegionId { get { return regionId; } }

        protected Area(uint territoryId, uint areaMasterActorId, uint actorNamespaceId, bool usesAuthoritativeNativeSlots, string zoneName, ushort regionId, string classPath, ushort bgmDay, ushort bgmNight, ushort bgmBattle, bool isIsolated, bool isInn, bool canRideChocobo, bool canStealth, bool isInstanceRaid)
            : base(areaMasterActorId)
        {
            this.territoryId = territoryId;
            this.actorNamespaceId = actorNamespaceId;
            this.usesAuthoritativeNativeSlots = usesAuthoritativeNativeSlots;
            nextPublicAreaActorNumber = usesAuthoritativeNativeSlots
                ? PublicTransientActorNumberStart
                : 1;
            this.zoneName = zoneName;
            this.regionId = regionId;
            this.canStealth = canStealth;
            this.isIsolated = isIsolated;
            this.isInn = isInn;
            this.canRideChocobo = canRideChocobo;
            this.isInstanceRaid = isInstanceRaid;

            this.bgmDay = bgmDay;
            this.bgmNight = bgmNight;
            this.bgmBattle = bgmBattle;

            this.displayNameId = 0;
            this.customDisplayName = "_areaMaster";
            this.actorName = String.Format("_areaMaster@{0:X5}", territoryId << 8);

            this.classPath = classPath;
            this.className = classPath.Substring(classPath.LastIndexOf("/") + 1);

            numXBlocks = (maxX - minX) / boundingGridSize;
            numYBlocks = (maxY - minY) / boundingGridSize;
            mActorBlock = new List<Actor>[numXBlocks, numYBlocks];
            halfWidth = numXBlocks / 2;
            halfHeight = numYBlocks / 2;

            for (int y = 0; y < numYBlocks; y++)
            {
                for (int x = 0; x < numXBlocks; x++)
                {
                    mActorBlock[x, y] = new List<Actor>();
                }
            }

            // Slot 1 is the native area-master identity even when the
            // territory's remaining static-NPC assignments have not yet
            // been recovered. Reserve it in every namespace so compatibility
            // allocation can never reuse the client's area-master actor.
            ReserveNativeActorSlot(
                NativeActorId.GetNativeSlot(areaMasterActorId),
                "area-master");
        }

        public uint GetTerritoryId()
        {
            return territoryId;
        }

        public uint GetActorNamespaceId()
        {
            return actorNamespaceId;
        }

        public bool UsesAuthoritativeNativeSlots()
        {
            return usesAuthoritativeNativeSlots;
        }

        public uint GetActorNameZoneId()
        {
            return territoryId;
        }

        public virtual string GetPrivateAreaName()
        {
            return "";
        }

        public virtual uint GetPrivateAreaType()
        {
            return 0;
        }

        public virtual bool IsPublic()
        {
            return true;
        }

        public virtual bool IsPrivate()
        {
            return !IsPublic();
        }

        public override SubPacket CreateScriptBindPacket()
        {
            List<LuaParam> lParams;
            lParams = LuaUtils.CreateLuaParamList(classPath, false, true, zoneName, "/Area/Zone/ZoneDefault", -1, (byte)1, true, false, false, false, false, false, false, false);
            return ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                "ZoneDefault",
                lParams,
                GetActorInstantiationAreaKey());
        }

        public override List<SubPacket> GetSpawnPackets()
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
            subpackets.Add(CreateScriptBindPacket());
            return subpackets;
        }

        // todo: handle instance areas in derived class? (see virtuals)
        #region Actor Management

        public void AddActorToZone(Actor actor)
        {
            lock (mActorList)
            {
                if (actor is Character)
                    ((Character)actor).ResetTempVars();

                if (mActorList.ContainsKey(actor.actorId))
                {
                    Actor existing = mActorList[actor.actorId];
                    DevDiagnostics.Trace(
                        "area.actor.duplicateId",
                        "area", zoneName,
                        "areaActorId", String.Format("0x{0:X}", actorId),
                        "actorId", String.Format("0x{0:X}", actor.actorId),
                        "incoming", actor.actorName,
                        "existing", existing == null ? null : existing.actorName,
                        "privateArea", this is PrivateArea);
                    throw new InvalidOperationException(String.Format(
                        "Actor 0x{0:X8} ({1}) is already published in area {2} by {3}.",
                        actor.actorId,
                        actor.actorName,
                        zoneName,
                        existing == null ? "(unknown)" : existing.actorName));
                }

                mActorList.Add(actor.actorId, actor);


                int gridX = (int)actor.positionX / boundingGridSize;
                int gridY = (int)actor.positionZ / boundingGridSize;

                gridX += halfWidth;
                gridY += halfHeight;

                //Boundries
                if (gridX < 0)
                    gridX = 0;
                if (gridX >= numXBlocks)
                    gridX = numXBlocks - 1;
                if (gridY < 0)
                    gridY = 0;
                if (gridY >= numYBlocks)
                    gridY = numYBlocks - 1;

                lock (mActorBlock)
                    mActorBlock[gridX, gridY].Add(actor);
            }
        }

        public void RemoveActorFromZone(Actor actor)
        {
            if (actor != null)
                lock (mActorList)
                {
                    mActorList.Remove(actor.actorId);

                    int gridX = (int)actor.positionX / boundingGridSize;
                    int gridY = (int)actor.positionZ / boundingGridSize;

                    gridX += halfWidth;
                    gridY += halfHeight;

                    //Boundries
                    if (gridX < 0)
                        gridX = 0;
                    if (gridX >= numXBlocks)
                        gridX = numXBlocks - 1;
                    if (gridY < 0)
                        gridY = 0;
                    if (gridY >= numYBlocks)
                        gridY = numYBlocks - 1;

                    lock (mActorBlock)
                        mActorBlock[gridX, gridY].Remove(actor);
                }
        }

        public void UpdateActorPosition(Actor actor)
        {
            int gridX = (int)actor.positionX / boundingGridSize;
            int gridY = (int)actor.positionZ / boundingGridSize;

            gridX += halfWidth;
            gridY += halfHeight;

            //Boundries
            if (gridX < 0)
                gridX = 0;
            if (gridX >= numXBlocks)
                gridX = numXBlocks - 1;
            if (gridY < 0)
                gridY = 0;
            if (gridY >= numYBlocks)
                gridY = numYBlocks - 1;

            int gridOldX = (int)actor.oldPositionX / boundingGridSize;
            int gridOldY = (int)actor.oldPositionZ / boundingGridSize;

            gridOldX += halfWidth;
            gridOldY += halfHeight;

            //Boundries
            if (gridOldX < 0)
                gridOldX = 0;
            if (gridOldX >= numXBlocks)
                gridOldX = numXBlocks - 1;
            if (gridOldY < 0)
                gridOldY = 0;
            if (gridOldY >= numYBlocks)
                gridOldY = numYBlocks - 1;

            //Still in same block
            if (gridX == gridOldX && gridY == gridOldY)
                return;

            lock (mActorBlock)
            {
                mActorBlock[gridOldX, gridOldY].Remove(actor);
                mActorBlock[gridX, gridY].Add(actor);
            }
        }

        public virtual List<T> GetActorsAroundPoint<T>(float x, float y, int checkDistance) where T : Actor
        {
            checkDistance /= boundingGridSize;

            int gridX = (int)x / boundingGridSize;
            int gridY = (int)y / boundingGridSize;

            gridX += halfWidth;
            gridY += halfHeight;

            //Boundries
            if (gridX < 0)
                gridX = 0;
            if (gridX >= numXBlocks)
                gridX = numXBlocks - 1;
            if (gridY < 0)
                gridY = 0;
            if (gridY >= numYBlocks)
                gridY = numYBlocks - 1;

            List<T> result = new List<T>();

            lock (mActorBlock)
            {
                int firstGridX = Math.Max(0, gridX - checkDistance);
                int lastGridX = Math.Min(numXBlocks - 1, gridX + checkDistance);
                int firstGridY = Math.Max(0, gridY - checkDistance);
                int lastGridY = Math.Min(numYBlocks - 1, gridY + checkDistance);
                for (int gx = firstGridX; gx <= lastGridX; gx++)
                {
                    for (int gy = firstGridY; gy <= lastGridY; gy++)
                    {
                        result.AddRange(mActorBlock[gx, gy].OfType<T>());
                    }
                }
            }

            //Remove players if isolation zone
            if (isIsolated)
                result.RemoveAll(actor => actor is Player);
            return result;
        }

        public virtual List<Actor> GetActorsAroundPoint(float x, float y, int checkDistance)
        {
            return GetActorsAroundPoint<Actor>(x, y, checkDistance);
        }

        public virtual List<Actor> GetActorsAroundActor(Actor actor, int checkDistance)
        {
            return GetActorsAroundActor<Actor>(actor, checkDistance);
        }

        public virtual List<T> GetActorsAroundActor<T>(Actor actor, int checkDistance) where T : Actor
        {
            checkDistance /= boundingGridSize;

            int gridX = (int)actor.positionX / boundingGridSize;
            int gridY = (int)actor.positionZ / boundingGridSize;

            gridX += halfWidth;
            gridY += halfHeight;

            //Boundries
            if (gridX < 0)
                gridX = 0;
            if (gridX >= numXBlocks)
                gridX = numXBlocks - 1;
            if (gridY < 0)
                gridY = 0;
            if (gridY >= numYBlocks)
                gridY = numYBlocks - 1;

            var result = new List<T>();

            lock (mActorBlock)
            {
                for (int gy = ((gridY - checkDistance) < 0 ? 0 : (gridY - checkDistance)); gy <= ((gridY + checkDistance) >= numYBlocks ? numYBlocks - 1 : (gridY + checkDistance)); gy++)
                {
                    for (int gx = ((gridX - checkDistance) < 0 ? 0 : (gridX - checkDistance)); gx <= ((gridX + checkDistance) >= numXBlocks ? numXBlocks - 1 : (gridX + checkDistance)); gx++)
                    {
                        result.AddRange(mActorBlock[gx, gy].OfType<T>());
                    }
                }
            }

            //Remove players if isolation zone
            if (isIsolated)
                result.RemoveAll(nearbyActor => nearbyActor is Player);

            return result;
        }

        #endregion

        public Actor FindActorInArea(uint id)
        {
            lock (mActorList)
            {
                if (!mActorList.ContainsKey(id))
                    return null;
                return mActorList[id];
            }
        }

        public T FindActorInArea<T>(uint id) where T : Actor
        {
            return FindActorInArea(id) as T;
        }

        /// <summary>
        /// Resolves the first NPC in this area whose actor class id matches
        /// <paramref name="classId"/>. Quest ENPCs are registered by class
        /// id, so a sequence flip that (re)enables a marker must bind the
        /// live NPC instance that owns the class. (Garlemald
        /// find_npc_by_class_id.)
        /// </summary>
        public Npc FindNpcByClassId(uint classId)
        {
            lock (mActorList)
            {
                foreach (Actor actor in mActorList.Values)
                {
                    if (actor is Npc npc && npc.GetActorClassId() == classId)
                        return npc;
                }
            }

            return null;
        }

        public Actor FindActorInZoneByUniqueID(string uniqueId)
        {
            lock (mActorList)
            {
                foreach (Actor a in mActorList.Values)
                {
                    if (a is Npc)
                    {
                        if (((Npc)a).GetUniqueId().ToLower().Equals(uniqueId))
                            return a;
                    }
                }
            }
            return null;
        }

        public Player FindPCInZone(string name)
        {
            lock (mActorList)
            {
                foreach (Player player in mActorList.Values.OfType<Player>())
                {
                    if (player.customDisplayName.ToLower().Equals(name.ToLower()))
                        return player;
                }
                return null;
            }
        }

        public Player FindPCInZone(uint id)
        {
            lock (mActorList)
            {
                if (!mActorList.ContainsKey(id))
                    return null;
                return (Player)mActorList[id];
            }
        }

        public void Clear()
        {
            lock (mActorList)
            {
                //Clear All
                mActorList.Clear();
                lock (mActorBlock)
                {
                    for (int y = 0; y < numYBlocks; y++)
                    {
                        for (int x = 0; x < numXBlocks; x++)
                        {
                            mActorBlock[x, y].Clear();
                        }
                    }
                }
            }
        }

        // todo: for zones override this to search contentareas (assuming flag is passed)
        public virtual List<T> GetAllActors<T>() where T : Actor
        {
            lock (mActorList)
            {
                List<T> actorList = new List<T>(mActorList.Count);
                actorList.AddRange(mActorList.Values.OfType<T>());
                return actorList;
            }
        }

        public int GetActorCount()
        {
            lock (mActorList)
            {
                return mActorList.Count;
            }
        }

        public virtual List<Actor> GetAllActors()
        {
            return GetAllActors<Actor>();
        }

        public virtual List<Player> GetPlayers()
        {
            return GetAllActors<Player>();
        }

        public virtual List<BattleNpc> GetMonsters()
        {
            return GetAllActors<BattleNpc>();
        }

        public int SetBattleNpcMinimumHpLock(uint minimumHp)
        {
            int updated = 0;
            lock (mActorList)
            {
                foreach (BattleNpc battleNpc in mActorList.Values.OfType<BattleNpc>())
                {
                    battleNpc.SetMod((uint)Modifier.MinimumHpLock, minimumHp);
                    updated++;
                    DevDiagnostics.Trace(
                        "area.battleNpc.minimumHpLock",
                        "area", zoneName,
                        "privateArea", this is PrivateArea,
                        "actor", String.Format("0x{0:X}", battleNpc.actorId),
                        "actorName", battleNpc.GetName(),
                        "uniqueId", battleNpc.GetUniqueId(),
                        "hp", battleNpc.GetHP(),
                        "maxHp", battleNpc.GetMaxHP(),
                        "minimumHpLock", minimumHp);
                }
            }
            return updated;
        }

        public int EngageAlliesForPlayer(Player player)
        {
            if (player == null)
                return 0;

            if (player.currentContentGroup == null)
            {
                DevDiagnostics.Trace(
                    "director.ally.engage",
                    "area", zoneName,
                    "player", String.Format("0x{0:X}", player.actorId),
                    "playerName", player.GetName(),
                    "state", "blocked",
                    "reason", "player has no content group");
                return 0;
            }

            int engaged = 0;
            Character target = player.target as Character;

            lock (mActorList)
            {
                if (target == null || target.IsDead() || !(target is BattleNpc) || target is Ally)
                    target = FindDirectorEnemyForPlayer(player);

                if (target == null)
                {
                    DevDiagnostics.Trace(
                        "director.ally.engage",
                        "area", zoneName,
                        "player", String.Format("0x{0:X}", player.actorId),
                        "playerName", player.GetName(),
                        "state", "blocked",
                        "reason", "no enemy target");
                    return 0;
                }

                foreach (Ally ally in mActorList.Values.OfType<Ally>())
                {
                    if (ally.IsDead() || ally.currentContentGroup != player.currentContentGroup)
                        continue;

                    ally.neutral = false;
                    ally.isAutoAttackEnabled = true;
                    ally.SetMod((uint)Modifier.MovementSpeed, 8);
                    ally.hateContainer.AddBaseHate(target);

                    if (target is BattleNpc battleNpc)
                        battleNpc.hateContainer.AddBaseHate(ally);

                    ally.Engage(target);
                    engaged++;

                    DevDiagnostics.Trace(
                        "director.ally.engage",
                        "area", zoneName,
                        "player", String.Format("0x{0:X}", player.actorId),
                        "playerName", player.GetName(),
                        "ally", String.Format("0x{0:X}", ally.actorId),
                        "allyName", ally.GetName(),
                        "target", String.Format("0x{0:X}", target.actorId),
                        "targetName", target.GetName(),
                        "state", ally.IsEngaged() ? "engaged" : "requested",
                        "allyContentGroup", ally.currentContentGroup == null ? "none" : ally.currentContentGroup.groupIndex.ToString(),
                        "playerContentGroup", player.currentContentGroup == null ? "none" : player.currentContentGroup.groupIndex.ToString());
                }
            }

            return engaged;
        }

        /// <summary>
        /// Releases the content allies against the player's selected live
        /// enemy. Untouched enemies join through the normal retaliation path
        /// after they are attacked instead of being pre-engaged as one burst.
        /// </summary>
        public int EngageContentBattleForPlayer(Player player)
        {
            if (player == null || player.currentContentGroup == null)
                return 0;

            Character target = player.target as Character;
            if (target == null ||
                target.IsDead() ||
                !(target is BattleNpc) ||
                target is Ally ||
                target.currentContentGroup != player.currentContentGroup)
            {
                DevDiagnostics.Trace(
                    "director.contentBattle.engage",
                    "area", zoneName,
                    "player", String.Format("0x{0:X}", player.actorId),
                    "playerName", player.customDisplayName,
                    "state", "blocked",
                    "reason", "player has no live hostile content target");
                return 0;
            }

            int engaged = EngageAlliesForPlayer(player);
            DevDiagnostics.Trace(
                "director.contentBattle.engage",
                "area", zoneName,
                "player", String.Format("0x{0:X}", player.actorId),
                "playerName", player.customDisplayName,
                "target", String.Format("0x{0:X}", target.actorId),
                "targetName", target.GetName(),
                "allies", engaged,
                "contentGroup", player.currentContentGroup.groupIndex,
                "enemyPreEngage", false);
            return engaged;
        }

        private Character FindDirectorEnemyForPlayer(Player player)
        {
            foreach (BattleNpc battleNpc in mActorList.Values.OfType<BattleNpc>())
            {
                if (battleNpc is Ally || battleNpc.IsDead())
                    continue;

                if (battleNpc.currentContentGroup != player.currentContentGroup)
                    continue;

                return battleNpc;
            }

            return null;
        }

        public virtual List<Ally> GetAllies()
        {
            return GetAllActors<Ally>();
        }

        public void BroadcastPacketsAroundActor(Actor actor, List<SubPacket> packets)
        {
            foreach (SubPacket packet in packets)
                BroadcastPacketAroundActor(actor, packet);
        }

        public void BroadcastPacketAroundActor(Actor actor, SubPacket packet)
        {
            if (isIsolated)
                return;

            List<Actor> aroundActor = GetActorsAroundActor(actor, 50);
            foreach (Actor a in aroundActor)
            {                
                if (a is Player)
                {
                    if (isIsolated)
                        continue;

                    SubPacket clonedPacket = new SubPacket(packet, a.actorId);
                    Player p = (Player)a;                        
                    p.QueuePacket(clonedPacket);
                }
            }            
        }

        public void SpawnActor(SpawnLocation location)
        {
            lock (mActorList)
            {
                ActorClass actorClass = Server.GetWorldManager().GetActorClass(location.classId);

                if (actorClass == null)
                    return;

                uint actorNumber;
                if (this is PrivateArea privateArea)
                {
                    actorNumber = privateArea.ReserveStaticActorNumber(location);
                }
                else if (location.nativeActorSlot.HasValue)
                {
                    actorNumber = ReserveNativeActorSlot(
                        location.nativeActorSlot.Value,
                        String.Format("static-spawn:{0}:{1}", location.spawnId, location.uniqueId));
                }
                else
                {
                    actorNumber = AllocateSpawnedActorNumber();
                    if (!(this is PrivateArea))
                    {
                        int objectNameOrdinal = ResolveObjectNameOrdinal(actorNumber, false);
                        DevDiagnostics.Trace(
                            "area.actor.spawn.compatibilitySlot",
                            "territory", territoryId,
                            "spawnId", location.spawnId,
                            "classId", location.classId,
                            "uniqueId", location.uniqueId,
                            "allocatedSlot", actorNumber,
                            "objectNameOrdinal", objectNameOrdinal);
                    }
                }
                Npc npc = new Npc((int)actorNumber, actorClass, location.uniqueId, this, location.x, location.y, location.z, location.rot, location.state, location.animId, null, location.nativeActorSlot.HasValue);
                TracePrivateAreaSpawn(npc, actorNumber, location.classId, location.uniqueId, false);


                npc.LoadEventConditions(actorClass.eventConditions);
                if (location.uniqueId == "conjurers_guild_scene_entry")
                    npc.SetPushCircleRange("pushDefault", 3.0f);

                AddActorToZone(npc);
            }
        }

        public Npc SpawnActor(uint classId, string uniqueId, float x, float y, float z, float rot = 0, ushort state = 0, uint animId = 0, bool isMob = false)
        {
            lock (mActorList)
            {
                ActorClass actorClass = Server.GetWorldManager().GetActorClass(classId);

                if (actorClass == null)
                    return null;

                uint actorNumber = AllocateSpawnedActorNumber(uniqueId);
                Npc npc;
                if (isMob)
                    npc = new BattleNpc((int)actorNumber, actorClass, uniqueId, this, x, y, z, rot, state, animId, null);
                else
                    npc = new Npc((int)actorNumber, actorClass, uniqueId, this, x, y, z, rot, state, animId, null);
                TracePrivateAreaSpawn(npc, actorNumber, classId, uniqueId, isMob);

                npc.LoadEventConditions(actorClass.eventConditions);
                npc.SetMaxHP(100);
                npc.SetHP(100);
                npc.ResetMoveSpeeds();

                AddActorToZone(npc);

                return npc;
            }
        }

        public Npc SpawnActor(uint classId, string uniqueId, float x, float y, float z, uint regionId, uint layoutId)
        {
            lock (mActorList)
            {
                ActorClass actorClass = Server.GetWorldManager().GetActorClass(classId);

                if (actorClass == null)
                    return null;

                uint actorNumber = AllocateSpawnedActorNumber(uniqueId);
                Npc npc = new Npc((int)actorNumber, actorClass, uniqueId, this, x, y, z, 0, regionId, layoutId);
                TracePrivateAreaSpawn(npc, actorNumber, classId, uniqueId, false);

                npc.LoadEventConditions(actorClass.eventConditions);

                AddActorToZone(npc);

                return npc;
            }
        }

        public BattleNpc GetBattleNpcById(uint id)
        {
            foreach (var bnpc in GetAllActors<BattleNpc>())
            {
                if (bnpc.GetBattleNpcId() == id)
                    return bnpc;
            }
            return null;
        }

        public void DespawnActor(string uniqueId)
        {
            DespawnActor(FindActorInZoneByUniqueID(uniqueId));
        }

        public void DespawnActor(Actor actor)
        {
            if (actor == null)
                return;

            RemoveActorFromZone(actor);
            ReleaseTransientActorNumber(actor.actorId);
        }

        internal uint AllocateSpawnedActorNumber(string uniqueId = "scripted")
        {
            if (this is PrivateArea privateArea)
                return privateArea.AllocateTransientActorNumber(uniqueId);

            lock (actorIdentityLock)
            {
                uint candidate = nextPublicAreaActorNumber;
                while (reservedActorSlots.ContainsKey(candidate))
                    candidate++;

                if (candidate > NativeActorId.MaximumSlot)
                    throw new InvalidOperationException(String.Format(
                        "Area {0} exhausted its transient actor namespace.",
                        zoneName));

                if (candidate >= nextPublicAreaActorNumber)
                    nextPublicAreaActorNumber = candidate + 1;

                if (usesAuthoritativeNativeSlots)
                {
                    while (nextPublicTransientObjectNameOrdinal >= 0
                        && reservedObjectNameOrdinals.ContainsKey(nextPublicTransientObjectNameOrdinal))
                    {
                        nextPublicTransientObjectNameOrdinal--;
                    }

                    if (nextPublicTransientObjectNameOrdinal < 0)
                    {
                        throw new InvalidOperationException(String.Format(
                            "Area {0} exhausted its two-character object-name namespace.",
                            zoneName));
                    }

                    int objectNameOrdinal = nextPublicTransientObjectNameOrdinal--;
                    transientObjectNameOrdinals.Add(candidate, objectNameOrdinal);
                    reservedObjectNameOrdinals.Add(objectNameOrdinal, "transient");
                }

                reservedActorSlots.Add(candidate, "transient");
                return candidate;
            }
        }

        internal int ResolveObjectNameOrdinal(uint actorNumber, bool usesNativeSlot)
        {
            if (usesNativeSlot)
                return NativeActorId.GetObjectNameOrdinal(actorNumber);

            lock (actorIdentityLock)
            {
                if (transientObjectNameOrdinals.TryGetValue(actorNumber, out int objectNameOrdinal))
                    return objectNameOrdinal;
            }

            if (usesAuthoritativeNativeSlots && !(this is PrivateArea))
            {
                throw new InvalidOperationException(String.Format(
                    "Actor slot 0x{0:X} in authoritative territory {1} has no object-name allocation.",
                    actorNumber,
                    territoryId));
            }

            return checked((int)actorNumber);
        }

        internal void ReleaseTransientActorNumber(uint actorId)
        {
            if (NativeActorId.GetKind(actorId) != NativeActorId.NonPlayerKind
                || NativeActorId.GetTerritoryId(actorId) != actorNamespaceId)
            {
                return;
            }

            uint actorSlot = NativeActorId.GetNativeSlot(actorId);
            lock (actorIdentityLock)
            {
                if (!reservedActorSlots.TryGetValue(actorSlot, out string owner)
                    || (!String.Equals(owner, "transient", StringComparison.Ordinal)
                        && !String.Equals(owner, "private-transient", StringComparison.Ordinal)))
                {
                    return;
                }

                reservedActorSlots.Remove(actorSlot);
                if (this is PrivateArea privateArea)
                {
                    privateArea.ReleaseTransientActorNumberFromParent(actorSlot);
                    if (reservedObjectNameOrdinals.TryGetValue(
                            checked((int)actorSlot),
                            out string privateObjectNameOwner)
                        && String.Equals(
                            privateObjectNameOwner,
                            "private-transient",
                            StringComparison.Ordinal))
                    {
                        reservedObjectNameOrdinals.Remove(
                            checked((int)actorSlot));
                    }
                }
                if (transientObjectNameOrdinals.TryGetValue(actorSlot, out int objectNameOrdinal))
                {
                    transientObjectNameOrdinals.Remove(actorSlot);
                    if (reservedObjectNameOrdinals.TryGetValue(objectNameOrdinal, out string objectNameOwner)
                        && String.Equals(objectNameOwner, "transient", StringComparison.Ordinal))
                    {
                        reservedObjectNameOrdinals.Remove(objectNameOrdinal);
                    }
                }
            }
        }

        internal uint ReserveNativeActorSlot(uint nativeSlot, string owner)
        {
            NativeActorId.ComposeNonPlayer(actorNamespaceId, nativeSlot);
            lock (actorIdentityLock)
            {
                if (reservedActorSlots.TryGetValue(nativeSlot, out string existingOwner))
                {
                    throw new InvalidOperationException(String.Format(
                        "Duplicate native actor slot 0x{0:X} in territory {1}: {2} conflicts with {3}.",
                        nativeSlot,
                        territoryId,
                        owner,
                        existingOwner));
                }

                int objectNameOrdinal = NativeActorId.GetObjectNameOrdinal(nativeSlot);
                if (reservedObjectNameOrdinals.TryGetValue(objectNameOrdinal, out string existingObjectNameOwner))
                {
                    throw new InvalidOperationException(String.Format(
                        "Native actor slot 0x{0:X} in territory {1} requires object-name ordinal {2}, but {3} already owns it.",
                        nativeSlot,
                        territoryId,
                        objectNameOrdinal,
                        existingObjectNameOwner));
                }

                reservedActorSlots.Add(nativeSlot, owner);
                reservedObjectNameOrdinals.Add(objectNameOrdinal, owner);
                return nativeSlot;
            }
        }

        internal uint ReserveCompatibilityActorSlot(uint actorSlot, string owner)
        {
            NativeActorId.ComposeNonPlayer(actorNamespaceId, actorSlot);
            if (actorSlot > PrivateAreaActorIdentityPolicy.MaximumActorNumber)
            {
                throw new InvalidOperationException(String.Format(
                    "Compatibility actor slot 0x{0:X} in territory {1} cannot be represented by the client object-name token.",
                    actorSlot,
                    territoryId));
            }

            lock (actorIdentityLock)
            {
                if (reservedActorSlots.TryGetValue(
                    actorSlot,
                    out string existingOwner))
                {
                    throw new InvalidOperationException(String.Format(
                        "Duplicate compatibility actor slot 0x{0:X} in territory {1}: {2} conflicts with {3}.",
                        actorSlot,
                        territoryId,
                        owner,
                        existingOwner));
                }

                int objectNameOrdinal = checked((int)actorSlot);
                if (reservedObjectNameOrdinals.TryGetValue(
                    objectNameOrdinal,
                    out string existingObjectNameOwner))
                {
                    throw new InvalidOperationException(String.Format(
                        "Compatibility actor slot 0x{0:X} in territory {1} conflicts with object-name owner {2}.",
                        actorSlot,
                        territoryId,
                        existingObjectNameOwner));
                }

                reservedActorSlots.Add(actorSlot, owner);
                reservedObjectNameOrdinals.Add(objectNameOrdinal, owner);
                return actorSlot;
            }
        }

        private void ReleaseNativeActorSlot(uint nativeSlot, string owner)
        {
            lock (actorIdentityLock)
            {
                if (reservedActorSlots.TryGetValue(nativeSlot, out string existingOwner)
                    && String.Equals(existingOwner, owner, StringComparison.Ordinal))
                {
                    reservedActorSlots.Remove(nativeSlot);
                    int objectNameOrdinal = NativeActorId.GetObjectNameOrdinal(nativeSlot);
                    if (reservedObjectNameOrdinals.TryGetValue(objectNameOrdinal, out string objectNameOwner)
                        && String.Equals(objectNameOwner, owner, StringComparison.Ordinal))
                    {
                        reservedObjectNameOrdinals.Remove(objectNameOrdinal);
                    }
                }
            }
        }

        private void TracePrivateAreaSpawn(Npc npc, uint actorNumber, uint classId, string uniqueId, bool isMob)
        {
            if (!(this is PrivateArea) || npc == null)
                return;

            PrivateArea privateArea = (PrivateArea)this;
            DevDiagnostics.Trace(
                "area.actor.spawn.private",
                "area", zoneName,
                "areaActorId", String.Format("0x{0:X}", actorId),
                "parentZoneActorId", String.Format("0x{0:X}", privateArea.GetParentZone().actorId),
                "privateArea", privateArea.GetPrivateAreaName(),
                "privateAreaType", privateArea.GetPrivateAreaType(),
                "classId", classId,
                "uniqueId", uniqueId,
                "actorNumber", actorNumber,
                "actorId", String.Format("0x{0:X}", npc.actorId),
                "actorName", npc.actorName,
                "isMob", isMob);
        }

        public Director GetWeatherDirector()
        {
            return mWeatherDirector;
        }

        public void ChangeWeather(ushort weather, ushort transitionTime, Player player, bool zoneWide = false)
        {
            weatherNormal = weather;

            if (player != null && !zoneWide)
            {
                player.QueuePacket(SetWeatherPacket.BuildPacket(player.actorId, weather, transitionTime));
            }
            if (zoneWide)
            {
                lock (mActorList)
                {
                    foreach (var actor in mActorList)
                    {
                        if (actor.Value is Player)
                        {
                            player = ((Player)actor.Value);
                            player.QueuePacket(SetWeatherPacket.BuildPacket(player.actorId, weather, transitionTime));
                        }
                    }
                }
            }
        }                

        public Director CreateDirector(string path, bool hasContentGroup, params object[] args)
        {
            DevDiagnostics.Trace(
                "director.create.request",
                "area", zoneName,
                "areaKind", GetType().Name,
                "territory", territoryId,
                "areaActorId", String.Format("0x{0:X}", actorId),
                "path", path ?? "",
                "hasContentGroup", hasContentGroup,
                "existingDirectorCount", currentDirectors.Count,
                "usesAuthoritativeNativeSlots", usesAuthoritativeNativeSlots,
                "argumentCount", args == null ? 0 : args.Length);

            lock (directorLock)
            {
                if (usesAuthoritativeNativeSlots
                    && NativeActorIdentityPolicy.TryGetResidentDirector(
                    territoryId,
                    path,
                    out uint nativeSlot,
                    out uint nativeClassId,
                    out string nativeClassPath))
                {
                    // Weather is the one area-persistent director created by
                    // Zone itself. Do not turn quest/warp directors into an
                    // area-wide singleton merely because their wire actor id
                    // comes from the native catalog.
                    if (String.Equals(path, "WeatherDirector", StringComparison.Ordinal))
                    {
                        Director existingWeather = currentDirectors.FirstOrDefault(
                            director => !director.IsDeleted()
                                && String.Equals(director.GetScriptPath(), path, StringComparison.Ordinal));
                        if (existingWeather != null)
                        {
                            TraceDirectorCreated(existingWeather, "reused-area-weather");
                            return existingWeather;
                        }
                    }

                    string slotOwner = "resident-director:" + path;
                    lock (actorIdentityLock)
                    {
                        if (!reservedActorSlots.TryGetValue(nativeSlot, out string existingSlotOwner))
                            ReserveNativeActorSlot(nativeSlot, slotOwner);
                        else if (!String.Equals(existingSlotOwner, slotOwner, StringComparison.Ordinal))
                            throw new InvalidOperationException(String.Format(
                                "Native director slot 0x{0:X} in territory {1} is owned by {2}, not {3}.",
                                nativeSlot,
                                territoryId,
                                existingSlotOwner,
                                slotOwner));
                    }

                    Director residentDirector = new Director(
                        nativeSlot,
                        this,
                        path,
                        hasContentGroup,
                        nativeClassId,
                        nativeClassPath,
                        args);
                    currentDirectors.Add(residentDirector);
                    TraceDirectorCreated(residentDirector, "created-catalog-resident");
                    return residentDirector;
                }

                Director director = new Director(AllocateSpawnedActorNumber(), this, path, hasContentGroup, args);
                currentDirectors.Add(director);
                TraceDirectorCreated(director, "created-runtime");
                return director;
            }
        }

        private void TraceDirectorCreated(Director director, string action)
        {
            DevDiagnostics.Trace(
                "director.create.result",
                "area", zoneName,
                "areaKind", GetType().Name,
                "territory", territoryId,
                "areaActorId", String.Format("0x{0:X}", actorId),
                "path", director == null ? "" : director.GetScriptPath(),
                "action", action ?? "",
                "actorId", director == null ? "" : String.Format("0x{0:X}", director.actorId),
                "nativeSlot", director == null ? 0 : NativeActorId.GetNativeSlot(director.actorId),
                "hasNativeSlot", director != null && director.HasNativeSlot(),
                "classId", director == null ? 0 : director.GetDirectorClassId(),
                "isCreated", director != null && director.IsCreated(),
                "isDeleted", director != null && director.IsDeleted(),
                "directorCount", currentDirectors.Count);
        }

        public Director CreateGuildleveDirector(uint glid, byte difficulty, Player owner, params object[] args)
        {
            String directorScriptPath = "";

            uint type = Server.GetGuildleveGamedata(glid).plateId;

            if (glid == 10801 || glid == 12401 || glid == 11601)
                directorScriptPath = "Guildleve/PrivateGLBattleTutorial";
            else
            {
                switch (type)
                {
                    case 20021:
                        directorScriptPath = "Guildleve/PrivateGLBattleSweepNormal";
                        break;
                    case 20022:
                        directorScriptPath = "Guildleve/PrivateGLBattleChaseNormal";
                        break;
                    case 20023:
                        directorScriptPath = "Guildleve/PrivateGLBattleOrbNormal";
                        break;
                    case 20024:
                        directorScriptPath = "Guildleve/PrivateGLBattleHuntNormal";
                        break;
                    case 20025:
                        directorScriptPath = "Guildleve/PrivateGLBattleGatherNormal";
                        break;
                    case 20026:
                        directorScriptPath = "Guildleve/PrivateGLBattleRoundNormal";
                        break;
                    case 20027:
                        directorScriptPath = "Guildleve/PrivateGLBattleSurviveNormal";
                        break;
                    case 20028:
                        directorScriptPath = "Guildleve/PrivateGLBattleDetectNormal";
                        break;                   
                }
            }

            lock (directorLock)
            {
                GuildleveDirector director = new GuildleveDirector(AllocateSpawnedActorNumber(), this, directorScriptPath, glid, difficulty, owner, args);
                currentDirectors.Add(director);
                return director;
            }
        }

        public void DeleteDirector(Director director)
        {
            if (director == null)
                return;

            lock (directorLock)
            {
                if (!currentDirectors.Remove(director))
                    return;

                // Native slots remain reserved for the area's lifetime. A
                // completed per-player logical director does not free its
                // stable client identity for an unrelated actor.
                if (!director.HasNativeSlot())
                    ReleaseTransientActorNumber(director.actorId);
            }
        }

        public Director GetDirectorById(uint id)
        {
            lock (directorLock)
                return currentDirectors.FirstOrDefault(
                    director => director.actorId == id && !director.IsDeleted());
        }

        public override void Update(DateTime tick)
        {
            lock (mActorList)
            {
                for (int i = 0; i < 8 && travelRequests.TryDequeue(out var travel); i++)
                {
                    System.Threading.Interlocked.Decrement(ref travelRequestCount);
                    travel();
                }
                foreach (Actor a in mActorList.Values.ToList())
                    a.Update(tick);

                if ((tick - lastUpdateScript).TotalMilliseconds > 1500)
                {
                    //LuaEngine.GetInstance().CallLuaFunctionForReturn(LuaEngine.GetScriptPath(this), "onUpdate", true, this, tick);
                    lastUpdateScript = tick;
                }
            }
        }

    }
}

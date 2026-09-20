using AetherXIV.Core.Common;
using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.send.actor;
using System;
using System.Collections.Generic;
using AetherXIV.Core.Map.actors.director;

namespace AetherXIV.Core.Map.actors.area
{
    class Zone : Area
    {        
        Dictionary<string, Dictionary<uint, PrivateArea>> privateAreas = new Dictionary<string, Dictionary<uint, PrivateArea>>();
        Dictionary<string, List<PrivateAreaContent>> contentAreas = new Dictionary<string, List<PrivateAreaContent>>();
        private readonly Object contentAreasLock = new Object();
        private readonly Object privateActorIdentityLock = new Object();
        private readonly Dictionary<uint, string> privateActorIdentities =
            new Dictionary<uint, string>();
        private uint nextPrivateTransientActorNumber =
            PrivateAreaActorIdentityPolicy.TransientActorNumberStart;

        public SharpNav.TiledNavMesh tiledNavMesh;
        public SharpNav.NavMeshQuery navMeshQuery;
        internal string TravelGeometryRevision;

        public Int64 pathCalls;
        public Int64 prevPathCalls = 0;
        public Int64 pathCallTime;

        public Zone(uint id, string zoneName, ushort regionId, string classPath, ushort bgmDay, ushort bgmNight, ushort bgmBattle, bool isIsolated, bool isInn, bool canRideChocobo, bool canStealth, bool isInstanceRaid, bool loadNavMesh = false)
            : base(id, NativeActorIdentityPolicy.GetAreaMasterActorId(id), id, NativeActorIdentityPolicy.IsAuthoritativePublicTerritory(id), zoneName, regionId, classPath, bgmDay, bgmNight, bgmBattle, isIsolated, isInn, canRideChocobo, canStealth, isInstanceRaid)
        {
            if (NativeActorIdentityPolicy.TryGetResidentDirector(
                id,
                "WeatherDirector",
                out _,
                out _,
                out _))
                mWeatherDirector = CreateDirector("WeatherDirector", false);
            else if (!isInn && !isInstanceRaid &&
                (regionId == 101 || regionId == 103 || regionId == 104) &&
                (zoneName.Contains("Field") || zoneName.Contains("Town")))
                mWeatherDirector = CreateDirector("WeatherDirector", false);


            if (loadNavMesh)
            {
                try
                {
                    string meshPath = System.IO.Path.Combine(AppContext.BaseDirectory, "navmesh", zoneName + ".snb");
                    string before = id == 170 ? Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(meshPath))) : null;
                    tiledNavMesh = utils.NavmeshUtils.LoadNavmesh(tiledNavMesh, zoneName + ".snb");
                    navMeshQuery = new SharpNav.NavMeshQuery(tiledNavMesh, 100);
                    const string verified = "A2A3C91B5876FBC64F0DDD1349AA5FA4AF626760D3AEA7F7ABD5AD86D644056A";
                    if (before == verified && Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.IO.File.ReadAllBytes(meshPath))) == verified)
                        TravelGeometryRevision = verified;

                    if (tiledNavMesh != null && tiledNavMesh.Tiles[0].PolyCount > 0)
                        Program.Log.Info($"Loaded navmesh for {zoneName}");
                }
                catch (Exception e)
                {
                    Program.Log.Error(e.Message);
                }
            }
        }

        public void AddPrivateArea(PrivateArea pa)
        {
            if (privateAreas.ContainsKey(pa.GetPrivateAreaName()))
                privateAreas[pa.GetPrivateAreaName()][pa.GetPrivateAreaType()] = pa;
            else
            {
                privateAreas[pa.GetPrivateAreaName()] = new Dictionary<uint, PrivateArea>();
                privateAreas[pa.GetPrivateAreaName()][pa.GetPrivateAreaType()] = pa;
            }
        }

        internal uint ReservePrivateStaticActorNumber(
            uint spawnId,
            string privateAreaName,
            uint privateAreaType,
            string uniqueId)
        {
            uint actorNumber =
                PrivateAreaActorIdentityPolicy.GetStaticActorNumber(spawnId);
            ClaimPrivateActorNumber(
                actorNumber,
                String.Format(
                    "static:{0}:{1}:{2}:{3}",
                    privateAreaName,
                    privateAreaType,
                    spawnId,
                    uniqueId));
            return actorNumber;
        }

        internal uint AllocatePrivateTransientActorNumber(
            string privateAreaName,
            uint privateAreaType,
            string uniqueId)
        {
            lock (privateActorIdentityLock)
            {
                while (privateActorIdentities.ContainsKey(
                    nextPrivateTransientActorNumber))
                {
                    nextPrivateTransientActorNumber++;
                }

                if (nextPrivateTransientActorNumber >
                    PrivateAreaActorIdentityPolicy.MaximumActorNumber)
                {
                    throw new InvalidOperationException(String.Format(
                        "Territory {0} exhausted its shared private-area transient actor namespace.",
                        GetTerritoryId()));
                }

                uint actorNumber = nextPrivateTransientActorNumber++;
                privateActorIdentities.Add(
                    actorNumber,
                    String.Format(
                        "transient:{0}:{1}:{2}",
                        privateAreaName,
                        privateAreaType,
                        uniqueId));
                return actorNumber;
            }
        }

        internal void ReleasePrivateTransientActorNumber(uint actorNumber)
        {
            lock (privateActorIdentityLock)
            {
                if (privateActorIdentities.TryGetValue(
                        actorNumber,
                        out string owner)
                    && owner.StartsWith("transient:", StringComparison.Ordinal))
                {
                    privateActorIdentities.Remove(actorNumber);
                }
            }
        }

        private void ClaimPrivateActorNumber(uint actorNumber, string owner)
        {
            lock (privateActorIdentityLock)
            {
                if (privateActorIdentities.TryGetValue(
                    actorNumber,
                    out string existingOwner))
                {
                    throw new InvalidOperationException(String.Format(
                        "Private actor slot 0x{0:X} in territory {1} is claimed by {2} and {3}.",
                        actorNumber,
                        GetTerritoryId(),
                        existingOwner,
                        owner));
                }

                privateActorIdentities.Add(actorNumber, owner);
            }
        }

        public PrivateArea GetPrivateArea(string type, uint number)
        {
            if (privateAreas.ContainsKey(type))
            {
                Dictionary<uint, PrivateArea> instances = privateAreas[type];
                if (instances.ContainsKey(number))
                    return instances[number];
            }

            lock (contentAreasLock)
            {
                if (contentAreas.ContainsKey(type))
                {
                    foreach (PrivateAreaContent contentArea in contentAreas[type])
                    {
                        if (contentArea.GetPrivateAreaType() == number)
                            return contentArea;
                    }
                }
            }

            return null;
        }

        public override SubPacket CreateScriptBindPacket()
        {
            bool isEntranceDesion = false;

            List<LuaParam> lParams;
            lParams = LuaUtils.CreateLuaParamList(classPath, false, true, zoneName, "", -1, canRideChocobo ? (byte)1 : (byte)0, canStealth, isInn, false, false, false, true, isInstanceRaid, isEntranceDesion);
            return ActorInstantiatePacket.BuildPacket(
                actorId,
                actorName,
                className,
                lParams,
                GetActorInstantiationAreaKey());
        }

        public void AddSpawnLocation(SpawnLocation spawn)
        {
            //Is it in a private area?
            if (!spawn.privAreaName.Equals(""))
            {
                if (privateAreas.ContainsKey(spawn.privAreaName))
                {
                    Dictionary<uint, PrivateArea> levels = privateAreas[spawn.privAreaName];
                    if (levels.ContainsKey(spawn.privAreaLevel))
                        levels[spawn.privAreaLevel].AddSpawnLocation(spawn);
                    else
                        Program.Log.Error(
                            "Tried to add a spawn location to non-existing private area level {0} in area \"{1}\" in zone {2}",
                            spawn.privAreaLevel,
                            spawn.privAreaName,
                            zoneName);
                }
                else
                    Program.Log.Error("Tried to add a spawn location to non-existing private area \"{0}\" in zone {1}", spawn.privAreaName, zoneName);
            }
            else            
                mSpawnLocations.Add(spawn);            
        }

        public void SpawnAllActors(bool doPrivAreas)
        {
            foreach (SpawnLocation spawn in mSpawnLocations)            
                SpawnActor(spawn);

            if (doPrivAreas)
            {
                foreach (Dictionary<uint, PrivateArea> areas in privateAreas.Values)
                {
                    foreach (PrivateArea pa in areas.Values)
                        pa.SpawnAllActors();
                }
            }
        }

        public Actor FindActorInZone(uint id)
        {
            lock (mActorList)
            {
                if (mActorList.TryGetValue(id, out Actor publicActor))
                    return publicActor;
            }

            foreach (Dictionary<uint, PrivateArea> paList in privateAreas.Values)
            {
                foreach (PrivateArea pa in paList.Values)
                {
                    Actor actor = pa.FindActorInArea(id);
                    if (actor != null)
                        return actor;
                }
            }

            foreach (PrivateAreaContent contentArea in GetContentAreaSnapshot())
            {
                Actor actor = contentArea.FindActorInArea(id);
                if (actor != null)
                    return actor;
            }

            return null;
        }

        /// <summary>
        /// Resolves a quest ENPC by actor class id across this zone's public
        /// list, its private areas, and its content areas, preferring the
        /// copy whose private-area routing matches the requesting player.
        /// Several city NPCs (Baderon, Momodi, Miounne, ...) are seeded both
        /// at the zone root and inside a PrivateAreaMasterPast phase under the
        /// same class id, so a marker/status broadcast must bind the copy the
        /// client actually spawned, with a root-copy fallback for a
        /// private-area player whose quest NPC only exists at the root.
        /// (Garlemald find_npc_by_class_id, #28.)
        /// </summary>
        public Npc FindNpcByClassId(
            uint classId,
            string requesterArea,
            uint requesterAreaType)
        {
            // A root requester never resolves a private-area copy.
            if (String.IsNullOrEmpty(requesterArea))
                return FindNpcByClassId(classId);

            Npc rootMatch = null;
            foreach (Actor actor in GetAllActors())
            {
                if (actor is Npc npc && npc.GetActorClassId() == classId
                    && rootMatch == null)
                {
                    rootMatch = npc;
                }
            }

            foreach (Dictionary<uint, PrivateArea> paList in privateAreas.Values)
            {
                foreach (PrivateArea pa in paList.Values)
                {
                    if (pa.GetPrivateAreaName() != requesterArea
                        || pa.GetPrivateAreaType() != requesterAreaType)
                    {
                        continue;
                    }

                    Npc exact = pa.FindNpcByClassId(classId);
                    if (exact != null)
                        return exact;
                }
            }

            foreach (PrivateAreaContent contentArea in GetContentAreaSnapshot())
            {
                if (contentArea.GetPrivateAreaName() != requesterArea
                    || contentArea.GetPrivateAreaType() != requesterAreaType)
                {
                    continue;
                }

                Npc exact = contentArea.FindNpcByClassId(classId);
                if (exact != null)
                    return exact;
            }

            return rootMatch;
        }

        public PrivateAreaContent CreateContentArea(Player starterPlayer, string areaClassPath, string contentScript, string areaName, string directorName, params object[] args)
        {
            Director director = CreateDirector(directorName, true, args);
            if (director == null)
                return null;

            director.StartDirector(false);

            PrivateAreaContent contentArea = new PrivateAreaContent(
                this,
                areaClassPath,
                areaName,
                1,
                director,
                starterPlayer);

            RegisterContentArea(contentArea);

            return contentArea;
        }

        internal void RegisterContentArea(PrivateAreaContent area)
        {
            if (area == null)
                return;

            lock (contentAreasLock)
            {
                string areaName = area.GetPrivateAreaName();
                if (!contentAreas.TryGetValue(
                        areaName,
                        out List<PrivateAreaContent> instances))
                {
                    instances = new List<PrivateAreaContent>();
                    contentAreas.Add(areaName, instances);
                }

                if (!instances.Contains(area))
                    instances.Add(area);
            }
        }

        public void DeleteContentArea(PrivateAreaContent area)
        {
            if (area == null)
                return;

            lock (contentAreasLock)
            {
                string areaName = area.GetPrivateAreaName();
                if (!contentAreas.TryGetValue(
                        areaName,
                        out List<PrivateAreaContent> instances))
                {
                    return;
                }

                instances.Remove(area);
                if (instances.Count == 0)
                    contentAreas.Remove(areaName);
            }
        }

        internal PrivateAreaContent[] GetContentAreaSnapshot()
        {
            lock (contentAreasLock)
            {
                List<PrivateAreaContent> snapshot =
                    new List<PrivateAreaContent>();
                foreach (List<PrivateAreaContent> instances in
                    contentAreas.Values)
                {
                    snapshot.AddRange(instances);
                }

                return snapshot.ToArray();
            }
        }

        public override void Update(DateTime tick)
        {
            base.Update(tick);

            foreach (var a in privateAreas.Values)
                foreach(var b in a.Values)
                    b.Update(tick);

            foreach (PrivateAreaContent contentArea in
                GetContentAreaSnapshot())
            {
                contentArea.Update(tick);
            }

            // todo: again, this is retarded but debug stuff
            var diffTime = tick - lastUpdate;
            
            if (diffTime.TotalSeconds >= 10)
            {
                if (this.pathCalls > 0)
                {
                    Program.Log.Debug("Number of pathfinding calls {0} average time {1}ms. {2} this tick", pathCalls, (float)(pathCallTime / pathCalls), pathCalls - prevPathCalls);
                    prevPathCalls = pathCalls;
                }
                lastUpdate = tick;
            }
        }
    }
}

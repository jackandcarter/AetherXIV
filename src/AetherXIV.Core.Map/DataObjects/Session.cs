using AetherXIV.Core.Common;

using AetherXIV.Core.Map.Actors;
using AetherXIV.Core.Map.packets.send.actor;
using System;
using System.Collections.Generic;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.actors.area;

namespace AetherXIV.Core.Map.dataobjects
{
    class Session
    {
        public uint id = 0;
        Player playerActor;
        public List<Actor> actorInstanceList = new List<Actor>();
        public uint languageCode = 1;        
        private uint lastPingPacket = Utils.UnixTimeStampUTC();
        private uint sessionEndMarkedAt = 0;

        public bool isUpdatesLocked = true;
        public bool isEnding = false;

        public string errorMessage = "";

        public Session(uint sessionId)
        {
            this.id = sessionId;
            playerActor = new Player(this, sessionId);
        }

        public void QueuePacket(List<SubPacket> packets)
        {
            foreach (SubPacket s in packets)
                QueuePacket(s);
        }

        public void QueuePacket(SubPacket subPacket)
        {
            subPacket.SetTargetId(id);
            Server.GetWorldConnection().QueuePacket(subPacket);
        }

        public Player GetActor()
        {
            return playerActor;
        }

        public void Ping()
        {
            lastPingPacket = Utils.UnixTimeStampUTC();
        }

        public void BeginEnding()
        {
            if (isEnding)
                return;

            isEnding = true;
            isUpdatesLocked = true;
            sessionEndMarkedAt = Utils.UnixTimeStampUTC();
        }

        public bool IsEndingExpired(uint now, uint graceSeconds)
        {
            return isEnding && now - sessionEndMarkedAt >= graceSeconds;
        }

        public bool CheckIfDCing()
        {
            uint currentTime = Utils.UnixTimeStampUTC();
            if (currentTime - lastPingPacket >= 5000) //Show D/C flag
                playerActor.SetDCFlag(true);
            else if (currentTime - lastPingPacket >= 30000) //DCed
                return true;
            else
                playerActor.SetDCFlag(false);
            return false;
        }

        public void UpdatePlayerActorPosition(float x, float y, float z, float rot, ushort moveState)
        {
            if (isUpdatesLocked)
                return;

            if (playerActor.positionX == x && playerActor.positionY == y && playerActor.positionZ == z && playerActor.rotation == rot)
                return;

            playerActor.oldPositionX = playerActor.positionX;
            playerActor.oldPositionY = playerActor.positionY;
            playerActor.oldPositionZ = playerActor.positionZ;
            playerActor.oldRotation = playerActor.rotation;

            playerActor.positionX = x;
            playerActor.positionY = y;
            playerActor.positionZ = z;
            playerActor.rotation = rot;
            playerActor.moveState = moveState;

            if (playerActor.GetZone() != null)
                playerActor.GetZone().UpdateActorPosition(playerActor);

            if (playerActor.zone2 != null && playerActor.zone2 != playerActor.GetZone())
                playerActor.zone2.UpdateActorPosition(playerActor);

            playerActor.QueuePositionUpdate(new Vector3(x,y,z));
        }

        public void UpdateInstance(List<Actor> list, bool force = false)
        {
            if (isUpdatesLocked && !force)
                return;

            List<BasePacket> basePackets = new List<BasePacket>();
            List<SubPacket> RemoveActorSubpackets = new List<SubPacket>();
            List<SubPacket> posUpdateSubpackets = new List<SubPacket>();

            // Remove from the end. The legacy forward loop incremented after
            // RemoveAt and therefore skipped the actor that shifted into the
            // removed slot whenever two residents left visibility together.
            // Retail replacement traces remove every departing resident.
            for (int i = actorInstanceList.Count - 1; i >= 0; i--)
            {
                Actor publishedActor = actorInstanceList[i];

                //Retainer Instance
                if (publishedActor is Retainer && playerActor.currentSpawnedRetainer == null)
                {
                    TraceInstanceActorRemoval(publishedActor, "retainer-dismissed");
                    QueuePacket(RemoveActorPacket.BuildPacket(publishedActor.actorId));
                    actorInstanceList.RemoveAt(i);
                }
                else if (!list.Contains(publishedActor) && !(publishedActor is Retainer))
                {
                    TraceInstanceActorRemoval(publishedActor, "left-visible-instance");
                    QueuePacket(RemoveActorPacket.BuildPacket(publishedActor.actorId));
                    actorInstanceList.RemoveAt(i);
                }
            }

            //Retainer Instance
            if (playerActor.currentSpawnedRetainer != null && !playerActor.sentRetainerSpawn)
            {
                Actor actor = playerActor.currentSpawnedRetainer;
                QueuePacket(actor.GetSpawnPackets(playerActor, 1));
                QueuePacket(actor.GetInitPackets());
                QueuePacket(actor.GetSetEventStatusPackets());
                actorInstanceList.Add(actor);
                ((Npc)actor).DoOnActorSpawn(playerActor);
                playerActor.sentRetainerSpawn = true;
            }

            //Add new actors or move
            for (int i = 0; i < list.Count; i++)
            {
                Actor actor = list[i];

                if (actor.actorId == playerActor.actorId)
                    continue;

                if (actorInstanceList.Contains(actor))
                {

                }
                else
                    SpawnInstanceActor(actor);
            }

        }

        private void SpawnInstanceActor(Actor actor)
        {
            List<SubPacket> spawnPackets = actor.GetSpawnPackets(playerActor, 1);
            List<SubPacket> initPackets = actor.GetInitPackets();
            Quest[] quests = actor is Npc questNpc
                ? playerActor.GetQuestsForNpc(questNpc)
                : Array.Empty<Quest>();
            QuestENpc overlay = actor is Npc overlayNpc
                ? playerActor.GetQuestEnpcOverlay(overlayNpc.GetActorClassId())
                : null;
            List<SubPacket> eventStatusPackets = overlay == null
                ? actor.GetSetEventStatusPackets()
                : new List<SubPacket>();

            QueuePacket(spawnPackets);
            QueuePacket(initPackets);
            QueuePacket(eventStatusPackets);
            actorInstanceList.Add(actor);

            DevDiagnostics.Trace(
                "zone.bootstrap.actor",
                "player", playerActor.customDisplayName,
                "actorId", String.Format("0x{0:X}", actor.actorId),
                "actorName", actor.actorName ?? "",
                "displayName", actor.customDisplayName ?? "",
                "classPath", actor.classPath ?? "",
                "className", actor.className ?? "",
                "areaKind", actor.zone == null ? "" : actor.zone.GetType().Name,
                "privateArea", actor.zone == null ? "" : actor.zone.GetPrivateAreaName(),
                "privateAreaType", actor.zone == null ? 0 : actor.zone.GetPrivateAreaType(),
                "spawnPackets", spawnPackets.Count,
                "initPackets", initPackets.Count,
                "eventStatusPackets", eventStatusPackets.Count);

            if (actor is Npc)
            {
                Npc npc = (Npc)actor;
                npc.DoOnActorSpawn(playerActor);

                if (overlay != null)
                {
                    DevDiagnostics.Trace(
                        "quest.enpc.spawnPresentation",
                        "player", playerActor.customDisplayName,
                        "actorId", String.Format("0x{0:X8}", npc.actorId),
                        "actorClassId", npc.GetActorClassId(),
                        "actorName", npc.customDisplayName ?? npc.actorName ?? "",
                        "candidateCount", quests.Length,
                        "candidates", DescribeQuestCandidates(quests),
                        "questFlag", overlay.QuestFlagType,
                        "talk", overlay.IsTalkEnabled,
                        "push", overlay.IsPushEnabled,
                        "emote", overlay.IsEmoteEnabled);
                    // Retail publishes the quest graphic after actor
                    // instantiation and then the quest-owned interaction
                    // statuses. The generic status pass must not run first:
                    // it duplicates the same talk state and also publishes an
                    // unrelated notice status that is absent from the trace.
                    QueuePacket(SetActorQuestGraphicPacket.BuildPacket(
                        npc.actorId,
                        overlay.QuestFlagType));
                    QueuePacket(npc.GetSetEventStatusPackets(
                        overlay.IsTalkEnabled,
                        overlay.IsEmoteEnabled,
                        overlay.IsPushEnabled,
                        noticeEnabled: null));
                }
            }
        }

        public void UpdateQuestNpcInInstance(QuestENpc questInstance, bool clearInstance = false)
        {
            bool wasLocked = isUpdatesLocked;
            LockUpdates(true);

            try
            {
                Actor actor = ResolveQuestNpc(questInstance.ActorClassId);
                if (actor == null)
                {
                    DevDiagnostics.Trace(
                        "quest.enpc.instancePresentation",
                        "player", playerActor.customDisplayName,
                        "action", "unresolved",
                        "actorClassId", questInstance.ActorClassId,
                        "zone", playerActor.zoneId,
                        "privateArea", playerActor.privateArea ?? "",
                        "privateAreaType", playerActor.privateAreaType,
                        "instanceActorCount", actorInstanceList.Count);
                    return;
                }

                if (clearInstance)
                {
                    TraceQuestNpcPresentation(actor, questInstance, "clear");
                    QueuePacket(SetActorQuestGraphicPacket.BuildPacket(actor.actorId, 0));
                    QueuePacket(actor.GetSetEventStatusPackets());
                    return;
                }

                TraceQuestNpcPresentation(actor, questInstance, "update");
                QueuePacket(SetActorQuestGraphicPacket.BuildPacket(
                    actor.actorId,
                    questInstance.QuestFlagType));
                QueuePacket(actor.GetSetEventStatusPackets(
                    questInstance.IsTalkEnabled,
                    questInstance.IsEmoteEnabled,
                    questInstance.IsPushEnabled,
                    noticeEnabled: null));
            }
            finally
            {
                LockUpdates(wasLocked);
            }
        }

        /// <summary>
        /// Graphic-only variant of UpdateQuestNpcInInstance: re-emits the
        /// head marker WITHOUT the SetEventStatus overrides. Used by
        /// QuestState.UpdateState to restore markers the 1.x client drops
        /// across cinematic playback. (Garlemald broadcast_quest_enpc_graphic.)
        /// </summary>
        public void UpdateQuestNpcGraphicInInstance(QuestENpc questInstance)
        {
            if (questInstance == null)
                return;

            bool wasLocked = isUpdatesLocked;
            LockUpdates(true);

            try
            {
                Actor actor = ResolveQuestNpc(questInstance.ActorClassId);
                if (actor == null)
                    return;

                TraceQuestNpcPresentation(actor, questInstance, "graphic");
                QueuePacket(SetActorQuestGraphicPacket.BuildPacket(
                    actor.actorId,
                    questInstance.QuestFlagType));
            }
            finally
            {
                LockUpdates(wasLocked);
            }
        }

        /// <summary>
        /// Resolves a quest ENPC by actor class id, mirroring Garlemald's
        /// find_npc_by_class_id. Fast path is the player's already-streamed
        /// instance list (preferring the copy whose area routing matches the
        /// player's current area); the fallback searches the current zone +
        /// seamless partner zones so an ENPC out of streaming range (or in the
        /// partner half of a split town) still resolves and its marker/status
        /// is emitted. (#28, #41.)
        /// </summary>
        private Npc ResolveQuestNpc(uint actorClassId)
        {
            Area playerArea = playerActor.zone;
            bool playerAtRoot = !(playerArea is PrivateArea);
            Npc rootFallback = null;

            foreach (Actor actor in actorInstanceList)
            {
                if (!(actor is Npc npc) || npc.GetActorClassId() != actorClassId)
                    continue;

                Area npcArea = npc.GetZone();
                if (playerAtRoot && !(npcArea is PrivateArea))
                    return npc;

                if (!playerAtRoot && playerArea is PrivateArea playerPrivate
                    && npcArea is PrivateArea npcPrivate
                    && npcPrivate.GetPrivateAreaName() == playerPrivate.GetPrivateAreaName()
                    && npcPrivate.GetPrivateAreaType() == playerPrivate.GetPrivateAreaType())
                {
                    return npc;
                }

                if (!(npcArea is PrivateArea) && rootFallback == null)
                    rootFallback = npc;
            }

            if (rootFallback != null)
                return rootFallback;

            Zone rootZone = playerArea as Zone;
            if (rootZone == null && playerArea is PrivateArea fallbackArea)
                rootZone = fallbackArea.GetParentZone();
            if (rootZone == null)
                return null;

            string requesterArea = null;
            uint requesterAreaType = 0;
            if (playerArea is PrivateArea routingArea)
            {
                requesterArea = routingArea.GetPrivateAreaName();
                requesterAreaType = routingArea.GetPrivateAreaType();
            }

            Npc zoneMatch = rootZone.FindNpcByClassId(
                actorClassId,
                requesterArea,
                requesterAreaType);
            if (zoneMatch != null)
                return zoneMatch;

            WorldManager world = Server.GetWorldManager();
            if (world != null)
            {
                foreach (Zone partnerZone in world.GetSeamlessPartnerZones(
                    rootZone.regionId,
                    rootZone.GetTerritoryId()))
                {
                    Npc partnerMatch = partnerZone.FindNpcByClassId(
                        actorClassId,
                        requesterArea,
                        requesterAreaType);
                    if (partnerMatch != null)
                        return partnerMatch;
                }
            }

            return null;
        }

        private static string DescribeQuestCandidates(Quest[] quests)
        {
            List<string> candidates = new List<string>();
            foreach (Quest quest in quests)
            {
                if (quest == null)
                    continue;

                candidates.Add(String.Format(
                    "{0}:{1}:seq={2}:accepted={3}",
                    quest.GetQuestId(),
                    quest.GetName(),
                    quest.GetSequence(),
                    quest.HasData()));
            }

            return String.Join(",", candidates);
        }

        private void TraceQuestNpcPresentation(
            Actor actor,
            QuestENpc questInstance,
            string action)
        {
            bool isClear = String.Equals(action, "clear", StringComparison.Ordinal);
            DevDiagnostics.Trace(
                "quest.enpc.instancePresentation",
                "player", playerActor.customDisplayName,
                "action", action,
                "actorId", String.Format("0x{0:X8}", actor.actorId),
                "actorClassId", questInstance.ActorClassId,
                "actorName", actor.customDisplayName ?? actor.actorName ?? "",
                "questFlag", isClear ? 0 : questInstance.QuestFlagType,
                "talk", !isClear && questInstance.IsTalkEnabled,
                "push", !isClear && questInstance.IsPushEnabled,
                "emote", !isClear && questInstance.IsEmoteEnabled);
        }

        private void TraceInstanceActorRemoval(Actor actor, string reason)
        {
            DevDiagnostics.Trace(
                "zone.instance.actorRemoval",
                "player", playerActor.customDisplayName,
                "actorId", String.Format("0x{0:X8}", actor.actorId),
                "actorName", actor.customDisplayName ?? actor.actorName ?? "",
                "reason", reason);
        }


        public void ClearInstance()
        {
            actorInstanceList.Clear();
        }

        public void LockUpdates(bool f)
        {
            isUpdatesLocked = f;
        }
    }
}

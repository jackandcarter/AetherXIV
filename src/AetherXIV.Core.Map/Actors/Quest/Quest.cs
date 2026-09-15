using AetherXIV.Core.Common;
using AetherXIV.Core.Map.lua;
using AetherXIV.Core.Map.packets.receive.events;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.actors.director;

namespace AetherXIV.Core.Map.Actors
{
    class Quest : Actor
    {
        public const uint SEQ_NOT_STARTED = 65535;
        public const uint SEQ_COMPLETED = 65534;

        private Player owner;
        private uint currentPhase = 0;
        private uint questFlags = 0;
        private Dictionary<string, Object> questData = new Dictionary<string, object>();
        private QuestState questState;
        private QuestData scriptData;
        private bool hasData;
        private bool isUpdating;

        public Quest(uint actorID, string name)
            : base(actorID)
        {
            actorName = name;
            scriptData = new QuestData(this);
            questState = new QuestState(null, this);
        }

        public Quest(Player owner, Quest staticQuest)
            : base(staticQuest.actorId)
        {
            this.owner = owner;
            actorName = staticQuest.actorName;
            className = staticQuest.className;
            classPath = staticQuest.classPath;
            currentPhase = SEQ_NOT_STARTED;
            scriptData = new QuestData(this);
            questState = new QuestState(owner, this);
            hasData = false;
            // Do not run onStateChange from construction. New quests are
            // initialized by OnAccept -> StartSequence after publication;
            // loaded quests are re-armed by the post-zone-in login path.
            // Running the hook here is before the player's zone actor bundle
            // exists and can call back into an incomplete Session.
        }

        public Quest(Player owner, Quest staticQuest, string questDataJson, uint questFlags, uint currentPhase)
            : base(staticQuest.actorId)
        {
            this.owner = owner;
            actorName = staticQuest.actorName;
            className = staticQuest.className;
            classPath = staticQuest.classPath;
            this.questFlags = questFlags;

            if (questDataJson != null)
                this.questData = JsonConvert.DeserializeObject<Dictionary<string, Object>>(questDataJson);
            else
                questData = null;

            if (questData == null)
                questData = new Dictionary<string, object>();

            this.currentPhase = currentPhase;
            scriptData = new QuestData(this);
            questState = new QuestState(owner, this);
            hasData = true;
            // Database hydration only restores the checkpoint. ENPC state is
            // deliberately re-established after the login zone-in bundle,
            // matching the Garlemald apply_quest_update_enpcs boundary.
        }
       
        public void SetQuestData(string dataName, object data)
        {            
                questData[dataName] = data;

            DevDiagnostics.Trace(
                "quest.data",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "dataName", dataName,
                "value", data);
        }

        public uint GetQuestId()
        {
            return actorId & 0xFFFFF;
        }

        public object GetQuestData(string dataName)
        {
            if (questData.ContainsKey(dataName))
                return questData[dataName];
            else
                return null;
        }

        public void ClearQuestData()
        {
            questData.Clear();

            DevDiagnostics.Trace(
                "quest.data",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "action", "clear");
            SaveDataIfOwned();
        }       

        public void ClearQuestFlags()
        {
            uint oldFlags = questFlags;
            questFlags = 0;

            DevDiagnostics.Trace(
                "quest.flags",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "action", "clear",
                "oldFlags", Hex(oldFlags),
                "newFlags", Hex(questFlags));
            SaveDataIfOwned();
        }

        public void SetQuestFlag(int bitIndex, bool value)
        {
            if (bitIndex < 0 || bitIndex >= 32)
            {
                Program.Log.Error("Tried to access bit flag >= 32 for questId: {0}", actorId);
                return;
            }
            
            uint oldFlags = questFlags;

            if (value)
                questFlags |= (uint)(1 << bitIndex);
            else
                questFlags &= (uint)~(1 << bitIndex);

            DevDiagnostics.Trace(
                "quest.flags",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "bitIndex", bitIndex,
                "value", value,
                "oldFlags", Hex(oldFlags),
                "newFlags", Hex(questFlags));

            SaveDataIfOwned();
        }

        public bool GetQuestFlag(int bitIndex)
        {
            if (bitIndex < 0 || bitIndex >= 32)
            {
                Program.Log.Error("Tried to access bit flag >= 32 for questId: {0}", actorId);
                return false;
            }
            else
            return (questFlags & (1 << bitIndex)) == (1 << bitIndex);
        }

        public uint GetPhase()
        {
            return currentPhase;
        }

        public uint GetSequence()
        {
            return currentPhase;
        }

        public uint getSequence()
        {
            return GetSequence();
        }

        public void NextPhase(uint phaseNumber)
        {
            StartSequence(phaseNumber);
        }

        public void StartSequence(uint sequence)
        {
            SetSequence(sequence, true);
            DoCompletionCheck();
        }

        private void SetSequence(uint sequence, bool sendJournalUpdate)
        {
            if (sequence == SEQ_NOT_STARTED)
                return;

            uint oldPhase = currentPhase;
            currentPhase = sequence;
            DevDiagnostics.Trace(
                "quest.phase",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "oldPhase", oldPhase,
                "newPhase", currentPhase);
            if (sendJournalUpdate)
                owner.SendGameMessage(Server.GetWorldManager().GetActor(), 25116, 0x20, (object)GetQuestId());
            SaveData();
            questState.UpdateState();
        }

        public void StartSequenceForNpcLs(uint sequence)
        {
            StartSequence(sequence);
        }

        public QuestData GetData()
        {
            return scriptData;
        }

        public bool HasData()
        {
            return hasData;
        }

        public bool IsInstance()
        {
            return owner != null;
        }

        public bool IsMainScenario()
        {
            uint questId = GetQuestId();
            return questId >= 110001 && questId <= 110021;
        }

        internal uint GetCounter(int counterIndex)
        {
            if (counterIndex < 0 || counterIndex >= 4)
                return 0;

            return GetQuestDataUInt32("counter" + counterIndex);
        }

        internal void SetCounter(int counterIndex, uint value)
        {
            if (counterIndex < 0 || counterIndex >= 4)
                return;

            SetQuestData("counter" + counterIndex, value);
            SaveDataIfOwned();
        }

        public uint GetQuestFlags()
        {
            return questFlags;
        }

        public string GetSerializedQuestData()
        {
            return JsonConvert.SerializeObject(questData, Formatting.Indented);
        }

        public void SaveData()
        {
            DevDiagnostics.Trace(
                "quest.save",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "phase", currentPhase,
                "flags", Hex(questFlags));
            Database.SaveQuest(owner, this);
        }

        public void DoAbandon()
        {
            LuaEngine.GetInstance().CallLuaFunctionForReturn(owner, this, "onAbandonQuest", true);
            owner.SendGameMessage(owner, Server.GetWorldManager().GetActor(), 25236, 0x20, (object)GetQuestId());
        }

        public void SetENpc(
            uint actorClassId,
            byte questFlagType = 0,
            bool isTalkEnabled = true,
            bool isPushEnabled = false,
            bool isEmoteEnabled = false,
            bool isSpawned = false)
        {
            questState.AddENpc(
                actorClassId,
                questFlagType,
                isTalkEnabled,
                isPushEnabled,
                isEmoteEnabled,
                isSpawned);
        }

        public void UpdateENPCs()
        {
            if (!hasData || !scriptData.Dirty)
                return;

            questState.UpdateState();
            scriptData.ClearDirty();
        }

        public bool HasENpc(uint actorClassId)
        {
            return questState.HasENpc(actorClassId);
        }

        public QuestENpc GetENpc(uint actorClassId)
        {
            return questState.GetENpc(actorClassId);
        }

        public QuestState GetQuestState()
        {
            return questState;
        }

        public bool IsQuestENPC(Player caller, Npc npc)
        {
            return npc != null && questState.HasENpc(npc.GetActorClassId());
        }

        public bool IsQuestENPCByScript(Player caller, Npc npc)
        {
            List<LuaParam> returned = LuaEngine.GetInstance().CallLuaFunctionForReturn(
                caller ?? owner,
                this,
                "IsQuestENPC",
                true,
                npc,
                this);
            return returned != null
                && returned.Count != 0
                && returned[0].typeID == 3
                && Convert.ToBoolean(returned[0].value);
        }

        internal void DeleteENpcState()
        {
            questState.DeleteState();
        }

        /// <summary>
        /// Legacy Meteor ENPC-membership routing: fires the quest's Lua hook
        /// (onTalk/onPush/onEmote) only when the NPC is registered as this
        /// quest's ENPC and the matching event type is enabled. Called from
        /// Player.StartEvent before the generic Lua dispatch, so quest-owned
        /// NPCs never fall through to the base NPC script's default routing.
        /// </summary>
        internal bool TryHandleNpcEvent(Player player, Npc npc, EventStartPacket start)
        {
            QuestENpc enpc = questState.GetENpc(npc.GetActorClassId());
            if (enpc == null)
                return false;

            DevDiagnostics.Trace(
                "quest.event.route.candidate",
                "player", player == null ? "" : player.customDisplayName,
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "npcClassId", npc.GetActorClassId(),
                "npcActor", String.Format("0x{0:X}", npc.actorId),
                "npcUniqueId", npc.GetUniqueId(),
                "npcAreaKind", npc.zone == null ? "" : npc.zone.GetType().Name,
                "npcPrivateArea", npc.zone == null ? "" : npc.zone.GetPrivateAreaName(),
                "npcPrivateAreaType", npc.zone == null ? 0 : npc.zone.GetPrivateAreaType(),
                "registered", true,
                "talk", enpc.IsTalkEnabled,
                "push", enpc.IsPushEnabled,
                "emote", enpc.IsEmoteEnabled,
                "questFlag", enpc.QuestFlagType,
                "eventName", start.eventName,
                "eventType", start.eventType);

            string hook = null;
            switch (start.eventType)
            {
                case 1 when enpc.IsTalkEnabled:
                    hook = "onTalk";
                    break;
                case 2 when enpc.IsPushEnabled:
                    hook = "onPush";
                    break;
                case 3 when enpc.IsEmoteEnabled:
                    hook = "onEmote";
                    break;
                default:
                    return false;
            }

            DevDiagnostics.Trace(
                "quest.event.route",
                "player", player == null ? "" : player.customDisplayName,
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "npcClassId", npc.GetActorClassId(),
                "npcActor", String.Format("0x{0:X}", npc.actorId),
                "hook", hook);
            LuaEngine.GetInstance().CallLuaFunction(
                player ?? owner,
                this,
                hook,
                false,
                npc);
            return true;
        }

        private void SaveDataIfOwned()
        {
            if (owner != null)
                SaveData();
        }

        public void DoCompletionCheck()
        {
            List<LuaParam> returned = LuaEngine.GetInstance().CallLuaFunctionForReturn(owner, this, "isObjectivesComplete", true);
            if (returned != null && returned.Count >= 1 && returned[0].typeID == 3)
            {
                owner.SendDataPacket("attention", Server.GetWorldManager().GetActor(), "", 25225, (object)GetQuestId());
                owner.SendGameMessage(Server.GetWorldManager().GetActor(), 25225, 0x20, (object)GetQuestId());
            }
        }

        public void OnNotice(Player player)
        {
            Player noticePlayer = player ?? owner;
            Director noticeDirector = noticePlayer == null
                ? null
                : noticePlayer.GetDirector(noticePlayer.currentEventOwner);
            DevDiagnostics.Trace(
                "quest.notice",
                "player", noticePlayer == null ? "" : noticePlayer.customDisplayName,
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "zone", noticePlayer == null ? 0 : noticePlayer.GetZoneID(),
                "privateArea", noticePlayer == null ? "" : noticePlayer.privateArea,
                "privateAreaType", noticePlayer == null ? 0 : noticePlayer.privateAreaType,
                "eventOwner", noticePlayer == null ? "0x0" : String.Format("0x{0:X}", noticePlayer.currentEventOwner),
                "eventOwnerName", noticeDirector == null ? "" : noticeDirector.GetName(),
                "eventOwnerPath", noticeDirector == null ? "" : noticeDirector.GetScriptPath(),
                "eventOwnerResolved", noticeDirector != null,
                "eventName", noticePlayer == null ? "" : noticePlayer.currentEventName,
                "eventType", noticePlayer == null ? 0 : noticePlayer.currentEventType,
                "pendingNpcLsFrom", GetNpcLsFrom(),
                "pendingNpcLsStep", GetNpcLsMessageStep(),
                "pendingNpcLs", GetNpcLsFrom() != 0,
                "questHasMiounneEnpc", HasENpc(1000230),
                "questHasVkorolonEnpc", HasENpc(1000458),
                "action", "dispatch");

            // onNotice may yield through callClientFunction. Use the same
            // coroutine-capable callback path as talk/push/event handlers;
            // the return-value path invokes Script.Call directly and cannot
            // park on _WAIT_EVENT.
            LuaEngine.GetInstance().CallLuaFunction(
                noticePlayer,
                this,
                "onNotice",
                true);

            DevDiagnostics.Trace(
                "quest.notice",
                "player", noticePlayer == null ? "" : noticePlayer.customDisplayName,
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "eventOwnerAfter", noticePlayer == null ? "0x0" : String.Format("0x{0:X}", noticePlayer.currentEventOwner),
                "eventNameAfter", noticePlayer == null ? "" : noticePlayer.currentEventName,
                "eventTypeAfter", noticePlayer == null ? 0 : noticePlayer.currentEventType,
                "action", "complete");
        }

        public void OnKillBNpc(Player player, uint actorClassId)
        {
            LuaEngine.GetInstance().CallLuaFunction(player ?? owner, this, "onKillBNpc", true, actorClassId);
        }

        public object[] GetJournalInformation()
        {
            List<LuaParam> returned = LuaEngine.GetInstance().CallLuaFunctionForReturn(
                owner,
                this,
                "getJournalInformation",
                true);
            return returned == null || returned.Count == 0
                ? Array.Empty<object>()
                : LuaUtils.CreateLuaParamObjectList(returned);
        }

        public object[] GetJournalMapMarkerList()
        {
            List<LuaParam> returned = LuaEngine.GetInstance().CallLuaFunctionForReturn(
                owner,
                this,
                "getJournalMapMarkerList",
                true);
            return returned == null || returned.Count == 0
                ? Array.Empty<object>()
                : LuaUtils.CreateLuaParamObjectList(returned);
        }

        public void OnAccept()
        {
            OnAccept(false);
        }

        public void OnAccept(bool invokeStart)
        {
            hasData = true;

            // Initial login quests are already positioned by the login flow.
            // Replacement quests, however, own their first transition through
            // onStart after the new instance has been published.
            if (invokeStart)
                LuaEngine.GetInstance().CallLuaFunction(owner, this, "onStart", true);

            if (currentPhase == SEQ_NOT_STARTED)
                StartSequence(0);
        }

        public void OnComplete()
        {
            LuaEngine.GetInstance().CallLuaFunctionForReturn(owner, this, "onFinish", true);
            currentPhase = SEQ_COMPLETED;
            hasData = false;
            questState.UpdateState();
        }

        public void OnAbandon()
        {
            LuaEngine.GetInstance().CallLuaFunctionForReturn(owner, this, "onFinish", false);
            currentPhase = SEQ_NOT_STARTED;
            hasData = false;
            questState.UpdateState();
        }

        public void SetTimeUpdate(bool value)
        {
            isUpdating = value;
        }

        public override void Update(DateTime tick)
        {
            if (isUpdating)
            {
                LuaEngine.GetInstance().CallLuaFunctionForReturn(
                    owner,
                    this,
                    "onTimeUpdate",
                    true,
                    Utils.UnixTimeStampUTC(tick));
            }
        }

        public override bool Equals(object obj)
        {
            return obj is Quest quest && quest.actorId == actorId;
        }

        public override int GetHashCode()
        {
            return actorId.GetHashCode();
        }

        public void OnNpcLs(Player player)
        {
            DevDiagnostics.Trace(
                "npcLinkshell.dispatch",
                "player", player == null ? "" : player.customDisplayName,
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "from", GetNpcLsFrom(),
                "messageStep", GetNpcLsMessageStep(),
                "hasPending", GetNpcLsFrom() != 0);
            LuaEngine.GetInstance().CallLuaFunction(
                player ?? owner,
                this,
                "onNpcLS",
                false,
                GetNpcLsFrom(),
                GetNpcLsMessageStep());
        }

        public bool HasNpcLsMsgs(uint from)
        {
            return GetNpcLsFrom() == from;
        }

        public uint GetNpcLsFrom()
        {
            return GetQuestDataUInt32("npcLsFrom");
        }

        public uint GetNpcLsMessageStep()
        {
            return GetQuestDataUInt32("npcLsMessageStep");
        }

        public void NewNpcLsMsg(uint from)
        {
            if (!TryGetNpcLinkshellIndex(from, out uint npcLsId))
            {
                DevDiagnostics.Trace(
                    "npcLinkshell.transition",
                    "player", PlayerName(),
                    "quest", actorName,
                    "questId", GetQuestId(),
                    "sequence", currentPhase,
                    "operation", "new",
                    "from", from,
                    "valid", false,
                    "reason", "outside-supported-range");
                return;
            }

            uint previousFrom = GetNpcLsFrom();
            uint previousStep = GetNpcLsMessageStep();
            SetQuestData("npcLsFrom", from);
            SetQuestData("npcLsMessageStep", 1u);
            owner.SetNpcLs(npcLsId, Player.NPCLS_ALERT);
            owner.SendGameMessage(Server.GetWorldManager().GetActor(), 25119, 0x20, (object)from);
            SaveData();
            DevDiagnostics.Trace(
                "npcLinkshell.transition",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "operation", "new",
                "from", from,
                "npcLsId", npcLsId,
                "zeroBasedIndex", npcLsId - 1,
                "previousFrom", previousFrom,
                "previousStep", previousStep,
                "newStep", GetNpcLsMessageStep(),
                "state", "alert",
                "ownership", owner.HasNpcLs(npcLsId));
        }

        public void ReadNpcLsMsg()
        {
            uint from = GetQuestDataUInt32("npcLsFrom");
            if (!TryGetNpcLinkshellIndex(from, out uint npcLsId))
            {
                DevDiagnostics.Trace(
                    "npcLinkshell.transition",
                    "player", PlayerName(),
                    "quest", actorName,
                    "questId", GetQuestId(),
                    "sequence", currentPhase,
                    "operation", "read",
                    "from", from,
                    "valid", false,
                    "reason", "no-valid-pending-slot");
                return;
            }

            uint step = GetQuestDataUInt32("npcLsMessageStep");
            SetQuestData("npcLsMessageStep", step + 1u);
            owner.SetNpcLs(npcLsId, Player.NPCLS_ACTIVE);
            SaveData();
            DevDiagnostics.Trace(
                "npcLinkshell.transition",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "operation", "read",
                "from", from,
                "npcLsId", npcLsId,
                "zeroBasedIndex", npcLsId - 1,
                "previousStep", step,
                "newStep", GetNpcLsMessageStep(),
                "state", "active",
                "ownership", owner.HasNpcLs(npcLsId));
        }

        public void EndOfNpcLsMsgs()
        {
            uint from = GetQuestDataUInt32("npcLsFrom");
            uint step = GetNpcLsMessageStep();
            bool valid = TryGetNpcLinkshellIndex(from, out uint npcLsId);
            if (valid)
                owner.SetNpcLs(npcLsId, Player.NPCLS_INACTIVE);

            SetQuestData("npcLsFrom", 0u);
            SetQuestData("npcLsMessageStep", 0u);
            SaveData();
            DevDiagnostics.Trace(
                "npcLinkshell.transition",
                "player", PlayerName(),
                "quest", actorName,
                "questId", GetQuestId(),
                "sequence", currentPhase,
                "operation", "end",
                "from", from,
                "npcLsId", valid ? npcLsId : 0,
                "zeroBasedIndex", valid ? npcLsId - 1 : 0,
                "previousStep", step,
                "newStep", 0,
                "state", "inactive",
                "valid", valid);
        }

        private uint GetQuestDataUInt32(string key)
        {
            object value = GetQuestData(key);
            if (value == null)
                return 0;

            try
            {
                return Convert.ToUInt32(value);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        private static bool TryGetNpcLinkshellIndex(uint from, out uint npcLsId)
        {
            // Keep the script-facing one-based identifier until Player.SetNpcLs;
            // that existing boundary performs the single conversion to the
            // zero-based playerWork slot.
            if (from == 0 || from > 64)
            {
                npcLsId = 0;
                return false;
            }

            npcLsId = from;
            return true;
        }

        private string PlayerName()
        {
            if (owner == null)
                return "";

            if (!String.IsNullOrEmpty(owner.customDisplayName))
                return owner.customDisplayName;

            return owner.GetName();
        }

        private static string Hex(uint value)
        {
            return String.Format("0x{0:X}", value);
        }

    }
}

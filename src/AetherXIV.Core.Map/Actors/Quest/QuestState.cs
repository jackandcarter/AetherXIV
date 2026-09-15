using AetherXIV.Core.Common;
using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Per-player presentation state for the NPCs owned by one quest.
    ///
    /// This is the legacy quest-system boundary: Lua rebuilds the desired
    /// state for a sequence and the owning Session applies only the changed
    /// presentation to actors instantiated for that player.
    /// </summary>
    class QuestState
    {
        private readonly Player owner;
        private readonly Quest parent;
        private Dictionary<uint, QuestENpc> currentENpcs = new Dictionary<uint, QuestENpc>();
        private Dictionary<uint, QuestENpc> oldENpcs = new Dictionary<uint, QuestENpc>();

        public QuestState(Player owner, Quest parent)
        {
            this.owner = owner;
            this.parent = parent;
        }

        public void AddENpc(
            uint classId,
            byte flagType = 0,
            bool isTalkEnabled = true,
            bool isPushEnabled = false,
            bool isEmoteEnabled = false,
            bool isSpawned = false)
        {
            QuestENpc previous = null;
            oldENpcs.TryGetValue(classId, out previous);
            oldENpcs.Remove(classId);

            QuestENpc current = new QuestENpc(
                classId,
                flagType,
                isTalkEnabled,
                isPushEnabled,
                isEmoteEnabled,
                isSpawned);

            currentENpcs[classId] = current;

            if (QuestENpc.ShouldBroadcast(previous, current))
                owner?.playerSession?.UpdateQuestNpcInInstance(current);
        }

        public QuestENpc GetENpc(uint classId)
        {
            currentENpcs.TryGetValue(classId, out QuestENpc enpc);
            return enpc;
        }

        public bool HasENpc(uint classId)
        {
            return currentENpcs.ContainsKey(classId);
        }

        public void UpdateState()
        {
            oldENpcs = currentENpcs;
            currentENpcs = new Dictionary<uint, QuestENpc>();
            DevDiagnostics.Trace(
                "quest.enpc.refresh",
                "player", PlayerName(),
                "quest", parent.GetName(),
                "questId", parent.GetQuestId(),
                "phase", parent.GetSequence(),
                "zone", owner == null ? 0 : owner.zoneId,
                "areaKind", owner == null || owner.zone == null ? "" : owner.zone.GetType().Name,
                "privateArea", owner == null ? "" : owner.privateArea ?? "",
                "privateAreaType", owner == null ? 0 : owner.privateAreaType,
                "previousCount", oldENpcs.Count,
                "requestedState", "rebuild");

            LuaEngine.GetInstance().CallLuaFunctionForReturn(
                owner,
                parent,
                "onStateChange",
                true,
                parent.GetSequence());

            foreach (QuestENpc stale in oldENpcs.Values)
                owner?.playerSession?.UpdateQuestNpcInInstance(stale, true);

            oldENpcs.Clear();

            // Garlemald apply_quest_update_enpcs: re-broadcast the quest
            // GRAPHIC (head marker) of every still-active ENPC after the
            // onStateChange re-run. The 1.x client drops quest-graphic state
            // across cinematic playback, so an unchanged SetENpc (which emits
            // no packets) still needs a fresh marker. Event status overrides
            // are intentionally NOT re-emitted here: repeating them re-killed
            // quest-owned push circles (the Ul'dah opening stopper's
            // exit/caution, registered isPushEnabled=false every sequence).
            foreach (QuestENpc active in currentENpcs.Values)
                owner?.playerSession?.UpdateQuestNpcGraphicInInstance(active);

            DevDiagnostics.Trace(
                "quest.enpc.refresh.complete",
                "player", PlayerName(),
                "quest", parent.GetName(),
                "questId", parent.GetQuestId(),
                "phase", parent.GetSequence(),
                "activeCount", currentENpcs.Count,
                "staleCount", oldENpcs.Count,
                "activeActors", DescribeENpcs(currentENpcs.Values));
        }

        public void DeleteState()
        {
            foreach (QuestENpc enpc in currentENpcs.Values)
                owner?.playerSession?.UpdateQuestNpcInInstance(enpc, true);

            currentENpcs.Clear();
            oldENpcs.Clear();
        }

        private static string DescribeENpcs(IEnumerable<QuestENpc> enpcs)
        {
            List<string> values = new List<string>();
            foreach (QuestENpc enpc in enpcs)
            {
                values.Add(String.Format(
                    "{0}:flag={1}:talk={2}:push={3}:emote={4}:spawn={5}",
                    enpc.ActorClassId,
                    enpc.QuestFlagType,
                    enpc.IsTalkEnabled,
                    enpc.IsPushEnabled,
                    enpc.IsEmoteEnabled,
                    enpc.IsSpawned));
            }

            return String.Join(",", values);
        }

        private string PlayerName()
        {
            if (owner == null)
                return "";

            return !String.IsNullOrEmpty(owner.customDisplayName)
                ? owner.customDisplayName
                : owner.GetName();
        }
    }
}

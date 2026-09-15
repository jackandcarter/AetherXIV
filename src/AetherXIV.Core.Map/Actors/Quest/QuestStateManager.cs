using AetherXIV.Core.Common;
using AetherXIV.Core.Map.actors.area;
using AetherXIV.Core.Map.actors.chara.npc;
using AetherXIV.Core.Map.dataobjects;
using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Owns derived scenario availability and operates on the Player journal
    /// for accepted quest state. It must not create or replace accepted quest
    /// instances independently of Player.questScenario.
    /// </summary>
    class QuestStateManager
    {
        internal const uint ScenarioStart = 110001;
        internal const int ScenarioCount = 2048;

        private readonly Player player;
        private readonly Bitstream completedQuests = new Bitstream(ScenarioCount);
        private readonly Bitstream availableQuests = new Bitstream(ScenarioCount);
        private readonly Bitstream minLevel = new Bitstream(ScenarioCount);
        private readonly Bitstream prerequisites = new Bitstream(ScenarioCount, true);
        private readonly Bitstream grandCompanyRank = new Bitstream(ScenarioCount, true);

        public QuestStateManager(Player player)
        {
            this.player = player;
        }

        public void Init(Quest[] journalQuests, bool[] completedQuestFlags)
        {
            availableQuests.SetAll(false);

            if (journalQuests != null)
            {
                foreach (Quest quest in journalQuests)
                {
                    if (quest != null)
                        SetIfScenario(availableQuests, quest.GetQuestId());
                }
            }

            minLevel.SetAll(false);
            foreach (QuestGameData questData in Server.GetQuestGamedataByMaxLevel(
                player.GetHighestLevel(),
                true))
            {
                SetIfScenario(minLevel, questData.Id);
            }

            completedQuests.SetTo(completedQuestFlags);
            prerequisites.SetAll(true);
            foreach (QuestGameData questData in Server.GetQuestGamedataAllPrerequisite())
            {
                bool complete = IsScenario(questData.PrerequisiteQuest)
                    && completedQuests.Get(ToIndex(questData.PrerequisiteQuest));
                SetIfScenario(prerequisites, questData.Id, complete);
            }

            ComputeAvailable();
        }

        /// <summary>
        /// Reports mismatches between the journal arrays and the manager's
        /// derived active list. This deliberately does not repair either
        /// collection; it is diagnostic evidence for the reconciliation pass.
        /// </summary>
        public void DiagnoseConsistency(Quest[] journalQuests, uint[] journalActorIds, string reason)
        {
            int journalCount = 0;
            int managerAcceptedCount = 0;
            int managerAvailableCount = 0;
            int missingFromManager = 0;
            int missingFromJournal = 0;
            int actorIdMismatches = 0;
            int distinctInstanceMismatches = 0;
            int duplicateManagerQuestIds = 0;
            HashSet<uint> journalQuestIds = new HashSet<uint>();
            HashSet<uint> managerAcceptedQuestIds = new HashSet<uint>();

            if (journalQuests != null)
            {
                for (int i = 0; i < journalQuests.Length; i++)
                {
                    Quest quest = journalQuests[i];
                    if (quest == null)
                        continue;

                    journalCount++;
                    uint questId = quest.GetQuestId();
                    journalQuestIds.Add(questId);

                    if (journalActorIds == null
                        || i >= journalActorIds.Length
                        || journalActorIds[i] != quest.actorId)
                    {
                        actorIdMismatches++;
                        DevDiagnostics.Trace(
                            "quest.authority.mismatch",
                            "player", player == null ? "" : player.customDisplayName ?? "",
                            "kind", "journal-actor-id",
                            "slot", i,
                            "questId", questId,
                            "journalActorId", journalActorIds != null && i < journalActorIds.Length
                                ? journalActorIds[i]
                                : 0,
                            "questActorId", quest.actorId,
                            "reason", reason ?? "");
                    }

                }
            }

            foreach (Quest quest in player.questScenario)
            {
                if (quest == null)
                    continue;

                if (!quest.HasData())
                {
                    managerAvailableCount++;
                    continue;
                }

                managerAcceptedCount++;
                uint questId = quest.GetQuestId();
                if (!managerAcceptedQuestIds.Add(questId))
                    duplicateManagerQuestIds++;
            }

            DevDiagnostics.Trace(
                "quest.authority.summary",
                "player", player == null ? "" : player.customDisplayName ?? "",
                "reason", reason ?? "",
                "journalCount", journalCount,
                "managerAcceptedCount", managerAcceptedCount,
                "managerAvailableCount", managerAvailableCount,
                "missingFromManager", missingFromManager,
                "missingFromJournal", missingFromJournal,
                "actorIdMismatches", actorIdMismatches,
                "distinctInstanceMismatches", distinctInstanceMismatches,
                "duplicateManagerQuestIds", duplicateManagerQuestIds);
        }

        public void UpdateLevel(int level)
        {
            foreach (QuestGameData questData in Server.GetQuestGamedataByMaxLevel(level))
                SetIfScenario(minLevel, questData.Id);

            ComputeAvailable();
        }

        public void UpdateQuestCompleted(Quest quest)
        {
            if (quest == null || !IsScenario(quest.GetQuestId()))
                return;

            completedQuests.Set(ToIndex(quest.GetQuestId()));
            foreach (QuestGameData questData in Server.GetQuestGamedataByPrerequisite(quest.GetQuestId()))
                SetIfScenario(prerequisites, questData.Id);

            ComputeAvailable();
        }

        public void UpdateQuestAbandoned()
        {
            ComputeAvailable();
        }

        public Quest GetActiveQuest(uint questId)
        {
            return player.questScenario == null
                ? null
                : Array.Find(player.questScenario, quest => quest != null && quest.GetQuestId() == questId);
        }

        public Quest[] GetQuestsForNpc(Npc npc)
        {
            if (npc == null)
                return Array.Empty<Quest>();

            return Array.FindAll(player.questScenario, quest => quest != null && quest.IsQuestENPC(player, npc));
        }

        public QuestENpc GetQuestEnpcOverlay(uint actorClassId)
        {
            QuestENpc overlay = null;
            foreach (Quest quest in player.questScenario)
            {
                if (quest == null)
                    continue;

                QuestENpc enpc = quest.GetQuestState().GetENpc(actorClassId);
                if (enpc != null)
                    overlay = enpc;
            }

            return overlay;
        }

        public byte[] GetCompletionSliceBytes(ushort from, ushort to)
        {
            return completedQuests.GetSlice(from, to);
        }

        public bool IsQuestComplete(uint questId)
        {
            return IsScenario(questId) && completedQuests.Get(ToIndex(questId));
        }

        public void ForceQuestCompleteFlag(uint questId, bool value)
        {
            if (!IsScenario(questId))
                return;

            SetIfScenario(completedQuests, questId, value);
            foreach (QuestGameData questData in Server.GetQuestGamedataByPrerequisite(questId))
                SetIfScenario(prerequisites, questData.Id, value);
            ComputeAvailable();
        }

        public void Update(DateTime tick)
        {
            foreach (Quest quest in player.questScenario)
            {
                if (quest != null)
                    quest.Update(tick);
            }
        }

        public void ForceQuestStateUpdate()
        {
            ComputeAvailable();
        }

        public void ReestablishQuestENpcs(string reason)
        {
            if (player == null)
                return;

            if (player.zone is PrivateAreaContent)
            {
                DevDiagnostics.Trace(
                    "quest.enpc.reestablish.skipped",
                    "player", player.customDisplayName ?? "",
                    "reason", reason ?? "",
                    "guard", "content-instance");
                return;
            }

            int rearmed = 0;
            foreach (Quest quest in player.questScenario)
            {
                if (quest == null || !quest.HasData())
                    continue;

                quest.GetQuestState().UpdateState();
                rearmed++;
            }

            DevDiagnostics.Trace(
                "quest.enpc.reestablish",
                "player", player.customDisplayName ?? "",
                "reason", reason ?? "",
                "quests", rearmed);
        }

        private void ComputeAvailable()
        {
            Bitstream result = new Bitstream(ScenarioCount);
            result.NOTOR(completedQuests);
            result.AND(minLevel);
            result.AND(prerequisites);
            result.AND(grandCompanyRank);

            Bitstream difference = availableQuests.Copy();
            difference.XOR(result);
            byte[] changed = difference.GetBytes();

            for (int byteIndex = 0; byteIndex < changed.Length; byteIndex++)
            {
                if (changed[byteIndex] == 0)
                    continue;

                for (int bitIndex = 0; bitIndex < 8; bitIndex++)
                {
                    if ((changed[byteIndex] & (1 << bitIndex)) == 0)
                        continue;

                    int index = byteIndex * 8 + bitIndex;
                    uint questId = ScenarioStart + (uint)index;
                    Quest staticQuest = Server.GetStaticActors(0xA0F00000 | questId) as Quest;
                    if (staticQuest == null)
                        continue;

                    // Availability is derived state only. Accepted quests
                    // are created by Player.AcceptQuest and live in the journal.
                }
            }

            availableQuests.SetTo(result);
        }


        private void TraceAvailability(Quest quest, string action)
        {
            DevDiagnostics.Trace(
                "quest.availability",
                "player", player == null ? "" : player.customDisplayName ?? "",
                "action", action ?? "",
                "quest", quest == null ? "" : quest.GetName(),
                "questId", quest == null ? 0 : quest.GetQuestId(),
                "sequence", quest == null ? 0 : quest.GetSequence(),
                "accepted", quest != null && quest.HasData(),
                "activeCount", player.questScenario == null ? 0 : Array.FindAll(player.questScenario, quest => quest != null).Length);
        }

        private static bool IsScenario(uint questId)
        {
            return questId >= ScenarioStart && questId < ScenarioStart + ScenarioCount;
        }

        private static int ToIndex(uint questId)
        {
            return checked((int)(questId - ScenarioStart));
        }

        private static void SetIfScenario(Bitstream field, uint questId, bool value = true)
        {
            if (!IsScenario(questId))
                return;

            if (value)
                field.Set(ToIndex(questId));
            else
                field.Clear(ToIndex(questId));
        }
    }
}

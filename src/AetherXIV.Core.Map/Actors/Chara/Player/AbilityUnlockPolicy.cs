using System;
using AetherXIV.Core.Map.Actors;
using System.Collections.Generic;
using System.Linq;

namespace AetherXIV.Core.Map.actors.chara.player
{
    public readonly record struct JobActionUnlock(byte JobId, ushort CommandId, byte Level, uint QuestId);

    public static class AbilityUnlockPolicy
    {
        public const int HotbarCapacity = 30;

        // Client showGetJobAbilityWidget calls establish the quest/action association.
        // Levels: official 1.21 tables. See evidence/ability-unlocks-2026-09-17.
        public static IReadOnlyList<JobActionUnlock> JobActions { get; } = Array.AsReadOnly(new[]
        {
            new JobActionUnlock(15, 27108, 30, 111221), // Shoulder Tackle: mnk0j1
            new JobActionUnlock(15, 27107, 35, 111222), // Spinning Heel: mnk0j2
            new JobActionUnlock(15, 27109, 40, 111223), // Fists of Wind: mnk0j3
            new JobActionUnlock(15, 27118, 45, 111224), // Dragon Kick: mnk0j4
            new JobActionUnlock(15, 27106, 50, 111226), // Hundred Fists: mnk0j6
            new JobActionUnlock(16, 27146, 30, 111281), // Cover: pld0j1
            new JobActionUnlock(16, 27147, 35, 111282), // Divine Veil: pld0j2
            new JobActionUnlock(16, 27149, 40, 111283), // Holy Succor: pld0j3
            new JobActionUnlock(16, 27159, 45, 111285), // Spirits Within: pld0j5
            new JobActionUnlock(16, 27148, 50, 111286), // Hallowed Ground: pld0j6
            new JobActionUnlock(17, 27186, 30, 111201), // Vengeance: war0j1
            new JobActionUnlock(17, 27187, 35, 111202), // Antagonize: war0j2
            new JobActionUnlock(17, 27188, 40, 111203), // Collusion: war0j3
            new JobActionUnlock(17, 27192, 45, 111205), // Steel Cyclone: war0j5
            new JobActionUnlock(17, 27189, 50, 111206), // Mighty Strikes: war0j6
            new JobActionUnlock(18, 27237, 30, 111301), // Ballad of Magi: brd0j1
            new JobActionUnlock(18, 27239, 35, 111302), // Minuet of Rigor: brd0j2
            new JobActionUnlock(18, 27238, 40, 111303), // Paeon of War: brd0j3
            new JobActionUnlock(18, 27232, 45, 111304), // Rain of Death: brd0j4
            new JobActionUnlock(18, 27227, 50, 111306), // Battle Voice: brd0j6
            new JobActionUnlock(19, 27266, 30, 111321), // Jump: drg0j1
            new JobActionUnlock(19, 27272, 35, 111322), // Disembowel: drg0j2
            new JobActionUnlock(19, 27267, 40, 111323), // Elusive Jump: drg0j3
            new JobActionUnlock(19, 27277, 45, 111325), // Ring of Talons: drg0j5
            new JobActionUnlock(19, 27268, 50, 111326), // Dragonfire Dive: drg0j6
            new JobActionUnlock(26, 27305, 30, 111261), // Convert: blm0j1
            new JobActionUnlock(26, 27319, 35, 111262), // Freeze: blm0j2
            new JobActionUnlock(26, 27318, 40, 111263), // Flare: blm0j3
            new JobActionUnlock(26, 27317, 45, 111264), // Sleepga: blm0j4
            new JobActionUnlock(26, 27316, 50, 111266), // Burst: blm0j6
            new JobActionUnlock(27, 27344, 30, 111241), // Presence of Mind: whm0j1
            new JobActionUnlock(27, 27358, 35, 111242), // Regen: whm0j2
            new JobActionUnlock(27, 27357, 40, 111243), // Esuna: whm0j3
            new JobActionUnlock(27, 27359, 45, 111244), // Holy: whm0j4
            new JobActionUnlock(27, 27345, 50, 111246), // Benediction: whm0j6
        });

        public static IReadOnlyList<ushort> GetEligibleActions(
            byte classOrJob, short level, bool hasQualifiedSoulCrystal,
            Func<uint, bool> isQuestCompleted, Func<byte, short, List<ushort>> actionsAtLevel)
        {
            byte baseClass = Player.ConvertJobIdToClassId(classOrJob);
            if (!Player.IsDiscipleOfWarOrMagicClass(baseClass))
                return Array.Empty<ushort>();
            var result = new List<ushort>();
            for (short learnedLevel = 1; learnedLevel <= Math.Min(level, (short)50); learnedLevel++)
                result.AddRange(actionsAtLevel(baseClass, learnedLevel).OrderBy(id => id));
            if (classOrJob != baseClass && hasQualifiedSoulCrystal)
                foreach (var action in JobActions)
                    if (action.JobId == classOrJob && action.Level <= level
                        && (action.Level == 30 || isQuestCompleted(action.QuestId)))
                        result.Add(action.CommandId);
            return result.Distinct().ToArray();
        }

        // Preserve layout and recast state. Never overwrite a player-selected slot.
        public static IReadOnlyList<KeyValuePair<ushort, ushort>> PlanMissingActions(
            IReadOnlyDictionary<ushort, uint> existing, IEnumerable<ushort> eligible)
        {
            var occupied = new HashSet<ushort>(existing.Keys);
            var known = new HashSet<uint>(existing.Values.Select(id => id & 0xFFFF));
            var additions = new List<KeyValuePair<ushort, ushort>>();
            foreach (ushort id in eligible)
            {
                if (!known.Add(id)) continue;
                ushort slot = 0;
                while (slot < HotbarCapacity && occupied.Contains(slot)) slot++;
                if (slot == HotbarCapacity) break;
                occupied.Add(slot);
                additions.Add(new KeyValuePair<ushort, ushort>(slot, id));
            }
            return additions;
        }
    }
}

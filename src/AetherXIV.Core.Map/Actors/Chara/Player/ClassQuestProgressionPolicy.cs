using System;

namespace AetherXIV.Core.Map.actors.chara.player
{
    public readonly record struct ClassQuestRequirement(
        uint QuestId,
        string QuestName,
        byte ClassId,
        short RequiredLevel,
        uint PrerequisiteQuestId,
        byte SecondaryClassId = 0,
        short SecondaryClassLevel = 0);

    public static class ClassQuestProgressionPolicy
    {
        private static readonly ClassQuestRequirement[] Requirements =
        {
            // Carpenter (Woodworking) — the complete retail 1.x guild-story chain.
            new(110300, "Wdk200", 29, 20, 0),
            new(110301, "Wdk300", 29, 30, 110300),
            new(110302, "Wdk306", 29, 36, 110301),
            // Bard: client quest sheet and Brd0j1-6 requirement delegates.
            // Jobs use their base class's stored level, not a separate skillLevel slot.
            new(111301, "Brd0j1", 7, 30, 0, 23, 15),
            new(111302, "Brd0j2", 18, 35, 111301, 23, 15),
            new(111303, "Brd0j3", 18, 40, 111302, 23, 15),
            new(111304, "Brd0j4", 18, 45, 111303, 23, 15),
            new(111305, "Brd0j5", 18, 45, 111304, 23, 15),
            new(111306, "Brd0j6", 18, 50, 111305, 23, 15)
        };

        public static bool TryGet(uint questId, out ClassQuestRequirement requirement)
        {
            foreach (ClassQuestRequirement candidate in Requirements)
            {
                if (candidate.QuestId == questId)
                {
                    requirement = candidate;
                    return true;
                }
            }

            requirement = default;
            return false;
        }

        public static bool MeetsRequirements(
            ClassQuestRequirement requirement,
            byte currentClassOrJob,
            Func<byte, short> getClassLevel,
            Func<uint, bool> isQuestCompleted)
        {
            if (getClassLevel == null)
                throw new ArgumentNullException(nameof(getClassLevel));
            if (isQuestCompleted == null)
                throw new ArgumentNullException(nameof(isQuestCompleted));

            return currentClassOrJob == requirement.ClassId
                && getClassLevel(AetherXIV.Core.Map.Actors.Player.ConvertJobIdToClassId(requirement.ClassId)) >= requirement.RequiredLevel
                && (requirement.SecondaryClassId == 0
                    || getClassLevel(requirement.SecondaryClassId) >= requirement.SecondaryClassLevel)
                && (requirement.PrerequisiteQuestId == 0
                    || isQuestCompleted(requirement.PrerequisiteQuestId));
        }
    }
}

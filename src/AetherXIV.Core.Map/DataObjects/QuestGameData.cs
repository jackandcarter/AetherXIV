namespace AetherXIV.Core.Map.dataobjects
{
    class QuestGameData
    {
        public uint Id { get; }
        public string ClassName { get; }
        public string Name { get; }
        public uint PrerequisiteQuest { get; }
        public int MinLevel { get; }
        public int MinGCRank { get; }

        public QuestGameData(
            uint id,
            string className,
            string name,
            uint prerequisiteQuest,
            int minLevel,
            int minGCRank)
        {
            Id = id;
            ClassName = className;
            Name = name;
            PrerequisiteQuest = prerequisiteQuest;
            MinLevel = minLevel;
            MinGCRank = minGCRank;
        }
    }
}

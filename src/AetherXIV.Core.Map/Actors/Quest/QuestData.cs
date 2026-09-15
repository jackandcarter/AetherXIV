namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Script-facing quest flags and counters. Values remain stored by Quest,
    /// preserving the existing questData/questFlags database contract.
    /// </summary>
    class QuestData
    {
        private readonly Quest quest;

        public bool Dirty { get; private set; }

        public QuestData(Quest quest)
        {
            this.quest = quest;
        }

        public uint GetFlags() => quest.GetQuestFlags();
        public bool GetFlag(int bitIndex) => quest.GetQuestFlag(bitIndex);
        public void SetFlag(int bitIndex)
        {
            quest.SetQuestFlag(bitIndex, true);
            Dirty = true;
        }

        public void ClearFlag(int bitIndex)
        {
            quest.SetQuestFlag(bitIndex, false);
            Dirty = true;
        }

        public uint GetCounter(int counterIndex) => quest.GetCounter(counterIndex);

        public void SetCounter(int counterIndex, uint value)
        {
            quest.SetCounter(counterIndex, value);
            Dirty = true;
        }

        public void SetTimeNow()
        {
            quest.SetQuestData("time", AetherXIV.Core.Common.Utils.UnixTimeStampUTC());
            quest.SaveData();
            Dirty = true;
        }

        public uint GetTime()
        {
            object value = quest.GetQuestData("time");
            return value == null ? 0u : System.Convert.ToUInt32(value);
        }

        public uint IncCounter(int counterIndex)
        {
            uint value = unchecked(GetCounter(counterIndex) + 1u);
            SetCounter(counterIndex, value);
            return value;
        }

        public uint DecCounter(int counterIndex)
        {
            uint value = unchecked(GetCounter(counterIndex) - 1u);
            SetCounter(counterIndex, value);
            return value;
        }

        public void ClearData()
        {
            quest.ClearQuestData();
            quest.ClearQuestFlags();
        }

        public void ClearDirty()
        {
            Dirty = false;
        }
    }
}

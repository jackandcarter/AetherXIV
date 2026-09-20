namespace AetherXIV.Core.Map.Actors
{
    static class NpcPropertyPolicy
    {
        // Keep generic NPCs out of the combat presentation path. Actual
        // BattleNpc instances enable this bit after base construction;
        // the client calls it property 3 (its property indices are one-based).
        private const uint ForbiddenNpcPropertyMask = 1u << 2;

        public static uint Sanitize(uint propertyFlags) =>
            propertyFlags & ~ForbiddenNpcPropertyMask;
    }
}

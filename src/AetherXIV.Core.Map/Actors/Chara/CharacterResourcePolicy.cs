using System;

namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Resource handling for a stat recalculation. Rebuilding maximum HP/MP is
    /// not a heal: an existing pool is preserved and clamped to the new
    /// maximum. A genuinely uninitialised pool is filled when its first
    /// non-zero maximum is established.
    /// </summary>
    static class CharacterResourcePolicy
    {
        public static short RestoreCurrent(short previousCurrent, short previousMaximum, short newMaximum)
        {
            if (newMaximum <= 0)
                return 0;

            if (previousMaximum <= 0 && previousCurrent <= 0)
                return newMaximum;

            return (short)Math.Max(0, Math.Min((int)previousCurrent, newMaximum));
        }
    }
}

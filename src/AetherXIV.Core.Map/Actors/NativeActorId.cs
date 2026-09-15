using System;

namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Encodes the native 1.x non-player actor namespace used on the wire.
    /// Repository IDs and database row order must never be passed here.
    /// </summary>
    static class NativeActorId
    {
        public const uint NonPlayerKind = 4;
        public const uint MaximumKind = 0xF;
        public const uint MaximumTerritoryId = 0x1FF;
        public const uint MaximumSlot = 0x7FFFF;
        public const int ObjectNameRadix = 62;
        public const int MaximumObjectNameOrdinal = (ObjectNameRadix * ObjectNameRadix) - 1;
        public const uint MaximumNativeSlotWithObjectName = MaximumObjectNameOrdinal + 1;

        public static uint ComposeNonPlayer(uint territoryId, uint nativeSlot)
        {
            return Compose(NonPlayerKind, territoryId, nativeSlot, true);
        }

        public static uint Compose(
            uint kind,
            uint territoryId,
            uint nativeSlot,
            bool requireNonZeroSlot = false)
        {
            if (kind > MaximumKind)
                throw new ArgumentOutOfRangeException(nameof(kind), kind, "Actor kind must fit bits 28..31.");
            if (territoryId > MaximumTerritoryId)
                throw new ArgumentOutOfRangeException(nameof(territoryId), territoryId, "Territory ID must fit bits 19..27.");
            if (nativeSlot > MaximumSlot)
                throw new ArgumentOutOfRangeException(nameof(nativeSlot), nativeSlot, "Native actor slot must fit bits 0..18.");
            if (requireNonZeroSlot && nativeSlot == 0)
                throw new ArgumentOutOfRangeException(nameof(nativeSlot), nativeSlot, "A declared native actor slot cannot be zero.");

            return (kind << 28) | (territoryId << 19) | nativeSlot;
        }

        public static uint GetKind(uint actorId)
        {
            return (actorId >> 28) & MaximumKind;
        }

        public static uint GetTerritoryId(uint actorId)
        {
            return (actorId >> 19) & MaximumTerritoryId;
        }

        public static uint GetNativeSlot(uint actorId)
        {
            return actorId & MaximumSlot;
        }

        public static int GetObjectNameOrdinal(uint nativeSlot)
        {
            if (nativeSlot == 0 || nativeSlot > MaximumNativeSlotWithObjectName)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(nativeSlot),
                    nativeSlot,
                    $"A native slot used in a two-character object name must be in range 1..{MaximumNativeSlotWithObjectName}.");
            }

            return checked((int)nativeSlot - 1);
        }
    }
}

using System;

namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Compatibility identity contract for private-area actors whose retail
    /// native slots have not been recovered. Static identities derive from
    /// the stable spawn key, never query order, and transient identities use
    /// a disjoint parent-territory range.
    /// </summary>
    static class PrivateAreaActorIdentityPolicy
    {
        public const uint StaticActorNumberBase = 0x700;
        public const uint MaximumStaticSpawnId = 0x4FF;
        public const uint TransientActorNumberStart = 0xC00;
        public const uint MaximumActorNumber =
            NativeActorId.MaximumObjectNameOrdinal;

        public static uint GetStaticActorNumber(uint spawnId)
        {
            if (spawnId == 0 || spawnId > MaximumStaticSpawnId)
            {
                throw new ArgumentOutOfRangeException(
                    nameof(spawnId),
                    spawnId,
                    $"Private static spawn IDs must be in range 1..{MaximumStaticSpawnId}; extend the reviewed compatibility partition before exceeding it.");
            }

            return StaticActorNumberBase + spawnId;
        }
    }
}

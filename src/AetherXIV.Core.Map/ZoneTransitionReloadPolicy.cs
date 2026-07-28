namespace AetherXIV.Core.Map
{
    enum ZoneTransitionReloadRecipe
    {
        FullMap,
        ResidentGeometry,
        PrivateAreaBoundary
    }

    static class ZoneTransitionReloadPolicy
    {
        public static ZoneTransitionReloadRecipe Select(
            uint currentZoneId,
            string currentPrivateArea,
            uint currentPrivateAreaType,
            uint destinationZoneId,
            string destinationPrivateArea,
            uint destinationPrivateAreaType)
        {
            if (currentZoneId != destinationZoneId)
                return ZoneTransitionReloadRecipe.FullMap;

            string currentArea = currentPrivateArea ?? "";
            string destinationArea = destinationPrivateArea ?? "";
            if (!System.String.Equals(currentArea, destinationArea, System.StringComparison.Ordinal)
                || currentPrivateAreaType != destinationPrivateAreaType)
            {
                return ZoneTransitionReloadRecipe.PrivateAreaBoundary;
            }

            return ZoneTransitionReloadRecipe.ResidentGeometry;
        }
    }

    /// <summary>
    /// Timing and batch limits recovered from the retail
    /// move_out_of_room.pcapng room-exit sequence.
    /// </summary>
    static class ZoneTransitionBootstrapPolicy
    {
        // Frame 49 closes the source event and sends state 0x0F. The first
        // destination AddActor is frame 87, 6.059397 seconds later.
        public const int RoomExitDelayMilliseconds = 6000;

        // After the player's destination bootstrap begins, the first group of
        // non-player actors arrives 439.643 ms later.
        public const int FirstActorBatchDelayMilliseconds = 440;

        // Subsequent retail actor groups arrive about every 120-150 ms and
        // contain at most eight AddActor records.
        public const int ActorBatchIntervalMilliseconds = 140;
        public const int ActorsPerBatch = 8;

        public static bool RequiresDeferredBootstrap(ZoneTransitionReloadRecipe recipe)
        {
            return recipe == ZoneTransitionReloadRecipe.PrivateAreaBoundary;
        }

        public static System.DateTime GetBootstrapDueAt(System.DateTime queuedAtUtc)
        {
            return queuedAtUtc.AddMilliseconds(RoomExitDelayMilliseconds);
        }

        public static System.DateTime GetFirstActorBatchDueAt(System.DateTime bootstrapStartedAtUtc)
        {
            return bootstrapStartedAtUtc.AddMilliseconds(FirstActorBatchDelayMilliseconds);
        }

        public static System.DateTime GetNextActorBatchDueAt(System.DateTime previousBatchDueAtUtc)
        {
            return previousBatchDueAtUtc.AddMilliseconds(ActorBatchIntervalMilliseconds);
        }
    }
}

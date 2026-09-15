namespace AetherXIV.Core.Map.Actors
{
    /// <summary>
    /// Applies strict native-slot behavior only where the reviewed identity
    /// catalog declares a complete public-area namespace.
    /// </summary>
    static class NativeActorIdentityPolicy
    {
        public static bool IsAuthoritativePublicTerritory(uint territoryId)
        {
            return NativeActorIdentityCatalog.TryGetTerritory(territoryId, out _);
        }

        public static uint GetAreaMasterActorId(uint territoryId)
        {
            if (NativeActorIdentityCatalog.TryGetTerritory(
                territoryId,
                out NativeActorTerritoryIdentity identity))
            {
                return NativeActorId.ComposeNonPlayer(
                    territoryId,
                    identity.publicArea.areaMasterNativeSlot);
            }

            // The area master is always a native non-player actor. An
            // incomplete static-NPC catalog must never turn the database
            // territory number into an actor ID.
            return NativeActorId.ComposeNonPlayer(territoryId, 1);
        }

        public static bool TryGetResidentDirector(
            uint territoryId,
            string scriptPath,
            out uint nativeSlot,
            out uint nativeClassId,
            out string nativeClassPath)
        {
            if (NativeActorIdentityCatalog.TryGetResidentDirector(
                territoryId,
                scriptPath,
                out NativeResidentDirectorIdentity identity))
            {
                nativeSlot = identity.nativeActorSlot;
                nativeClassId = identity.nativeClassId;
                nativeClassPath = identity.nativeClassPath;
                return true;
            }

            nativeSlot = 0;
            nativeClassId = 0;
            nativeClassPath = null;
            return false;
        }
    }
}

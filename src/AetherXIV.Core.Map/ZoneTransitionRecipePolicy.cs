using AetherXIV.Core.Map.dataobjects;
using System;
using System.Collections.Generic;

namespace AetherXIV.Core.Map
{
    /// <summary>
    /// Classifies a DoZoneChange against the Garlemald three-recipe model.
    /// The 1.x client's scene teardown is driven by the 0x00E2 order machine
    /// and the Mass Delete keep-list commit; using the wrong recipe for the
    /// geometry class strands the client on "Now Loading" or deletes the
    /// player's own actor mid-scene.
    ///
    /// Resident-geometry transitions (same zone — any public⇄private
    /// combination — or a same-region public seamless partner pair) use the
    /// wipe + 0x00E2(0x10) content-reload shape with no keep-list commit.
    /// Everything else (cross-region, or same-region different-layout map
    /// changes) uses the 0x00E2(0x02) full-zone-change latch with the
    /// keep-list commit at the end of the bundle and no up-front wipe.
    /// </summary>
    static class ZoneTransitionRecipePolicy
    {
        public readonly struct ZoneTransitionRecipeDecision
        {
            public bool SameZone { get; init; }

            public bool SameRegion { get; init; }

            public bool SeamlessPairPublic { get; init; }

            public bool UsesResidentGeometryRecipe { get; init; }

            public string RecipeName => UsesResidentGeometryRecipe
                ? "resident-geometry-wipe-0x10"
                : "full-zone-change-0x02-keep-list";
        }

        public static ZoneTransitionRecipeDecision Classify(
            IReadOnlyDictionary<uint, List<SeamlessBoundry>> seamlessBoundaryList,
            uint currentZoneId,
            uint destinationZoneId,
            string currentPrivateArea,
            string destinationPrivateArea,
            ushort currentRegionId,
            ushort destinationRegionId)
        {
            bool sameZone = currentZoneId == destinationZoneId;
            bool sameRegion = currentRegionId == destinationRegionId;
            bool bothPublic = String.IsNullOrEmpty(currentPrivateArea)
                && destinationPrivateArea == null;
            bool seamlessPairPublic = sameRegion && !sameZone && bothPublic
                && IsSeamlessPartnerPair(seamlessBoundaryList, currentZoneId, destinationZoneId);

            return new ZoneTransitionRecipeDecision
            {
                SameZone = sameZone,
                SameRegion = sameRegion,
                SeamlessPairPublic = seamlessPairPublic,
                UsesResidentGeometryRecipe = sameZone || seamlessPairPublic,
            };
        }

        public static bool IsSeamlessPartnerPair(
            IReadOnlyDictionary<uint, List<SeamlessBoundry>> seamlessBoundaryList,
            uint zoneA,
            uint zoneB)
        {
            if (seamlessBoundaryList == null)
                return false;

            foreach (List<SeamlessBoundry> bounds in seamlessBoundaryList.Values)
            {
                foreach (SeamlessBoundry pair in bounds)
                {
                    if ((pair.zoneId1 == zoneA && pair.zoneId2 == zoneB)
                        || (pair.zoneId1 == zoneB && pair.zoneId2 == zoneA))
                        return true;
                }
            }

            return false;
        }
    }
}

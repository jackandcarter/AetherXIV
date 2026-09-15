using System;

namespace AetherXIV.Core.Map.actors.area
{
    /// <summary>
    /// Identifies anonymous world transition actors whose EventStart is a
    /// deliberate server-side no-op. This is intentionally an allowlist: an
    /// unknown PopulaceStandard actor must remain visible as a diagnostic.
    /// </summary>
    internal static class AnonymousTransitionActorPolicy
    {
        private const uint GridaniaCanopyZoneId = 155;
        private const uint GridaniaPublicCanopyExitClassId = 1099046;
        private const string PushDefaultEvent = "pushDefault";

        public static bool IsExpectedNoOp(
            uint zoneId,
            uint actorClassId,
            string uniqueId,
            string eventName)
        {
            return zoneId == GridaniaCanopyZoneId
                && actorClassId == GridaniaPublicCanopyExitClassId
                && String.IsNullOrEmpty(uniqueId)
                && String.Equals(eventName, PushDefaultEvent, StringComparison.Ordinal);
        }
    }
}

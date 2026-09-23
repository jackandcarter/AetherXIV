using System.Globalization;

namespace AetherXIV.Core.Map.actors.area
{
    /// <summary>
    /// Builds the player-scoped signal keys used by content directors.
    /// The wire/string format is shared by tutorials, escorts, and other
    /// content; content-specific validation remains in the owning script or
    /// policy.
    /// </summary>
    static class ContentSignalPolicy
    {
        public static string BuildPlayerSignal(string signal, uint playerActorId)
        {
            return signal + ":" + playerActorId.ToString(CultureInfo.InvariantCulture);
        }
    }
}

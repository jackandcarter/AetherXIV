namespace AetherXIV.Core.Map
{
    static class ZoneTransitionReadinessPolicy
    {
        /// <summary>
        /// Retail sends several opcode-0x0007 messages during login. The
        /// transition-ready acknowledgement is the game-message form whose
        /// trailing signed value is -1.
        /// </summary>
        public static bool IsReady(bool packetValid, int unknown)
        {
            return packetValid && unknown == -1;
        }
    }
}

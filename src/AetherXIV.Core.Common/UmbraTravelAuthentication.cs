#nullable enable
using System;
using System.Security.Cryptography;
using AetherXIV.Protocol;

namespace AetherXIV.Core.Common;

// One instance per zone connection. A character ID alone grants no travel rights.
public sealed class UmbraTravelAuthentication
{
    private readonly TimeProvider clock;
    private byte[]? challenge;
    private long issued;
    private DateTimeOffset authenticatedUntil;
    private uint character;
    public UmbraTravelAuthentication(TimeProvider? clock = null) { this.clock = clock ?? TimeProvider.System; }
    public void Reset() { challenge = null; character = 0; authenticatedUntil = default; }
    public string Issue(uint characterId)
    {
        character = characterId;
        authenticatedUntil = default;
        challenge = RandomNumberGenerator.GetBytes(32);
        issued = clock.GetTimestamp();
        return Convert.ToHexString(challenge);
    }
    public bool IsAuthenticated(uint characterId) => characterId != 0 && character == characterId && clock.GetUtcNow() < authenticatedUntil;
    public bool HasPendingChallenge(uint characterId) => challenge != null && characterId == character &&
        clock.GetElapsedTime(issued) <= TimeSpan.FromSeconds(30);
    public bool Verify(uint characterId, string? proof, string? activeSessionToken, DateTimeOffset sessionExpiry)
    {
        byte[]? nonce = challenge;
        challenge = null; // Every proof attempt consumes the challenge.
        if (nonce == null || characterId != character || sessionExpiry <= clock.GetUtcNow() ||
            clock.GetElapsedTime(issued) > TimeSpan.FromSeconds(30) || proof?.Length != 64 ||
            string.IsNullOrWhiteSpace(activeSessionToken)) return false;
        byte[] provided;
        try { provided = Convert.FromHexString(proof); } catch (FormatException) { return false; }
        if (!CryptographicOperations.FixedTimeEquals(provided,
            UmbraTravelWire.CreateProof(activeSessionToken, nonce, characterId))) return false;
        authenticatedUntil = sessionExpiry;
        return true;
    }
}

using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace AetherXIV.Protocol;

// New AetherXIV extension, not a claimed retail opcode. Carried by the existing
// zone game-message connection; never by the developer HTTP bridge.
public enum UmbraTravelOperation { Challenge, Authenticate, Preview, Commit, Reply }
public sealed record UmbraTravelWireLanding(string Id, float X, float Y, float Z, float Adjustment);
public sealed record UmbraTravelMessage
{
    public int Version { get; init; } = 1;
    public Guid RequestId { get; init; }
    public UmbraTravelOperation Operation { get; init; }
    public uint ZoneId { get; init; }
    public uint MapId { get; init; }
    public float X { get; init; }
    public float Z { get; init; }
    public string? Token { get; init; }
    public string? CandidateId { get; init; }
    public string? Proof { get; init; }
    public string? Status { get; init; }
    public string? Message { get; init; }
    public long ExpiresAtUnixMs { get; init; }
    public UmbraTravelWireLanding[]? Candidates { get; init; }
}

public static class UmbraTravelWire
{
    public const ushort RequestOpcode = 0x0F00;
    public const ushort ReplyOpcode = 0x0F01;
    public const int MaximumPayload = 16384;
    public static byte[] Encode(UmbraTravelMessage message)
    {
        Validate(message);
        var data = JsonSerializer.SerializeToUtf8Bytes(message);
        if (data.Length > MaximumPayload) throw new InvalidDataException("Travel payload too large.");
        return data;
    }
    public static bool TryDecode(ReadOnlySpan<byte> bytes, out UmbraTravelMessage? message)
    {
        message = null;
        if (bytes.Length is 0 or > MaximumPayload) return false;
        try
        {
            var value = JsonSerializer.Deserialize<UmbraTravelMessage>(bytes);
            if (value is null) return false;
            Validate(value); message = value; return true;
        }
        catch (Exception e) when (e is JsonException or InvalidDataException) { return false; }
    }
    private static void Validate(UmbraTravelMessage m)
    {
        if (m.Version != 1 || m.RequestId == Guid.Empty || !Enum.IsDefined(m.Operation) ||
            !float.IsFinite(m.X) || !float.IsFinite(m.Z) || m.Token?.Length > 64 ||
            m.Proof?.Length > 64 || m.CandidateId?.Length > 128 || m.Message?.Length > 512 ||
            m.Status?.Length > 32 || m.Candidates?.Length > 64)
            throw new InvalidDataException("Invalid travel message.");
    }
    public static byte[] CreateProof(string sessionToken, ReadOnlySpan<byte> challenge, uint characterId)
    {
        if (challenge.Length != 32 || string.IsNullOrWhiteSpace(sessionToken) || characterId == 0)
            throw new ArgumentException("An active login session and challenge are required.");
        byte[] domain = Encoding.ASCII.GetBytes("AetherXIV-Umbra-Travel-v1\0");
        byte[] input = new byte[domain.Length + 36];
        domain.CopyTo(input, 0);
        challenge.CopyTo(input.AsSpan(domain.Length, 32));
        System.Buffers.Binary.BinaryPrimitives.WriteUInt32LittleEndian(input.AsSpan(domain.Length + 32), characterId);
        return HMACSHA256.HashData(Encoding.UTF8.GetBytes(sessionToken), input);
    }
}

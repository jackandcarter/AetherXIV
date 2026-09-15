/*
 * AetherXIV
 * Copyright (C) 2026 Demi Dev Unit
 *
 * This file is part of AetherXIV.
 * See THIRD_PARTY_NOTICES.md for historical and third-party attribution.
 *
 * AetherXIV is free software: you may redistribute it and/or modify it
 * under the terms of the GNU Affero General Public License as published by
 * the Free Software Foundation, either version 3 of the License, or
 * (at your option) any later version.
 *
 * SPDX-License-Identifier: AGPL-3.0-or-later
 */

using System.Security.Cryptography;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace AetherXIV.Launcher.Core;

public sealed record UmbraSignedDocument(
    [property: JsonPropertyName("key_id")] string KeyId,
    [property: JsonPropertyName("payload")] string Payload,
    [property: JsonPropertyName("signature")] string Signature);

public static class UmbraOfficialTrust
{
    public const string StableSigningKeyId = "umbra-stable-2026-01";

    private const string StableSigningKeySpkiBase64 =
        "MFkwEwYHKoZIzj0CAQYIKoZIzj0DAQcDQgAE1yyHAj1SY+TAl+NoU7/U/PiE3HFGzknu4mAG4O+p+ApsDIjbGgC9ewTBZ49GEoyqCz+leqmJRnX1i0tXWJWP+A==";

    public static IReadOnlyDictionary<string, string> SigningKeys { get; } =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [StableSigningKeyId] = StableSigningKeySpkiBase64
        };
}

public static class UmbraSignedDocumentVerifier
{
    private const int MaximumPayloadBytes = 4 * 1024 * 1024;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static T VerifyAndDeserialize<T>(
        string signedJson,
        IReadOnlyDictionary<string, string>? trustedKeys = null)
    {
        if (string.IsNullOrWhiteSpace(signedJson))
            throw new InvalidDataException("Umbra signed document is empty.");

        UmbraSignedDocument envelope = JsonSerializer.Deserialize<UmbraSignedDocument>(
            signedJson,
            JsonOptions) ?? throw new InvalidDataException("Umbra signed document could not be read.");

        IReadOnlyDictionary<string, string> keys = trustedKeys ?? UmbraOfficialTrust.SigningKeys;
        if (!keys.TryGetValue(envelope.KeyId, out string? publicKeyBase64))
            throw new CryptographicException($"Umbra document uses an untrusted signing key: {envelope.KeyId}");

        byte[] payload = DecodeBase64(envelope.Payload, "payload");
        if (payload.Length == 0 || payload.Length > MaximumPayloadBytes)
            throw new InvalidDataException($"Umbra signed payload must be between 1 and {MaximumPayloadBytes} bytes.");

        byte[] signature = DecodeBase64(envelope.Signature, "signature");
        byte[] publicKey = DecodeBase64(publicKeyBase64, "public key");

        using ECDsa verifier = ECDsa.Create();
        verifier.ImportSubjectPublicKeyInfo(publicKey, out int bytesRead);
        if (bytesRead != publicKey.Length)
            throw new CryptographicException("Umbra signing key contains trailing data.");

        if (!verifier.VerifyData(
                payload,
                signature,
                HashAlgorithmName.SHA256,
                DSASignatureFormat.IeeeP1363FixedFieldConcatenation))
        {
            throw new CryptographicException("Umbra document signature is invalid.");
        }

        return JsonSerializer.Deserialize<T>(payload, JsonOptions)
            ?? throw new InvalidDataException("Umbra signed payload could not be read.");
    }

    private static byte[] DecodeBase64(string value, string label)
    {
        try
        {
            return Convert.FromBase64String(value ?? "");
        }
        catch (FormatException ex)
        {
            throw new InvalidDataException($"Umbra signed document {label} is not valid base64.", ex);
        }
    }
}

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

using AetherXIV.Protocol;

namespace AetherXIV.Protocol.Tests;

public sealed class LuaParameterCodecTests
{
    [Fact]
    public void LuaParametersRoundTripKnownPrimitiveTypes()
    {
        LuaParameter[] parameters =
        [
            new(LuaParameterType.Int32, -42),
            new(LuaParameterType.UInt32, 42u),
            new(LuaParameterType.String, "noticeEvent"),
            new(LuaParameterType.BooleanTrue, null),
            new(LuaParameterType.UInt8, (byte)7)
        ];

        byte[] encoded = LuaParameterCodec.Encode(parameters);
        IReadOnlyList<LuaParameter> decoded = LuaParameterCodec.Decode(encoded);

        Assert.Equal(0x0F, encoded[^1]);
        Assert.Equal([0x00, 0xFF, 0xFF, 0xFF, 0xD6], encoded[..5]);
        Assert.Equal(parameters.Length, decoded.Count);
        Assert.Equal(-42, decoded[0].Value);
        Assert.Equal(42u, decoded[1].Value);
        Assert.Equal("noticeEvent", decoded[2].Value);
        Assert.Equal(true, decoded[3].Value);
        Assert.Equal((byte)7, decoded[4].Value);
    }

    [Fact]
    public void StructuredLegacyParameterTypesRoundTrip()
    {
        LuaParameter[] parameters =
        [
            new(LuaParameterType.ItemReference, new LuaItemReference(0x11223344, 5, 6, 7)),
            new(LuaParameterType.ItemOffer, new LuaItemOffer(0x55667788, 9, 10, 11, 12, 13, 14)),
            new(LuaParameterType.PairOfUInt64, new LuaUInt64Pair(15, 16)),
            new(LuaParameterType.UInt16LittleEndian, (ushort)0x1234)
        ];

        IReadOnlyList<LuaParameter> decoded = LuaParameterCodec.Decode(LuaParameterCodec.Encode(parameters));

        Assert.Equal(parameters, decoded);
    }
}

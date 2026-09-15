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

using System.Text;

namespace AetherXIV.Protocol;

public readonly record struct EventStartPacket(
    uint TriggerActorId,
    uint OwnerActorId,
    uint ServerCodes,
    uint Unknown,
    byte EventType,
    string EventName,
    IReadOnlyList<LuaParameter> Parameters)
{
    public const uint ClientScriptErrorServerCode = 0x39800010;

    public const byte ClientScriptErrorEventType = 0x7F;

    public ReadOnlyMemory<byte> RawParameterPayload { get; init; }

    public string? ClientScriptErrorText { get; init; }

    public bool IsClientScriptError =>
        ServerCodes == ClientScriptErrorServerCode || EventType == ClientScriptErrorEventType;

    public uint ClientScriptErrorIndex => TriggerActorId;

    public uint ClientScriptErrorCount => OwnerActorId;
}

public sealed class EventStartPacketCodec : IPacketCodec<EventStartPacket>
{
    public const int PayloadSize = 0xD8 - 0x20;
    public const int FixedHeaderSize = 0x11;
    public const int MaximumEventNameBytes = 0x20;

    public PacketOpcode Opcode => PacketOpcode.EventStart;

    public Type PacketType => typeof(EventStartPacket);

    public EventStartPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        Require(payload, FixedHeaderSize + MaximumEventNameBytes);

        uint triggerActorId = PacketBinary.ReadUInt32LittleEndian(payload);
        uint ownerActorId = PacketBinary.ReadUInt32LittleEndian(payload[4..]);
        uint serverCodes = PacketBinary.ReadUInt32LittleEndian(payload[8..]);
        uint unknown = PacketBinary.ReadUInt32LittleEndian(payload[12..]);
        byte eventType = payload[16];
        ReadOnlySpan<byte> eventPayload = payload[FixedHeaderSize..];
        if (serverCodes == EventStartPacket.ClientScriptErrorServerCode
            || eventType == EventStartPacket.ClientScriptErrorEventType)
        {
            int errorOffset = eventType == EventStartPacket.ClientScriptErrorEventType
                ? 0x31
                : FixedHeaderSize;
            ReadOnlySpan<byte> errorPayload = payload[errorOffset..];
            int errorTerminator = errorPayload.IndexOf((byte)0);
            if (errorTerminator >= 0)
                errorPayload = errorPayload[..errorTerminator];

            return new EventStartPacket(triggerActorId, ownerActorId, serverCodes, unknown, eventType, string.Empty, [])
            {
                RawParameterPayload = eventPayload.ToArray(),
                ClientScriptErrorText = Encoding.ASCII.GetString(errorPayload)
            };
        }

        ReadOnlySpan<byte> eventNameField = eventPayload[..MaximumEventNameBytes];
        int terminator = eventNameField.IndexOf((byte)0);
        if (terminator < 0)
            throw new InvalidDataException("Event start name is missing its bounded null terminator.");

        string eventName = Encoding.ASCII.GetString(eventNameField[..terminator]);

        // Retail uses both layouts, and command events are always fixed.
        // Compact events (ordinary actor/combat events such as a tight
        // talkDefault) write the name, its null terminator, and then the
        // typed list contiguously, so the list's 0x0F terminator lands at or
        // before the fixed 0x20-byte boundary. Command events such as
        // RequestQuestJournalCommand instead retain a legacy FIXED 0x20-byte
        // name field whose unused bytes are arbitrary client memory, with the
        // typed list starting only at the boundary. That padding can
        // coincidentally parse as a valid typed Lua list (observed live: a
        // mangled questId that made GetQuest miss and dropped the qtdata
        // reply entirely), so decode-success alone must never select the
        // compact form — command event padding is never a parameter list.
        // For non-command events, a compact list must also FIT inside the
        // name window; anything that runs past the boundary is fixed layout.
        ReadOnlySpan<byte> compactParameterPayload = eventPayload[(terminator + 1)..];
        ReadOnlySpan<byte> fixedParameterPayload = payload[
            (FixedHeaderSize + MaximumEventNameBytes)..];
        ReadOnlySpan<byte> parameterPayload;
        IReadOnlyList<LuaParameter> parameters;
        if (eventName.StartsWith("command", StringComparison.Ordinal))
        {
            parameters = DecodeFixedOrEmpty(fixedParameterPayload);
            parameterPayload = fixedParameterPayload;
        }
        else
        {
            int compactTerminatorOffset;
            try
            {
                parameters = LuaParameterCodec.Decode(
                    compactParameterPayload,
                    out compactTerminatorOffset);
                // 0x0F must sit at or before the fixed boundary: payload position
                // FixedHeaderSize + terminator + 1 + offset <= FixedHeaderSize +
                // MaximumEventNameBytes.
                if (terminator + 1 + compactTerminatorOffset <= MaximumEventNameBytes)
                {
                    parameterPayload = compactParameterPayload;
                }
                else
                {
                    parameters = DecodeFixedOrEmpty(fixedParameterPayload);
                    parameterPayload = fixedParameterPayload;
                }
            }
            catch (NotSupportedException)
            {
                parameters = DecodeFixedOrEmpty(fixedParameterPayload);
                parameterPayload = fixedParameterPayload;
            }
            catch (InvalidDataException)
            {
                parameters = DecodeFixedOrEmpty(fixedParameterPayload);
                parameterPayload = fixedParameterPayload;
            }
        }

        return new EventStartPacket(triggerActorId, ownerActorId, serverCodes, unknown, eventType, eventName, parameters)
        {
            RawParameterPayload = parameterPayload.ToArray()
        };
    }

    public SubPacket Encode(uint sourceActorId, EventStartPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.OwnerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), packet.ServerCodes);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(12), packet.Unknown);
        payload[16] = packet.EventType;
        // Retail 1.23b clients always send a fixed 0x20-byte name field whose
        // unused bytes are arbitrary client memory, with the typed parameter
        // list starting at the fixed boundary (verified across the retail
        // corpus; the variable junk between the name terminator and the list
        // is client buffer reuse and is never reproduced).
        WriteFixedString(payload.AsSpan(FixedHeaderSize), MaximumEventNameBytes, packet.EventName);
        int parameterOffset = FixedHeaderSize + MaximumEventNameBytes;
        byte[] parameterPayload = EncodeParameterPayload(packet.Parameters, packet.RawParameterPayload);
        if (parameterPayload.Length > payload.Length - parameterOffset)
            throw new InvalidDataException("Event start parameter payload exceeds the packet boundary.");
        parameterPayload.CopyTo(payload.AsSpan(parameterOffset));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }

    private static void Require(ReadOnlySpan<byte> payload, int requiredLength)
    {
        if (payload.Length < requiredLength)
            throw new InvalidDataException($"Event start payload ended before {requiredLength} bytes.");
    }

    internal static string ReadFixedString(ReadOnlySpan<byte> payload, int length)
    {
        if (payload.Length < length)
            throw new InvalidDataException("Fixed string payload ended unexpectedly.");

        ReadOnlySpan<byte> slice = payload[..length];
        int terminator = slice.IndexOf((byte)0);
        if (terminator >= 0)
            slice = slice[..terminator];

        return Encoding.ASCII.GetString(slice);
    }

    internal static void WriteFixedString(Span<byte> payload, int length, string value)
    {
        if (payload.Length < length)
            throw new InvalidDataException("Fixed string target ended unexpectedly.");

        int count = Math.Min(Encoding.ASCII.GetByteCount(value), length);
        Encoding.ASCII.GetBytes(value, payload[..count]);
    }

    internal static IReadOnlyList<LuaParameter> DecodeKnownLuaParametersOrEmpty(ReadOnlySpan<byte> payload)
    {
        return TryDecodeKnownLuaParameters(payload, out IReadOnlyList<LuaParameter> parameters)
            ? parameters
            : [];
    }

    private static IReadOnlyList<LuaParameter> DecodeFixedOrEmpty(
        ReadOnlySpan<byte> fixedParameterPayload)
    {
        return TryDecodeKnownLuaParameters(fixedParameterPayload, out IReadOnlyList<LuaParameter> parameters)
            ? parameters
            : [];
    }

    private static bool TryDecodeKnownLuaParameters(
        ReadOnlySpan<byte> payload,
        out IReadOnlyList<LuaParameter> parameters)
    {
        parameters = [];
        if (payload.IndexOf((byte)0x0F) < 0)
            return false;

        try
        {
            parameters = LuaParameterCodec.Decode(payload);
            return true;
        }
        catch (NotSupportedException)
        {
            return false;
        }
        catch (InvalidDataException)
        {
            return false;
        }
    }

    public static byte[] EncodeParameterPayload(
        IReadOnlyList<LuaParameter> parameters,
        ReadOnlyMemory<byte> rawParameterPayload)
    {
        return rawParameterPayload.Length > 0 && parameters.Count == 0
            ? rawParameterPayload.ToArray()
            : LuaParameterCodec.Encode(parameters);
    }
}

public readonly record struct EventUpdatePacket(
    uint TriggerActorId,
    uint ServerCodes,
    uint Unknown1,
    uint Unknown2,
    byte EventType,
    IReadOnlyList<LuaParameter> Parameters)
{
    public ReadOnlyMemory<byte> RawParameterPayload { get; init; }
}
public sealed class EventUpdatePacketCodec : IPacketCodec<EventUpdatePacket>
{
    public const int PayloadSize = 0x78 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.EventUpdate;

    public Type PacketType => typeof(EventUpdatePacket);

    public EventUpdatePacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x11)
            throw new InvalidDataException("Event update payload ended before the fixed header.");

        ReadOnlySpan<byte> parameterPayload = payload[17..];
        return new EventUpdatePacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            PacketBinary.ReadUInt32LittleEndian(payload[8..]),
            PacketBinary.ReadUInt32LittleEndian(payload[12..]),
            payload[16],
            EventStartPacketCodec.DecodeKnownLuaParametersOrEmpty(parameterPayload))
        {
            RawParameterPayload = parameterPayload.ToArray()
        };
    }

    public SubPacket Encode(uint sourceActorId, EventUpdatePacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.ServerCodes);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(8), packet.Unknown1);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(12), packet.Unknown2);
        payload[16] = packet.EventType;
        EventStartPacketCodec.EncodeParameterPayload(packet.Parameters, packet.RawParameterPayload).CopyTo(payload.AsSpan(17));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct KickEventPacket(
    uint TriggerActorId,
    uint OwnerActorId,
    byte EventType,
    string EventName,
    IReadOnlyList<LuaParameter> Parameters);

public sealed class KickEventPacketCodec : IPacketCodec<KickEventPacket>
{
    public const int PayloadSize = 0x90 - 0x20;

    public PacketOpcode Opcode => PacketOpcode.KickEvent;

    public Type PacketType => typeof(KickEventPacket);

    public KickEventPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x30)
            throw new InvalidDataException("Kick event payload ended before the fixed header.");

        return new KickEventPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            payload[8],
            EventStartPacketCodec.ReadFixedString(payload[16..], 0x20),
            LuaParameterCodec.Decode(payload[0x30..]));
    }

    public SubPacket Encode(uint sourceActorId, KickEventPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.OwnerActorId);
        payload[8] = packet.EventType;
        payload[9] = 0x17;
        PacketBinary.WriteUInt16LittleEndian(payload.AsSpan(10), 0x75DC);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(12), 0x30400000);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(16), 0x20, packet.EventName);
        LuaParameterCodec.Encode(packet.Parameters).CopyTo(payload.AsSpan(0x30));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct RunEventFunctionPacket(
    uint TriggerActorId,
    uint OwnerActorId,
    byte EventType,
    string EventName,
    string FunctionName,
    IReadOnlyList<LuaParameter> Parameters)
{
    public ReadOnlyMemory<byte> TrailingBytes { get; init; }
}

public sealed class RunEventFunctionPacketCodec : IPacketCodec<RunEventFunctionPacket>
{
    // Every observed retail 1.23b RunEventFunction packet uses the compact
    // 0xB0 envelope. Larger legacy buffers are an implementation detail, not
    // part of the wire contract, and can make the client dispatch against an
    // invalid event-function object.
    public const int PayloadSize = 0xB0 - 0x20;

    public const int ParameterOffset = 0x49;

    /// <summary>
    /// Retail closes every RunEventFunction payload with a 7-byte trailing
    /// field at payload offsets 137..143: a float32 (little-endian, observed
    /// 3.0-3.5, movement-state-like) followed by three zero bytes. Its
    /// semantics are not yet identified, so decode preserves the observed
    /// bytes and the server currently emits zeros.
    /// </summary>
    public const int TrailingByteCount = 7;

    public PacketOpcode Opcode => PacketOpcode.RunEventFunction;

    public Type PacketType => typeof(RunEventFunctionPacket);

    public RunEventFunctionPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x49)
            throw new InvalidDataException("Run event function payload ended before the fixed header.");

        return new RunEventFunctionPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            PacketBinary.ReadUInt32LittleEndian(payload[4..]),
            payload[8],
            EventStartPacketCodec.ReadFixedString(payload[9..], 0x20),
            EventStartPacketCodec.ReadFixedString(payload[0x29..], 0x20),
            LuaParameterCodec.Decode(payload[0x49..]))
        {
            TrailingBytes = payload.Length >= PayloadSize
                ? payload[^TrailingByteCount..].ToArray()
                : Array.Empty<byte>(),
        };
    }

    public SubPacket Encode(uint sourceActorId, RunEventFunctionPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.TriggerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), packet.OwnerActorId);
        payload[8] = packet.EventType;
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(9), 0x20, packet.EventName);
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(0x29), 0x20, packet.FunctionName);
        byte[] encodedParameters = LuaParameterCodec.Encode(packet.Parameters);
        if (encodedParameters.Length > PayloadSize - ParameterOffset - TrailingByteCount)
            throw new ArgumentException(
                $"Run event function parameters exceed the 0x{PayloadSize + 0x20:X} packet contract.",
                nameof(packet));

        encodedParameters.CopyTo(payload.AsSpan(ParameterOffset));
        if (packet.TrailingBytes.Length == TrailingByteCount)
            packet.TrailingBytes.Span.CopyTo(payload.AsSpan(PayloadSize - TrailingByteCount));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

public readonly record struct EndEventPacket(
    uint SourcePlayerActorId,
    byte EventType,
    string EventName)
{
    public ReadOnlyMemory<byte> TrailingBytes { get; init; }
}

public sealed class EndEventPacketCodec : IPacketCodec<EndEventPacket>
{
    public const int PayloadSize = 0x50 - 0x20;

    /// <summary>
    /// Retail closes every EndEvent payload with a 7-byte trailing field at
    /// payload offsets 41..47: the constant prefix F2 D4 09 followed by a
    /// session-scoped u32 (client-clock-like). Its semantics are not yet
    /// identified, so decode preserves the observed bytes and the server
    /// currently emits zeros.
    /// </summary>
    public const int TrailingFieldOffset = 0x29;

    public const int TrailingByteCount = 7;

    public PacketOpcode Opcode => PacketOpcode.EndEvent;

    public Type PacketType => typeof(EndEventPacket);

    public EndEventPacket Decode(SubPacket packet)
    {
        if (packet.Header.Opcode != Opcode)
            throw new ArgumentException($"Expected opcode {Opcode} but received {packet.Header.Opcode}.", nameof(packet));

        ReadOnlySpan<byte> payload = packet.Payload.Span;
        if (payload.Length < 0x29)
            throw new InvalidDataException("End event payload ended before the fixed body.");

        return new EndEventPacket(
            PacketBinary.ReadUInt32LittleEndian(payload),
            payload[8],
            EventStartPacketCodec.ReadFixedString(payload[9..], 0x20))
        {
            TrailingBytes = payload.Length >= TrailingFieldOffset + TrailingByteCount
                ? payload.Slice(TrailingFieldOffset, TrailingByteCount).ToArray()
                : Array.Empty<byte>(),
        };
    }

    public SubPacket Encode(uint sourceActorId, EndEventPacket packet)
    {
        byte[] payload = new byte[PayloadSize];
        PacketBinary.WriteUInt32LittleEndian(payload, packet.SourcePlayerActorId);
        PacketBinary.WriteUInt32LittleEndian(payload.AsSpan(4), 0);
        payload[8] = packet.EventType;
        EventStartPacketCodec.WriteFixedString(payload.AsSpan(9), 0x20, packet.EventName);
        if (packet.TrailingBytes.Length == TrailingByteCount)
            packet.TrailingBytes.Span.CopyTo(payload.AsSpan(TrailingFieldOffset, TrailingByteCount));
        return SubPacket.Create(Opcode, sourceActorId, payload);
    }
}

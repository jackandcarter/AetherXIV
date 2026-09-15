using AetherXIV.Protocol;
using AetherXIV.Core.Map.lua;
using System;
using System.Collections.Generic;
using LegacySubPacket = AetherXIV.Core.Common.SubPacket;

namespace AetherXIV.Core.Map.packets
{
    /// <summary>
    /// The shipping Map boundary for protocol codecs. Legacy Map packet classes
    /// may retain their call-site API, but overlapping wire layouts must be
    /// encoded and decoded by AetherXIV.Protocol through this adapter.
    /// </summary>
    static class ProtocolPacketAdapter
    {
        public static LegacySubPacket Encode<TPacket>(
            IPacketCodec<TPacket> codec,
            uint sourceActorId,
            TPacket packet)
        {
            AetherXIV.Protocol.SubPacket encoded = codec.Encode(sourceActorId, packet);
            return new LegacySubPacket(
                checked((ushort)encoded.Header.Opcode),
                encoded.Header.SourceActorId,
                encoded.Payload.ToArray());
        }

        public static AetherXIV.Protocol.SubPacket DecodeInput(LegacySubPacket packet)
        {
            if (packet == null)
                throw new ArgumentNullException(nameof(packet));
            if (packet.header.type != 0x03)
                throw new ArgumentException("Only game-message subpackets can be decoded by a protocol codec.", nameof(packet));

            return AetherXIV.Protocol.SubPacket.Create(
                (PacketOpcode)packet.gameMessage.opcode,
                packet.header.sourceId,
                packet.data);
        }

        /// <summary>
        /// Decodes the client's map login handshake response (opcode 0x0002,
        /// the login-zone-bootstrap packet the client echoes once it accepts
        /// the server handshake). The actor id is read at payload offset 0x08;
        /// the 1.23b client sends 0 there until the zone bootstrap resolves,
        /// so callers must treat a zero actor id as valid. Returns false
        /// (never throws) when the packet is not a well-formed handshake
        /// response or is not a game-message subpacket.
        /// </summary>
        public static bool TryDecodeMapLoginHandshake(
            LegacySubPacket packet,
            out uint actorId)
        {
            actorId = 0;
            if (packet == null
                || packet.header.type != 0x03
                || packet.data == null
                || packet.data.Length < AetherXIV.Protocol.MapLoginHandshakeResponsePacketCodec.PayloadSize)
            {
                return false;
            }

            try
            {
                AetherXIV.Protocol.MapLoginHandshakeResponsePacket decoded =
                    new AetherXIV.Protocol.MapLoginHandshakeResponsePacketCodec().Decode(
                        DecodeInput(packet));
                actorId = decoded.ActorId;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        public static IReadOnlyList<LuaParameter> EncodeLuaParameters(
            IReadOnlyList<LuaParam> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            List<LuaParameter> encoded = new List<LuaParameter>(parameters.Count);
            foreach (LuaParam parameter in parameters)
            {
                LuaParameterType type = (LuaParameterType)checked((byte)parameter.typeID);
                object value = parameter.value;
                switch (type)
                {
                    case LuaParameterType.ItemReference:
                        LuaUtils.ItemRefParam item = (LuaUtils.ItemRefParam)value;
                        value = new LuaItemReference(
                            item.actorId,
                            item.unknown,
                            item.slot,
                            item.itemPackage);
                        break;
                    case LuaParameterType.ItemOffer:
                        LuaUtils.ItemOfferParam offer = (LuaUtils.ItemOfferParam)value;
                        value = new LuaItemOffer(
                            offer.actorId,
                            offer.offerSlot,
                            offer.offerPackageId,
                            offer.unknown1,
                            offer.seekSlot,
                            offer.seekPackageId,
                            offer.unknown2);
                        break;
                    case LuaParameterType.PairOfUInt64:
                        LuaUtils.Type9Param pair = (LuaUtils.Type9Param)value;
                        value = new LuaUInt64Pair(pair.item1, pair.item2);
                        break;
                }
                encoded.Add(new LuaParameter(type, value));
            }
            return encoded;
        }

        public static List<LuaParam> DecodeLuaParameters(
            IReadOnlyList<LuaParameter> parameters)
        {
            if (parameters == null)
                throw new ArgumentNullException(nameof(parameters));

            List<LuaParam> decoded = new List<LuaParam>(parameters.Count);
            foreach (LuaParameter parameter in parameters)
            {
                object value = parameter.Value;
                switch (parameter.Type)
                {
                    case LuaParameterType.ItemReference:
                        LuaItemReference item = (LuaItemReference)value;
                        value = new LuaUtils.ItemRefParam(
                            item.ActorId,
                            item.Unknown,
                            item.Slot,
                            item.InventoryType);
                        break;
                    case LuaParameterType.ItemOffer:
                        LuaItemOffer offer = (LuaItemOffer)value;
                        value = new LuaUtils.ItemOfferParam(
                            offer.ActorId,
                            offer.RewardSlot,
                            offer.RewardPackageId,
                            offer.Unknown1,
                            offer.SeekSlot,
                            offer.SeekPackageId,
                            offer.Unknown2);
                        break;
                    case LuaParameterType.PairOfUInt64:
                        LuaUInt64Pair pair = (LuaUInt64Pair)value;
                        value = new LuaUtils.Type9Param(pair.First, pair.Second);
                        break;
                }
                decoded.Add(new LuaParam((byte)parameter.Type, value));
            }
            return decoded;
        }
    }
}

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

using System;
using System.IO;

namespace AetherXIV.Core.Map.packets.receive
{
    /// <summary>
    /// Client-to-server list-object lifecycle acknowledgement (opcode 0x0130).
    /// The opcode overlaps server-to-client RunEventFunction by direction.
    /// </summary>
    class ClientListObjectLifecycleAcknowledgePacket
    {
        public const ushort OPCODE = 0x0130;
        public const int PAYLOAD_SIZE = 0x10;
        public const uint ACTOR_LIST_TYPE = 0x2711;

        public bool invalidPacket;
        public uint actorId;
        public uint listType;
        public uint reserved0;
        public uint reserved1;

        public ClientListObjectLifecycleAcknowledgePacket(byte[] data)
        {
            if (data == null || data.Length != PAYLOAD_SIZE)
            {
                invalidPacket = true;
                return;
            }

            try
            {
                using MemoryStream stream = new MemoryStream(data, writable: false);
                using BinaryReader reader = new BinaryReader(stream);
                actorId = reader.ReadUInt32();
                listType = reader.ReadUInt32();
                reserved0 = reader.ReadUInt32();
                reserved1 = reader.ReadUInt32();
            }
            catch (Exception)
            {
                invalidPacket = true;
            }
        }

        public bool IsCanonicalActorListAcknowledge()
        {
            return !invalidPacket
                && listType == ACTOR_LIST_TYPE
                && reserved0 == 0
                && reserved1 == 0;
        }
    }
}

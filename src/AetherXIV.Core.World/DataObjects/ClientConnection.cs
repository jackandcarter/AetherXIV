using System;
using System.Net.Sockets;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;

using AetherXIV.Core.Common;
using AetherXIV.Core.World.DataObjects;

namespace AetherXIV.Core.World
{
    class ClientConnection
    {
        //Connection stuff
        public Socket socket;
        public byte[] buffer;
        private BlockingCollection<BasePacket> SendPacketQueue = new BlockingCollection<BasePacket>(1000);
        private readonly object sendLock = new object();
        private readonly object relayLock = new object();
        private readonly List<SubPacket> relaySubPacketQueue = new List<SubPacket>();
        public int lastPartialSize = 0;

        //Instance Stuff
        public Session owner;

        public void QueuePacket(BasePacket packet)
        {
            DevDiagnostics.TraceWireBasePacket("World", "server-to-client", packet);
            if (SendPacketQueue.Count == SendPacketQueue.BoundedCapacity - 1)
                FlushQueuedSendPackets();

            SendPacketQueue.Add(packet);
        }

        public void QueuePacket(SubPacket subpacket)
        {
            if (SendPacketQueue.Count == SendPacketQueue.BoundedCapacity - 1)
                FlushQueuedSendPackets();

            bool isAuthed = true;
            bool isEncrypted = false;
            subpacket.SetTargetId(owner.sessionId);
            BasePacket packet = BasePacket.CreatePacket(subpacket, isAuthed, isEncrypted);
            SendPacketQueue.Add(packet);
            if (DevDiagnostics.IsLinkpearlDiagnosticOpcode(subpacket.gameMessage.opcode))
            {
                DevDiagnostics.Trace(
                    "world.packet.queued",
                    "session", owner == null ? 0 : owner.sessionId,
                    "character", owner == null ? "" : owner.characterName ?? "",
                    "opcode", String.Format("0x{0:X4}", subpacket.gameMessage.opcode),
                    "type", String.Format("0x{0:X4}", subpacket.header.type),
                    "source", String.Format("0x{0:X8}", subpacket.header.sourceId),
                    "target", String.Format("0x{0:X8}", subpacket.header.targetId),
                    "subpacketSize", subpacket.header.subpacketSize,
                    "basePacketSize", packet.header.packetSize,
                    "queueDepth", SendPacketQueue.Count);
            }
        }

        public void QueueRelayPacket(SubPacket subpacket)
        {
            if (subpacket == null)
                return;

            subpacket.SetTargetId(owner.sessionId);
            DevDiagnostics.TraceWireSubPacket("World", "server-to-client-relay", subpacket);
            lock (relayLock)
                relaySubPacketQueue.Add(subpacket);
        }

        /// <summary>
        /// Groups Map relay traffic into the compressed multi-subpacket frames
        /// used by the retail World connection. A single actor bootstrap batch
        /// can still span multiple frames, but an actor's related records are
        /// no longer each flushed as an isolated base packet.
        /// </summary>
        public void FlushRelayPackets()
        {
            List<SubPacket> pending;
            lock (relayLock)
            {
                if (relaySubPacketQueue.Count == 0)
                    return;

                pending = new List<SubPacket>(relaySubPacketQueue);
                relaySubPacketQueue.Clear();
            }

            int offset = 0;
            int frameCount = 0;
            int largestFrameSubpacketCount = 0;
            int largestFrameUncompressedBodyBytes = 0;
            int largestFrameWireBytes = 0;
            int oversizedSingleSubpackets = 0;
            int eventBoundarySplits = 0;
            while (offset < pending.Count)
            {
                List<SubPacket> frameSubpackets = new List<SubPacket>();
                int bodyBytes = 0;
                bool frameContainsRunEventFunction = false;
                bool frameContainsEndEvent = false;

                while (offset < pending.Count)
                {
                    SubPacket candidate = pending[offset];
                    int candidateSize = candidate.header.subpacketSize;
                    if (WorldRelayFramePolicy.RequiresBoundaryBefore(
                        frameContainsRunEventFunction,
                        frameContainsEndEvent,
                        candidate.header.type,
                        candidate.gameMessage.opcode))
                    {
                        eventBoundarySplits++;
                        break;
                    }

                    if (!WorldRelayFramePolicy.CanAppend(
                        frameSubpackets.Count,
                        bodyBytes,
                        candidateSize))
                    {
                        break;
                    }

                    if (frameSubpackets.Count == 0
                        && candidateSize
                            > WorldRelayFramePolicy
                                .MaximumUncompressedBodyBytes)
                    {
                        oversizedSingleSubpackets++;
                    }

                    frameSubpackets.Add(candidate);
                    bodyBytes += candidateSize;
                    frameContainsRunEventFunction |=
                        candidate.header.type == 0x0003
                        && candidate.gameMessage.opcode
                            == WorldRelayFramePolicy
                                .RunEventFunctionOpcode;
                    frameContainsEndEvent |=
                        candidate.header.type == 0x0003
                        && candidate.gameMessage.opcode
                            == WorldRelayFramePolicy.EndEventOpcode;
                    offset++;
                }                    BasePacket frame = BasePacket.CreatePacket(
                        frameSubpackets,
                    isAuthed: true,
                    isCompressed: true);
                SendPacketQueue.Add(frame);
                frameCount++;
                largestFrameSubpacketCount = Math.Max(
                    largestFrameSubpacketCount,
                    frameSubpackets.Count);
                largestFrameUncompressedBodyBytes = Math.Max(
                    largestFrameUncompressedBodyBytes,
                    bodyBytes);
                largestFrameWireBytes = Math.Max(
                    largestFrameWireBytes,
                    frame.header.packetSize);
            }

            DevDiagnostics.Trace(
                "world.relay.batch",
                "session", owner.sessionId,
                "character", owner.characterName ?? "",
                "subpackets", pending.Count,
                "frames", frameCount,
                "largestFrameSubpacketCount",
                    largestFrameSubpacketCount,
                "largestFrameUncompressedBodyBytes",
                    largestFrameUncompressedBodyBytes,
                "largestFrameWireBytes", largestFrameWireBytes,
                "oversizedSingleSubpackets", oversizedSingleSubpackets,
                "eventBoundarySplits", eventBoundarySplits,
                "maximumSubpacketsPerFrame",
                    WorldRelayFramePolicy.MaximumSubpacketsPerFrame,
                "maximumUncompressedBodyBytes",
                    WorldRelayFramePolicy
                        .MaximumUncompressedBodyBytes);
            FlushQueuedSendPackets();
        }

        public void FlushQueuedSendPackets()
        {
            lock (sendLock)
            {
                if (socket == null || !socket.Connected)
                    return;

                while (SendPacketQueue.Count > 0)
                {
                    BasePacket packet = SendPacketQueue.Take();

                    byte[] packetBytes = packet.GetPacketBytes();
                    DevDiagnostics.Trace(
                        "world.packet.write",
                        "session", owner == null ? 0 : owner.sessionId,
                        "character", owner == null ? "" : owner.characterName ?? "",
                        "packetCount", packet.header.numSubpackets,
                        "bytes", packetBytes.Length,
                        "compressed", packet.header.isCompressed,
                        "queueDepthBeforeWrite", SendPacketQueue.Count,
                        "transport", "base");

                    try
                    {
                        SendAll(socket, packetBytes);
                    }
                    catch (Exception e)
                    { Program.Log.Error(e, "Weird case, socket was d/ced: {0}"); }
                }
            }
        }

        private static void SendAll(Socket destination, byte[] payload)
        {
            int sent = 0;
            while (sent < payload.Length)
            {
                int written = destination.Send(
                    payload,
                    sent,
                    payload.Length - sent,
                    SocketFlags.None);
                if (written <= 0)
                    throw new SocketException(
                        (int)SocketError.ConnectionReset);

                sent += written;
            }
        }

        public String GetAddress()
        {
            return String.Format("{0}:{1}", (socket.RemoteEndPoint as IPEndPoint).Address, (socket.RemoteEndPoint as IPEndPoint).Port);
        }

        public bool IsConnected()
        {
            return (socket.Poll(1, SelectMode.SelectRead) && socket.Available == 0);
        }

        public void Disconnect()
        {
            try
            {
                if (socket != null && socket.Connected)
                    socket.Shutdown(SocketShutdown.Both);
            }
            catch (SocketException)
            {
            }
            catch (ObjectDisposedException)
            {
            }

            try
            {
                socket?.Close();
            }
            catch (ObjectDisposedException)
            {
            }
        }
    }
}

using System;
using System.Net.Sockets;

using AetherXIV.Core.Common;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Net;
using AetherXIV.Core.Map.packets.WorldPackets.Send;

namespace AetherXIV.Core.Map.dataobjects
{
    class ZoneConnection
    {
        private const int MAXIMUM_RELAY_WRITE_BYTES = 0xF000;

        //Connection stuff
        public Socket socket;
        public byte[] buffer;
        private BlockingCollection<SubPacket> SendPacketQueue = new BlockingCollection<SubPacket>(1000);
        private readonly object sendLock = new object();
        public int lastPartialSize = 0;

        public void QueuePacket(SubPacket subpacket)
        {
            DevDiagnostics.TraceWireSubPacket("Map", "server-to-world", subpacket);
            if (SendPacketQueue.Count == SendPacketQueue.BoundedCapacity - 1)
                FlushQueuedSendPackets();

            SendPacketQueue.Add(subpacket);
        }

        public void FlushQueuedSendPackets()
        {
            lock (sendLock)
            {
                if (socket == null || !socket.Connected)
                    return;

                byte[] deferredPacket = null;
                int relayedPackets = 0;
                int relayWrites = 0;
                int relayedBytes = 0;
                while (deferredPacket != null || SendPacketQueue.Count > 0)
                {
                    List<byte[]> packetBytes = new List<byte[]>();
                    int writeBytes = 0;

                    byte[] firstPacket =
                        deferredPacket ?? SendPacketQueue.Take().GetBytes();
                    deferredPacket = null;
                    packetBytes.Add(firstPacket);
                    writeBytes += firstPacket.Length;

                    while (SendPacketQueue.Count > 0)
                    {
                        byte[] nextPacket = SendPacketQueue.Take().GetBytes();
                        if (writeBytes + nextPacket.Length
                            > MAXIMUM_RELAY_WRITE_BYTES)
                        {
                            deferredPacket = nextPacket;
                            break;
                        }

                        packetBytes.Add(nextPacket);
                        writeBytes += nextPacket.Length;
                    }

                    byte[] writeBuffer = new byte[writeBytes];
                    int offset = 0;
                    foreach (byte[] bytes in packetBytes)
                    {
                        Array.Copy(bytes, 0, writeBuffer, offset, bytes.Length);
                        offset += bytes.Length;
                    }

                    try
                    {
                        SendAll(socket, writeBuffer);
                        relayedPackets += packetBytes.Count;
                        relayWrites++;
                        relayedBytes += writeBuffer.Length;
                    }
                    catch (Exception e)
                    { Program.Log.Error(e, "Weird case, socket was d/ced: {0}"); }
                }

                if (relayedPackets > 1)
                {
                    DevDiagnostics.Trace(
                        "map.relay.flush",
                        "subpackets", relayedPackets,
                        "writes", relayWrites,
                        "bytes", relayedBytes,
                        "maximumWriteBytes", MAXIMUM_RELAY_WRITE_BYTES);
                }
            }
        }

        private static void SendAll(Socket target, byte[] bytes)
        {
            int sent = 0;
            while (sent < bytes.Length)
            {
                int count =
                    target.Send(
                        bytes,
                        sent,
                        bytes.Length - sent,
                        SocketFlags.None);
                if (count <= 0)
                    throw new SocketException((int)SocketError.ConnectionReset);
                sent += count;
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
            if (socket.Connected)
                socket.Disconnect(false);
        }

        public void RequestZoneChange(uint sessionId, uint destinationZoneId, byte spawnType, float spawnX, float spawnY, float spawnZ, float spawnRotation)
        {
            WorldRequestZoneChangePacket.BuildPacket(sessionId, destinationZoneId, spawnType, spawnX, spawnY, spawnZ, spawnRotation).DebugPrintSubPacket();
            QueuePacket(WorldRequestZoneChangePacket.BuildPacket(sessionId, destinationZoneId, spawnType, spawnX, spawnY, spawnZ, spawnRotation));
        }
    }
}

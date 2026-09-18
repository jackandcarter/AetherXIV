using System;
using System.Diagnostics;
using AetherXIV.Core.Common;
using AetherXIV.Core.World.DataObjects;
using AetherXIV.Protocol;

namespace AetherXIV.Core.World;

internal static class UmbraTravelRelay
{
    internal static void Process(Server server, ClientConnection connection, AetherXIV.Core.Common.SubPacket packet)
    {
        Session owner = connection.owner;
        if (owner == null || owner.type != Session.Channel.ZONE ||
            !ReferenceEquals(owner.clientConnection, connection) ||
            !ReferenceEquals(server.GetSession(owner.sessionId), owner) ||
            packet.header.sourceId != owner.sessionId || packet.header.targetId != owner.sessionId ||
            !UmbraTravelWire.TryDecode(packet.data, out var request)) return;
        long now = Stopwatch.GetTimestamp();
        if (connection.LastTravelRequestAt == 0 || Stopwatch.GetElapsedTime(connection.LastTravelRequestAt, now).TotalSeconds >= 1)
        { connection.LastTravelRequestAt = now; connection.TravelRequestsInWindow = 0; }
        if (++connection.TravelRequestsInWindow > 8) return;
        void Reply(string status, string message, string token = null) => connection.QueuePacket(
            new AetherXIV.Core.Common.SubPacket(UmbraTravelWire.ReplyOpcode, owner.sessionId,
                UmbraTravelWire.Encode(new UmbraTravelMessage { RequestId = request.RequestId,
                    Operation = UmbraTravelOperation.Reply, Status = status, Message = message, Token = token })));
        if (request.Operation == UmbraTravelOperation.Challenge)
        {
            Reply("Challenge", "Prove the active account session.", connection.TravelAuthentication.Issue(owner.sessionId));
            return;
        }
        if (request.Operation == UmbraTravelOperation.Authenticate)
        {
            bool valid = connection.TravelAuthentication.HasPendingChallenge(owner.sessionId)
                && Database.TryGetTravelSession(owner.sessionId, out string token, out DateTimeOffset expiry)
                && connection.TravelAuthentication.Verify(owner.sessionId, request.Proof, token, expiry);
            Reply(valid ? "Authenticated" : "Denied", valid ? "Travel connection authenticated." : "Account session verification failed.");
            return;
        }
        if (!connection.TravelAuthentication.IsAuthenticated(owner.sessionId))
        { Reply("Denied", "Authenticate the travel connection first."); return; }
        if (request.Operation is not (UmbraTravelOperation.Preview or UmbraTravelOperation.Commit)) return;
        if (owner.routing1 == null) { Reply("Unavailable", "No map route is ready."); return; }
        // Only this authenticated branch forwards the extension to Map.
        owner.routing1.SendPacket(packet);
    }
}

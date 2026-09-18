using System;
using System.Linq;
using AetherXIV.Core.Map.dataobjects;
using AetherXIV.Core.Map.actors.area;
using AetherXIV.Protocol;

namespace AetherXIV.Core.Map.utils;

internal static class UmbraTravelHandler
{
    // Initial verified geometry coverage. Other maps fail closed.
    private const uint ZoneId = 170, MapId = 1101;
    internal static void Enqueue(ZoneConnection connection, Session session, UmbraTravelMessage request)
    {
        if (!ReferenceEquals(connection, Server.GetWorldConnection()) || session == null) return;
        var area = session.GetActor().GetZone();
        if (area == null || !area.QueueTravel(() =>
            {
                try { Process(connection, session, request); }
                catch (Exception) { Reply(session, request, "Failed", "Map travel processing failed."); }
            }))
            Reply(session, request, "Unavailable", "Map travel queue is unavailable.");
    }
    private static void Process(ZoneConnection connection, Session session, UmbraTravelMessage request)
    {
        if (!ReferenceEquals(connection, Server.GetWorldConnection()) ||
            !ReferenceEquals(session, Server.GetServer().GetSession(session.id))) return;
        var player = session.GetActor();
        if (session.isEnding || session.isUpdatesLocked || player.IsDead())
        { Reply(session, request, "Denied", "Travel is unavailable during this character state."); return; }
        if (player.zoneId != ZoneId || player.GetZone() is not Zone zone ||
            !String.IsNullOrEmpty(player.privateArea) || player.privateAreaType != 0 ||
            zone.tiledNavMesh == null || zone.navMeshQuery == null || zone.TravelGeometryRevision == null)
        { Reply(session, request, "Unavailable", "This zone has no verified map-travel geometry."); return; }
        var destination = new TravelDestination(ZoneId, "", 0, MapId, "", zone.TravelGeometryRevision);
        if (request.Operation == UmbraTravelOperation.Preview)
        {
            if (request.ZoneId != ZoneId || request.MapId != MapId)
            { Reply(session, request, "InvalidDestination", "Select the current supported zone map."); return; }
            var landing = NavmeshLandingResolver.Resolve(zone.tiledNavMesh, zone.navMeshQuery, request.X, request.Z);
            if (landing.Surfaces.Count == 0 || landing.Surfaces.Count > 64)
            { Reply(session, request, "NoLanding", "No unambiguous set of supported landings was found."); return; }
            var candidates = landing.Surfaces.Select((p, i) => new TravelLanding(i.ToString(System.Globalization.CultureInfo.InvariantCulture), p.X, p.Y, p.Z)).ToArray();
            var grant = session.TravelPreviews.Issue(destination, candidates);
            if (grant == null) { Reply(session, request, "Unavailable", "Too many pending previews."); return; }
            session.QueuePacket(Packet(new UmbraTravelMessage {
                Operation = UmbraTravelOperation.Reply, RequestId = request.RequestId,
                Status = candidates.Length == 1 ? "Ready" : "Ambiguous", Message = "Confirm a resolved landing to travel.",
                Token = grant.Token, ExpiresAtUnixMs = grant.ExpiresAt.ToUnixTimeMilliseconds(),
                Candidates = candidates.Select((p, i) => new UmbraTravelWireLanding(p.Id, p.X, p.Y, p.Z, landing.Surfaces[i].HorizontalAdjustment)).ToArray()
            }, session.id));
            return;
        }
        if (request.Operation != UmbraTravelOperation.Commit) return;
        var claim = session.TravelPreviews.Claim(request.Token, request.CandidateId, destination);
        if (claim.Status == TravelClaimStatus.Finished)
        { Reply(session, request, claim.Outcome.Completed ? "Completed" : "Failed", claim.Outcome.Message); return; }
        if (claim.Status != TravelClaimStatus.Acquired)
        { Reply(session, request, "Expired", "The preview cannot be executed; select a new destination."); return; }
        TravelOutcome outcome;
        try
        {
            var p = claim.Landing;
            var recheck = NavmeshLandingResolver.Resolve(zone.tiledNavMesh, zone.navMeshQuery, p.X, p.Z);
            if (!recheck.Surfaces.Any(s => MathF.Abs(s.X-p.X)<0.05f && MathF.Abs(s.Z-p.Z)<0.05f && MathF.Abs(s.Y-p.Y)<0.05f))
                outcome = new(false, "The landing is no longer valid.");
            else
            {
                Server.GetWorldManager().DoPlayerMoveInZone(player, p.X, p.Y, p.Z, player.rotation);
                outcome = new(true, "Teleport applied by the map server.");
            }
        }
        catch (Exception)
        { outcome = new(false, "Teleport result is uncertain; check your position before retrying."); }
        session.TravelPreviews.Finish(request.Token, request.CandidateId, outcome);
        Reply(session, request, outcome.Completed ? "Completed" : "Failed", outcome.Message);
    }
    private static AetherXIV.Core.Common.SubPacket Packet(UmbraTravelMessage message, uint id) =>
        new(UmbraTravelWire.ReplyOpcode, id, UmbraTravelWire.Encode(message));
    private static void Reply(Session s, UmbraTravelMessage r, string status, string message) =>
        s.QueuePacket(Packet(new UmbraTravelMessage { RequestId = r.RequestId, Operation = UmbraTravelOperation.Reply,
            Status = status, Message = message }, s.id));
}

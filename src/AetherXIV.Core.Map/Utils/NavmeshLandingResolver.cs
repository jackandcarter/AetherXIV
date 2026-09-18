// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

using System;
using System.Collections.Generic;
using System.Linq;
using SharpNav;
using SharpNav.Geometry;
using SharpNav.Pathfinding;

namespace AetherXIV.Core.Map.utils;

public enum LandingQueryStatus { Ready, Ambiguous, NoLanding, Unavailable, Invalid }
public sealed record LandingSurface(float X, float Y, float Z, float HorizontalAdjustment);
public sealed record LandingQueryResult(LandingQueryStatus Status, IReadOnlyList<LandingSurface> Surfaces);

/// <summary>
/// Destination-only mesh query: no walking path from the origin is required.
/// Call on the owning map thread. Coordinates are NAVMESH coordinates; callers
/// must verify the zone's game/mesh transform and map floor before offering travel.
/// Results describe mesh surfaces, not dynamic collision or access permission.
/// </summary>
public static class NavmeshLandingResolver
{
    /// <summary>
    /// Searches all elevations in this mesh. This avoids selecting the origin's
    /// floor or inventing a Y value when the map pin contains only two axes.
    /// The query must belong to this mesh; coordinates still use mesh space.
    /// </summary>
    public static LandingQueryResult Resolve(TiledNavMesh mesh, NavMeshQuery query,
        float x, float z, float snapRadius = 3f)
    {
        if (!TryGetHeightRange(mesh, out float minimum, out float maximum))
            return Result(LandingQueryStatus.Unavailable);
        return Resolve(query, x, z, minimum, maximum, snapRadius);
    }

    internal static bool TryGetHeightRange(TiledNavMesh mesh, out float minimum, out float maximum)
    {
        minimum = float.PositiveInfinity;
        maximum = float.NegativeInfinity;
        if (mesh == null) return false;
        foreach (var tile in mesh.Tiles)
        {
            if (tile == null || tile.Verts == null) continue;
            // Detail triangles can rise above/below the coarse polygon vertices.
            foreach (var vertex in tile.Verts.Concat(tile.DetailVerts ?? Array.Empty<Vector3>()))
            {
                if (!float.IsFinite(vertex.X) || !float.IsFinite(vertex.Y) || !float.IsFinite(vertex.Z))
                    return false;
                minimum = MathF.Min(minimum, vertex.Y);
                maximum = MathF.Max(maximum, vertex.Y);
            }
        }
        return float.IsFinite(minimum) && float.IsFinite(maximum) && maximum - minimum <= 100000f;
    }

    public static LandingQueryResult Resolve(NavMeshQuery query, float x, float z,
        float minimumHeight, float maximumHeight, float snapRadius = 3f)
    {
        if (query == null) return Result(LandingQueryStatus.Unavailable);
        if (!float.IsFinite(x) || !float.IsFinite(z) || !float.IsFinite(minimumHeight) ||
            !float.IsFinite(maximumHeight) || minimumHeight > maximumHeight ||
            !float.IsFinite(snapRadius) || snapRadius <= 0 || snapRadius > 10f ||
            maximumHeight - minimumHeight > 100000f)
            return Result(LandingQueryStatus.Invalid);

        var center = new Vector3(x, minimumHeight + (maximumHeight - minimumHeight) / 2f, z);
        var extent = new Vector3(snapRadius, (maximumHeight - minimumHeight) / 2f + 0.1f, snapRadius);
        var polygons = new List<NavPolyId>();
        if (!query.QueryPolygons(ref center, ref extent, polygons))
            return Result(LandingQueryStatus.NoLanding);

        var surfaces = new List<LandingSurface>();
        foreach (var polygon in polygons)
        {
            var point = new Vector3();
            if (!query.ClosestPointOnPoly(polygon, center, ref point)) continue;
            float height = 0;
            if (!query.GetPolyHeight(polygon, point, ref height) || !float.IsFinite(height) ||
                height < minimumHeight || height > maximumHeight) continue;
            float dx = point.X - x, dz = point.Z - z;
            float distance = MathF.Sqrt(dx * dx + dz * dz);
            if (!float.IsFinite(distance) || distance > snapRadius) continue;
            surfaces.Add(new LandingSurface(point.X, height, point.Z, distance));
        }

        // Merge only coincident boundary hits, not distinct surfaces at similar
        // heights on opposite sides of a wall. Preserve ambiguity for the caller.
        var unique = new List<LandingSurface>();
        foreach (var point in surfaces.OrderBy(p => p.HorizontalAdjustment).ThenBy(p => p.Y))
        {
            if (unique.Any(p => MathF.Abs(p.X - point.X) < 0.05f &&
                MathF.Abs(p.Z - point.Z) < 0.05f && MathF.Abs(p.Y - point.Y) < 0.05f)) continue;
            unique.Add(point);
        }
        return new LandingQueryResult(unique.Count switch
        {
            0 => LandingQueryStatus.NoLanding,
            1 => LandingQueryStatus.Ready,
            _ => LandingQueryStatus.Ambiguous
        }, unique.AsReadOnly());
    }

    private static LandingQueryResult Result(LandingQueryStatus status) =>
        new(status, Array.Empty<LandingSurface>());
}

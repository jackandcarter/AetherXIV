// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Aether.Umbra.Framework;

// Map-local coordinates, AFTER the verified adapter has removed viewport pan/zoom.
// These are calibration observations, not a declaration of native field layouts.
internal readonly record struct UmbraMapControlPoint(double U, double V, double X, double Z)
{
    internal bool IsFinite => double.IsFinite(U) && double.IsFinite(V) &&
        double.IsFinite(X) && double.IsFinite(Z);
}

/// <summary>
/// Immutable affine calibration fitted to three non-collinear measured points.
/// Requires independent validation observations. No shipped map calibration is
/// implied by this type; adapters must also match build, map and floor identity.
/// </summary>
internal sealed class UmbraMapCalibration
{
    private readonly double ax, bx, cx, az, bz, cz;
    private UmbraMapCalibration(double ax, double bx, double cx, double az, double bz, double cz)
    {
        this.ax = ax; this.bx = bx; this.cx = cx;
        this.az = az; this.bz = bz; this.cz = cz;
    }

    internal static bool TryCreate(IReadOnlyList<UmbraMapControlPoint> anchors,
        IReadOnlyList<UmbraMapControlPoint> validation, double maximumError,
        out UmbraMapCalibration? calibration)
    {
        calibration = null;
        if (anchors.Count != 3 || validation.Count < 2 || !double.IsFinite(maximumError) ||
            maximumError <= 0 || anchors.Any(p => !p.IsFinite) || validation.Any(p => !p.IsFinite)) return false;
        var a = anchors[0]; var b = anchors[1]; var c = anchors[2];
        double du = b.U - a.U, dv = b.V - a.V, eu = c.U - a.U, ev = c.V - a.V;
        double determinant = du * ev - dv * eu;
        double scale = Math.Sqrt((du * du + dv * dv) * (eu * eu + ev * ev));
        if (!double.IsFinite(scale) || scale == 0 || Math.Abs(determinant) <= scale * 1e-6) return false;
        double ax = ((b.X - a.X) * ev - (c.X - a.X) * dv) / determinant;
        double bx = (du * (c.X - a.X) - eu * (b.X - a.X)) / determinant;
        double az = ((b.Z - a.Z) * ev - (c.Z - a.Z) * dv) / determinant;
        double bz = (du * (c.Z - a.Z) - eu * (b.Z - a.Z)) / determinant;
        if (Math.Abs(ax * bz - bx * az) < 1e-12) return false;
        var fitted = new UmbraMapCalibration(ax, bx, a.X - ax * a.U - bx * a.V,
            az, bz, a.Z - az * a.U - bz * a.V);
        var seen = new HashSet<(double, double)>();
        foreach (var p in validation)
        {
            if (anchors.Any(q => q.U == p.U && q.V == p.V) || !seen.Add((p.U, p.V))) return false;
            if (!fitted.TryToWorld(p.U, p.V, out float x, out float z) ||
                Math.Sqrt((x - p.X) * (x - p.X) + (z - p.Z) * (z - p.Z)) > maximumError) return false;
        }
        calibration = fitted;
        return true;
    }

    internal bool TryToWorld(double u, double v, out float x, out float z)
    {
        x = (float)(ax * u + bx * v + cx);
        z = (float)(az * u + bz * v + cz);
        return float.IsFinite(x) && float.IsFinite(z);
    }
}

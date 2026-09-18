// Copyright (C) 2026 Demi Dev Unit
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Aether.Umbra.Framework;

/// <summary>
/// The 1.23b map grid, derived from mapNavi_data columns 3/4 (zero-based).
/// This is NOT a screen-pixel transform, zone identifier, or terrain-height query.
/// The native adapter must establish the active navigation row and reject the
/// overview mode (native +0x9f0 == 1), which displays no player grid coordinates.
/// See tools/Development/verify-umbra-map-grid.py for the native evidence chain.
/// </summary>
internal sealed class UmbraMapGrid
{
    internal const string ClientSha256 = "9341f2b4567440b310a4d494f5cc5599ca334ba51c8042247317ff466492f2e9";
    internal const double CellSize = 100;
    private readonly float originX, originZ;

    private UmbraMapGrid(int offsetX, int offsetZ)
    {
        // Match the client's signed-int to float conversion before negation.
        originX = -(float)offsetX;
        originZ = -(float)offsetZ;
    }

    internal static bool TryCreate(string clientSha256, ReadOnlySpan<int> navigationColumns,
        out UmbraMapGrid? grid)
    {
        grid = null;
        if (!string.Equals(clientSha256, ClientSha256, StringComparison.OrdinalIgnoreCase) ||
            navigationColumns.Length != 18) return false;
        grid = new(navigationColumns[3], navigationColumns[4]);
        return true;
    }

    // Continuous coordinates retain the position within a cell for pin placement.
    internal bool TryToGrid(float x, float z, out double u, out double v)
    {
        u = ((double)x - originX) / CellSize;
        v = ((double)z - originZ) / CellSize;
        return double.IsFinite(u) && double.IsFinite(v);
    }

    internal bool TryToWorld(double u, double v, out float x, out float z)
    {
        x = (float)(u * CellSize + originX);
        z = (float)(v * CellSize + originZ);
        return float.IsFinite(x) && float.IsFinite(z);
    }

    internal bool TryGetDisplayedCell(float x, float z, out int column, out int row)
    {
        column = row = 0;
        if (!TryToGrid(x, z, out var u, out var v)) return false;
        // Native CVTTSD2SI truncates toward zero; Math.Floor differs below zero.
        u = Math.Truncate(u); v = Math.Truncate(v);
        if (u < int.MinValue || u > int.MaxValue || v < int.MinValue || v > int.MaxValue)
            return false;
        column = (int)u; row = (int)v;
        return true;
    }
}

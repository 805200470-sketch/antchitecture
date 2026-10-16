// Ant Garden core — fills the distance grids of PathField (pathfield.js _buildGrids, the stamping part).
//
// The prototype stamps every centreline segment and every chamber over the whole grid (its stamp
// reach is the full grid size), which is O(segments × cells). The tiled stamper below produces the
// same float32 grids bit for bit while visiting only the segments that can matter for each cell:
//
//  * c (centre distance) only ever changes when dist < c, so it ends as float32(min dist) whatever the
//    order; d likewise ends as float32(min v) (capped by its start value).
//  * hw is written together with c, so it comes from the last segment that lowered c. That segment
//    is one whose distance rounds to the same float32 as the minimum; any segment further away
//    cannot be it. Those few segments are always candidates and are visited in the original order.
//  * Candidates for a tile are all segments whose bounding box comes within an upper bound of the
//    tile's nearest-segment distance (for c and hw), or within reach of its smallest d (for d).
//    The margins are far larger than any rounding error, so no needed segment is ever dropped.
//  * Chambers run in the original order after the segments and skip only cells where neither c nor
//    d can change (|noise2| ≤ 2.74 bounds the lobe radius).
//
// The reference stampers (a direct port) are kept for the tests, which compare both bit for bit.
using System;
using System.Collections.Generic;

namespace AntGarden.Core
{
    internal static class FieldStamper
    {
        /// <summary>Tests only: use the direct port instead of the tiled stamper.</summary>
        internal static bool UseReference = false;

        private const int Tile = 8; // cells per tile side
        private const int SuperTile = 32; // cells per super-tile side (a multiple of Tile)
        private const double Margin = 1e-6; // absolute slack on every bound (rounding errors are ~1e-13)
        private const double NoiseBound = 3.0; // |Noise.Noise2| < 2.74 (3 corners × 70 × max t⁴·√2·r)

        /// <summary>The grid being filled (float32, row-major).</summary>
        internal sealed class Target
        {
            public float[] D, C, Hw;
            public int Nx, Nz;
            public double MinX, MinZ, Cell, Band;
        }

        // ---- shared helpers --------------------------------------------------------------------

        // Index ranges as in JS: Math.max(0, Math.floor(...)) / Math.min(n - 1, Math.ceil(...)); NaN → empty.
        private static int Lo(double v)
        {
            double f = Math.Floor(v);
            return double.IsNaN(f) ? int.MaxValue : (int)JsMath.Max(0, JsMath.Min(f, int.MaxValue));
        }

        private static int Hi(double v, int n)
        {
            double c = Math.Ceiling(v);
            return double.IsNaN(c) ? int.MinValue : (int)JsMath.Min(n - 1, JsMath.Max(c, int.MinValue));
        }

        /// <summary>Centreline segments in JS stamping order, with what the stamper needs about each.</summary>
        private sealed class Segments
        {
            public readonly int Count;
            public readonly double[] Ax, Az, Ha, Vx, Vz, Len2, Dh;
            public readonly int[] X0, X1, Z0, Z1; // JS stamp index range
            public readonly double[] MinX, MaxX, MinZ, MaxZ, HwMin, HwMax;

            public Segments(List<Edge> edges, Target g)
            {
                int n = 0;
                foreach (Edge e in edges) n += Math.Max(0, e.Pts.Count - 1);
                Count = n;
                Ax = new double[n]; Az = new double[n]; Ha = new double[n];
                Vx = new double[n]; Vz = new double[n]; Len2 = new double[n]; Dh = new double[n];
                X0 = new int[n]; X1 = new int[n]; Z0 = new int[n]; Z1 = new int[n];
                MinX = new double[n]; MaxX = new double[n]; MinZ = new double[n]; MaxZ = new double[n];
                HwMin = new double[n]; HwMax = new double[n];
                int s = 0;
                foreach (Edge e in edges)
                {
                    for (int k = 0; k < e.Pts.Count - 1; k++, s++)
                    {
                        double ax = e.Pts[k][0], az = e.Pts[k][1], bx = e.Pts[k + 1][0], bz = e.Pts[k + 1][1];
                        double ha = e.Hws[k], hb = e.Hws[k + 1];
                        Ax[s] = ax;
                        Az[s] = az;
                        Ha[s] = ha;
                        Vx[s] = bx - ax;
                        Vz[s] = bz - az;
                        Len2[s] = Vx[s] * Vx[s] + Vz[s] * Vz[s];
                        Dh[s] = hb - ha;
                        double R = JsMath.Max(ha, hb) + g.Band;
                        X0[s] = Lo((JsMath.Min(ax, bx) - R - g.MinX) / g.Cell);
                        X1[s] = Hi((JsMath.Max(ax, bx) + R - g.MinX) / g.Cell, g.Nx);
                        Z0[s] = Lo((JsMath.Min(az, bz) - R - g.MinZ) / g.Cell);
                        Z1[s] = Hi((JsMath.Max(az, bz) + R - g.MinZ) / g.Cell, g.Nz);
                        MinX[s] = Math.Min(ax, bx);
                        MaxX[s] = Math.Max(ax, bx);
                        MinZ[s] = Math.Min(az, bz);
                        MaxZ[s] = Math.Max(az, bz);
                        HwMin[s] = Math.Min(ha, hb);
                        HwMax[s] = Math.Max(ha, hb);
                    }
                }
            }

            /// <summary>Distance from a point to segment s (for bounds only).</summary>
            public double PointDist(double px, double pz, int s)
            {
                double wx = px - Ax[s];
                double wz = pz - Az[s];
                double t = Len2[s] > 0 ? JsMath.Clamp((wx * Vx[s] + wz * Vz[s]) / Len2[s], 0, 1) : 0;
                double dx = wx - Vx[s] * t;
                double dz = wz - Vz[s] * t;
                return Math.Sqrt(dx * dx + dz * dz);
            }

            /// <summary>Lower bound of the distance between segment s and any point of the box.</summary>
            public double BoxDist(int s, double x0, double x1, double z0, double z1)
            {
                double dx = Math.Max(0, Math.Max(MinX[s] - x1, x0 - MaxX[s]));
                double dz = Math.Max(0, Math.Max(MinZ[s] - z1, z0 - MaxZ[s]));
                return Math.Sqrt(dx * dx + dz * dz);
            }

            public bool Covers(int s, int ix0, int ix1, int iz0, int iz1)
            {
                return X0[s] <= ix0 && X1[s] >= ix1 && Z0[s] <= iz0 && Z1[s] >= iz1;
            }

            public bool Touches(int s, int ix0, int ix1, int iz0, int iz1)
            {
                return X0[s] <= ix1 && X1[s] >= ix0 && Z0[s] <= iz1 && Z1[s] >= iz0;
            }
        }

        // ---- segments -----------------------------------------------------------------------------

        /// <summary>Stamps every centreline segment (pathfield.js `stamp` for each edge, in order).</summary>
        internal static void StampSegments(List<Edge> edges, Target g)
        {
            if (UseReference)
            {
                foreach (Edge e in edges)
                {
                    for (int k = 0; k < e.Pts.Count - 1; k++)
                        ReferenceSegment(g, e.Pts[k][0], e.Pts[k][1], e.Pts[k + 1][0], e.Pts[k + 1][1], e.Hws[k], e.Hws[k + 1], g.Band);
                }
                return;
            }
            var segs = new Segments(edges, g);
            if (segs.Count == 0) return;
            var all = new int[segs.Count];
            for (int s = 0; s < all.Length; s++) all[s] = s;
            var superCand = new int[segs.Count];
            var tileCand = new int[segs.Count];
            for (int sz = 0; sz < g.Nz; sz += SuperTile)
            {
                int ez = Math.Min(sz + SuperTile, g.Nz) - 1;
                for (int sx = 0; sx < g.Nx; sx += SuperTile)
                {
                    int ex = Math.Min(sx + SuperTile, g.Nx) - 1;
                    int nSuper = Select(segs, g, all, all.Length, sx, ex, sz, ez, superCand);
                    if (nSuper == 0) continue;
                    for (int tz = sz; tz <= ez; tz += Tile)
                    {
                        int tez = Math.Min(tz + Tile - 1, ez);
                        for (int tx = sx; tx <= ex; tx += Tile)
                        {
                            int tex = Math.Min(tx + Tile - 1, ex);
                            int nTile = Select(segs, g, superCand, nSuper, tx, tex, tz, tez, tileCand);
                            if (nTile > 0) StampTile(segs, g, tileCand, nTile, tx, tex, tz, tez);
                        }
                    }
                }
            }
        }

        /// <summary>
        /// The subset of <paramref name="src"/> (kept in order) that can affect any cell of the box
        /// [ix0..ix1] × [iz0..iz1]: segments that come within reach of the nearest one (c and hw), or
        /// whose v = dist - hw can reach the smallest v (d).
        /// </summary>
        private static int Select(Segments segs, Target g, int[] src, int nsrc, int ix0, int ix1, int iz0, int iz1, int[] dst)
        {
            double x0 = g.MinX + ix0 * g.Cell;
            double x1 = g.MinX + ix1 * g.Cell;
            double z0 = g.MinZ + iz0 * g.Cell;
            double z1 = g.MinZ + iz1 * g.Cell;
            double cx = 0.5 * (x0 + x1);
            double cz = 0.5 * (z0 + z1);
            double halfDiag = 0.5 * Math.Sqrt((x1 - x0) * (x1 - x0) + (z1 - z0) * (z1 - z0));
            // Upper bounds, valid for every cell of the box, from segments that stamp the whole box:
            // the nearest-centreline distance (U) and the smallest v = dist - hw (V).
            double U = double.PositiveInfinity;
            double V = double.PositiveInfinity;
            for (int i = 0; i < nsrc; i++)
            {
                int s = src[i];
                if (!segs.Covers(s, ix0, ix1, iz0, iz1)) continue;
                double far = segs.PointDist(cx, cz, s) + halfDiag;
                if (far < U) U = far;
                double v = far - segs.HwMin[s];
                if (v < V) V = v;
            }
            double uLimit = U * (1 + 1e-6) + Margin;
            double vLimit = V + Margin;
            int n = 0;
            for (int i = 0; i < nsrc; i++)
            {
                int s = src[i];
                if (!segs.Touches(s, ix0, ix1, iz0, iz1)) continue;
                double near = segs.BoxDist(s, x0, x1, z0, z1);
                if (near <= uLimit || near - segs.HwMax[s] - Margin <= vLimit) dst[n++] = s;
            }
            return n;
        }

        /// <summary>The JS stamp, per cell, over the candidate segments in their original order.</summary>
        private static void StampTile(Segments segs, Target g, int[] cand, int ncand, int ix0, int ix1, int iz0, int iz1)
        {
            // locals: Mono's JIT does not hoist field loads out of the loops
            float[] d = g.D, c = g.C, hwAt = g.Hw;
            int[] sx0 = segs.X0, sx1 = segs.X1, sz0 = segs.Z0, sz1 = segs.Z1;
            double[] sAx = segs.Ax, sAz = segs.Az, sHa = segs.Ha, sVx = segs.Vx, sVz = segs.Vz, sLen2 = segs.Len2, sDh = segs.Dh;
            double minX = g.MinX, minZ = g.MinZ, cell = g.Cell;
            int nx = g.Nx;
            for (int iz = iz0; iz <= iz1; iz++)
            {
                double pz = minZ + iz * cell;
                for (int ix = ix0; ix <= ix1; ix++)
                {
                    double px = minX + ix * cell;
                    int idx = iz * nx + ix;
                    float cv = c[idx];
                    float dv = d[idx];
                    float hv = hwAt[idx];
                    for (int k = 0; k < ncand; k++)
                    {
                        int s = cand[k];
                        if (ix < sx0[s] || ix > sx1[s] || iz < sz0[s] || iz > sz1[s]) continue;
                        // segmentDistance(px, pz, ax, az, bx, bz) exactly as in the reference
                        double vx = sVx[s];
                        double vz = sVz[s];
                        double len2 = sLen2[s];
                        double wx = px - sAx[s];
                        double wz = pz - sAz[s];
                        double t = 0;
                        if (len2 > 0)
                        {
                            t = (wx * vx + wz * vz) / len2;
                            t = t < 0 ? 0 : t > 1 ? 1 : t; // JsMath.Clamp(t, 0, 1)
                        }
                        double ddx = wx - vx * t;
                        double ddz = wz - vz * t;
                        double dist = Math.Sqrt(ddx * ddx + ddz * ddz);
                        double hw = sHa[s] + sDh[s] * t;
                        if (dist < cv)
                        {
                            cv = (float)dist;
                            hv = (float)hw;
                        }
                        double v = dist - hw;
                        if (v < dv) dv = (float)v;
                    }
                    c[idx] = cv;
                    d[idx] = dv;
                    hwAt[idx] = hv;
                }
            }
        }

        /// <summary>Reference: pathfield.js `stamp(ax, az, bx, bz, ha, hb, reach)`, a direct port.</summary>
        private static void ReferenceSegment(Target g, double ax, double az, double bx, double bz, double ha, double hb, double reach)
        {
            float[] d = g.D, c = g.C, hwAt = g.Hw;
            double R = JsMath.Max(ha, hb) + reach;
            int x0 = Lo((JsMath.Min(ax, bx) - R - g.MinX) / g.Cell);
            int x1 = Hi((JsMath.Max(ax, bx) + R - g.MinX) / g.Cell, g.Nx);
            int z0 = Lo((JsMath.Min(az, bz) - R - g.MinZ) / g.Cell);
            int z1 = Hi((JsMath.Max(az, bz) + R - g.MinZ) / g.Cell, g.Nz);
            for (int iz = z0; iz <= z1; iz++)
            {
                double pz = g.MinZ + iz * g.Cell;
                int idx = iz * g.Nx + x0;
                for (int ix = x0; ix <= x1; ix++, idx++)
                {
                    double px = g.MinX + ix * g.Cell;
                    // segmentDistance(px, pz, ax, az, bx, bz, seg)
                    double vx = bx - ax;
                    double vz = bz - az;
                    double wx = px - ax;
                    double wz = pz - az;
                    double len2 = vx * vx + vz * vz;
                    double t = len2 > 0 ? JsMath.Clamp((wx * vx + wz * vz) / len2, 0, 1) : 0;
                    double ddx = wx - vx * t;
                    double ddz = wz - vz * t;
                    double dist = Math.Sqrt(ddx * ddx + ddz * ddz);
                    double hw = ha + (hb - ha) * t;
                    if (dist < c[idx])
                    {
                        c[idx] = (float)dist;
                        hwAt[idx] = (float)hw;
                    }
                    double v = dist - hw;
                    if (v < d[idx]) d[idx] = (float)v;
                }
            }
        }

        // ---- chambers -----------------------------------------------------------------------------

        /// <summary>
        /// Chambers at junctions and inner dead ends: irregular, organic outlines
        /// (pathfield.js, the node loop of _buildGrids). Runs after the segments, nodes in order.
        /// </summary>
        internal static void StampChambers(List<Node> nodes, Noise noise, double chamberNoise, Target g)
        {
            foreach (Node n in nodes)
            {
                if (!(n.Clear > 0) || n.Degree == 2) continue;
                double R = n.Clear * (1 + chamberNoise) + g.Band;
                int x0 = Lo((n.X - R - g.MinX) / g.Cell);
                int x1 = Hi((n.X + R - g.MinX) / g.Cell, g.Nx);
                int z0 = Lo((n.Z - R - g.MinZ) / g.Cell);
                int z1 = Hi((n.Z + R - g.MinZ) / g.Cell, g.Nz);
                if (UseReference)
                {
                    ChamberCells(n, noise, chamberNoise, g, x0, x1, z0, z1, double.PositiveInfinity);
                    continue;
                }
                // The lobe radius rr = clear·(1 + chamberNoise·noise2) never exceeds rrMax.
                double rrMax = n.Clear * (1 + Math.Abs(chamberNoise) * NoiseBound);
                for (int tz = z0; tz <= z1; tz += Tile)
                {
                    int tez = Math.Min(tz + Tile - 1, z1);
                    for (int tx = x0; tx <= x1; tx += Tile)
                    {
                        int tex = Math.Min(tx + Tile - 1, x1);
                        // largest c and d in the tile: below them nothing can change
                        double maxC = double.NegativeInfinity;
                        double maxD = double.NegativeInfinity;
                        bool nan = false;
                        for (int iz = tz; iz <= tez; iz++)
                        {
                            for (int ix = tx; ix <= tex; ix++)
                            {
                                int idx = iz * g.Nx + ix;
                                double cv = g.C[idx];
                                double dv = g.D[idx];
                                if (double.IsNaN(cv) || double.IsNaN(dv)) nan = true;
                                if (cv > maxC) maxC = cv;
                                if (dv > maxD) maxD = dv;
                            }
                        }
                        double bx0 = g.MinX + tx * g.Cell, bx1 = g.MinX + tex * g.Cell;
                        double bz0 = g.MinZ + tz * g.Cell, bz1 = g.MinZ + tez * g.Cell;
                        double ddx = Math.Max(0, Math.Max(bx0 - n.X, n.X - bx1));
                        double ddz = Math.Max(0, Math.Max(bz0 - n.Z, n.Z - bz1));
                        double near = Math.Sqrt(ddx * ddx + ddz * ddz);
                        if (!nan && near >= maxC + Margin && near - rrMax >= maxD + Margin) continue;
                        ChamberCells(n, noise, chamberNoise, g, tx, tex, tz, tez, rrMax);
                    }
                }
            }
        }

        /// <summary>
        /// The JS chamber loop body over a block of cells. The noise lookup is skipped for cells where
        /// d cannot drop (v ≥ dist - rrMax ≥ d); rrMax = +∞ never skips (reference).
        /// </summary>
        private static void ChamberCells(Node n, Noise noise, double chamberNoise, Target g, int x0, int x1, int z0, int z1, double rrMax)
        {
            float[] d = g.D, c = g.C;
            for (int iz = z0; iz <= z1; iz++)
            {
                double pz = g.MinZ + iz * g.Cell;
                for (int ix = x0; ix <= x1; ix++)
                {
                    double px = g.MinX + ix * g.Cell;
                    double dx = px - n.X;
                    double dz = pz - n.Z;
                    double dist = JsMath.Hypot(dx, dz);
                    int idx = iz * g.Nx + ix;
                    if (!(dist - rrMax >= d[idx] + Margin))
                    {
                        double a = JsMath.Atan2(dz, dx);
                        // low angular frequency → rounded, cave-like lobes rather than spikes
                        double rr = n.Clear * (1 + chamberNoise * noise.Noise2(JsMath.Cos(a) * 0.55 + n.X * 0.13, JsMath.Sin(a) * 0.55 + n.Z * 0.13));
                        double v = dist - rr;
                        if (v < d[idx]) d[idx] = (float)v;
                    }
                    if (dist < c[idx]) c[idx] = (float)dist;
                }
            }
        }
    }
}

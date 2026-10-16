// Ant Garden core — port of HexFrame from src/pathfield.js.
using System;
using System.Collections.Generic;

namespace AntGarden.Core
{
    /// <summary>One side of a module hexagon (world units).</summary>
    public sealed class FrameEdge
    {
        /// <summary>First vertex.</summary>
        public double X0, Z0;
        /// <summary>Second vertex.</summary>
        public double X1, Z1;
        /// <summary>Centre of the hexagon this side belongs to.</summary>
        public double Cx, Cz;
        /// <summary>True when a neighbouring module shares this side (an interior seam, not a wall).</summary>
        public bool Shared;
    }

    /// <summary>
    /// Outline of one or more pointy-top hexagons that share edges (a stitched world).
    /// <see cref="Dist"/> is the signed distance to the outer rim — negative inside — so the seams
    /// between neighbouring hexagons are interior floor, not walls.
    /// </summary>
    /// <remarks>Immutable after construction: safe to query from several threads.</remarks>
    public sealed class HexFrame
    {
        private static readonly double SQRT3 = Math.Sqrt(3);

        /// <summary>Hexagon centres.</summary>
        public List<(double x, double z)> Centers;
        /// <summary>Circumradius (centre to vertex).</summary>
        public double R, Apothem;
        /// <summary>All six sides of every hexagon, in centre order; <see cref="Rim"/> = the unshared ones.</summary>
        public List<FrameEdge> Edges, Rim;

        /// <param name="centers">hexagon centres</param>
        /// <param name="r">circumradius (centre to vertex)</param>
        public HexFrame(IList<(double x, double z)> centers, double r)
        {
            Centers = new List<(double x, double z)>(centers);
            R = r;
            Apothem = (r * SQRT3) / 2;
            Edges = new List<FrameEdge>(Centers.Count * 6);
            for (int ci = 0; ci < Centers.Count; ci++)
            {
                var c = Centers[ci];
                for (int k = 0; k < 6; k++)
                {
                    double a0 = Math.PI / 2 + (k * Math.PI) / 3;
                    double a1 = a0 + Math.PI / 3;
                    double am = a0 + Math.PI / 6; // edge midpoint direction
                    double nx = c.x + JsMath.Cos(am) * 2 * Apothem;
                    double nz = c.z + JsMath.Sin(am) * 2 * Apothem;
                    bool shared = false;
                    for (int oi = 0; oi < Centers.Count; oi++)
                    {
                        if (oi != ci && JsMath.Hypot(Centers[oi].x - nx, Centers[oi].z - nz) < 1)
                        {
                            shared = true;
                            break;
                        }
                    }
                    Edges.Add(new FrameEdge
                    {
                        X0 = c.x + r * JsMath.Cos(a0),
                        Z0 = c.z + r * JsMath.Sin(a0),
                        X1 = c.x + r * JsMath.Cos(a1),
                        Z1 = c.z + r * JsMath.Sin(a1),
                        Cx = c.x,
                        Cz = c.z,
                        Shared = shared,
                    });
                }
            }
            Rim = new List<FrameEdge>();
            foreach (var e in Edges)
            {
                if (!e.Shared) Rim.Add(e);
            }
        }

        /// <summary>Signed distance to the outer rim (negative inside the union of hexagons).</summary>
        public double Dist(double x, double z)
        {
            double best = double.PositiveInfinity;
            for (int i = 0; i < Centers.Count; i++)
            {
                double ax = Math.Abs(x - Centers[i].x);
                double az = Math.Abs(z - Centers[i].z);
                double d = JsMath.Max(ax, ax * 0.5 + az * 0.8660254) - Apothem;
                if (d < best) best = d;
            }
            if (Centers.Count == 1) return best;
            double m = double.PositiveInfinity;
            for (int i = 0; i < Rim.Count; i++)
            {
                FrameEdge e = Rim[i];
                double ex = e.X1 - e.X0;
                double ez = e.Z1 - e.Z0;
                double t = JsMath.Clamp(((x - e.X0) * ex + (z - e.Z0) * ez) / (ex * ex + ez * ez), 0, 1);
                double d = JsMath.Hypot(x - e.X0 - ex * t, z - e.Z0 - ez * t);
                if (d < m) m = d;
            }
            return best < 1e-6 ? -m : m;
        }
    }
}

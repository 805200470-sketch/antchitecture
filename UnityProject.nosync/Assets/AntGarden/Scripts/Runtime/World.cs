using System;
using System.Collections.Generic;
using AntGarden.Core;
using UnityEngine;

namespace AntGarden
{
    /// <summary>A circular footprint the ants cannot walk through (props on the canyon floor).</summary>
    public struct Circle
    {
        public float x, z, r, h;
        public Circle(float x, float z, float r, float h) { this.x = x; this.z = z; this.r = r; this.h = h; }
    }

    /// <summary>Uniform grid of circles for fast neighbourhood queries (hundreds of props, dozens of ants).</summary>
    public class CircleGrid
    {
        readonly float cell;
        readonly Dictionary<long, List<Circle>> cells = new Dictionary<long, List<Circle>>();
        readonly List<Circle> scratch = new List<Circle>();
        public int Count { get; private set; }

        public CircleGrid(float cell = 4f) { this.cell = cell; }

        static long Key(int i, int j) => ((long)i << 32) ^ (uint)j;

        public void Add(Circle c)
        {
            int i0 = Mathf.FloorToInt((c.x - c.r) / cell), i1 = Mathf.FloorToInt((c.x + c.r) / cell);
            int j0 = Mathf.FloorToInt((c.z - c.r) / cell), j1 = Mathf.FloorToInt((c.z + c.r) / cell);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                if (!cells.TryGetValue(Key(i, j), out var list)) cells[Key(i, j)] = list = new List<Circle>();
                list.Add(c);
            }
            Count++;
        }

        /// <summary>Circles whose cells touch the given point's cell (callers test the exact distance).</summary>
        public List<Circle> Near(float x, float z)
        {
            scratch.Clear();
            int i = Mathf.FloorToInt(x / cell), j = Mathf.FloorToInt(z / cell);
            if (cells.TryGetValue(Key(i, j), out var list)) scratch.AddRange(list);
            return scratch;
        }

        /// <summary>True when a disc of radius r at (x, z) overlaps any stored circle.</summary>
        public bool Overlaps(float x, float z, float r)
        {
            int i0 = Mathf.FloorToInt((x - r) / cell), i1 = Mathf.FloorToInt((x + r) / cell);
            int j0 = Mathf.FloorToInt((z - r) / cell), j1 = Mathf.FloorToInt((z + r) / cell);
            for (int j = j0; j <= j1; j++)
            for (int i = i0; i <= i1; i++)
            {
                if (!cells.TryGetValue(Key(i, j), out var list)) continue;
                foreach (var c in list)
                {
                    float dx = c.x - x, dz = c.z - z, m = c.r + r;
                    if (dx * dx + dz * dz < m * m) return true;
                }
            }
            return false;
        }
    }

    /// <summary>Interaction point along the paths where user objects will be placed later.</summary>
    public class StorySlot
    {
        public string id;
        public Vector3 position;
        public GameObject marker;
    }

    public class WNode { public string id, type; public float x, z, hw, clear; public int degree, region; public bool inside; }
    public class WEdge { public string id, source, target; public List<Vector2> pts; public List<float> hws; public float hw; public bool connector; public int region; }
    public class WExit { public float x, z, dirX, dirZ; public int region; }
    public class WFrameEdge { public float x0, z0, x1, z1, cx, cz; public bool shared; public int region; }
    public class WModule { public string id; public float x, z; public int region; }

    /// <summary>
    /// One separately generated area of the world: a single nest or a stitched group of nests, placed at
    /// an offset, with its own look (biome). Regions are not connected on foot; the player jumps between
    /// them through portal gates or the map.
    /// </summary>
    public class Region
    {
        public int index;
        public PathField field;
        public Vector2 offset;      // Unity-space translation of the region's core origin
        public Biome biome;
        public string[] ids;
        public string label;
        public int connectors, paths;      // for the label in either language (stitched / single nest)
        public string scene;               // a ruin scene built into this region ("relics", "temple") or null
        public float minX, maxX, minZ, maxZ;
        // honeycomb cells: one hexagon of a stitched field shared with its neighbours (see World.AddCells)
        public bool cell;
        public float cellX, cellZ, cellR;  // centre (Unity space) and circumradius of the hexagon
        public int q, r;                   // axial position in the honeycomb (pointy-top)

        /// <summary>Signed distance to this cell's hexagon outline (negative inside).</summary>
        public float CellDist(float x, float z)
        {
            float dx = x - cellX, dz = z - cellZ, apothem = cellR * 0.8660254f;
            float d0 = Mathf.Abs(dx), d1 = Mathf.Abs(dx * 0.5f + dz * 0.8660254f), d2 = Mathf.Abs(-dx * 0.5f + dz * 0.8660254f);
            return Mathf.Max(d0, Mathf.Max(d1, d2)) - apothem;
        }
    }

    /// <summary>
    /// The generated world in Unity space. The core (a bit-exact port of the browser prototype) works in
    /// image space: x right, z = image down. Unity is left-handed, so using those numbers directly would
    /// mirror the nest. Everything crosses this boundary with z negated (and the region's offset added),
    /// which keeps the 3D canyon and the map in the same orientation as the source photo.
    /// On top of the core heights the world adds gentle relief, so even the canyon floor rolls.
    /// </summary>
    /// <summary>One placed plant or prop and how it is coloured, for the particle look (ParticleLook).</summary>
    public struct PlantInstance
    {
        public PlantRef plant;
        public Transform t;
        public float hue, sat;     // the region's hue rotation and saturation (as the Plant shader applies them)
        public Color glow;         // the region's glow colour
        public Color scan;         // the pale colour its points take in the particle look (the region's own)
        public bool far;           // one of the huge trunks beyond the frame (the particle look gives it few points)
        public bool relic;         // a thing, not a plant: the particle look keeps its own colours (no tint by kind)
    }

    /// <summary>Where one plant was placed (bottom of a stack), for debug views and later lookups.</summary>
    public struct Placement
    {
        public string plant;
        public int region;
        public float x, z, height;
    }

    public class World
    {
        public readonly List<Region> regions = new List<Region>();
        public readonly CircleGrid colliders = new CircleGrid();
        public readonly CircleGrid reserved = new CircleGrid();   // ground kept free of plants (ruin props)
        public readonly List<PlantInstance> plantInstances = new List<PlantInstance>();   // for the particle look
        public readonly CircleGrid steps = new CircleGrid();      // low things ants walk over (h = height of the top)
        public float tunnelClimb = 3.2f;     // how far up the banks an ant may climb under a tunnel roof
        public readonly List<TunnelRoof> roofs = new List<TunnelRoof>();
        public GlowPlants glow;              // glowing plants: their own brightness, touch flares
        public ColonyTrails trails;          // pheromone on the path graph (ant-colony algorithm)
        public readonly List<Placement> placements = new List<Placement>();
        public readonly List<StorySlot> slots = new List<StorySlot>();
        public readonly List<WNode> nodes = new List<WNode>();
        public readonly List<WEdge> edges = new List<WEdge>();
        public readonly List<WExit> exits = new List<WExit>();
        public readonly List<WFrameEdge> frameEdges = new List<WFrameEdge>();
        public readonly List<WModule> modules = new List<WModule>();
        public float minX = float.MaxValue, maxX = float.MinValue, minZ = float.MaxValue, maxZ = float.MinValue;
        public readonly float bankWidth;
        public string label;
        readonly Noise noise;
        readonly ReliefSettings relief;

        public World(ReliefSettings relief, int seed)
        {
            this.relief = relief;
            noise = new Noise(seed);
            bankWidth = (float)PathConfig.Default().GroundBankWidth;
        }

        public int perRow = 4;               // regions side by side before the next row starts, to the south
        int inRow;
        float rowMaxX, rowMinZ, rowMaxZ;

        /// <summary>
        /// Add a region: after the first, each goes to the east of the row, clear of the others, and every
        /// perRow regions a new row starts to the south (so a world of many nests reads as an archipelago, not a strip).
        /// </summary>
        public Region AddRegion(PathField f, string[] ids, string label, Biome biome, float gap)
        {
            var r = new Region { index = regions.Count, field = f, ids = ids, label = label, biome = biome };
            float w = (float)(f.Bounds.MaxX - f.Bounds.MinX);
            float coreCentreZ = (float)-(f.Bounds.MinZ + f.Bounds.MaxZ) / 2;
            if (regions.Count == 0) r.offset = Vector2.zero;
            else if (inRow < perRow)
            {
                // core x range [MinX, MaxX] → place its left edge `gap` beyond the row's right edge
                r.offset = new Vector2(rowMaxX + gap - (float)f.Bounds.MinX, (rowMinZ + rowMaxZ) / 2 - coreCentreZ);
            }
            else
            {
                // a new row: its top edge `gap` south of everything so far, starting at the west edge
                r.offset = new Vector2(minX - (float)f.Bounds.MinX, minZ - gap + (float)f.Bounds.MinZ);
                inRow = 0;
                rowMaxX = rowMaxZ = float.MinValue;
                rowMinZ = float.MaxValue;
            }
            r.minX = (float)f.Bounds.MinX + r.offset.x;
            r.maxX = r.minX + w;
            r.minZ = (float)-f.Bounds.MaxZ + r.offset.y;
            r.maxZ = (float)-f.Bounds.MinZ + r.offset.y;
            minX = Mathf.Min(minX, r.minX); maxX = Mathf.Max(maxX, r.maxX);
            minZ = Mathf.Min(minZ, r.minZ); maxZ = Mathf.Max(maxZ, r.maxZ);
            if (inRow == 0) { rowMaxX = r.maxX; rowMinZ = r.minZ; rowMaxZ = r.maxZ; }
            else { rowMaxX = Mathf.Max(rowMaxX, r.maxX); rowMinZ = Mathf.Min(rowMinZ, r.minZ); rowMaxZ = Mathf.Max(rowMaxZ, r.maxZ); }
            inRow++;

            string p = $"r{r.index}:";
            float ox = r.offset.x, oz = r.offset.y;
            foreach (Node n in f.Nodes)
                nodes.Add(new WNode { id = p + n.Id, type = n.Type, x = (float)n.X + ox, z = (float)-n.Z + oz, hw = (float)n.Hw, clear = (float)n.Clear, degree = n.Degree, inside = n.Inside, region = r.index });
            foreach (Edge e in f.Edges)
            {
                var pts = new List<Vector2>(e.Pts.Count);
                foreach (double[] q in e.Pts) pts.Add(new Vector2((float)q[0] + ox, (float)-q[1] + oz));
                var hws = new List<float>(e.Hws.Count);
                foreach (double h in e.Hws) hws.Add((float)h);
                edges.Add(new WEdge { id = p + e.Id, source = p + e.Source, target = p + e.Target, pts = pts, hws = hws, hw = (float)e.Hw, connector = e.Connector, region = r.index });
            }
            foreach (Exit x in f.Exits) exits.Add(new WExit { x = (float)x.X + ox, z = (float)-x.Z + oz, dirX = (float)x.DirX, dirZ = (float)-x.DirZ, region = r.index });
            foreach (FrameEdge fe in f.FrameEdges)
                frameEdges.Add(new WFrameEdge { x0 = (float)fe.X0 + ox, z0 = (float)-fe.Z0 + oz, x1 = (float)fe.X1 + ox, z1 = (float)-fe.Z1 + oz, cx = (float)fe.Cx + ox, cz = (float)-fe.Cz + oz, shared = fe.Shared, region = r.index });
            if (f.Modules != null && f.Modules.Count > 0)
                foreach (ModuleInfo m in f.Modules) modules.Add(new WModule { id = m.Id, x = (float)m.X + ox, z = (float)-m.Z + oz, region = r.index });
            else
            {
                var b = f.Bounds;
                modules.Add(new WModule { id = ids[0], x = (float)(b.MinX + b.MaxX) / 2 + ox, z = (float)-(b.MinZ + b.MaxZ) / 2 + oz, region = r.index });
            }
            regions.Add(r);
            return r;
        }

        /// <summary>The region a point belongs to: the one whose frame is nearest (inside wins).</summary>
        readonly Dictionary<long, Region> cellMap = new Dictionary<long, Region>();
        Region cellOrigin;   // the honeycomb's first cell (axial 0, 0)

        /// <summary>
        /// A honeycomb of nests: one stitched field, one region per hexagon module (each with its own biome, ruin
        /// scene and colony) all sharing the field, so ground, canyons and walls run on across the seams. The
        /// field's nodes, edges, exits and frame edges each go to the cell they lie in.
        /// </summary>
        public List<Region> AddCells(PathField f, IList<(string id, Biome biome, string scene)> cells)
        {
            var added = new List<Region>();
            float R = (float)f.R, apothem = R * 0.8660254f;
            ModuleInfo m0 = f.Modules[0];
            for (int i = 0; i < cells.Count; i++)
            {
                ModuleInfo m = f.Modules[i];
                var r = new Region { index = regions.Count, field = f, ids = new[] { cells[i].id }, label = "", biome = cells[i].biome, scene = cells[i].scene, offset = Vector2.zero, cell = true };
                r.cellX = (float)m.X;
                r.cellZ = (float)-m.Z;
                r.cellR = R;
                // axial position from the centre's offset to the first cell (pointy-top: rows 1.5 R apart)
                double dz = m.Z - m0.Z, dx = m.X - m0.X;
                r.r = (int)Math.Round(dz / (1.5 * R));
                r.q = (int)Math.Round(dx / (2 * apothem) - r.r / 2.0);
                r.minX = r.cellX - apothem; r.maxX = r.cellX + apothem;
                r.minZ = r.cellZ - R; r.maxZ = r.cellZ + R;
                minX = Mathf.Min(minX, r.minX); maxX = Mathf.Max(maxX, r.maxX);
                minZ = Mathf.Min(minZ, r.minZ); maxZ = Mathf.Max(maxZ, r.maxZ);
                cellMap[Key(r.q, r.r)] = r;
                if (i == 0) cellOrigin = r;
                regions.Add(r);
                added.Add(r);
            }
            string p = "r" + added[0].index + ":";
            Region At(double x, double z) => RegionAt(x, z);
            foreach (Node n in f.Nodes)
                nodes.Add(new WNode { id = p + n.Id, type = n.Type, x = (float)n.X, z = (float)-n.Z, hw = (float)n.Hw, clear = (float)n.Clear, degree = n.Degree, inside = n.Inside, region = At(n.X, -n.Z).index });
            foreach (Edge e in f.Edges)
            {
                var pts = new List<Vector2>(e.Pts.Count);
                foreach (double[] q in e.Pts) pts.Add(new Vector2((float)q[0], (float)-q[1]));
                var hws = new List<float>(e.Hws.Count);
                foreach (double h in e.Hws) hws.Add((float)h);
                Vector2 mid = pts[pts.Count / 2];
                edges.Add(new WEdge { id = p + e.Id, source = p + e.Source, target = p + e.Target, pts = pts, hws = hws, hw = (float)e.Hw, connector = e.Connector, region = At(mid.x, mid.y).index });
            }
            foreach (Exit x in f.Exits) exits.Add(new WExit { x = (float)x.X, z = (float)-x.Z, dirX = (float)x.DirX, dirZ = (float)-x.DirZ, region = At(x.X, -x.Z).index });
            foreach (FrameEdge fe in f.FrameEdges)
                frameEdges.Add(new WFrameEdge { x0 = (float)fe.X0, z0 = (float)-fe.Z0, x1 = (float)fe.X1, z1 = (float)-fe.Z1, cx = (float)fe.Cx, cz = (float)-fe.Cz, shared = fe.Shared, region = At(fe.Cx, -fe.Cz).index });
            foreach (ModuleInfo m in f.Modules) modules.Add(new WModule { id = m.Id, x = (float)m.X, z = (float)-m.Z, region = At(m.X, -m.Z).index });
            return added;
        }

        static long Key(int q, int r) => ((long)q << 32) ^ (uint)r;

        public Region RegionAt(double x, double z)
        {
            if (regions.Count == 1) return regions[0];
            if (cellOrigin != null)
            {
                // a honeycomb cell straight from the position (pointy-top axial coordinates, cube rounding)
                Region o = cellOrigin;
                double px = x - o.cellX, pz = -(z - o.cellZ);   // the field's axes (z down)
                double fq = (Math.Sqrt(3) / 3 * px - pz / 3) / o.cellR, fr = 2.0 / 3 * pz / o.cellR;
                double fs = -fq - fr;
                double rq = Math.Round(fq), rr = Math.Round(fr), rs = Math.Round(fs);
                double eq = Math.Abs(rq - fq), er = Math.Abs(rr - fr), es = Math.Abs(rs - fs);
                if (eq > er && eq > es) rq = -rr - rs;
                else if (er > es) rr = -rq - rs;
                if (cellMap.TryGetValue(Key((int)rq, (int)rr), out Region hit)) return hit;
                // just outside the honeycomb: the nearest cell
                Region near = null;
                float nd = float.MaxValue;
                foreach (Region c in regions)
                {
                    if (!c.cell) continue;
                    float d = c.CellDist((float)x, (float)z);
                    if (d < nd) { nd = d; near = c; }
                }
                if (near != null) return near;
            }
            Region best = null;
            double bd = double.MaxValue;
            foreach (Region r in regions)
            {
                // cheap reject: far outside this region's box
                double ax = Math.Max(0, Math.Max(r.minX - x, x - r.maxX)), az = Math.Max(0, Math.Max(r.minZ - z, z - r.maxZ));
                if (ax + az > bd) continue;
                double d = r.field.HexDist(x - r.offset.x, -(z - r.offset.y));
                if (d < bd) { bd = d; best = r; }
            }
            return best;
        }

        // ---- queries (Unity space in, Unity space out)
        public double CoreHeightD(Region r, double x, double z) => r.field.Height(x - r.offset.x, -(z - r.offset.y));

        public double HeightD(double x, double z)
        {
            Region r = RegionAt(x, z);
            return Strata(r, x, z, CoreHeightD(r, x, z)) + Relief(r, x, z);
        }

        /// <summary>
        /// Soil layers on the canyon banks: the slope is gathered into soft ledges (flatter treads, steeper
        /// risers), like bands of sediment, so the banks read as built-up earth rather than one smooth ramp.
        /// The heights where the bank starts and ends are unchanged, and the floor (where ants walk) is untouched.
        /// </summary>
        double Strata(Region r, double x, double z, double h)
        {
            if (relief == null || relief.strata <= 0) return h;
            double d = r.field.PathDist(x - r.offset.x, -(z - r.offset.y));
            double mask = Smooth(0.2, 1.3, d) * (1 - Smooth(bankWidth - 0.3, bankWidth + 1.2, d));
            if (mask <= 0) return h;
            double p = relief.strataPeriod;
            double hs = h + noise.Noise2(x * 0.09 + 3.1, z * 0.09 - 5.7) * p * 0.35;   // ledges wander a little
            double k = Math.Floor(hs / p), f = hs / p - k;
            double terrace = (k + Smooth(0.35, 0.65, f)) * p;   // flat treads, short steep risers
            return h + (terrace - hs) * relief.strata * mask;
        }

        static double Smooth(double a, double b, double v)
        {
            double t = Math.Max(0, Math.Min(1, (v - a) / (b - a)));
            return t * t * (3 - 2 * t);
        }

        public double PathDistD(double x, double z)
        {
            Region r = RegionAt(x, z);
            return r.field.PathDist(x - r.offset.x, -(z - r.offset.y));
        }

        public double HexDistD(double x, double z)
        {
            double best = double.MaxValue;
            foreach (Region r in regions) best = Math.Min(best, r.field.HexDist(x - r.offset.x, -(z - r.offset.y)));
            return best;
        }

        public float Height(float x, float z) => (float)HeightD(x, z);

        /// <summary>Ceiling height at (x, z) in a tunnel region, +infinity under the open sky.</summary>
        public float Ceiling(float x, float z)
        {
            float best = float.PositiveInfinity;
            foreach (TunnelRoof t in roofs) best = Mathf.Min(best, t.Height(x, z));
            return best;
        }
        /// <summary>The ceiling for the camera: smooth, and never above the real one.</summary>
        public float CameraCeiling(float x, float z)
        {
            float best = float.PositiveInfinity;
            foreach (TunnelRoof t in roofs) best = Mathf.Min(best, t.LowHeight(x, z));
            return best;
        }
        /// <summary>
        /// Where feet stand: the ground, or the top of a low thing lying on it (a pebble, a leaf, the rusted key)
        /// as a soft dome over its footprint, so ants walk over such things instead of through them.
        /// </summary>
        public float WalkHeight(float x, float z)
        {
            float h = Height(x, z), top = 0;
            if (steps.Count > 0)
                foreach (Circle c in steps.Near(x, z))
                {
                    float dx = x - c.x, dz = z - c.z, d2 = (dx * dx + dz * dz) / (c.r * c.r);
                    if (d2 >= 1f) continue;
                    float u = Mathf.Clamp01((1 - Mathf.Sqrt(d2)) / 0.55f);   // a rounded rim, a broad top
                    top = Mathf.Max(top, c.h * u * u * (3 - 2 * u));
                }
            return h + top;
        }

        /// <summary>
        /// Walkable distance for an ant that may climb the banks (negative = it may stand there): the canyon
        /// floor plus a band up the slope, climb units past the canyon edge (less under a tunnel roof, which comes
        /// down over the banks), and never past the hexagon frame. The banks rise to their crest about
        /// bankWidth past the edge, so a climb a little short of that stops the ant just below the top.
        /// </summary>
        public float ClimbDist(float x, float z, float climb)
        {
            Region r = RegionAt(x, z);
            if (r.scene != null) climb = Mathf.Min(climb, tunnelClimb);
            double lx = x - r.offset.x, lz = -(z - r.offset.y);
            return (float)Math.Max(r.field.PathDist(lx, lz) - climb, r.field.HexDist(lx, lz) + r.field.Cfg.FrameWallMargin);
        }

        public Vector2 GradClimb(float x, float z, float climb)
        {
            const float e = 0.08f;
            return new Vector2(ClimbDist(x + e, z, climb) - ClimbDist(x - e, z, climb),
                               ClimbDist(x, z + e, climb) - ClimbDist(x, z - e, climb)) / (2 * e);
        }

        public float PathDist(float x, float z) => (float)PathDistD(x, z);
        public float HexDist(float x, float z) => (float)HexDistD(x, z);

        public float Dist(float x, float z)
        {
            Region r = RegionAt(x, z);
            return (float)r.field.Dist(x - r.offset.x, -(z - r.offset.y));
        }

        public float HalfWidth(float x, float z)
        {
            Region r = RegionAt(x, z);
            return (float)r.field.HalfWidth(x - r.offset.x, -(z - r.offset.y));
        }

        /// <summary>
        /// Rolling relief added to the core height: long swells plus small bumps, gentle on the canyon
        /// floor and rougher on the banks and plateau. Walkability is unchanged (it uses the core distances).
        /// </summary>
        public double Relief(Region r, double x, double z)
        {
            if (relief == null || relief.floor <= 0 && relief.bank <= 0) return 0;
            double d = r.field.PathDist(x - r.offset.x, -(z - r.offset.y));
            double t = Math.Max(0, Math.Min(1, (d + 1) / (bankWidth + 1)));
            double amp = relief.floor + (relief.bank - relief.floor) * t * t * (3 - 2 * t);
            double swell = noise.Fbm2(x * relief.scale, z * relief.scale, 3);
            double mid = noise.Fbm2(x * relief.midScale - 7.3, z * relief.midScale + 4.1, 2) * relief.mid;
            double bumps = noise.Noise2(x * relief.bumpScale + 31.7, z * relief.bumpScale - 12.3) * relief.bumps;
            return amp * swell + mid + bumps;
        }

        /// <summary>Surface normal of the final ground (core heights plus relief).</summary>
        public Vector3 Normal(float x, float z)
        {
            const double e = 0.3;
            double hx = HeightD(x + e, z) - HeightD(x - e, z);
            double hz = HeightD(x, z + e) - HeightD(x, z - e);
            return new Vector3((float)-hx, (float)(2 * e), (float)-hz).normalized;
        }

        /// <summary>Gradient of the walkable distance (points out of the canyon).</summary>
        public Vector2 GradDist(float x, float z)
        {
            Region r = RegionAt(x, z);
            var g = r.field.GradDist(x - r.offset.x, -(z - r.offset.y));
            return new Vector2((float)g.x, (float)-g.z);
        }

        /// <summary>Spawn position and heading (radians, 0 = +Z, increasing towards +X) in a region.</summary>
        public (float x, float z, float heading) SpawnPoint(int region = 0)
        {
            Region r = regions[region];
            var sp = r.field.SpawnPoint();
            return ((float)sp.x + r.offset.x, (float)-sp.z + r.offset.y, (float)(Math.PI - sp.heading));
        }

        /// <summary>Nearest walkable point to (x, z) within a search radius (for teleport snapping).</summary>
        public bool SnapToWalkable(float x, float z, float margin, out Vector2 result)
        {
            result = new Vector2(x, z);
            if (Dist(x, z) < -margin && !colliders.Overlaps(x, z, 0.45f)) return true;
            for (float r = 0.5f; r < 30f; r += 0.5f)
            {
                int steps = Mathf.Max(8, Mathf.CeilToInt(r * 6));
                for (int k = 0; k < steps; k++)
                {
                    float a = k * Mathf.PI * 2 / steps;
                    float px = x + Mathf.Cos(a) * r, pz = z + Mathf.Sin(a) * r;
                    if (Dist(px, pz) < -margin && !colliders.Overlaps(px, pz, 0.45f))
                    {
                        result = new Vector2(px, pz);
                        return true;
                    }
                }
            }
            return false;
        }
    }
}

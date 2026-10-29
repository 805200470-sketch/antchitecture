using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// Glowing specks that show the way through a ruin tunnel, scattered as if by nature: thick in some stretches,
    /// bare in others, a little clump here, a few steps of an ant's track there, and now and then a mote floating
    /// over the path. Every way into the ruin gets a track; the tracks join the main route, which runs down the
    /// main tunnel to the ruin's heart (the amber shrine, the button plaza), where the specks gather round it.
    /// Each speck has its own size, brightness and breathing pace. At rest they are dim; the player's ant brightens
    /// the ones it passes, which stay bright a moment behind it, and the way on ahead glows a touch more
    /// (Shaders/GuideTrail.shader). The tracks' lanes are kept clear of plants: a trodden path.
    /// </summary>
    public static class GuideTrail
    {
        const float Meander = 0.5f;       // how far a track wanders from the middle of the path
        const float Spread = 0.3f;        // how widely the specks scatter across it (a normal spread, clamped)
        static Vector3 lag;               // a point following the player's ant with a delay: the afterglow
        static bool lagSet;

        /// <summary>The planned tracks of one ruin: from every way in to the heart, distances along them to it.</summary>
        public class Route
        {
            public readonly List<(List<Vector2> pts, List<float> d, bool main)> tracks = new List<(List<Vector2>, List<float>, bool)>();
            public List<Vector2> mainTrack;   // the main track's points inside the region, from the way in to the heart
            public List<Vector2> mainFull;    // the same from where it starts, in the neighbouring nest
            public Vector2 goal;
            public float goalRadius;
            public int ways;
            public float noiseSeed;
        }

        static bool Inside(World world, Region region, float x, float z) => world.regions.Count == 1 || world.RegionAt(x, z) == region;

        /// <summary>A track wanders a little: by its distance to the heart, so tracks agree where they meet.</summary>
        static float Wander(float d, float noiseSeed) => (Mathf.PerlinNoise(d * 0.06f, noiseSeed) - 0.5f) * 2f * Meander;

        /// <summary>
        /// Plans the tracks (before the plants grow, so their lanes can be kept clear).
        /// </summary>
        /// <param name="main">the main tunnel; mainForward: it is walked from pts[0] (its far end is the heart's side)</param>
        /// <param name="entry">where the player comes from (the main track starts at the way in nearest to it)</param>
        /// <param name="goal">the heart; the tracks stop goalRadius short of it and a ring of prints goes round it</param>
        public static Route Plan(World world, Region region, WEdge main, bool mainForward, Vector2 entry,
            Vector2 goal, float goalRadius, int seed)
        {
            var route = new Route { goal = goal, goalRadius = goalRadius, noiseSeed = seed * 0.37f % 97f };
            bool In(float x, float z) => Inside(world, region, x, z);

            // the region's own paths, and the connectors that cross into it from its neighbours
            var edges = new List<WEdge>();
            foreach (WEdge e in world.edges)
                foreach (Vector2 p in e.pts)
                    if (In(p.x, p.y)) { edges.Add(e); break; }
            var adj = new Dictionary<string, List<(WEdge e, string to, float w)>>();
            void Link(string a, string b, WEdge e, float w)
            {
                if (!adj.TryGetValue(a, out var list)) adj[a] = list = new List<(WEdge, string, float)>();
                list.Add((e, b, w));
            }
            var pos = new Dictionary<string, Vector2>();
            foreach (WEdge e in edges)
            {
                // the main tunnel is the way: a route takes it whenever it can
                float w = Length(e.pts) * (e == main ? 0.15f : 1f);
                Link(e.source, e.target, e, w);
                Link(e.target, e.source, e, w);
                pos[e.source] = e.pts[0];
                pos[e.target] = e.pts[e.pts.Count - 1];
            }
            string heart = mainForward ? main.target : main.source;

            // shortest ways to the heart (Dijkstra; a cell holds a few dozen nodes)
            var dist = new Dictionary<string, float> { [heart] = 0 };
            var next = new Dictionary<string, (WEdge e, string to)>();
            var open = new List<string> { heart };
            var done = new HashSet<string>();
            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++) if (dist[open[i]] < dist[open[bi]]) bi = i;
                string u = open[bi];
                open.RemoveAt(bi);
                if (!done.Add(u) || !adj.TryGetValue(u, out var list)) continue;
                foreach (var (e, v, w) in list)
                {
                    float d = dist[u] + w;
                    if (dist.TryGetValue(v, out float old) && old <= d) continue;
                    dist[v] = d;
                    next[v] = (e, u);
                    open.Add(v);
                }
            }

            // the ways in: a connector crossing in from a neighbour (its outer end), or a canyon running out
            // through the frame; the main one is the way in nearest where the player comes from
            var ways = new List<string>();
            foreach (WEdge e in edges)
            {
                bool a = In(e.pts[0].x, e.pts[0].y), b = In(e.pts[e.pts.Count - 1].x, e.pts[e.pts.Count - 1].y);
                if (a != b) ways.Add(a ? e.target : e.source);
            }
            if (!region.cell)   // in a honeycomb the canyons running out through the outer rim are dead ends
                foreach (WExit x in world.exits)
                    if (x.region == region.index) ways.Add(Nearest(pos, dist, new Vector2(x.x, x.z)));
            if (ways.Count == 0 || !region.cell) ways.Add(Nearest(pos, dist, entry));
            ways.RemoveAll(w => w == null || !dist.ContainsKey(w));
            route.ways = ways.Count;
            if (ways.Count == 0) return route;
            ways.Sort((p, q) => (pos[p] - entry).sqrMagnitude.CompareTo((pos[q] - entry).sqrMagnitude));

            // trace each way in to the heart; a later track stops where it meets one already drawn
            var tree = new Dictionary<string, float> { [heart] = 0 };   // node → distance along the tracks to the heart
            for (int wi = 0; wi < ways.Count; wi++)
            {
                var chain = new List<(WEdge e, bool fwd, string from)>();
                string n = ways[wi];
                while (!tree.ContainsKey(n) && next.TryGetValue(n, out var step))
                {
                    chain.Add((step.e, step.e.source == n, n));
                    n = step.to;
                }
                if (chain.Count == 0 || !tree.ContainsKey(n)) continue;
                var pts = new List<Vector2>();
                var starts = new List<int>();
                foreach (var (e, fwd, _) in chain)
                {
                    starts.Add(Mathf.Max(0, pts.Count - 1));
                    for (int k = pts.Count == 0 ? 0 : 1; k < e.pts.Count; k++) pts.Add(e.pts[fwd ? k : e.pts.Count - 1 - k]);
                }
                var d = new List<float>(new float[pts.Count]);
                float acc = tree[n];
                d[pts.Count - 1] = acc;
                for (int k = pts.Count - 2; k >= 0; k--) { acc += (pts[k + 1] - pts[k]).magnitude; d[k] = acc; }
                for (int c = 0; c < chain.Count; c++) tree[chain[c].from] = d[starts[c]];
                // stop short of the heart: the ring takes over there
                for (int k = 0; k < pts.Count; k++)
                    if ((pts[k] - goal).sqrMagnitude < goalRadius * goalRadius)
                    {
                        pts.RemoveRange(k, pts.Count - k);
                        d.RemoveRange(k, d.Count - k);
                        break;
                    }
                if (pts.Count >= 2) route.tracks.Add((pts, d, wi == 0));
                if (wi == 0) { route.mainTrack = pts.FindAll(p => In(p.x, p.y)); route.mainFull = pts; }
            }
            return route;
        }

        /// <summary>
        /// Keeps the tracks' lanes clear of plants: an ant trail is trodden bare (the main one wider).
        /// </summary>
        public static void Reserve(World world, Region region, Route route)
        {
            foreach (var (pts, d, isMain) in route.tracks)
            {
                float lane = isMain ? 0.95f : 0.7f, since = 1f;
                for (int k = 1; k < pts.Count; k++)
                {
                    Vector2 a = pts[k - 1], b = pts[k];
                    float seg = (b - a).magnitude;
                    if (seg < 1e-4f) continue;
                    Vector2 dir = (b - a) / seg, nrm = new Vector2(-dir.y, dir.x);
                    for (float t = 0; t < seg; t += 0.4f)
                    {
                        if ((since += 0.4f) < 0.8f) continue;
                        since = 0;
                        Vector2 p = a + dir * t + nrm * Wander(Mathf.Lerp(d[k - 1], d[k], t / seg), route.noiseSeed);
                        if (Inside(world, region, p.x, p.y)) world.reserved.Add(new Circle(p.x, p.y, lane, 1f));
                    }
                }
            }
            world.reserved.Add(new Circle(route.goal.x, route.goal.y, route.goalRadius + 0.6f, 1f));
        }

        /// <summary>The particles (after the props are placed: they lie over and round them).</summary>
        /// <param name="density">how thickly they lie, relative (1 = as designed)</param>
        public static MeshRenderer Build(World world, Region region, Route route, Material mat, Color tint, float gain,
            float density, Transform parent, int seed)
        {
            if (route == null || route.tracks.Count == 0) return null;
            tint = Color.Lerp(tint, Color.white, 0.12f);
            bool In(float x, float z) => Inside(world, region, x, z);
            float ns = route.noiseSeed;
            var rng = new System.Random(seed);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float Gauss()
            {
                double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
                return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2 * System.Math.PI * u2));
            }
            bool Floor(float x, float z) => In(x, z) && world.Dist(x, z) < -0.2f && !world.colliders.Overlaps(x, z, 0.05f);
            var glow = new GlowParticles();
            int[] counts = new int[3];

            // a speck resting on the floor
            void Speck(float x, float z, float d, float size, float bright)
            {
                if (!Floor(x, z)) return;
                glow.Add(new Vector3(x, world.WalkHeight(x, z) + size * 0.75f, z), Vector3.zero, 0, d, Rand(0, 1), size, Rand(0.35f, 1.4f), 0, tint * Mathf.Clamp01(bright));
                counts[0]++;
            }
            // a mote floating over the path
            void Mote(float x, float z, float d, float bright)
            {
                if (!Floor(x, z)) return;
                float y = Mathf.Min(world.WalkHeight(x, z) + Rand(0.35f, 1.0f), world.Ceiling(x, z) - 0.4f);
                glow.Add(new Vector3(x, y, z), Vector3.zero, 1, d, Rand(0, 1), Rand(0.05f, 0.1f), Rand(0.3f, 0.9f), Rand(0.03f, 0.12f), tint * Mathf.Clamp01(bright));
                counts[1]++;
            }
            // dust drifting from a to b and starting again: the path's flow toward the heart
            void Drift(Vector2 a, Vector2 b, float d, float bright, float speed)
            {
                if (!Floor(a.x, a.y) || !Floor(b.x, b.y)) return;
                var from = new Vector3(a.x, world.WalkHeight(a.x, a.y) + Rand(0.03f, 0.3f), a.y);
                var to = new Vector3(b.x, world.WalkHeight(b.x, b.y) + Rand(0.03f, 0.3f), b.y);
                float life = (b - a).magnitude / speed;
                glow.Add(from, to - from, 2, d, Rand(0, 1), Rand(0.025f, 0.055f), life, Rand(0.04f, 0.25f), tint * Mathf.Clamp01(bright));
                counts[2]++;
            }
            // a speck's own brightness: most are faint, a few stand out
            float Bright(float dense, float kind) => (0.3f + 0.7f * Mathf.Pow((float)rng.NextDouble(), 1.5f)) * (0.75f + 0.4f * dense) * kind;
            float Size() => 0.025f + 0.045f * Mathf.Pow((float)rng.NextDouble(), 2f);

            foreach (var (pts, d, isMain) in route.tracks)
            {
                var cum = new float[pts.Count];
                for (int k = 1; k < pts.Count; k++) cum[k] = cum[k - 1] + (pts[k] - pts[k - 1]).magnitude;
                float total = cum[pts.Count - 1];
                if (total < 0.5f) continue;
                (Vector2 p, Vector2 dir, float dd) At(float s)
                {
                    s = Mathf.Clamp(s, 0, total);
                    int lo = 1, hi = pts.Count - 1;
                    while (lo < hi) { int mid = (lo + hi) / 2; if (cum[mid] < s) lo = mid + 1; else hi = mid; }
                    float len = cum[lo] - cum[lo - 1];
                    float t = len > 1e-4f ? (s - cum[lo - 1]) / len : 0;
                    Vector2 dir = len > 1e-4f ? (pts[lo] - pts[lo - 1]) / len : Vector2.up;
                    return (Vector2.Lerp(pts[lo - 1], pts[lo], t), dir, Mathf.Lerp(d[lo - 1], d[lo], t));
                }
                float kind = isMain ? 1f : 0.6f;
                // how thick it all lies: a slow noise along the way, from thin stretches to thick ones
                float Dense(float dd) => Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.7f, Mathf.PerlinNoise(dd * 0.085f, ns + 5.3f)));
                Vector2 Off(Vector2 p, Vector2 dir, float lateral) => p + new Vector2(-dir.y, dir.x) * lateral;

                // specks in a band along the middle of the way: a random walk whose rate swells and thins
                for (float s = Rand(0, 0.5f); s < total;)
                {
                    var (p, dir, dd) = At(s);
                    float dense = Dense(dd);
                    Vector2 q = Off(p, dir, Wander(dd, ns) + Mathf.Clamp(Gauss() * 0.18f, -0.55f, 0.55f)) + dir * Rand(-0.08f, 0.08f);
                    float b = Bright(dense, kind);
                    Speck(q.x, q.y, dd, Size(), b);
                    // now and then a little clump: spores, or where many feet have pressed
                    if (rng.NextDouble() < 0.12 + 0.2 * dense)
                        for (int m = rng.Next(2, 6); m > 0; m--)
                        {
                            float a = Rand(0, Mathf.PI * 2), r = Rand(0.06f, 0.35f);
                            Speck(q.x + Mathf.Cos(a) * r, q.y + Mathf.Sin(a) * r, dd, Rand(0.02f, 0.04f), b * Rand(0.4f, 1f));
                        }
                    // rarely, a few steps of an ant's track, a little askew to the path
                    if (rng.NextDouble() < 0.015 + 0.02 * dense)
                    {
                        float ang = Rand(-0.4f, 0.4f), cs = Mathf.Cos(ang), sn = Mathf.Sin(ang);
                        Vector2 f = new Vector2(dir.x * cs - dir.y * sn, dir.x * sn + dir.y * cs), side = new Vector2(-f.y, f.x);
                        Vector2 c = q;
                        for (int i = rng.Next(4, 9); i > 0; i--)
                        {
                            Vector2 sq = c + side * ((i % 2 == 0 ? 1 : -1) * Rand(0.08f, 0.14f)) + new Vector2(Rand(-0.03f, 0.03f), Rand(-0.03f, 0.03f));
                            Speck(sq.x, sq.y, dd, Rand(0.02f, 0.035f), b * Rand(0.5f, 0.9f));
                            c += f * Rand(0.18f, 0.3f);
                        }
                    }
                    float rate = Mathf.Lerp(2f, 7f, dense) * kind * density;   // specks per unit of path
                    s += Mathf.Min(1.5f, -Mathf.Log(1f - (float)rng.NextDouble() * 0.999f) / Mathf.Max(0.05f, rate));
                }
                // dust drifting along the way toward the heart, lane after lane, overlapping: the flow shows which way
                for (float s = Rand(0, 0.4f); s < total - 0.8f; s += Rand(0.3f, 0.6f) / (kind * Mathf.Max(0.2f, density)))
                {
                    var (p, dir, dd) = At(s);
                    float len = Mathf.Min(Rand(2.4f, 4.2f), total - s);
                    var (p1, dir1, _) = At(s + len);
                    float lateral = Mathf.Clamp(Gauss() * 0.22f, -0.55f, 0.55f);
                    Vector2 a = Off(p, dir, Wander(dd, ns) + lateral);
                    Vector2 b = Off(p1, dir1, Wander(dd - len, ns) + lateral + Rand(-0.15f, 0.15f));
                    Drift(a, b, dd, Rand(0.5f, 1f) * (0.8f + 0.3f * Dense(dd)) * kind, Rand(0.5f, 0.95f));
                }
                // a few motes floating higher over the main way
                if (!isMain) continue;
                for (float s = Rand(1, 4); s < total;)
                {
                    var (p, dir, dd) = At(s);
                    float dense = Dense(dd);
                    Vector2 m = Off(p, dir, Wander(dd, ns) * 0.6f + Mathf.Clamp(Gauss() * 0.35f, -0.9f, 0.9f));
                    Mote(m.x, m.y, dd, Rand(0.35f, 0.85f) * (0.75f + 0.25f * dense));
                    s += Mathf.Min(14f, -Mathf.Log(1f - (float)rng.NextDouble() * 0.999f) / Mathf.Lerp(0.06f, 0.2f, dense));
                }
            }
            // round the heart the specks gather, thick close in and thinning outward, more on some sides, and a
            // little light rises off it
            Vector2 goal = route.goal;
            float gr = route.goalRadius, side0 = Rand(0, 100);
            for (int k = Mathf.RoundToInt(50 + gr * 18); k > 0; k--)
            {
                float a = Rand(0, Mathf.PI * 2);
                if (Mathf.PerlinNoise(Mathf.Cos(a) * 1.4f + side0, Mathf.Sin(a) * 1.4f) < (float)rng.NextDouble() * 0.8f) continue;
                float r = gr - 0.5f + Mathf.Pow((float)rng.NextDouble(), 1.8f) * 2.6f;
                Speck(goal.x + Mathf.Cos(a) * r, goal.y + Mathf.Sin(a) * r, 0, Size(), Bright(1f, 1f));
            }
            for (int k = 24; k > 0; k--)
            {
                float a = Rand(0, Mathf.PI * 2), r = gr + Rand(-0.4f, 1.6f);
                float x = goal.x + Mathf.Cos(a) * r, z = goal.y + Mathf.Sin(a) * r;
                if (!Floor(x, z)) continue;
                var from = new Vector3(x, world.WalkHeight(x, z) + Rand(0.05f, 0.4f), z);
                float rise = Mathf.Min(Rand(1.5f, 3f), world.Ceiling(x, z) - from.y - 0.4f);
                if (rise < 0.5f) continue;
                glow.Add(from, new Vector3(Rand(-0.3f, 0.3f), rise, Rand(-0.3f, 0.3f)), 2, 0, Rand(0, 1), Rand(0.03f, 0.06f), Rand(4f, 7f), 0, tint * Rand(0.5f, 0.9f));
            }
            if (glow.Count == 0) return null;
            var block = new MaterialPropertyBlock();
            // set here, not in the saved material, so a tweak needs no asset rebuild
            // (in the nests lifted above v15's exposure it kept less of the lift than the motes and dust round it, so it
            // stays the brightest light along the floor without burning white: NestTheme.LightComp)
            block.SetFloat("_Gain", 1.4f * gain * NestTheme.LightComp(0.5f));
            // its motes floating over the way none of the lift: there they were white balls in the air, not the floor's
            // light leading on (the user, 10-29: "白光…已经分不清楚引导的光线了")
            block.SetFloat("_MoteGain", NestTheme.LightComp(0.5f));
            block.SetFloat("_Base", 0.42f);
            block.SetFloat("_Near", 0.28f);
            block.SetFloat("_Ahead", 0.45f);
            block.SetFloat("_AheadReach", 10f);
            block.SetFloat("_PlayerD", -1f);
            block.SetFloat("_Pulse", 2f);     // living light running ahead of the ant, like a heartbeat
            block.SetFloat("_PulseEvery", 2.6f);
            block.SetFloat("_PulseSpeed", 5f);
            block.SetFloat("_PulseReach", 16f);
            var mr = glow.Finish($"GuideTrail_r{region.index}", mat, parent, block);
            Debug.Log($"[AntGarden] guide trail r{region.index}: {route.tracks.Count} tracks, {counts[0]} specks, {counts[2]} drifting, {counts[1]} motes, {glow.Count} in all, {route.ways} ways in");
            return mr;
        }

        /// <summary>Each frame: where the player's ant is, and the delayed point behind it that leaves the afterglow.</summary>
        public static void Follow(Vector3 ant, float dt)
        {
            // a jump (a gate, the map) moves the afterglow along at once instead of sweeping it across the world
            if (!lagSet || (ant - lag).sqrMagnitude > 36f) { lag = ant; lagSet = true; }
            else lag += (ant - lag) * (1f - Mathf.Exp(-dt / 1.1f));
            Shader.SetGlobalVector("_TrailPlayer", ant);
            Shader.SetGlobalVector("_TrailLag", lag);
        }

        /// <summary>The ant's distance to the heart along the route (where it is on a track), or -1 off the tracks.</summary>
        public static float PlayerD(Route route, Vector3 ant)
        {
            if (route == null) return -1;
            var p = new Vector2(ant.x, ant.z);
            if ((p - route.goal).sqrMagnitude < (route.goalRadius + 1f) * (route.goalRadius + 1f)) return 0;
            float best = 9f, d = -1;   // within 3 units of a track
            foreach (var (pts, ds, _) in route.tracks)
            {
                if (pts.Count == 1 && (pts[0] - p).sqrMagnitude < best) { best = (pts[0] - p).sqrMagnitude; d = ds[0]; }
                // the nearest point on each stretch, and the distance there (between its two ends'): it runs on as the ant
                // walks instead of stepping from one track point to the next (they are about a unit apart)
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    Vector2 a = pts[k], ab = pts[k + 1] - a;
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
                    float q = (a + ab * t - p).sqrMagnitude;
                    if (q < best) { best = q; d = Mathf.Lerp(ds[k], ds[k + 1], t); }
                }
            }
            return d;
        }

        static float Length(List<Vector2> pts)
        {
            float len = 0;
            for (int i = 1; i < pts.Count; i++) len += (pts[i] - pts[i - 1]).magnitude;
            return len;
        }

        static string Nearest(Dictionary<string, Vector2> pos, Dictionary<string, float> reachable, Vector2 p)
        {
            string best = null;
            float bd = float.MaxValue;
            foreach (var kv in pos)
            {
                if (!reachable.ContainsKey(kv.Key)) continue;
                float d = (kv.Value - p).sqrMagnitude;
                if (d < bd) { bd = d; best = kv.Key; }
            }
            return best;
        }
    }

    /// <summary>
    /// Collects small glowing particles into one mesh for Shaders/GuideTrail.shader: each a quad whose four
    /// corners sit at its start point (the shader spreads them to face the camera and moves the drifting ones).
    /// </summary>
    public sealed class GlowParticles
    {
        readonly List<Vector3> verts = new List<Vector3>(), normals = new List<Vector3>();
        readonly List<Vector2> uv = new List<Vector2>(), uv2 = new List<Vector2>(), uv3 = new List<Vector2>(), uv4 = new List<Vector2>();
        readonly List<Color> colors = new List<Color>();
        readonly List<int> tris = new List<int>();
        float reach;

        public int Count => verts.Count / 4;

        /// <param name="flow">a drifting particle's whole way (kind 2), else zero</param>
        /// <param name="kind">0 speck, 1 mote, 2 drifting</param>
        /// <param name="d">distance along a track to its heart (for the way-ahead glow)</param>
        /// <param name="pace">specks and motes: how fast they breathe; drifting: seconds for the whole way</param>
        /// <param name="extra">motes: how far they bob; drifting: how far they rise and sink on the way</param>
        public void Add(Vector3 at, Vector3 flow, float kind, float d, float phase, float size, float pace, float extra, Color color)
        {
            int i0 = verts.Count;
            for (int c = 0; c < 4; c++)
            {
                verts.Add(at);
                normals.Add(flow);
                uv.Add(new Vector2(c == 0 || c == 3 ? -1 : 1, c < 2 ? -1 : 1));
                uv2.Add(new Vector2(d, kind));
                uv3.Add(new Vector2(phase, size));
                uv4.Add(new Vector2(pace, extra));
                colors.Add(color);
            }
            tris.Add(i0); tris.Add(i0 + 1); tris.Add(i0 + 2); tris.Add(i0); tris.Add(i0 + 2); tris.Add(i0 + 3);
            reach = Mathf.Max(reach, flow.magnitude + extra + size);
        }

        public MeshRenderer Finish(string name, Material mat, Transform parent, MaterialPropertyBlock block)
        {
            var mesh = new Mesh { name = name, indexFormat = verts.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16 };
            mesh.SetVertices(verts);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, uv);
            mesh.SetUVs(1, uv2);
            mesh.SetUVs(2, uv3);
            mesh.SetUVs(3, uv4);
            mesh.SetColors(colors);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateBounds();
            Bounds bounds = mesh.bounds;
            bounds.Expand(2f * reach + 1f);   // the quads spread out and the drifting ones travel in the shader
            mesh.bounds = bounds;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
            mr.shadowCastingMode = ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mr.lightProbeUsage = LightProbeUsage.Off;
            mr.reflectionProbeUsage = ReflectionProbeUsage.Off;
            if (block != null) mr.SetPropertyBlock(block);
            return mr;
        }
    }
}

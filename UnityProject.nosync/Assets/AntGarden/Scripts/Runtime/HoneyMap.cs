using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using AntGarden.Core;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The honeycomb drawn as one map (11-04). The six particle nests stay separate worlds (the user, 11-04, through the
    /// hub: keep the ring, no stitched field), and the maps draw them joined: each nest's own canyons in its hexagon of
    /// the ring (GameConfig.labWorld; BigMap.RingAt, the first at the top, on clockwise), in its own colour (its segment
    /// of the colour wheel, as the ring's cells were), its module turned in steps of 60° (so its frame is the hexagon and
    /// the six tile as a honeycomb) the way that makes the jumps between them shortest; inside each, light, the way its
    /// light leads (from the gate an ant from the previous nest arrives at, to its way out); between the nests, dotted (as
    /// portals were on the old map), the jump from each way out to the next nest's gate back: one journey round the ring.
    /// Its doors (gates) are dots in the colour of the nest they lead to. Shown in two steps (the user, 11-04: "单个地图要显示
    /// 完全，然后再点开来才是更多的地图"): the minimap and the big map first show the nest the ant is in, whole; one step further
    /// the whole ring (HoneyMapUi). Its colours are each nest's hue, low in chroma (a quieter, more grown-up map).
    /// The canyons of the nests the ant is not in are carved here as the game carves them (Game.MakeField, the same width
    /// and passages), once a session and off the main thread (Prepare, at the start of a nest's build: a second or so,
    /// while the nest itself takes ten), and their gates are found as Game.RingGates and LabNest.Plan find them (their way
    /// out, the exit farthest along the canyons from where the ant starts; their way back, the exit facing the previous
    /// nest; any left over to whichever of the two lies more its way). MapCheck checks them against each nest's own.
    /// </summary>
    public class HoneyMap
    {
        /// <summary>Between two cells of the ring, of a cell's circumradius (BigMap's cells used 7 of 132 pixels).</summary>
        public const float Gap = 0.06f;
        const float BaseTex = 320f;      // picture pixels per cell circumradius: the ring and the minimap show a cell 120 to 170 across
        const float DetailTex = 600f;    // and the nest the ant is in, shown whole on the big map, about 500 across
        const float Bulge = 0.2f;        // how far a jump's arc bows out from the ring's middle, of its length
        const float LineHalf = 0.7f;     // the way its light leads: half its width, in units of the nest
        static readonly float Sqrt3 = Mathf.Sqrt(3f);
        static readonly float[] Turnings = { 30f, 90f, 150f, 210f, 270f, 330f };   // pointy-top modules into flat-top cells

        /// <summary>A gate as the map knows it: where its canyon runs out of the nest, and the nest it leads to.</summary>
        public class Door
        {
            public Vector2 at;
            public string to;
            public int toIndex = -1;
            public bool wayOut, wayBack;   // where the light leads (to the next nest); the one facing the previous nest
        }

        /// <summary>One nest of the ring: its colour, frame, doors, the way its light leads, and its picture.</summary>
        public class Nest
        {
            public string id, biome;
            public float hue;
            public Color colour;          // its rim's colour: its hue, low in chroma (HSV(hue, 0.3, 0.7))
            public float R, apothem;      // its hexagon frame (units of the nest)
            public float turn;            // its picture is turned this far clockwise on the map (degrees; 30 + 60k)
            public Vector2 start;         // where the ant starts in it (no gate to arrive by)
            public Vector2? wayOut;       // the end of the way its light leads (LabNest.Heart)
            public readonly List<Door> doors = new List<Door>();
            public readonly List<Vector2> journey = new List<Vector2>();   // from its gate back to its way out
            internal string spec;
            internal Dictionary<string, string> texts;
            internal PathField field;              // (kept: the nest the ant comes to is drawn again, sharper: Detail)
            internal volatile Picture picture, detail;   // its picture, and the sharper one of it (only once the ant is in it)
            internal volatile bool painting;

            /// <summary>The door an ant coming from that nest arrives at (Game.SpawnPlayer: GatesBack, set for the way out
            /// and the way back), else any door to it; null when none leads there.</summary>
            public Door Arrival(int from) => doors.Find(d => d.toIndex == from && (d.wayOut || d.wayBack)) ?? doors.Find(d => d.toIndex == from);

            /// <summary>Its picture (the ring's and the minimap's), and the sharper one when it is painted (else null).</summary>
            public Picture Picture => picture;
            public Picture Detail => detail;
        }

        /// <summary>A nest's picture: north up, its middle the nest's, ppu pixels a unit of the nest; its texture made on the
        /// main thread when first wanted and kept from world to world.</summary>
        public class Picture
        {
            public int w, h;
            public float ppu, halfX, halfZ;
            internal Color32[] pixels;
            internal string name;
            Texture2D texture;

            /// <summary>Its size on the board (board units: a cell's circumradius is 1) for a nest of circumradius R.</summary>
            public Vector2 Size(float R) => new Vector2(w, h) / (ppu * R);

            public Texture2D Texture
            {
                get
                {
                    if (texture != null || pixels == null) return texture;
                    texture = new Texture2D(w, h, TextureFormat.RGBA32, true, false)
                    {
                        name = name, wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Trilinear,
                        hideFlags = HideFlags.DontUnloadUnusedAsset,   // (a world's reload keeps it)
                    };
                    texture.ignoreMipmapLimit = true;   // (the low preset's half-size textures are not for the map)
                    texture.SetPixels32(pixels);
                    texture.Apply(true, false);
                    return texture;
                }
            }
        }

        /// <summary>The light's way on from one nest to the next: from its way out to the next one's gate back.</summary>
        public class Jump { public int a, b; public Door from, to; }

        public readonly List<Nest> nests = new List<Nest>();
        public readonly List<Jump> jumps = new List<Jump>();
        volatile bool done;
        volatile string error;
        public bool Done => done;
        public bool Ready => done && error == null;
        public string Error => error;
        public int Count => nests.Count;
        public int IndexOf(string id) => nests.FindIndex(x => x.id == id);

        static HoneyMap shared;
        static string sharedKey;

        /// <summary>The map of the honeycomb this session (being drawn, drawn, or null outside the particle nests).</summary>
        public static HoneyMap Shared => shared;

        /// <summary>The map is drawn and the nest the ant is in is one of its ring.</summary>
        public static bool ReadyHere => shared != null && shared.Ready && shared.IndexOf(Game.Selection) >= 0;

        /// <summary>The ant is in a nest of the ring (its map drawn or still being drawn).</summary>
        public static bool WantedHere => shared != null && !(shared.done && shared.error != null) && shared.IndexOf(Game.Selection) >= 0;

        /// <summary>
        /// Starts drawing the honeycomb's map (Game.Build, in a particle nest): the ring's nests read here (Unity's paths are
        /// the main thread's), their canyons carved and drawn on a thread of their own. Once a session: a world's reload,
        /// or the language switching, keeps it.
        /// </summary>
        public static HoneyMap Prepare(GameConfig cfg, IList<string> worlds)
        {
            var labs = new List<string>();
            if (worlds != null) foreach (string w in worlds) if (w.StartsWith("lab:")) labs.Add(w);
            if (labs.Count < 2) return null;
            string width = Game.Arg("-labwidth");
            bool passages = Game.Arg("-passages") != "before";
            string key = string.Join(",", labs) + "|" + width + "|" + passages;
            if (shared != null && sharedKey == key)
            {
                shared.WantDetail(shared.IndexOf(Game.Selection));   // (the nest the ant comes to, sharper)
                return shared;
            }
            var map = new HoneyMap();
            try
            {
                foreach (string id in labs)
                {
                    string spec = id.Substring(4), biome = "blue";
                    int hash = spec.LastIndexOf('#');
                    if (hash >= 0) spec = spec.Substring(0, hash);
                    int at = spec.LastIndexOf('@');
                    if (at >= 0) { biome = spec.Substring(at + 1); spec = spec.Substring(0, at); }
                    float hue = Mathf.Repeat(NestTheme.For(biome).hue, 360f);
                    map.nests.Add(new Nest { id = id, biome = biome, hue = hue, colour = Color.HSVToRGB(hue / 360f, 0.3f, 0.7f), spec = spec, texts = Game.ReadNetworks(spec) });
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[AntGarden] honeycomb map: the nests could not be read: " + e.GetBaseException().Message);
                return null;
            }
            double labWidth = width != null ? double.Parse(width, CultureInfo.InvariantCulture) : cfg.labWidth;
            Action<PathConfig> lanes = passages ? Game.LabPassages(cfg) : null;
            shared = map;
            sharedKey = key;
            int here = map.IndexOf(Game.Selection);
            new Thread(() => map.Build(labWidth, lanes, here)) { IsBackground = true, Name = "HoneyMap" }.Start();
            return map;
        }

        /// <summary>The nest k's sharper picture, painted off the main thread if it is not yet (the big map shows the nest the
        /// ant is in whole, about 500 pixels across: its ring picture would be blurred there).</summary>
        public void WantDetail(int k)
        {
            if (k < 0 || k >= nests.Count || !Ready) return;
            Nest nest = nests[k];
            if (nest.detail != null || nest.painting || nest.field == null) return;
            nest.painting = true;
            new Thread(() =>
            {
                try { nest.detail = Paint(nest, DetailTex); }
                catch (Exception e) { Debug.LogWarning("[AntGarden] honeycomb map: " + nest.biome + "'s sharper picture failed: " + e.GetBaseException().Message); }
                finally { nest.painting = false; }
            }) { IsBackground = true, Name = "HoneyMap detail" }.Start();
        }

        // ---- the board: a cell's circumradius is 1, the ring's middle at 0, y up --------------------------------------

        /// <summary>Where cell k's middle is on the board.</summary>
        public Vector2 Centre(int k) => BigMap.RingAt(k, nests.Count) * (Sqrt3 + Gap);

        /// <summary>A point of nest k (units of the nest: x east, y north) on the board.</summary>
        public Vector2 ToBoard(int k, Vector2 world) => BoardAt(k, world, nests[k].turn);

        /// <summary>A point of the board in nest k (units of the nest).</summary>
        public Vector2 FromBoard(int k, Vector2 board) => Turn((board - Centre(k)) * nests[k].R, -nests[k].turn);

        /// <summary>The cell of the board a point is in, or -1.</summary>
        public int CellAt(Vector2 board)
        {
            for (int k = 0; k < nests.Count; k++) if (InCell(board - Centre(k), 0f)) return k;
            return -1;
        }

        /// <summary>A point (from a cell's middle) inside its hexagon (flat side up, circumradius 1) by at least margin.</summary>
        public static bool InCell(Vector2 d, float margin)
        {
            float x = Mathf.Abs(d.x), y = Mathf.Abs(d.y);
            return Mathf.Max(y, 0.8660254f * x + 0.5f * y) <= 0.8660254f - margin;
        }

        /// <summary>The board's extent: every cell's hexagon.</summary>
        public Rect Bounds()
        {
            Vector2 lo = new Vector2(float.MaxValue, float.MaxValue), hi = -lo;
            for (int k = 0; k < nests.Count; k++)
            {
                lo = Vector2.Min(lo, Centre(k) - new Vector2(1f, 0.8660254f));
                hi = Vector2.Max(hi, Centre(k) + new Vector2(1f, 0.8660254f));
            }
            return Rect.MinMaxRect(lo.x, lo.y, hi.x, hi.y);
        }

        /// <summary>A jump's arc on the board (from its way out to the next nest's gate back), bowing out from the ring's middle.</summary>
        public List<Vector2> Arc(Jump j, int steps = 48)
        {
            Vector2 to = j.to != null ? j.to.at : nests[j.b].start;
            return ArcBetween(ToBoard(j.a, j.from.at), ToBoard(j.b, to), steps);
        }

        Vector2 BoardAt(int k, Vector2 world, float turn) => Centre(k) + Turn(world, turn) / nests[k].R;

        /// <summary>A direction turned clockwise by deg (as bearings: 0 north, 90 east).</summary>
        public static Vector2 Turn(Vector2 v, float deg)
        {
            float t = deg * Mathf.Deg2Rad, c = Mathf.Cos(t), s = Mathf.Sin(t);
            return new Vector2(v.x * c + v.y * s, -v.x * s + v.y * c);
        }

        static List<Vector2> ArcBetween(Vector2 a, Vector2 b, int steps)
        {
            Vector2 mid = (a + b) / 2f, along = b - a;
            Vector2 n = new Vector2(-along.y, along.x).normalized;
            if (Vector2.Dot(n, mid) < 0f) n = -n;
            Vector2 c = mid + n * along.magnitude * Bulge;
            var pts = new List<Vector2>(steps + 1);
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps, u = 1f - t;
                pts.Add(u * u * a + 2f * u * t * c + t * t * b);
            }
            return pts;
        }

        // ---- drawing it (off the main thread) -----------------------------------------------------------------------

        void Build(double labWidth, Action<PathConfig> lanes, int here)
        {
            try
            {
                var clock = System.Diagnostics.Stopwatch.StartNew();
                int n = nests.Count;
                foreach (Nest nest in nests)
                {
                    nest.field = Game.MakeField(nest.spec, nest.texts, new List<double> { labWidth }, lanes).Item1;
                    nest.R = (float)nest.field.R;
                    nest.apothem = (float)nest.field.Apothem;
                    var sp = nest.field.SpawnPoint();
                    nest.start = new Vector2((float)sp.x, (float)-sp.z);
                }
                for (int k = 0; k < n; k++) FindDoors(k);
                for (int k = 0; k < n; k++)
                {
                    Nest nest = nests[k];
                    Door back = nest.Arrival((k + n - 1) % n), way = nest.doors.Find(d => d.wayOut);
                    if (way != null) nest.journey.AddRange(Route(nest.field, back != null ? back.at : nest.start, way.at));
                }
                for (int a = 0; a < n; a++)
                {
                    Door way = nests[a].doors.Find(d => d.wayOut);
                    if (way != null) jumps.Add(new Jump { a = a, b = way.toIndex, from = way, to = nests[way.toIndex].Arrival(a) });
                }
                Turns();
                foreach (Nest nest in nests) nest.picture = Paint(nest, BaseTex);
                if (here >= 0) nests[here].detail = Paint(nests[here], DetailTex);
                foreach (Nest nest in nests) nest.texts = null;
                var turned = new List<string>();
                foreach (Nest nest in nests) turned.Add($"{nest.biome} {nest.turn:F0}°");
                Debug.Log($"[AntGarden] honeycomb map: {n} nests drawn in {clock.ElapsedMilliseconds} ms (off the main thread), turned {string.Join(", ", turned)}");
            }
            catch (Exception e)
            {
                error = e.GetBaseException().Message;
                Debug.LogWarning("[AntGarden] honeycomb map failed: " + e);
            }
            finally { done = true; }
        }

        static Vector2 W(double[] p) => new Vector2((float)p[0], (float)-p[1]);   // the field's (x, z) as the world's

        /// <summary>
        /// Nest k's doors, as the game makes its gates (Game.RingGates): its way out, the exit nearest the end of the way
        /// its light leads (LabNest.Plan), to the next nest; of the others, the one whose direction from the nest's middle
        /// best matches the previous nest's on the map, to that one; any left over to whichever of the two lies more its way.
        /// </summary>
        void FindDoors(int li)
        {
            Nest nest = nests[li];
            PathField f = nest.field;
            int n = nests.Count, next = (li + 1) % n, prev = (li + n - 1) % n;
            nest.wayOut = WayOut(f, nest.start);
            var free = new List<Vector2>();
            foreach (Exit ex in f.Exits) free.Add(new Vector2((float)ex.X, (float)-ex.Z));
            if (free.Count == 0) return;
            Vector2 here = BigMap.RingAt(li, n), toNext = (BigMap.RingAt(li + 1, n) - here).normalized, toPrev = (BigMap.RingAt(li - 1, n) - here).normalized;
            float minX = (float)f.Bounds.MinX, w = (float)(f.Bounds.MaxX - f.Bounds.MinX);
            var mid = new Vector2((minX + minX + w) / 2, ((float)-f.Bounds.MaxZ + (float)-f.Bounds.MinZ) / 2);
            Vector2 Dir(Vector2 ex) => (ex - mid).normalized;
            Vector2 heart = nest.wayOut ?? Vector2.zero;   // (the game's LabNest.Heart: zero when it found no way)
            int way = 0;
            for (int i = 0; i < free.Count; i++)
                if ((free[i] - heart).sqrMagnitude < (free[way] - heart).sqrMagnitude) way = i;
            int back = -1;
            for (int i = 0; i < free.Count; i++)
                if (i != way && (back < 0 || Vector2.Dot(Dir(free[i]), toPrev) > Vector2.Dot(Dir(free[back]), toPrev))) back = i;
            var order = new List<int> { way };
            for (int i = 0; i < free.Count; i++) if (i != way) order.Add(i);
            foreach (int i in order)
            {
                Vector2 ex = free[i];
                int dest = i == way ? next : i == back ? prev : Vector2.Dot(Dir(ex), toNext) >= Vector2.Dot(Dir(ex), toPrev) ? next : prev;
                nest.doors.Add(new Door { at = ex, to = nests[dest].id, toIndex = dest, wayOut = i == way, wayBack = i == back });
            }
        }

        /// <summary>LabNest.Plan's way out: of the exits, the one whose canyon's far end lies farthest along the canyons from
        /// the node nearest where the ant starts; with no exit, the deepest inner dead end. Null when the nest has no canyons.</summary>
        static Vector2? WayOut(PathField f, Vector2 spawn)
        {
            var adj = new Dictionary<string, List<(string to, float len)>>();
            var pos = new Dictionary<string, Vector2>();
            foreach (Edge e in f.Edges)
            {
                if (e.Pts.Count < 2) continue;
                float len = 0;
                for (int i = 1; i < e.Pts.Count; i++) len += (W(e.Pts[i]) - W(e.Pts[i - 1])).magnitude;
                Link(adj, e.Source, (e.Target, len));
                Link(adj, e.Target, (e.Source, len));
                pos[e.Source] = W(e.Pts[0]);
                pos[e.Target] = W(e.Pts[e.Pts.Count - 1]);
            }
            if (pos.Count == 0) return null;
            string start = null;
            float best = float.MaxValue;
            foreach (var kv in pos) { float d = (kv.Value - spawn).sqrMagnitude; if (d < best) { best = d; start = kv.Key; } }
            Dictionary<string, float> dist = Distances(adj, start);
            Vector2? heart = null;
            float far = -1f;
            foreach (Exit x in f.Exits)
            {
                var at = new Vector2((float)x.X, (float)-x.Z);
                Edge edge = null;
                float bd = 9f;
                foreach (Edge e in f.Edges)
                {
                    if (e.Pts.Count < 2) continue;
                    foreach (double[] q in e.Pts) { float d2 = (W(q) - at).sqrMagnitude; if (d2 < bd) { bd = d2; edge = e; } }
                }
                if (edge == null) continue;
                Vector2 s = W(edge.Pts[0]), t = W(edge.Pts[edge.Pts.Count - 1]);
                bool fwd = f.HexDist(t.x, -t.y) > f.HexDist(s.x, -s.y);
                if (!dist.TryGetValue(fwd ? edge.Target : edge.Source, out float d) || d <= far) continue;
                far = d;
                heart = at;
            }
            if (heart != null) return heart;
            Node endNode = null;
            foreach (Node nd in f.Nodes)
            {
                if (!nd.Inside || !dist.ContainsKey(nd.Id)) continue;
                bool deadEnd = nd.Degree == 1;
                if (endNode == null || (deadEnd && (endNode.Degree != 1 || dist[nd.Id] > dist[endNode.Id]))
                    || (!deadEnd && endNode.Degree != 1 && nd.Clear > endNode.Clear)) endNode = nd;
            }
            return endNode == null ? (Vector2?)null : new Vector2((float)endNode.X, (float)-endNode.Z);
        }

        static void Link<T>(Dictionary<string, List<T>> adj, string key, T v)
        {
            if (!adj.TryGetValue(key, out var list)) adj[key] = list = new List<T>();
            list.Add(v);
        }

        static Dictionary<string, float> Distances(Dictionary<string, List<(string to, float len)>> adj, string start)
        {
            var dist = new Dictionary<string, float> { [start] = 0 };
            var open = new List<string> { start };
            var settled = new HashSet<string>();
            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++) if (dist[open[i]] < dist[open[bi]]) bi = i;
                string u = open[bi];
                open.RemoveAt(bi);
                if (!settled.Add(u) || !adj.TryGetValue(u, out var list)) continue;
                foreach (var (v, len) in list)
                {
                    float d = dist[u] + len;
                    if (dist.TryGetValue(v, out float old) && old <= d) continue;
                    dist[v] = d;
                    open.Add(v);
                }
            }
            return dist;
        }

        /// <summary>The way along the canyons' middle lines from one point of them to another (each taken at the canyon
        /// point nearest it): along its own canyon to one of its ends, on through the nest, and in along the other's.</summary>
        static List<Vector2> Route(PathField f, Vector2 from, Vector2 to)
        {
            var lines = new List<(string s, string t, List<Vector2> pts)>();
            foreach (Edge e in f.Edges)
            {
                if (e.Pts.Count < 2) continue;
                var pts = new List<Vector2>(e.Pts.Count);
                foreach (double[] q in e.Pts) pts.Add(W(q));
                lines.Add((e.Source, e.Target, pts));
            }
            (int e, int i) Near(Vector2 p)
            {
                (int, int) at = (-1, -1);
                float bd = float.MaxValue;
                for (int e = 0; e < lines.Count; e++)
                    for (int i = 0; i < lines[e].pts.Count; i++)
                    {
                        float d = (lines[e].pts[i] - p).sqrMagnitude;
                        if (d < bd) { bd = d; at = (e, i); }
                    }
                return at;
            }
            float Length(List<Vector2> pts) { float l = 0; for (int i = 1; i < pts.Count; i++) l += (pts[i] - pts[i - 1]).magnitude; return l; }
            List<Vector2> Slice(List<Vector2> pts, int i0, int i1)
            {
                var r = new List<Vector2>();
                if (i0 <= i1) for (int i = i0; i <= i1; i++) r.Add(pts[i]);
                else for (int i = i0; i >= i1; i--) r.Add(pts[i]);
                return r;
            }
            var (ea, ia) = Near(from);
            var (eb, ib) = Near(to);
            if (ea < 0 || eb < 0) return new List<Vector2>();
            List<Vector2> best = ea == eb ? Slice(lines[ea].pts, ia, ib) : null;
            float bestLen = best != null ? Length(best) : float.MaxValue;
            // the canyons as a graph of their ends
            var adj = new Dictionary<string, List<(string to, int line, bool reversed, float len)>>();
            for (int e = 0; e < lines.Count; e++)
            {
                float len = Length(lines[e].pts);
                Link(adj, lines[e].s, (lines[e].t, e, false, len));
                Link(adj, lines[e].t, (lines[e].s, e, true, len));
            }
            for (int sa = 0; sa < 2; sa++)
            for (int sb = 0; sb < 2; sb++)
            {
                string src = sa == 0 ? lines[ea].s : lines[ea].t, dst = sb == 0 ? lines[eb].s : lines[eb].t;
                // the shortest way from src to dst, and the canyons it takes
                var dist = new Dictionary<string, float> { [src] = 0 };
                var came = new Dictionary<string, (string from, int line, bool reversed)>();
                var open = new List<string> { src };
                var settled = new HashSet<string>();
                while (open.Count > 0)
                {
                    int bi = 0;
                    for (int i = 1; i < open.Count; i++) if (dist[open[i]] < dist[open[bi]]) bi = i;
                    string u = open[bi];
                    open.RemoveAt(bi);
                    if (!settled.Add(u) || !adj.TryGetValue(u, out var list)) continue;
                    foreach (var (v, line, reversed, len) in list)
                    {
                        float d = dist[u] + len;
                        if (dist.TryGetValue(v, out float old) && old <= d) continue;
                        dist[v] = d;
                        came[v] = (u, line, reversed);
                        open.Add(v);
                    }
                }
                if (!dist.ContainsKey(dst)) continue;
                var path = Slice(lines[ea].pts, ia, sa == 0 ? 0 : lines[ea].pts.Count - 1);
                var chain = new List<(int line, bool reversed)>();
                for (string u = dst; u != src; u = came[u].from) chain.Add((came[u].line, came[u].reversed));
                for (int c = chain.Count - 1; c >= 0; c--)
                {
                    var pts = lines[chain[c].line].pts;
                    path.AddRange(chain[c].reversed ? Slice(pts, pts.Count - 1, 0) : pts);
                }
                path.AddRange(Slice(lines[eb].pts, sb == 0 ? 0 : lines[eb].pts.Count - 1, ib));
                float total = Length(path);
                if (total < bestLen) { bestLen = total; best = path; }
            }
            return best ?? new List<Vector2>();
        }

        /// <summary>
        /// How each nest's picture is turned (30° + 60°k: its frame stays the cell): the way that makes the jumps between
        /// the nests shortest and keeps them out of the cells (a jump's arc inside a cell counts seven times its length).
        /// Every way of turning them for a ring of up to seven; more, one nest at a time for a few rounds.
        /// </summary>
        void Turns()
        {
            int n = nests.Count, m = Turnings.Length;
            var cost = new float[jumps.Count, m, m];
            for (int j = 0; j < jumps.Count; j++)
            {
                Jump jump = jumps[j];
                Vector2 to = jump.to != null ? jump.to.at : nests[jump.b].start;
                for (int ta = 0; ta < m; ta++)
                for (int tb = 0; tb < m; tb++)
                    cost[j, ta, tb] = ArcCost(ArcBetween(BoardAt(jump.a, jump.from.at, Turnings[ta]), BoardAt(jump.b, to, Turnings[tb]), 40));
            }
            int[] pick = new int[n], best = new int[n];
            float Total()
            {
                float c = 0;
                for (int j = 0; j < jumps.Count; j++) c += cost[j, pick[jumps[j].a], pick[jumps[j].b]];
                return c;
            }
            float bestCost = float.MaxValue;
            if (n <= 7)
            {
                int combos = 1;
                for (int k = 0; k < n; k++) combos *= m;
                for (int c = 0; c < combos; c++)
                {
                    for (int k = 0, r = c; k < n; k++, r /= m) pick[k] = r % m;
                    float t = Total();
                    if (t < bestCost) { bestCost = t; Array.Copy(pick, best, n); }
                }
            }
            else
            {
                for (int round = 0; round < 4; round++)
                    for (int k = 0; k < n; k++)
                        for (int o = 0; o < m; o++)
                        {
                            pick[k] = o;
                            float t = Total();
                            if (t < bestCost) { bestCost = t; Array.Copy(pick, best, n); }
                            else pick[k] = best[k];
                        }
            }
            for (int k = 0; k < n; k++) nests[k].turn = Turnings[best[k]];
        }

        float ArcCost(List<Vector2> pts)
        {
            float c = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                float seg = (pts[i] - pts[i - 1]).magnitude;
                c += seg;
                Vector2 mid = (pts[i] + pts[i - 1]) / 2f;
                for (int k = 0; k < nests.Count; k++)
                    if (InCell(mid - Centre(k), 0.05f)) c += 6f * seg;
            }
            return c;
        }

        /// <summary>
        /// A nest's picture, north up, perCell pixels for its circumradius: inside its hexagon a dark ground in its colour,
        /// its canyons' banks a little lighter and their floors (the canyons as the game carves them, PathField's distance)
        /// light, the way its light leads near white; outside clear. Its hue only, low in chroma (11-04: "更加成熟"; the
        /// first drawing's saturated cells read loud), and no hill shading or texture.
        /// </summary>
        static Picture Paint(Nest nest, float perCell)
        {
            PathField f = nest.field;
            var pic = new Picture { name = "HoneyMap " + nest.biome + " " + perCell, ppu = perCell / nest.R };
            float ppu = pic.ppu;
            int w = pic.w = Mathf.CeilToInt(2f * (nest.apothem + 1.5f) * ppu), h = pic.h = Mathf.CeilToInt(2f * (nest.R + 1.5f) * ppu);
            pic.halfX = w / ppu / 2f;
            pic.halfZ = h / ppu / 2f;
            float hue = nest.hue / 360f;
            Color ground = Color.HSVToRGB(hue, 0.28f, 0.2f), bank = Color.HSVToRGB(hue, 0.26f, 0.3f), floor = Color.HSVToRGB(hue, 0.2f, 0.62f), light = Color.HSVToRGB(hue, 0.06f, 0.95f);
            float bankWidth = (float)f.Cfg.GroundBankWidth;
            var line = new float[w * h];
            Stroke(line, w, h, pic, nest.journey, LineHalf);
            var px = new Color32[w * h];
            for (int j = 0; j < h; j++)
            {
                float z = -pic.halfZ + (j + 0.5f) / ppu;
                for (int i = 0; i < w; i++)
                {
                    float x = -pic.halfX + (i + 0.5f) / ppu;
                    int at = j * w + i;
                    float inside = Mathf.Clamp01(0.5f - (float)f.HexDist(x, -z) * ppu);
                    if (inside <= 0f) { px[at] = Byte(ground, 0f); continue; }   // (clear, in its ground's colour: no dark fringe)
                    float d = (float)f.PathDist(x, -z);
                    float near = 1f - Mathf.Clamp01(d / bankWidth);
                    Color c = Color.Lerp(ground, bank, near * near);
                    c = Color.Lerp(c, floor, Mathf.Clamp01(0.5f - d * ppu));
                    c = Color.Lerp(c, light, line[at] * 0.92f);
                    px[at] = Byte(c, inside);
                }
            }
            pic.pixels = px;
            return pic;
        }

        static Color32 Byte(Color c, float a) =>
            new Color32((byte)Mathf.RoundToInt(Mathf.Clamp01(c.r) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.g) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(c.b) * 255f), (byte)Mathf.RoundToInt(Mathf.Clamp01(a) * 255f));

        /// <summary>A line through the points (units of the nest) as coverage of the picture's pixels, its edge smoothed.</summary>
        static void Stroke(float[] cover, int w, int h, Picture pic, List<Vector2> pts, float half)
        {
            float ppu = pic.ppu, r = half * ppu;
            Vector2 Px(Vector2 p) => new Vector2((p.x + pic.halfX) * ppu, (p.y + pic.halfZ) * ppu);
            for (int s = 1; s < pts.Count; s++)
            {
                Vector2 a = Px(pts[s - 1]), b = Px(pts[s]), ab = b - a;
                float len2 = Mathf.Max(ab.sqrMagnitude, 1e-6f);
                int i0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.x, b.x) - r - 1)), i1 = Mathf.Min(w - 1, Mathf.CeilToInt(Mathf.Max(a.x, b.x) + r + 1));
                int j0 = Mathf.Max(0, Mathf.FloorToInt(Mathf.Min(a.y, b.y) - r - 1)), j1 = Mathf.Min(h - 1, Mathf.CeilToInt(Mathf.Max(a.y, b.y) + r + 1));
                for (int j = j0; j <= j1; j++)
                for (int i = i0; i <= i1; i++)
                {
                    var p = new Vector2(i + 0.5f, j + 0.5f);
                    float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                    float c = Mathf.Clamp01(r + 0.5f - (a + ab * t - p).magnitude);
                    if (c > cover[j * w + i]) cover[j * w + i] = c;
                }
            }
        }
    }
}

using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The particle nest (world "lab:path-012@blue"), chapter one, an experiment toward the texture of Marshmallow
    /// Laser Feast's "Dream". One nest, its canyons narrowed into passages (labWidth), made only of dense pale
    /// points (ParticleLook, replace mode), in a darker, bluer night with golden motes. Warm specks lie on the floor
    /// from where the ant starts through the nest to its way out, a light gate (no heart: "不要用心脏的概念了"), and
    /// living pulses run along them ahead of the ant (GuideTrail). A chapter card opens it, one quiet goal at a time
    /// sits top left, and reaching the way out closes the chapter. (Heart, in the code: the way's end.)
    /// </summary>
    public class LabNest
    {
        readonly World world;
        readonly Region region;
        readonly RuntimeRefs refs;
        GuideTrail.Route route;
        MeshRenderer trail;
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        Vector2 heart;
        const float HeartRadius = 2.6f;
        const float PulseSpeed = 3.6f, PulsePeriod = 8f;   // the guiding pulses: units/s, and units apart (one every 2.2 s)
        int beat;              // 0 walking in, 1 a plant lit on the way, 2 the way out reached
        float cardIn = 1.5f;   // the chapter card comes a moment after the nest appears
        bool begun;
        float shownD = -1;   // the ant's distance to the heart along the way, as the pulses on the floor are shown

        public Vector2 Heart => heart;
        /// <summary>The main glowing track, from where the ant starts to the way out.</summary>
        public List<Vector2> Track => route?.mainFull;
        /// <summary>All the glowing tracks (the main one and the other ways in).</summary>
        public List<List<Vector2>> Tracks
        {
            get
            {
                var all = new List<List<Vector2>>();
                if (route != null) foreach (var tr in route.tracks) all.Add(tr.pts);
                return all;
            }
        }
        public bool Done => beat >= 2;
        public GuideTrail.Route Route => route;
        public float WalkHeight(Vector2 p) => world.WalkHeight(p.x, p.y);
        /// <summary>True when p is on the open floor of the nest, at least margin from its walls.</summary>
        public bool OnFloor(Vector2 p, float margin) => world.HexDist(p.x, p.y) < -1f && world.Dist(p.x, p.y) < -margin;
        /// <summary>The things left beside the way (id, where on the floor, and the point of the way beside it).</summary>
        public readonly List<(string id, Vector2 at, Vector2 from)> Objects = new List<(string, Vector2, Vector2)>();

        // rings spreading over the floor now and then (NestField.cginc): from no step in particular, at their own
        // irregular times and places round the ant, and from a plant its antennae light
        readonly Vector4[] ripples = new Vector4[8];
        int rippleNext;
        float nextRipple = 2f;
        readonly System.Random calm = new System.Random(1701);

        public LabNest(World world, Region region, RuntimeRefs refs)
        {
            this.world = world;
            this.region = region;
            this.refs = refs;
        }

        /// <summary>The way out and the glowing way to it (before the plants grow: its lane is kept clear).</summary>
        public void Plan(Vector2 spawn)
        {
            // the nest as a graph of its canyons
            var adj = new Dictionary<string, List<(WEdge e, string to, float len)>>();
            var pos = new Dictionary<string, Vector2>();
            foreach (WEdge e in world.edges)
            {
                if (e.region != region.index || e.pts.Count < 2) continue;
                float len = 0;
                for (int i = 1; i < e.pts.Count; i++) len += (e.pts[i] - e.pts[i - 1]).magnitude;
                Add(adj, e.source, (e, e.target, len));
                Add(adj, e.target, (e, e.source, len));
                pos[e.source] = e.pts[0];
                pos[e.target] = e.pts[e.pts.Count - 1];
            }
            if (pos.Count == 0) return;
            // distances along the canyons from the node nearest where the ant starts
            string start = null;
            float best = float.MaxValue;
            foreach (var kv in pos) { float d = (kv.Value - spawn).sqrMagnitude; if (d < best) { best = d; start = kv.Key; } }
            var dist = new Dictionary<string, float> { [start] = 0 };
            var open = new List<string> { start };
            var done = new HashSet<string>();
            while (open.Count > 0)
            {
                int bi = 0;
                for (int i = 1; i < open.Count; i++) if (dist[open[i]] < dist[open[bi]]) bi = i;
                string u = open[bi];
                open.RemoveAt(bi);
                if (!done.Add(u) || !adj.TryGetValue(u, out var list)) continue;
                foreach (var (_, v, len) in list)
                {
                    float d = dist[u] + len;
                    if (dist.TryGetValue(v, out float old) && old <= d) continue;
                    dist[v] = d;
                    open.Add(v);
                }
            }
            // the way leads through the nest and out, to the exit farthest along the canyons from where the ant starts,
            // where a light gate stands (the user, 2026-10-27: "不要用心脏的概念了"; landmarks beside the way, its light
            // leading to the way out): no chamber it ends in, nothing standing at its end. (Heart keeps its name: the
            // way's end.) A nest with no exit: its deepest inner dead end, as before
            WEdge main = null;
            bool forward = false;
            float far = -1f;
            foreach (WExit ex in world.exits)
            {
                if (ex.region != region.index) continue;
                var at = new Vector2(ex.x, ex.z);
                // the canyon it runs out along (the exit is one of its points), and that canyon's end beyond the frame
                WEdge edge = null;
                float bd = 9f;
                foreach (WEdge e in world.edges)
                {
                    if (e.region != region.index || e.pts.Count < 2) continue;
                    foreach (Vector2 q in e.pts) { float d2 = (q - at).sqrMagnitude; if (d2 < bd) { bd = d2; edge = e; } }
                }
                if (edge == null) continue;
                Vector2 s = edge.pts[0], t = edge.pts[edge.pts.Count - 1];
                bool fwd = world.HexDist(t.x, t.y) > world.HexDist(s.x, s.y);
                if (!dist.TryGetValue(fwd ? edge.target : edge.source, out float d) || d <= far) continue;
                far = d;
                main = edge;
                forward = fwd;
                heart = at;
            }
            if (main == null)
            {
                WNode endNode = null;
                foreach (WNode n in world.nodes)
                {
                    if (n.region != region.index || !n.inside || !dist.ContainsKey(n.id)) continue;
                    bool deadEnd = n.degree == 1;
                    if (endNode == null || (deadEnd && (endNode.degree != 1 || dist[n.id] > dist[endNode.id]))
                        || (!deadEnd && endNode.degree != 1 && n.clear > endNode.clear)) endNode = n;
                }
                if (endNode == null) return;
                heart = new Vector2(endNode.x, endNode.z);
                foreach (var (e, _, _) in adj[endNode.id]) { main = e; break; }
                if (main == null) return;
                forward = main.target == endNode.id;
                far = dist[endNode.id];
            }
            route = GuideTrail.Plan(world, region, main, forward, spawn, heart, HeartRadius, 9001);
            GuideTrail.Reserve(world, region, route);
            Debug.Log($"[AntGarden] particle nest: the way leads to ({heart.x:F0}, {heart.y:F0}), " + (world.exits.Exists(x => x.region == region.index) ? "its way out" : "its deepest dead end (no exit)")
                + $", {far:F0} units of canyon from where the ant starts");
        }

        static void Add(Dictionary<string, List<(WEdge, string, float)>> adj, string key, (WEdge, string, float) v)
        {
            if (!adj.TryGetValue(key, out var list)) adj[key] = list = new List<(WEdge, string, float)>();
            list.Add(v);
        }

        /// <summary>
        /// Things left in the nest besides its plants, so its forest is not only plants (before the plants grow, so
        /// they keep clear of them): relics of the world above at ant scale, a teacup sunk into the bank, a key lying
        /// beside the way, stacks of coins, a button sunk flat into a chamber floor, and a temple spire and its cells.
        /// Each beside the way (never on it), alternating sides along it; made of points like everything else.
        /// </summary>
        public void PlaceObjects(Transform parent)
        {
            List<Vector2> track = route?.mainTrack;
            if (track == null || track.Count < 4) return;
            var root = new GameObject("NestObjects").transform;
            root.SetParent(parent, false);
            float length = 0;
            var along = new List<float> { 0 };
            for (int i = 1; i < track.Count; i++) along.Add(length += (track[i] - track[i - 1]).magnitude);
            // (id, size: its longest side, where along the way, which side; walkable: ants climb onto it)
            var rng = new System.Random(5150);
            float U(float a, float b) => a + (b - a) * (float)rng.NextDouble();
            string what = (NestTheme.Current ?? NestTheme.Cyan).objects;
            // (the kiln's burnt log lies along its model's z, its closed end to +z, the view it was made from, its carved open
            // end to -z: turned this far, the open end looks at the way slantwise, so its charred side shows as well)
            const float EmberLogOpen = 150f;
            var plan = new List<(string id, float size, float at, int side, bool walk, bool flat)>();
            if (what == "relics")
                plan.AddRange(new (string, float, float, int, bool, bool)[]
                {
                    ("relic_coins", 1.5f, 0.1f, 1, true, false),
                    ("temple_cells", 1.3f, 0.2f, -1, false, false),
                    ("relic_teacup", 2.8f, 0.32f, 1, false, false),
                    ("relic_coins", 1.2f, 0.44f, -1, true, false),
                    ("relic_key", 2.8f, 0.56f, 1, true, false),
                    ("temple_spire", 4.6f, 0.68f, -1, false, false),
                    ("temple_cells", 1.1f, 0.78f, 1, false, false),
                    ("relic_button", 2.2f, 0.88f, -1, true, true),
                });
            else if (what == "giant")
                plan.AddRange(new (string, float, float, int, bool, bool)[]
                {
                    // the giant's relics (azure): its teacup, coins, key and button, nothing of the temple's
                    ("relic_coins", 1.5f, 0.1f, 1, true, false),
                    ("relic_teacup", 2.8f, 0.3f, -1, false, false),
                    ("relic_key", 2.8f, 0.5f, 1, true, false),
                    ("relic_coins", 1.2f, 0.66f, -1, true, false),
                    ("relic_button", 2.2f, 0.86f, -1, true, true),
                });
            else if (what == "machine")
                plan.AddRange(new (string, float, float, int, bool, bool)[]
                {
                    // the verdigris city: the giant's coins, keys and a button left in the nest, green with verdigris
                    // (its gears are its own things: SolidThings)
                    ("relic_coins", 1.6f, 0.12f, 1, true, false),
                    ("relic_key", 3.0f, 0.3f, -1, true, false),
                    ("relic_coins", 1.3f, 0.48f, -1, true, false),
                    ("relic_button", 2.4f, 0.66f, 1, true, true),
                    ("relic_key", 2.6f, 0.82f, 1, true, false),
                });
            else if (what == "moss")
                plan.AddRange(new (string, float, float, int, bool, bool)[]
                {
                    // the moss forest's landmarks beside the way (no heart: "不要用心脏的概念了"), its own Tripo plants as
                    // large as the ant's landmarks should be: a liverwort colony with its parasols a third of the way in,
                    // a grove of moss capsules hanging like lamps halfway, pixie-cup lichens with their scarlet caps two
                    // thirds in, and a smaller one of each near the way in and the way out (02_Concepts/Moss_v1); the
                    // capsules three side by side, a grove (one alone read thin, review k12)
                    ("pixie_cup_lichen", 2.4f, 0.12f, 1, false, false),
                    ("liverwort_parasols", 4.0f, 0.33f, 1, false, false),
                    ("moss_capsules", 2.2f, 0.5f, -1, false, false),
                    ("moss_capsules", 1.8f, 0.49f, -1, false, false),
                    ("moss_capsules", 1.6f, 0.51f, -1, false, false),
                    ("pixie_cup_lichen", 3.2f, 0.66f, -1, false, false),
                    ("liverwort_parasols", 3.0f, 0.85f, 1, false, false),
                });
            else if (what == "ember")
                plan.AddRange(new (string, float, float, int, bool, bool)[]
                {
                    // the kiln temple (02_Concepts/Kiln_Redesign_v1): its temple a cluster of charred stumps grown together
                    // like a cathedral termite mound (the user: "神殿的建筑去找一下pinterest上的参考，现在这个太简单了";
                    // Docs/参考_神殿/分析.md), in the chamber where the way forks, its split side and its carved galleries to
                    // the way (a landmark, not the goal: k1 put it a little before halfway, where the narrow way left it no
                    // room on the floor, so it stood up on the rim, far and small); a burnt log fallen across the way from
                    // it, open end to the way (k2 laid it at the temple's foot, between it and the way: it hid the temple);
                    // two smaller pieces of log halfway and at the wide place near the way out. (The temple taller than the
                    // canyon's walls, about 6 high, so it reads as a temple to an ant: at 3.8 it stood half as high as the
                    // earth pillars behind it, k1 plant review)
                    ("char_stump_temple", 6.0f, 0.03f, -1, false, false),
                    ("ember_log", 3.2f, 0.08f, 1, false, false),
                    ("ember_log", 2.6f, 0.45f, 1, false, false),
                    ("ember_log", 3.0f, 0.86f, -1, false, false),
                });
            else if (what == "amber")
                plan.AddRange(new (string, float, float, int, bool, bool)[]
                {
                    // the amber nest's landmarks beside the way (no heart): a fallen pine cone, giant to an ant, beads and
                    // tears of resin on its scales (02_Concepts/Amber_v1_候选/A), near the way in; a smaller one further on
                    // (planned a third of the way in, at this size there was room for it only up on the rim, out of sight)
                    ("resin_cone", 5.4f, 0.06f, 1, false, false),   // (at 4.2, in the game's view, only twice the ant: not yet a landmark)
                    ("resin_cone", 3.6f, 0.74f, -1, false, false),
                });
            else if (what == "temple")
            {
                // the temple: clay spires in pairs down both sides of the way, a carved arch between each pair facing
                // it, and clay cells at the foot of the banks (02_Concepts/Particle_Styles_v1/temple_*_v2.png); the
                // finer models made for it (temple_*_v2) where they are built, the ruin's otherwise
                string Fine(string id) => refs.Plant(id + "_v2")?.prefab != null ? id + "_v2" : id;
                string spire = Fine("temple_spire"), arch = Fine("temple_arch"), cells = Fine("temple_cells");
                int archSide = 1;
                for (float f = 0.06f; f <= 0.86f; f += 0.1f)
                {
                    plan.Add((spire, U(3.8f, 6f), f, -1, false, false));
                    plan.Add((spire, U(3.8f, 6f), f + 0.02f, 1, false, false));
                    plan.Add((arch, U(3.4f, 4.4f), f + 0.05f, archSide, false, false));
                    archSide = -archSide;
                }
                foreach (float f in new[] { 0.12f, 0.22f, 0.33f, 0.45f, 0.57f, 0.68f, 0.8f })
                    plan.Add((cells, U(1.0f, 1.6f), f, f < 0.5f ? 1 : -1, false, false));
                // small spires among them, like chimneys (the same finer model, smaller)
                int small = -1;
                foreach (float f in new[] { 0.16f, 0.28f, 0.4f, 0.52f, 0.64f, 0.76f })
                {
                    plan.Add((spire, U(1.5f, 2.5f), f, small, false, false));
                    small = -small;
                }
            }
            int placed = 0;
            var skipped = new List<string>();
            foreach (var o in plan)
            {
                PlantRef p = refs.Plant(o.id);
                if (p == null || p.prefab == null) continue;
                float longest = Mathf.Max(1f, Mathf.Max(p.size.x, p.size.z));
                float scale = o.size / longest, r = Mathf.Max(0.3f, p.footprintRadius * scale);
                // the place along the way, and the way's direction there
                float want = o.at * length;
                int k = 1;
                while (k < track.Count - 2 && along[k] < want) k++;
                Vector2 a = track[k], dir = (track[k + 1] - track[k - 1]).normalized, n = new Vector2(-dir.y, dir.x) * o.side;
                // a place beside the way, on fairly flat floor (a chamber, a wider stretch), never on the way itself:
                // looked for near where it is planned, the nearer the better
                bool found = false;
                Vector2 c = a;
                float bestScore = float.MaxValue;
                for (int dk = -40; dk <= 40; dk += 2)
                {
                    int kk = Mathf.Clamp(k + dk, 1, track.Count - 2);
                    Vector2 q = track[kk], qd = (track[kk + 1] - track[kk - 1]).normalized;
                    for (int sideTry = 0; sideTry < 2; sideTry++)
                    {
                        Vector2 qn = new Vector2(-qd.y, qd.x) * (sideTry == 0 ? o.side : -o.side);
                        for (float off = r + 0.9f; off < r + 11f; off += 0.35f)
                        {
                            Vector2 cc = q + qn * off;
                            // on the canyon floor, or up on the rim of the plateau looking down into it
                            float dd = world.Dist(cc.x, cc.y);
                            bool rim = dd > world.bankWidth * 1.15f && dd < world.bankWidth * 1.15f + 3f;
                            if (world.HexDist(cc.x, cc.y) > -(r + 1.5f) || (dd > 1f && !rim)) continue;
                            if (world.reserved.Overlaps(cc.x, cc.y, r * 0.95f) || world.colliders.Overlaps(cc.x, cc.y, r * 0.8f)) continue;
                            // flat enough under it: it must not sink into a wall (the dark ground would hide it)
                            float lo = float.MaxValue, hi = float.MinValue;
                            for (int qa = 0; qa < 8; qa++)
                            {
                                float ang = qa * Mathf.PI / 4;
                                float hh = world.Height(cc.x + Mathf.Cos(ang) * r * 0.8f, cc.y + Mathf.Sin(ang) * r * 0.8f);
                                lo = Mathf.Min(lo, hh);
                                hi = Mathf.Max(hi, hh);
                            }
                            if (hi - lo > Mathf.Max(0.45f, 0.2f * o.size)) continue;
                            float score = Mathf.Abs(dk) * 0.6f + off + (sideTry == 0 ? 0 : 1.5f) + (rim ? 4f : 0);
                            if (score < bestScore) { bestScore = score; c = cc; a = q; dir = qd; found = true; }
                        }
                    }
                }
                if (!found) { skipped.Add(o.id); continue; }
                float yaw = o.id == "relic_key" ? Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f
                    : o.id.StartsWith("temple_arch") ? Mathf.Atan2(a.x - c.x, a.y - c.y) * Mathf.Rad2Deg   // an arch faces the way
                    : o.id == "ember_log" ? Mathf.Atan2(a.x - c.x, a.y - c.y) * Mathf.Rad2Deg + EmberLogOpen + U(-12f, 12f)   // its open end to the way
                    : o.id == "char_stump_temple" ? Mathf.Atan2(a.x - c.x, a.y - c.y) * Mathf.Rad2Deg   // its split side (its model's front) to the way
                    : o.id == "resin_cone" ? Mathf.Atan2(dir.x, dir.y) * Mathf.Rad2Deg + 90f + U(-15f, 15f)   // lying along the way, its side to it (end on, its scales read as a gold flower)
                    : (float)rng.NextDouble() * 360f;
                float y = world.Height(c.x, c.y);
                for (int q = 0; q < 6; q++)
                {
                    float ang = q * Mathf.PI / 3;
                    y = Mathf.Min(y, world.Height(c.x + Mathf.Cos(ang) * r, c.y + Mathf.Sin(ang) * r));
                }
                y -= o.flat ? scale * 0.75f : 0.06f * scale;
                // the teacup lies a little tipped, sunk into the floor
                Quaternion rot = o.id == "relic_teacup" ? Quaternion.Euler(14f, yaw, -9f) : Quaternion.Euler(0, yaw, 0);
                var go = Object.Instantiate(p.prefab, new Vector3(c.x, y, c.y), rot, root);
                go.transform.localScale = Vector3.one * scale;
                // (the moss forest's landmarks are living plants: drawn as its plants are, with their markings, not as relics)
                // (the kiln's burnt wood glows in its own ember red: in the relics' pale amber its embers read white, k4)
                Color objGlow = what == "ember" && NestTheme.Current != null ? NestTheme.Current.glowDeep : new Color(1f, 0.82f, 0.5f);
                world.plantInstances.Add(new PlantInstance { plant = p, t = go.transform, hue = 0, sat = 1, glow = objGlow, scan = new Color(1f, 0.93f, 0.8f), relic = what != "moss" });
                world.placements.Add(new Placement { plant = o.id, region = region.index, x = c.x, z = c.y, height = scale });
                world.reserved.Add(new Circle(c.x, c.y, r + 0.4f, 1f));
                // the key, the coins and the button: ants climb onto them; the rest block
                Bounds bounds = new Bounds(go.transform.position, Vector3.zero);
                foreach (Renderer mr in go.GetComponentsInChildren<Renderer>()) bounds.Encapsulate(mr.bounds);
                float top = Mathf.Max(0.05f, bounds.max.y - world.Height(c.x, c.y));
                float cs = Mathf.Cos(-yaw * Mathf.Deg2Rad), sn = Mathf.Sin(-yaw * Mathf.Deg2Rad);
                if (p.circles != null && p.circles.Length > 0 && p.circles.Length <= 16)
                    foreach (Vector3 ci in p.circles)
                    {
                        float cx = ci.x * cs - ci.y * sn, cz = ci.x * sn + ci.y * cs;
                        if (o.walk) world.steps.Add(new Circle(c.x + cx * scale, c.y + cz * scale, ci.z * scale * 1.35f, top * 0.9f));
                        else world.colliders.Add(new Circle(c.x + cx * scale, c.y + cz * scale, ci.z * scale, scale));
                    }
                else if (o.walk) world.steps.Add(new Circle(c.x, c.y, r * 1.05f, top * 0.9f));
                else world.colliders.Add(new Circle(c.x, c.y, r, scale));
                Objects.Add((o.id, c, a));
                if (what == "amber") Debug.Log($"[AntGarden] particle nest: {o.id} at ({c.x:F1}, {c.y:F1}), {o.size:F1} long, planned {o.at:P0} along the way");
                // the temple's spires, arches and cells answer an antenna as its relics did in the ruin: they light up
                if ((what == "temple" || what == "moss" || what == "ember" || what == "amber") && world.glow != null) world.glow.AddTouchable(new Vector3(c.x, y, c.y), r, top, (NestTheme.Current ?? NestTheme.Cyan).glow[0]);
                placed++;
            }
            Debug.Log($"[AntGarden] particle nest: {placed} things left beside the way" + (skipped.Count > 0 ? $" (no room for {string.Join(", ", skipped)})" : ""));
        }

        /// <summary>The warm specks on the floor (after the plants have grown), and the field the guiding pulses read.</summary>
        public void Build(Transform parent, float gain, float density)
        {
            if (route == null || refs.guideTrail == null) return;
            trail = GuideTrail.Build(world, region, route, refs.guideTrail, (NestTheme.Current ?? NestTheme.Cyan).trail, gain * 1.8f, density, parent, 77);
            // the specks' pulses keep step with the chevrons on the floor
            trail.GetPropertyBlock(block);
            block.SetFloat("_PulseSpeed", PulseSpeed);
            block.SetFloat("_PulseEvery", PulsePeriod / PulseSpeed);
            block.SetFloat("_PulseReach", 15f);
            trail.SetPropertyBlock(block);
            BakeTrackField();
        }

        /// <summary>
        /// For the guiding pulses (Shaders/Chevron.cginc): near every glowing way, on a grid of 1/5 unit, the distance
        /// along it to the heart and the signed distance off its middle (nearest way wins); -1 where there is none.
        /// </summary>
        Texture2D field;   // kept here: a texture only the shaders know of would be unloaded as unused
        Color[] fieldData;
        Vector4 fieldRect;
        int fieldW, fieldH;
        float logIn = 4f;

        void BakeTrackField()
        {
            const float cell = 0.2f, band = 1.6f;
            float x0 = world.minX - 1, z0 = world.minZ - 1;
            int w = Mathf.Min(2048, Mathf.CeilToInt((world.maxX - world.minX + 2) / cell));
            int h = Mathf.Min(2048, Mathf.CeilToInt((world.maxZ - world.minZ + 2) / cell));
            var data = new Color[w * h];
            var best = new float[w * h];
            for (int i = 0; i < data.Length; i++) { data[i] = new Color(-1, 0, 0, 0); best[i] = float.MaxValue; }
            foreach (var (pts, ds, _) in route.tracks)
                for (int k = 0; k + 1 < pts.Count; k++)
                {
                    Vector2 a = pts[k], b = pts[k + 1], ab = b - a;
                    float len2 = ab.sqrMagnitude;
                    if (len2 < 1e-8f) continue;
                    int i0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.x, b.x) - band - x0) / cell)), i1 = Mathf.Min(w - 1, Mathf.CeilToInt((Mathf.Max(a.x, b.x) + band - x0) / cell));
                    int j0 = Mathf.Max(0, Mathf.FloorToInt((Mathf.Min(a.y, b.y) - band - z0) / cell)), j1 = Mathf.Min(h - 1, Mathf.CeilToInt((Mathf.Max(a.y, b.y) + band - z0) / cell));
                    for (int j = j0; j <= j1; j++)
                    for (int i = i0; i <= i1; i++)
                    {
                        var p = new Vector2(x0 + (i + 0.5f) * cell, z0 + (j + 0.5f) * cell);
                        float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / len2);
                        Vector2 off = p - (a + ab * t);
                        float d2 = off.sqrMagnitude;
                        if (d2 > band * band || d2 >= best[j * w + i]) continue;
                        best[j * w + i] = d2;
                        float side = Mathf.Sign(ab.x * off.y - ab.y * off.x);
                        data[j * w + i] = new Color(Mathf.Lerp(ds[k], ds[k + 1], t), side * Mathf.Sqrt(d2), 0, 0);
                    }
                }
            fieldData = data;
            fieldW = w;
            fieldH = h;
            fieldRect = new Vector4(x0, z0, w * cell, h * cell);
            field = new Texture2D(w, h, TextureFormat.RGFloat, false, true)
                { filterMode = FilterMode.Bilinear, wrapMode = TextureWrapMode.Clamp, hideFlags = HideFlags.DontUnloadUnusedAsset };
            field.SetPixels(data);
            field.Apply(false, true);
            Shader.SetGlobalTexture("_TrackField", field);
            Shader.SetGlobalVector("_TrackRect", new Vector4(x0, z0, w * cell, h * cell));
            Shader.SetGlobalFloat("_ChevronOn", 0f);   // the floor's pulses: off (the pheromones lead instead)
            Shader.SetGlobalFloat("_ChevronSpeed", PulseSpeed);
            Shader.SetGlobalFloat("_ChevronPeriod", PulsePeriod);
            Shader.SetGlobalFloat("_ChevronDebug", Game.Arg("-chevdebug") is string dbg ? (dbg == "2" ? 2f : 1f) : 0f);
            Shader.SetGlobalVector("_ChevronColor", new Vector4(0.6f, 0.95f, 1f, 1.5f));   // pale cyan, of a piece with the nest
        }

        /// <summary>A plant lit on the way: the goal moves on.</summary>
        public void OnPlantLit(Hud hud)
        {
            if (beat != 0) return;
            beat = 1;
            hud.SetObjective(Loc.T("继续跟着光走，它通向出口", "Go on: the light leads to the way out"));
        }

        /// <summary>The nest's living field: the wind, and no rings yet.</summary>
        public void StartField()
        {
            Shader.SetGlobalVectorArray("_Ripples", ripples);
            Shader.SetGlobalVector("_NestWind", new Vector4(0.045f, 0.35f, 6f, 0));
            // a few grains of the floor glow faintly and breathe (NestField.cginc), in the cool glow colour
            Color grain = (NestTheme.Current ?? NestTheme.Cyan).grainGlow.linear;
            Shader.SetGlobalVector("_GrainGlow", new Vector4(grain.r, grain.g, grain.b, 0.35f * (NestTheme.Current ?? NestTheme.Cyan).grainGain));
        }

        /// <summary>Now and then, at irregular times, a faint ring spreads over the floor somewhere round the ant (never at
        /// its feet, never in step with it), as if the ground breathed.</summary>
        void Breathe(float dt, Vector3 ant)
        {
            if ((nextRipple -= dt) > 0) return;
            nextRipple = 1.6f + 5f * (float)(calm.NextDouble() * calm.NextDouble());
            float a = (float)calm.NextDouble() * 6.283f, r = 2.5f + 7f * (float)calm.NextDouble();
            Ripple(new Vector3(ant.x + Mathf.Cos(a) * r, 0, ant.z + Mathf.Sin(a) * r), 0.18f + 0.22f * (float)calm.NextDouble());
        }

        void Ripple(Vector3 at, float strength)
        {
            ripples[rippleNext] = new Vector4(at.x, Time.time, at.z, strength);
            rippleNext = (rippleNext + 1) % ripples.Length;
            Shader.SetGlobalVectorArray("_Ripples", ripples);
        }

        /// <summary>A plant the antennae lit: a ring spreads from it.</summary>
        public void RippleAt(Vector3 at) => Ripple(at, 0.5f);

        public void Tick(float dt, Vector3 ant, Hud hud)
        {
            Breathe(dt, ant);
            if (!begun && (cardIn -= dt) <= 0)
            {
                begun = true;
                // the same chapter, with the same words, in every nest (each colour of the room's lights opens its own)
                hud.ShowChapter(Loc.T("第一章 · 入口", "Chapter One · The Way In"), Loc.T("跟着地上的光，穿过蚁穴", "Follow the light on the ground through the nest"));
                hud.SetObjective(Loc.T("跟着地上的光走", "Follow the light on the ground"));
            }
            float playerD = route != null ? GuideTrail.PlayerD(route, ant) : -1;
            // eased over a few frames (a change of track, or the heart's ring, would otherwise move the pulses at once)
            shownD = playerD < 0 || shownD < 0 || Mathf.Abs(playerD - shownD) > 4f ? playerD : shownD + (playerD - shownD) * (1f - Mathf.Exp(-dt / 0.12f));
            playerD = shownD;
            if (trail != null)
            {
                trail.GetPropertyBlock(block);
                block.SetFloat("_PlayerD", playerD);
                trail.SetPropertyBlock(block);
            }
            Shader.SetGlobalFloat("_TrailPlayerD", playerD);   // the guiding pulses on the floor run ahead from here
            if (logIn > 0 && (logIn -= dt) <= 0 && fieldData != null)
            {
                int i = Mathf.Clamp((int)((ant.x - fieldRect.x) / fieldRect.z * fieldW), 0, fieldW - 1), j = Mathf.Clamp((int)((ant.z - fieldRect.y) / fieldRect.w * fieldH), 0, fieldH - 1);
                Color f = fieldData[j * fieldW + i];
                Debug.Log($"[AntGarden] guiding pulses: the ant {playerD:F1} from the heart, the field under it {f.r:F1} along, {f.g:F2} off the middle, chevrons {Shader.GetGlobalFloat("_ChevronOn")}");
            }
            if (begun && beat < 2 && route != null && (new Vector2(ant.x, ant.z) - heart).magnitude < HeartRadius + 1.5f)
            {
                beat = 2;
                hud.ShowChapter(Loc.T("第一章 · 完成", "Chapter One · Done"), Loc.T("下一章：育儿室（还没有开放）", "Next: the Nursery (not open yet)"));
                hud.SetObjective(Loc.T("第一章完成", "Chapter one done"));
                Voices.Chime(ant + Vector3.up, 1.3f);
            }
        }
    }
}

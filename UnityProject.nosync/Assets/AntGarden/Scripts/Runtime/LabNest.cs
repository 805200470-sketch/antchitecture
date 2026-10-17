using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The particle nest (world "lab:path-012@blue"), chapter one, an experiment toward the texture of Marshmallow
    /// Laser Feast's "Dream". One nest, its canyons narrowed into passages (labWidth), made only of dense pale
    /// points (ParticleLook, replace mode), in a darker, bluer night with golden motes. Warm specks lie on the floor
    /// from the way in to the nest's heart, its deepest chamber, and living pulses run along them ahead of the ant
    /// (GuideTrail). A chapter card opens it, one quiet goal at a time sits top left, and reaching the heart closes
    /// the chapter.
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
        int beat;              // 0 walking in, 1 a plant lit on the way, 2 the heart reached
        float cardIn = 1.5f;   // the chapter card comes a moment after the nest appears
        bool begun;

        public Vector2 Heart => heart;
        /// <summary>The main glowing track, from the way in to the heart.</summary>
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

        public LabNest(World world, Region region, RuntimeRefs refs)
        {
            this.world = world;
            this.region = region;
            this.refs = refs;
        }

        /// <summary>The heart and the glowing way to it (before the plants grow: its lane is kept clear).</summary>
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
            // from the node nearest where the ant starts, the deepest inner dead end is the heart
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
            WNode heartNode = null;
            foreach (WNode n in world.nodes)
            {
                if (n.region != region.index || !n.inside || !dist.ContainsKey(n.id)) continue;
                bool deadEnd = n.degree == 1;
                if (heartNode == null || (deadEnd && (heartNode.degree != 1 || dist[n.id] > dist[heartNode.id]))
                    || (!deadEnd && heartNode.degree != 1 && n.clear > heartNode.clear)) heartNode = n;
            }
            if (heartNode == null) return;
            heart = new Vector2(heartNode.x, heartNode.z);
            // the way in ends at the heart: any canyon that reaches it
            WEdge main = null;
            foreach (var (e, _, _) in adj[heartNode.id]) { main = e; break; }
            if (main == null) return;
            route = GuideTrail.Plan(world, region, main, main.target == heartNode.id, spawn, heart, HeartRadius, 9001);
            GuideTrail.Reserve(world, region, route);
            Debug.Log($"[AntGarden] particle nest: heart at ({heart.x:F0}, {heart.y:F0}), {dist[heartNode.id]:F0} units of canyon from the way in");
        }

        static void Add(Dictionary<string, List<(WEdge, string, float)>> adj, string key, (WEdge, string, float) v)
        {
            if (!adj.TryGetValue(key, out var list)) adj[key] = list = new List<(WEdge, string, float)>();
            list.Add(v);
        }

        /// <summary>The warm specks on the floor (after the plants have grown).</summary>
        public void Build(Transform parent, float gain, float density)
        {
            if (route == null || refs.guideTrail == null) return;
            trail = GuideTrail.Build(world, region, route, refs.guideTrail, new Color(1f, 0.8f, 0.48f), gain * 1.7f, density, parent, 77);
        }

        /// <summary>A plant lit on the way: the goal moves on.</summary>
        public void OnPlantLit(Hud hud)
        {
            if (beat != 0) return;
            beat = 1;
            hud.SetObjective(Loc.T("继续往深处走，找到蚁穴的心脏", "Go on, deeper, to the heart of the nest"));
        }

        public void Tick(float dt, Vector3 ant, Hud hud)
        {
            if (!begun && (cardIn -= dt) <= 0)
            {
                begun = true;
                hud.ShowChapter(Loc.T("第一章 · 入口", "Chapter One · The Way In"), Loc.T("跟着地上的光，找到蚁穴的心脏", "Follow the light on the ground to the heart of the nest"));
                hud.SetObjective(Loc.T("跟着地上的光往深处走", "Follow the light deeper in"));
            }
            if (trail != null)
            {
                trail.GetPropertyBlock(block);
                block.SetFloat("_PlayerD", GuideTrail.PlayerD(route, ant));
                trail.SetPropertyBlock(block);
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

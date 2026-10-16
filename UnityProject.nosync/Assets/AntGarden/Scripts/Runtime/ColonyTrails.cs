using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Pheromone trails on the path graph: the ant-colony algorithm as real colonies run it.
    /// Every path edge carries two pheromones in each direction, "to food" and "to home". Ants out searching
    /// follow "to food" and lay "to home" behind them; ants carrying food follow "to home" and lay "to food".
    /// At a junction an ant picks the next path with probability (tau0 + pheromone)^alpha x straightness, so
    /// it mostly keeps going the way the colony goes, sometimes explores. Pheromone evaporates, so the routes
    /// between the nest (the largest chamber) and the food (the dead ends) that are walked most become the
    /// colony's highways. A short simulation at load time lays the first trails.
    /// </summary>
    public class ColonyTrails
    {
        readonly ColonySettings cfg;
        readonly World world;
        // per edge: [0] to food along the edge, [1] to food against it, [2] to home along, [3] to home against
        readonly Dictionary<WEdge, float[]> level = new Dictionary<WEdge, float[]>();
        readonly Dictionary<string, List<(WEdge e, bool forward)>> outgoing = new Dictionary<string, List<(WEdge, bool)>>();
        readonly Dictionary<string, WNode> nodes = new Dictionary<string, WNode>();
        readonly Dictionary<int, string> home = new Dictionary<int, string>();
        readonly HashSet<string> food = new HashSet<string>();

        public ColonyTrails(World world, ColonySettings cfg, int seed)
        {
            this.world = world;
            this.cfg = cfg;
            foreach (WNode n in world.nodes) nodes[n.id] = n;
            foreach (WEdge e in world.edges)
            {
                if (e.pts == null || e.pts.Count < 2) continue;
                level[e] = new float[4];
                Out(e.source).Add((e, true));
                Out(e.target).Add((e, false));
            }
            foreach (Region r in world.regions)
            {
                // the nest: the roomiest junction chamber; the food: the dead ends inside the hexagon
                WNode best = null;
                foreach (WNode n in world.nodes)
                    if (n.region == r.index && n.inside && n.degree >= 3 && (best == null || n.clear > best.clear)) best = n;
                if (best == null) continue;
                home[r.index] = best.id;
                foreach (WNode n in world.nodes)
                    if (n.region == r.index && n.inside && n.degree == 1 && n.id != best.id) food.Add(n.id);
            }
            PreWarm(new System.Random(seed));
        }

        List<(WEdge e, bool forward)> Out(string node)
        {
            if (!outgoing.TryGetValue(node, out var list)) outgoing[node] = list = new List<(WEdge, bool)>();
            return list;
        }

        static int Slot(bool forward, bool toFood) => (toFood ? 0 : 2) + (forward ? 0 : 1);

        public bool IsFood(string node) => node != null && food.Contains(node);
        public bool IsHome(string node, int region) => node != null && home.TryGetValue(region, out var h) && h == node;

        /// <summary>Pheromone for walking along e (forward = from its source to its target).</summary>
        public float Level(WEdge e, bool forward, bool toFood) => level.TryGetValue(e, out var l) ? l[Slot(forward, toFood)] : 0;

        /// <summary>
        /// An ant has walked e (forward = source to target). Looking back, that way leads to where it came
        /// from: carrying food it marks "to food" there, searching it marks "to home".
        /// </summary>
        public void Lay(WEdge e, bool walkedForward, bool carrying, float amount)
        {
            if (!level.TryGetValue(e, out var l)) return;
            int s = Slot(!walkedForward, carrying);
            l[s] = Mathf.Min(cfg.maxLevel, l[s] + amount);
        }

        public void Evaporate(float dt)
        {
            float k = Mathf.Exp(-cfg.evaporation * dt);
            foreach (var l in level.Values)
                for (int i = 0; i < 4; i++) l[i] *= k;
        }

        /// <summary>Direction of travel at the start of an outgoing edge, on the ground plane.</summary>
        static Vector2 StartDir(WEdge e, bool forward)
        {
            int n = e.pts.Count;
            Vector2 a = forward ? e.pts[0] : e.pts[n - 1];
            Vector2 b = forward ? e.pts[Mathf.Min(2, n - 1)] : e.pts[Mathf.Max(0, n - 3)];
            return (b - a).normalized;
        }

        /// <summary>
        /// The next path at a junction for an ant arriving along `from` heading `inDir`: roulette over
        /// (tau0 + pheromone)^alpha x exp(straightness x cos turn), never straight back unless it is a dead end.
        /// Returns false when the node has no paths at all.
        /// </summary>
        public bool Choose(string node, WEdge from, Vector2 inDir, bool carrying, float alpha, float straightness, System.Random rng,
                           out WEdge next, out bool forward)
        {
            next = null;
            forward = true;
            if (!outgoing.TryGetValue(node, out var opts) || opts.Count == 0) return false;
            int count = 0;
            var w = new float[opts.Count];
            float total = 0;
            for (int i = 0; i < opts.Count; i++)
            {
                if (opts[i].e == from && opts.Count > 1) continue;
                float tau = cfg.tau0 + Level(opts[i].e, opts[i].forward, !carrying);
                float turn = Vector2.Dot(inDir, StartDir(opts[i].e, opts[i].forward));
                w[i] = Mathf.Pow(tau, alpha) * Mathf.Exp(straightness * turn);
                total += w[i];
                count++;
            }
            if (count == 0) return false;
            // now and then an ant ignores the trail and explores
            bool explore = rng.NextDouble() < cfg.explore;
            double k = rng.NextDouble() * (explore ? count : total);
            for (int i = 0; i < opts.Count; i++)
            {
                if (w[i] <= 0) continue;
                k -= explore ? 1 : w[i];
                if (k <= 0 || i == opts.Count - 1) { next = opts[i].e; forward = opts[i].forward; return true; }
            }
            for (int i = opts.Count - 1; i >= 0; i--) if (w[i] > 0) { next = opts[i].e; forward = opts[i].forward; return true; }
            return false;
        }

        /// <summary>
        /// Lay the first trails: virtual ants (no bodies) walk every region's graph at once, before anyone
        /// looks. All regions step together, so evaporation treats them alike.
        /// </summary>
        void PreWarm(System.Random rng)
        {
            var ants = new List<(int region, string node, WEdge from, Vector2 dir, bool carrying)>();
            foreach (var kv in home)
                for (int i = 0; i < cfg.prewarmAnts; i++) ants.Add((kv.Key, kv.Value, null, Vector2.zero, false));
            for (int step = 0; step < cfg.prewarmSteps; step++)
            {
                for (int i = 0; i < ants.Count; i++)
                {
                    var (region, node, from, dir, carrying) = ants[i];
                    if (!Choose(node, from, dir, carrying, cfg.alpha, cfg.straightness, rng, out WEdge e, out bool fwd)) continue;
                    string end = fwd ? e.target : e.source;
                    Lay(e, fwd, carrying, cfg.deposit);
                    bool goal = false;
                    if (!carrying && IsFood(end)) { carrying = true; goal = true; }
                    else if (carrying && IsHome(end, region)) { carrying = false; goal = true; }
                    int n = e.pts.Count;
                    Vector2 arrive = fwd ? (e.pts[n - 1] - e.pts[Mathf.Max(0, n - 3)]) : (e.pts[0] - e.pts[Mathf.Min(n - 1, 2)]);
                    // after a pick-up or drop-off, or at a dead end, the way back is allowed
                    ants[i] = (region, end, goal || Out(end).Count == 1 ? null : e, arrive.normalized, carrying);
                }
                Evaporate(cfg.prewarmStepSeconds);
            }
        }

        /// <summary>For the log: per region the nest, food, edges and pheromone, and the busiest edges.</summary>
        public string Summary()
        {
            var parts = new List<string>();
            foreach (Region r in world.regions)
            {
                int edges = 0, foodHere = 0;
                float sum = 0;
                foreach (var kv in level) if (kv.Key.region == r.index) { edges++; sum += kv.Value[0] + kv.Value[1] + kv.Value[2] + kv.Value[3]; }
                foreach (string f in food) if (nodes.TryGetValue(f, out var nd) && nd.region == r.index) foodHere++;
                string h = home.TryGetValue(r.index, out var hid) ? hid : "none";
                int deg = home.ContainsKey(r.index) && outgoing.TryGetValue(hid, out var ol) ? ol.Count : 0;
                parts.Add($"r{r.index}: home {h} ({deg} paths), food {foodHere}, edges {edges}, pheromone {sum:F0}");
            }
            return string.Join(" | ", parts) + " | " + Busiest();
        }

        string Busiest()
        {
            var list = new List<(float, string)>();
            foreach (var kv in level) list.Add((kv.Value[0] + kv.Value[1] + kv.Value[2] + kv.Value[3], kv.Key.id));
            list.Sort((a, b) => b.Item1.CompareTo(a.Item1));
            float sum = 0;
            foreach (var x in list) sum += x.Item1;
            var top = new List<string>();
            for (int i = 0; i < Mathf.Min(5, list.Count); i++) top.Add($"{list[i].Item2} {list[i].Item1:F1}");
            return $"busiest: {string.Join(", ", top)}";
        }
    }
}

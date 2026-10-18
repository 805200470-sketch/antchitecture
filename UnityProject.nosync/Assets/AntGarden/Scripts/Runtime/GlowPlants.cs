using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The glowing plants as living things. Each one gets its own brightness when it is placed (some barely
    /// glow, a few are bright) and its own breathing rhythm; when the player's ant touches one with an antenna
    /// tip (the walk sweeps them forward; Space reaches out on purpose), it flares up many times brighter,
    /// lights the ground around it for a moment and passes a softer flare on to its neighbours like a ripple,
    /// then slowly fades back.
    /// </summary>
    public class GlowPlants
    {
        class Glow
        {
            public Vector3 pos;
            public float reach, height, amount, t, flash;
            public Color color;
            public MeshRenderer[] renderers;
            public bool active, touching;
            public int story = -1;          // a memory plant: which preset story it keeps (-1: none)
        }

        /// <summary>A memory plant was touched: which story, and where (for the line and its chime).</summary>
        public System.Action<int, Vector3> onStory;
        public System.Action<Vector3> onFlare;   // an antenna lit a plant (for its sound)
        int stories;

        const float Cell = 4f;
        readonly GlowSettings cfg;
        readonly List<Glow> all = new List<Glow>();
        readonly Dictionary<long, List<Glow>> cells = new Dictionary<long, List<Glow>>();
        readonly List<Glow> active = new List<Glow>();
        readonly List<Glow> touching = new List<Glow>();
        readonly List<Glow> near = new List<Glow>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        readonly Light[] lights;
        readonly System.Random rng;

        public int Count => all.Count;

        /// <summary>The flares lit now, brightest first (as many as fit): where (xyz), and how far round and how high
        /// they reach and how bright they are (x, y, z), for the particle look. Returns how many.</summary>
        public int Flares(Vector4[] pos, Vector4[] info)
        {
            int n = 0;
            foreach (var g in active)
            {
                if (n >= pos.Length) break;
                if (g.flash <= 0.01f) continue;
                pos[n] = g.pos;
                info[n] = new Vector4(Mathf.Max(g.reach, g.height * 0.45f) + 0.3f, g.height, g.flash, 0);
                n++;
            }
            return n;
        }

        public GlowPlants(GlowSettings cfg, Transform parent, int seed)
        {
            this.cfg = cfg;
            rng = new System.Random(seed);
            lights = new Light[Mathf.Max(0, cfg.lights)];
            for (int i = 0; i < lights.Length; i++)
            {
                var l = new GameObject("GlowFlareLight").AddComponent<Light>();
                l.transform.SetParent(parent, false);
                l.type = LightType.Point;
                l.shadows = LightShadows.None;
                l.enabled = false;
                lights[i] = l;
            }
        }

        static long Key(int cx, int cz) => ((long)cx << 32) ^ (uint)cz;

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        /// <summary>
        /// A newly placed plant: give it its own brightness, breathing phase and glow colour, and remember it
        /// if it glows at all (so it can be touched). Returns nothing; the renderers' property blocks are set here.
        /// </summary>
        public void Add(MeshRenderer[] renderers, Vector3 pos, float radius, float height, Color glowTint, bool touchable)
        {
            float pick = Range(0, 1), boost;
            if (pick < cfg.dormantShare) boost = cfg.dormantBoost;
            else if (pick > 1 - cfg.brightShare) boost = Range(cfg.brightBoost.x, cfg.brightBoost.y);
            else boost = Range(cfg.normalBoost.x, cfg.normalBoost.y);
            Color.RGBToHSV(glowTint, out float h, out float s, out float v);
            h = Mathf.Repeat(h + Range(-cfg.hueJitter, cfg.hueJitter) / 360f, 1);
            Color color = Color.HSVToRGB(h, Mathf.Clamp01(s * Range(0.85f, 1.1f)), v);
            float phase = Range(0, 1);
            bool glows = false;
            foreach (var mr in renderers)
            {
                if (mr.sharedMaterial != null && mr.sharedMaterial.HasProperty("_GlowStrength") && mr.sharedMaterial.GetFloat("_GlowStrength") > 0) glows = true;
                mr.GetPropertyBlock(block);
                block.SetFloat("_GlowBoost", boost);
                block.SetFloat("_GlowPhase", phase);
                block.SetFloat("_GlowFlash", 0);
                block.SetColor("_GlowTint", color);
                mr.SetPropertyBlock(block);
            }
            if (!glows || !touchable) return;
            var g = new Glow { pos = pos, reach = radius + cfg.touchReach, height = height, color = color, renderers = renderers };
            if (Range(0, 1) < cfg.memoryShare)
            {
                // a memory plant: it keeps a story, and breathes differently (two slow blinks, then rest)
                g.story = stories++;
                foreach (var mr in renderers)
                {
                    mr.GetPropertyBlock(block);
                    block.SetFloat("_GlowMemory", 1);
                    block.SetFloat("_GlowBoost", Mathf.Max(boost, cfg.normalBoost.y));
                    mr.SetPropertyBlock(block);
                }
            }
            g.reach = Mathf.Max(radius, 0.12f) + cfg.touchReach;
            all.Add(g);
            long k = Key(Mathf.FloorToInt(pos.x / Cell), Mathf.FloorToInt(pos.z / Cell));
            if (!cells.TryGetValue(k, out var list)) cells[k] = list = new List<Glow>();
            list.Add(g);
        }

        void Near(Vector3 p, float r, List<Glow> result)
        {
            result.Clear();
            int x0 = Mathf.FloorToInt((p.x - r) / Cell), x1 = Mathf.FloorToInt((p.x + r) / Cell);
            int z0 = Mathf.FloorToInt((p.z - r) / Cell), z1 = Mathf.FloorToInt((p.z + r) / Cell);
            for (int cx = x0; cx <= x1; cx++)
            for (int cz = z0; cz <= z1; cz++)
                if (cells.TryGetValue(Key(cx, cz), out var list)) result.AddRange(list);
        }

        static float FlatDist(Vector3 a, Vector3 b) => Mathf.Sqrt((a.x - b.x) * (a.x - b.x) + (a.z - b.z) * (a.z - b.z));

        /// <summary>Start (or strengthen) a flare after a delay in seconds.</summary>
        void Flare(Glow g, float amount, float delay)
        {
            if (g.active && g.flash > amount * 0.8f && g.t >= 0) return;
            g.amount = Mathf.Max(amount, g.active ? g.flash : 0);
            g.t = -delay;
            if (!g.active) { g.active = true; active.Add(g); }
        }

        float Envelope(Glow g)
        {
            if (g.t < 0) return 0;
            if (g.t < cfg.rise) return g.amount * Mathf.SmoothStep(0, 1, g.t / cfg.rise);
            float t = g.t - cfg.rise - cfg.hold;
            if (t < 0) return g.amount;
            float k = Mathf.Clamp01(t / cfg.fade);
            return g.amount * (1 - k * k * (3 - 2 * k));
        }

        static readonly Vector3[] tips = new Vector3[2];

        /// <summary>Touches by the ant's antenna tips, then advance every flare and the borrowed lights.</summary>
        public void Tick(float dt, AntController ant)
        {
            tips[0] = ant.AntennaTip(0);
            tips[1] = ant.AntennaTip(1);
            float extra = ant.Talking ? cfg.talkReach : 0;   // reaching out on purpose (Space)
            // plants the antennae have left can be touched again
            for (int i = touching.Count - 1; i >= 0; i--)
            {
                var t = touching[i];
                if (Mathf.Min(FlatDist(tips[0], t.pos), FlatDist(tips[1], t.pos)) > t.reach + extra + 0.3f) { t.touching = false; touching.RemoveAt(i); }
            }
            Near(ant.Position, Cell, near);
            foreach (var g in near)
            {
                if (g.touching) continue;
                bool hit = false;
                foreach (Vector3 tip in tips)
                    if (FlatDist(tip, g.pos) < g.reach + extra && tip.y < g.pos.y + g.height + 0.2f) hit = true;
                if (!hit) continue;
                g.touching = true;
                touching.Add(g);
                Flare(g, 1f, 0);
                onFlare?.Invoke(g.pos);
                if (g.story >= 0) onStory?.Invoke(g.story, g.pos);
                // the light runs on to the neighbours, a little weaker and a little later
                var ring = new List<Glow>();
                Near(g.pos, cfg.ripple, ring);
                foreach (var n in ring)
                {
                    if (n == g) continue;
                    float d = FlatDist(g.pos, n.pos);
                    if (d > cfg.ripple) continue;
                    Flare(n, cfg.rippleAmount * (1 - 0.5f * d / cfg.ripple), d / Mathf.Max(0.1f, cfg.rippleSpeed));
                }
            }

            for (int i = active.Count - 1; i >= 0; i--)
            {
                var g = active[i];
                g.t += dt;
                float f = Envelope(g);
                bool done = g.t > cfg.rise + cfg.hold + cfg.fade;
                if (done) f = 0;
                if (Mathf.Abs(f - g.flash) > 0.004f || done)
                {
                    g.flash = f;
                    foreach (var mr in g.renderers)
                    {
                        if (mr == null) continue;
                        mr.GetPropertyBlock(block);
                        block.SetFloat("_GlowFlash", f);
                        mr.SetPropertyBlock(block);
                    }
                }
                if (done) { g.active = false; g.flash = 0; active.RemoveAt(i); }
            }

            // lend the few point lights to the brightest flares, so the ground around them lights up too
            if (lights.Length == 0) return;
            active.Sort((a, b) => b.flash.CompareTo(a.flash));
            for (int i = 0; i < lights.Length; i++)
            {
                Light l = lights[i];
                if (i < active.Count && active[i].flash > 0.02f)
                {
                    Glow g = active[i];
                    l.enabled = true;
                    l.transform.position = g.pos + Vector3.up * Mathf.Max(0.3f, g.height * 0.55f);
                    l.color = g.color;
                    l.range = cfg.lightRange * Mathf.Clamp(0.6f + g.height * 0.15f, 0.6f, 1.6f);
                    l.intensity = cfg.lightIntensity * g.flash;
                }
                else l.enabled = false;
            }
        }
    }
}

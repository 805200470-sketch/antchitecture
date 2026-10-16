using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Places the plant assets along the canyon in distance bands from the canyon edge, so everything
    /// grows out of the recognized path: spore stalks and moss on the floor, curls and mounds on the
    /// banks, porous trunks on the plateau, a fan tree as the landmark of every junction chamber, and
    /// dark forest silhouettes beyond the hexagon. Each plant is sunk to the lowest ground under its
    /// footprint and tilted with the slope, and its base becomes collision circles.
    /// </summary>
    public class Scatter
    {
        readonly World world;
        readonly Region region;
        readonly RuntimeRefs refs;
        readonly ScatterSettings cfg;
        readonly float density;
        readonly System.Random rng;
        readonly Transform parent;
        readonly CircleGrid occupied = new CircleGrid(6f);
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        public int Placed { get; private set; }

        public Scatter(World world, Region region, RuntimeRefs refs, ScatterSettings cfg, float density, int seed, Transform parent)
        {
            this.world = world;
            this.region = region;
            this.refs = refs;
            this.cfg = cfg;
            this.density = density;
            this.parent = parent;
            rng = new System.Random(seed);
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        float Range(Vector2 v) => Range(v.x, v.y);

        /// <summary>Run every rule; yields between rules so the loading bar keeps moving.</summary>
        public IEnumerator Run(Action<float> progress)
        {
            Landmarks();
            int n = cfg.rules.Length;
            for (int i = 0; i < n; i++)
            {
                Rule(cfg.rules[i]);
                progress?.Invoke((i + 1f) / (n + 1f));
                yield return null;
            }
            Surroundings();
            progress?.Invoke(1f);
        }

        void Rule(ScatterRule rule)
        {
            if (!string.IsNullOrEmpty(rule.biomes) && !GameConfig.InList(rule.biomes, region.biome.id)) return;
            PlantRef plant = refs.Plant(rule.plant);
            if (plant == null || plant.prefab == null) return;
            float cell = rule.spacing / Mathf.Sqrt(Mathf.Max(0.1f, density));
            int salt = rule.plant.GetHashCode() & 0xffff;
            for (float gz = region.minZ; gz < region.maxZ; gz += cell)
            for (float gx = region.minX; gx < region.maxX; gx += cell)
            {
                // clumped, not even: a cell in a rich patch tries for several plants, one in a bare stretch rarely
                // for any (the same number overall)
                float want = Patch(rule, salt, gx + cell * 0.5f, gz + cell * 0.5f);
                int tries = (int)want + (Range(0, 1) < want - (int)want ? 1 : 0);
                for (int attempt = 0; attempt < tries; attempt++)
                    Spot(rule, plant, gx + Range(0, cell), gz + Range(0, cell));
            }
        }

        /// <summary>
        /// How many plants of this rule a spot wants (1 on average): patches where the ground suits it, partly from
        /// a field all plants share (so different kinds gather into the same groves) and partly from its own, and
        /// a few strays in between. patchiness 0 spreads the rule evenly.
        /// </summary>
        float Patch(ScatterRule rule, int salt, float x, float z)
        {
            if (rule.patchiness <= 0) return 1;
            float shared = Mathf.PerlinNoise(x * 0.045f + 11.3f, z * 0.045f - 4.7f) * 0.6f + Mathf.PerlinNoise(x * 0.11f - 3.1f, z * 0.11f + 8.9f) * 0.4f;
            float f = 1f / Mathf.Max(2f, rule.spacing * cfg.patchSize);
            float own = Mathf.PerlinNoise(x * f + (salt % 97) * 1.37f, z * f - (salt % 89) * 1.53f);
            float n = shared * 0.65f + own * 0.35f;
            float t = Mathf.Clamp01((n - 0.42f) / 0.16f);
            // a little over 1 on average: crowded patches lose some tries to plants already there
            return 1.12f * Mathf.Lerp(1f, 2.2f * t * t * (3 - 2 * t), rule.patchiness);
        }

        void Spot(ScatterRule rule, PlantRef plant, float x, float z)
        {
            if (region.cell && world.RegionAt(x, z) != region) return;   // a honeycomb cell grows only its own hexagon
            float d = world.PathDist(x, z);
            if (d < rule.dMin || d > rule.dMax) return;
            float h = Range(rule.height);
            int stack = Mathf.Max(1, rule.stack);
            float seg = h / stack;
            float r = plant.footprintRadius * seg;
            if (world.HexDist(x, z) > -(r + 1.2f)) return;
            if (occupied.Overlaps(x, z, r * 0.9f) || world.reserved.Overlaps(x, z, r)) return;
            int members = rng.Next(rule.cluster.x, rule.cluster.y + 1);
            for (int m = 0; m < Mathf.Max(1, members); m++)
            {
                float mx = x, mz = z, mh = seg;
                if (m > 0)
                {
                    float a = Range(0, Mathf.PI * 2), rr = Range(0.4f, 1f) * rule.spacing * 0.35f;
                    mx += Mathf.Cos(a) * rr;
                    mz += Mathf.Sin(a) * rr;
                    mh = seg * Range(0.55f, 1.05f);
                    if (world.HexDist(mx, mz) > -1.5f) continue;
                    if (region.cell && world.RegionAt(mx, mz) != region) continue;
                    float dm = world.PathDist(mx, mz);
                    if (dm < rule.dMin - 0.5f || dm > rule.dMax + 0.5f) continue;
                    if (occupied.Overlaps(mx, mz, plant.footprintRadius * mh * 0.8f) || world.reserved.Overlaps(mx, mz, plant.footprintRadius * mh)) continue;
                }
                Place(plant, mx, mz, mh, stack, rule.tilt, rule.tintJitter, rule.dim, !rule.noShadow);
            }
        }

        /// <summary>A fan tree in every junction chamber, set into the widest gap between the paths.</summary>
        void Landmarks()
        {
            string kind = string.IsNullOrEmpty(region.biome.landmarkPlant) ? cfg.landmark.plant : region.biome.landmarkPlant;
            PlantRef plant = refs.Plant(kind);
            if (plant == null || plant.prefab == null) plant = refs.Plant(cfg.landmark.plant);
            if (plant == null || plant.prefab == null) return;
            foreach (WNode node in world.nodes)
            {
                if (node.region != region.index || node.degree < 3 || !node.inside || node.clear < cfg.landmark.minClearing) continue;
                var dirs = new List<float>();
                foreach (WEdge e in world.edges)
                {
                    if (e.region != region.index) continue;
                    if (e.source == node.id) dirs.Add(Dir(node, e.pts[Mathf.Min(3, e.pts.Count - 1)]));
                    if (e.target == node.id) dirs.Add(Dir(node, e.pts[Mathf.Max(0, e.pts.Count - 4)]));
                }
                if (dirs.Count == 0) continue;
                dirs.Sort();
                float bestGap = -1, bestDir = 0;
                for (int i = 0; i < dirs.Count; i++)
                {
                    float a0 = dirs[i], a1 = i + 1 < dirs.Count ? dirs[i + 1] : dirs[0] + Mathf.PI * 2;
                    if (a1 - a0 > bestGap) { bestGap = a1 - a0; bestDir = (a0 + a1) / 2; }
                }
                float off = node.clear * 0.62f;
                float x = node.x + Mathf.Cos(bestDir) * off, z = node.z + Mathf.Sin(bestDir) * off;
                if (world.HexDist(x, z) > -4 || world.reserved.Overlaps(x, z, 1.5f)) continue;
                Place(plant, x, z, Range(cfg.landmark.height), 1, 0.05f, 0.06f, 1f, true);
            }
        }

        static float Dir(WNode n, Vector2 p) => Mathf.Atan2(p.y - n.z, p.x - n.x);

        /// <summary>Huge dark trunks and fan trees rising out of the fog beyond the frame.</summary>
        void Surroundings()
        {
            float baseY = world.Height((region.minX + region.maxX) / 2, region.minZ) - 10f;
            string[] kinds = string.IsNullOrEmpty(region.biome.surroundings)
                ? new[] { "trunk_night", "fan_tree", "trunk_night" }
                : region.biome.surroundings.Split(',');
            var available = new List<PlantRef>();
            foreach (string k in kinds)
            {
                PlantRef p = refs.Plant(k.Trim());
                if (p != null && p.prefab != null) available.Add(p);
            }
            if (available.Count == 0) return;
            // a honeycomb cell: only as many as its share of the outer rim (inner cells have none)
            int want = cfg.surroundingsCount;
            if (region.cell)
            {
                int outer = 0;
                foreach (WFrameEdge fe in world.frameEdges) if (fe.region == region.index && !fe.shared) outer++;
                want = Mathf.RoundToInt(cfg.surroundingsCount * 0.7f * outer / 6f);
                if (want == 0) return;
            }
            int placed = 0;
            for (int tries = 0; tries < want * 20 && placed < want; tries++)
            {
                float x = Range(region.minX - cfg.surroundingsReach, region.maxX + cfg.surroundingsReach);
                float z = Range(region.minZ - cfg.surroundingsReach, region.maxZ + cfg.surroundingsReach);
                float hd = world.HexDist(x, z);
                if (hd < 10 || hd > cfg.surroundingsReach) continue;
                PlantRef plant = available[placed % available.Count];
                float h = Range(26f, 48f);
                GameObject go = UnityEngine.Object.Instantiate(plant.prefab, new Vector3(x, baseY, z), Quaternion.Euler(0, Range(0, 360), 0), parent);
                go.transform.localScale = Vector3.one * h;
                SetLook(go, plant.name, new Color(0.35f, 0.42f, 0.48f), true);
                world.glow?.Add(go.GetComponentsInChildren<MeshRenderer>(), go.transform.position, 0, h, region.biome.plantGlow, false);
                placed++;
            }
        }

        /// <summary>Plants live on two layers the camera stops drawing at different distances (see Game.CullFar).</summary>
        public const int PlantLayer = 8, BigPlantLayer = 10;

        static void SetLayer(GameObject go, int layer)
        {
            foreach (Transform t in go.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = layer;
        }

        void Place(PlantRef plant, float x, float z, float scale, int stack, float tilt, float jitter, float dim, bool shadows = true)
        {
            // under a tunnel roof nothing grows through the ceiling: a plant that would is grown smaller, or not at all
            float ceiling = world.Ceiling(x, z);
            if (!float.IsInfinity(ceiling))
            {
                float room = ceiling - world.Height(x, z) - 0.4f;
                if (scale * stack > room)
                {
                    if (room < scale * stack * 0.45f) return;
                    scale = room / stack;
                }
            }
            float r = plant.footprintRadius * scale;
            // sink to the lowest ground under the footprint so no edge of the base floats
            float y = world.Height(x, z);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3;
                y = Mathf.Min(y, world.Height(x + Mathf.Cos(a) * r, z + Mathf.Sin(a) * r));
            }
            y -= cfg.sink * scale * stack;
            Vector3 up = Vector3.Slerp(Vector3.up, world.Normal(x, z), tilt);
            float yaw = Range(0, 360);
            Quaternion rot = Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, yaw, 0);
            Color tint = new Color(1 + Range(-jitter, jitter), 1 + Range(-jitter, jitter) * 0.6f, 1 + Range(-jitter, jitter)) * dim;
            tint.a = 1;
            var renderers = new List<MeshRenderer>();
            for (int s = 0; s < stack; s++)
            {
                Vector3 pos = new Vector3(x, y, z) + up * (s * scale * 0.93f);
                Quaternion segRot = s == 0 ? rot : Quaternion.FromToRotation(Vector3.up, up) * Quaternion.Euler(0, Range(0, 360), 0);
                GameObject go = UnityEngine.Object.Instantiate(plant.prefab, pos, segRot, parent);
                go.transform.localScale = Vector3.one * scale;
                SetLayer(go, scale * stack > 6f ? BigPlantLayer : PlantLayer);
                SetLook(go, plant.name, tint, s == 0); // only the bottom segment of a stack fades into the ground
                if (!shadows)
                    foreach (var mr in go.GetComponentsInChildren<MeshRenderer>()) mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderers.AddRange(go.GetComponentsInChildren<MeshRenderer>());
                Placed++;
            }
            // its own glow brightness and rhythm; glowing plants can be touched
            world.glow?.Add(renderers.ToArray(), new Vector3(x, y, z), r, scale * stack, region.biome.plantGlow, true);
            occupied.Add(new Circle(x, z, r * 1.1f, scale * stack));
            world.placements.Add(new Placement { plant = plant.name, region = region.index, x = x, z = z, height = scale * stack });
            // nothing is walked through: low, broad things (pebbles, leaves, moss pads, seed piles) become a dome the
            // ants climb onto, everything else blocks them
            Bounds b = renderers[0].bounds;
            foreach (MeshRenderer mr in renderers) b.Encapsulate(mr.bounds);
            float top = b.max.y - world.Height(x, z);
            if (top <= cfg.stepUp || (top <= cfg.stepUpBroad && top <= 0.8f * r))
            {
                world.steps.Add(new Circle(x, z, r * 1.05f, Mathf.Max(0.02f, top * 0.92f)));
                return;
            }
            // low arches and the like: block the middle too, too low to pass under
            if (top < 1.4f) world.colliders.Add(new Circle(x, z, r * 0.5f, scale * stack));
            if (plant.circles != null && plant.circles.Length > 0)
            {
                float c = Mathf.Cos(-yaw * Mathf.Deg2Rad), sn = Mathf.Sin(-yaw * Mathf.Deg2Rad);
                foreach (Vector3 ci in plant.circles)
                {
                    // rotate the footprint circle by the plant's yaw (Unity yaw turns +Z towards +X)
                    float cx = ci.x * c - ci.y * sn, cz = ci.x * sn + ci.y * c;
                    world.colliders.Add(new Circle(x + cx * scale, z + cz * scale, ci.z * scale, scale * stack));
                }
            }
            else world.colliders.Add(new Circle(x, z, r, scale * stack));
        }

        /// <summary>
        /// Per-instance look: tint, the region's palette (hue, saturation, glow, ground) and base blend.
        /// Plants painted for this biome keep their own colours.
        /// </summary>
        void SetLook(GameObject go, string plant, Color tint, bool groundBlend)
        {
            Biome b = region.biome;
            bool native = GameConfig.InList(b.nativePlants, plant);
            foreach (var mr in go.GetComponentsInChildren<MeshRenderer>())
            {
                mr.GetPropertyBlock(block);
                block.SetColor("_Tint", tint);
                block.SetFloat("_HueShift", native ? 0f : b.plantHue);
                block.SetFloat("_Saturation", native ? 1f : b.plantSaturation);
                block.SetColor("_GlowTint", b.plantGlow);
                block.SetColor("_GroundColor", b.groundColor);
                block.SetFloat("_GroundMul", groundBlend ? 1f : 0f);
                mr.SetPropertyBlock(block);
            }
        }
    }
}

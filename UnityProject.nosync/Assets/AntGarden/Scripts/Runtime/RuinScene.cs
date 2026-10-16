using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// A small ruin scene built into one region (world spec "path-NNN@biome#relics" or "#temple"):
    /// the canyon closed over into a tunnel (TunnelRoof), the ruin's props set along the main tunnel, and every
    /// prop a relic that answers when the ant touches it (antenna tips or E): it glows, chimes and says its next
    /// line. "relics": the giants' relics (a teacup hall, coin terraces, a rusted key, a button plaza).
    /// "temple": the ancestors' temple (clay spires and carved arches along both banks, hexagon cells, and the
    /// amber shrine with an ancestor inside at the end of the tunnel).
    /// Plan() runs before the plants grow (it reserves the props' ground), Build() after.
    /// </summary>
    public class RuinScene
    {
        class Spot { public string id; public Vector3 pos; public float size, yaw; public bool collide, flat; }
        class Relic
        {
            public string id;
            public Vector3 pos;
            public float reach;
            public MeshRenderer[] renderers;
            public Light light;
            public float flash, baseLight;
            public bool touching;
        }

        public readonly string kind;
        public readonly Region region;
        public TunnelRoof roof;
        readonly World world;
        readonly RuntimeRefs refs;
        readonly System.Random rng;
        readonly List<Spot> spots = new List<Spot>();
        readonly List<Relic> relics = new List<Relic>();
        readonly Dictionary<string, int> said = new Dictionary<string, int>();
        readonly MaterialPropertyBlock block = new MaterialPropertyBlock();
        Vector3 shrineSpot;
        bool hasShrine;

        public static string Title(string kind) => kind == "temple"
            ? Loc.T("遗迹 · 古蚁圣殿", "Ruins · The Ancestors' Temple")
            : Loc.T("遗迹 · 巨人遗物", "Ruins · The Giants' Relics");

        public RuinScene(World world, Region region, RuntimeRefs refs, string kind, int seed)
        {
            this.world = world;
            this.region = region;
            this.refs = refs;
            this.kind = kind;
            rng = new System.Random(seed);
            roof = new TunnelRoof(world, region, kind == "temple" ? 8.5f : 6.5f);
        }

        float R(float a, float b) => a + (float)rng.NextDouble() * (b - a);

        // ------------------------------------------------------------------ layout

        WEdge MainTunnel()
        {
            WEdge best = null;
            float bestLen = 0;
            foreach (WEdge e in world.edges)
            {
                // a path of this nest's own: in a honeycomb not a connector across a seam, and inside the cell
                if (e.region != region.index || e.pts.Count < 4 || (region.cell && e.connector)) continue;
                float len = 0;
                for (int i = 1; i < e.pts.Count; i++)
                    if (!region.cell || world.RegionAt(e.pts[i].x, e.pts[i].y) == region) len += (e.pts[i] - e.pts[i - 1]).magnitude;
                if (len > bestLen) { bestLen = len; best = e; }
            }
            return best;
        }

        /// <summary>Point, direction and half-width at a fraction of the way along an edge.</summary>
        static (Vector2 p, Vector2 dir, float hw) At(WEdge e, float frac)
        {
            float total = 0;
            for (int i = 1; i < e.pts.Count; i++) total += (e.pts[i] - e.pts[i - 1]).magnitude;
            float want = Mathf.Clamp01(frac) * total, acc = 0;
            for (int i = 1; i < e.pts.Count; i++)
            {
                float seg = (e.pts[i] - e.pts[i - 1]).magnitude;
                if (acc + seg >= want || i == e.pts.Count - 1)
                {
                    float t = seg > 0 ? (want - acc) / seg : 0;
                    Vector2 dir = (e.pts[i] - e.pts[i - 1]).normalized;
                    float hw = e.hws != null && e.hws.Count == e.pts.Count ? Mathf.Lerp(e.hws[i - 1], e.hws[i], t) : e.hw;
                    return (Vector2.Lerp(e.pts[i - 1], e.pts[i], t), dir, hw);
                }
                acc += seg;
            }
            return (e.pts[0], Vector2.up, e.hw);
        }

        void Add(string id, Vector2 p, float size, float yaw, bool collide, bool flat = false)
        {
            spots.Add(new Spot { id = id, pos = new Vector3(p.x, 0, p.y), size = size, yaw = yaw, collide = collide, flat = flat });
            // keep the plants off the prop's ground
            world.reserved.Add(new Circle(p.x, p.y, size * 0.6f, size));
        }

        static float Yaw(Vector2 facing) => Mathf.Atan2(facing.x, facing.y) * Mathf.Rad2Deg;

        /// <summary>Decide where every prop goes (before the plants grow).</summary>
        public void Plan()
        {
            WEdge e = MainTunnel();
            if (e == null) return;
            if (kind == "temple")
            {
                // clay spires in pairs down both banks, carved arches between them facing the path
                for (float f = 0.1f; f <= 0.86f; f += 0.085f)
                {
                    var (p, dir, hw) = At(e, f);
                    Vector2 n = new Vector2(-dir.y, dir.x);
                    foreach (int side in new[] { -1, 1 })
                    {
                        Vector2 q = p + n * side * (hw + 1.4f);
                        Add("temple_spire", q, R(5.5f, 8f), R(0, 360), true);
                    }
                    var (p2, dir2, hw2) = At(e, f + 0.042f);
                    Vector2 n2 = new Vector2(-dir2.y, dir2.x);
                    int s2 = rng.NextDouble() < 0.5 ? -1 : 1;
                    Add("temple_arch", p2 + n2 * s2 * (hw2 + 1.0f), R(4.2f, 5.2f), Yaw(-n2 * s2), true);
                }
                foreach (float f in new[] { 0.33f, 0.58f, 0.77f })
                {
                    var (p, dir, hw) = At(e, f);
                    Vector2 n = new Vector2(-dir.y, dir.x);
                    int side = f < 0.5f ? 1 : -1;
                    Add("temple_cells", p + n * side * hw * 0.62f, R(1.1f, 1.5f), R(0, 360), true);
                }
                // the amber shrine at the end of the tunnel
                var (end, endDir, _) = At(e, 0.97f);
                shrineSpot = new Vector3(end.x, 0, end.y);
                hasShrine = true;
                world.reserved.Add(new Circle(end.x, end.y, 2.4f, 4f));
            }
            else
            {
                var (a, da, hwa) = At(e, 0.3f);
                Vector2 na = new Vector2(-da.y, da.x);
                Add("relic_teacup", a + na * (hwa + 2.2f), 6f, Yaw(-na), true);
                var (b, db, hwb) = At(e, 0.5f);
                Vector2 nb = new Vector2(-db.y, db.x);
                Add("relic_coins", b - nb * (hwb + 1.1f) + db * 1.6f, 1.9f, R(0, 360), true);
                Add("relic_coins", b - nb * (hwb + 1.4f) - db * 1.8f, 1.4f, R(0, 360), true);
                var (c, dc, hwc) = At(e, 0.68f);
                Vector2 nc = new Vector2(-dc.y, dc.x);
                Add("relic_key", c + nc * hwc * 0.7f, 7f, Yaw(dc) + 90f, true);
                // the button lies flat in the roomiest chamber: a plaza the ants walk across
                WNode best = null;
                foreach (WNode nd in world.nodes)
                    if (nd.region == region.index && nd.inside && nd.degree >= 3 && (best == null || nd.clear > best.clear)) best = nd;
                if (best != null) Add("relic_button", new Vector2(best.x, best.z), 3.6f, R(0, 360), false, true);
            }
        }

        // ------------------------------------------------------------------ build

        /// <summary>The tunnel roof and the props, after the plants have grown.</summary>
        public void Build(Material ground, Transform parent, float roofStep, int seed)
        {
            if (refs.roots != null)
            {
                refs.roots.SetFloat("_Smooth", 0.08f);   // matte roots (the saved material may predate it)
                refs.roots.SetFloat("_Glow", 1.4f);      // the glowing tips: a soft accent, not beads of light
            }
            roof.Build(ground, refs.roots, parent, roofStep, seed);
            // light bounced up from the glowing floor: a dim fill from below, so the earthen roof and the roots
            // hanging from it read in the dark instead of looking like an open night sky
            var fill = new GameObject("TunnelBounceLight").AddComponent<Light>();
            fill.transform.SetParent(parent, false);
            fill.type = LightType.Directional;
            fill.transform.rotation = Quaternion.Euler(-72f, 35f, 0);
            fill.color = Color.Lerp(region.biome.plantGlow, new Color(0.6f, 0.6f, 0.6f), 0.45f);
            fill.intensity = kind == "temple" ? 0.42f : 0.36f;
            fill.shadows = LightShadows.None;
            fill.cullingMask = 1 << TunnelRoof.Layer;   // only the roof and its roots: other nests in the world keep their night
            var root = new GameObject($"Ruins_{kind}_r{region.index}").transform;
            root.SetParent(parent, false);
            foreach (Spot s in spots) PlaceProp(s, root);
            if (hasShrine) BuildShrine(root);
        }

        void PlaceProp(Spot s, Transform parent)
        {
            PlantRef p = refs.Plant(s.id);
            if (p == null || p.prefab == null) { Debug.LogWarning($"[AntGarden] ruin prop {s.id} not built yet"); return; }
            // size is the prop's longest dimension (models are normalised to 1 unit tall)
            float longest = Mathf.Max(1f, Mathf.Max(p.size.x, p.size.z));
            float scale = s.size / longest;
            float r = p.footprintRadius * scale;
            float y = world.Height(s.pos.x, s.pos.z);
            for (int k = 0; k < 6; k++)
            {
                float a = k * Mathf.PI / 3;
                y = Mathf.Min(y, world.Height(s.pos.x + Mathf.Cos(a) * r, s.pos.z + Mathf.Sin(a) * r));
            }
            y -= s.flat ? scale * 0.75f : 0.12f * scale;   // the button sinks until only its face shows
            var go = Object.Instantiate(p.prefab, new Vector3(s.pos.x, y, s.pos.z), Quaternion.Euler(0, s.yaw, 0), parent);
            go.transform.localScale = Vector3.one * scale;
            var renderers = go.GetComponentsInChildren<MeshRenderer>();
            foreach (var mr in renderers)
            {
                mr.GetPropertyBlock(block);
                block.SetColor("_Tint", s.id == "relic_coins" ? new Color(1.5f, 1.45f, 1.3f) : Color.white);   // verdigris reads too dark in the tunnel
                block.SetFloat("_HueShift", 0);
                block.SetFloat("_Saturation", 1);
                block.SetColor("_GlowTint", new Color(1f, 0.82f, 0.5f));
                block.SetColor("_GroundColor", region.biome.groundColor);
                block.SetFloat("_GroundMul", s.flat ? 0 : 1);
                block.SetFloat("_GlowBoost", 1);
                block.SetFloat("_GlowFlash", 0);
                mr.SetPropertyBlock(block);
            }
            // the key is a bridge, the coins are terraces and the button a plaza: ants climb onto them. The
            // rest (cup, spires, arches, cells) block.
            Bounds bounds = renderers[0].bounds;
            foreach (MeshRenderer mr in renderers) bounds.Encapsulate(mr.bounds);
            float top = Mathf.Max(0.05f, bounds.max.y - world.Height(s.pos.x, s.pos.z));
            if (s.flat || s.id == "relic_key" || s.id == "relic_coins")
            {
                float c = Mathf.Cos(-s.yaw * Mathf.Deg2Rad), sn = Mathf.Sin(-s.yaw * Mathf.Deg2Rad);
                if (p.circles != null && p.circles.Length > 0 && p.circles.Length <= 16)
                    foreach (Vector3 ci in p.circles)   // a long thing: a ridge of overlapping domes along it
                    {
                        float cx = ci.x * c - ci.y * sn, cz = ci.x * sn + ci.y * c;
                        world.steps.Add(new Circle(s.pos.x + cx * scale, s.pos.z + cz * scale, ci.z * scale * 1.35f, top * 0.9f));
                    }
                else world.steps.Add(new Circle(s.pos.x, s.pos.z, r * 1.05f, top * 0.9f));
            }
            else if (s.collide)
            {
                float c = Mathf.Cos(-s.yaw * Mathf.Deg2Rad), sn = Mathf.Sin(-s.yaw * Mathf.Deg2Rad);
                if (p.circles != null && p.circles.Length > 0)
                    foreach (Vector3 ci in p.circles)
                    {
                        float cx = ci.x * c - ci.y * sn, cz = ci.x * sn + ci.y * c;
                        world.colliders.Add(new Circle(s.pos.x + cx * scale, s.pos.z + cz * scale, ci.z * scale, scale));
                    }
                else world.colliders.Add(new Circle(s.pos.x, s.pos.z, r, scale));
            }
            world.placements.Add(new Placement { plant = s.id, region = region.index, x = s.pos.x, z = s.pos.z, height = scale });
            relics.Add(new Relic { id = s.id, pos = new Vector3(s.pos.x, y, s.pos.z), reach = r + 0.5f, renderers = renderers });
        }

        /// <summary>A rough block of amber with an ancestor (a winged Ant_003) inside and a warm light.</summary>
        void BuildShrine(Transform parent)
        {
            float y = world.Height(shrineSpot.x, shrineSpot.z);
            var go = new GameObject("AmberShrine");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(shrineSpot.x, y, shrineSpot.z);
            // the resin: a lumpy ellipsoid, wider at the base
            var mesh = new Mesh { name = "AmberBlock" };
            var src = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            Mesh sphere = src.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(src);
            var verts = sphere.vertices;
            for (int i = 0; i < verts.Length; i++)
            {
                Vector3 v = verts[i];
                float lump = 1 + 0.12f * Mathf.Sin(v.x * 9 + 1.3f) * Mathf.Sin(v.y * 7 - 0.4f) + 0.08f * Mathf.Sin(v.z * 11);
                float widen = 1 + 0.25f * Mathf.Clamp01(-v.y * 2);
                verts[i] = new Vector3(v.x * 2.4f * widen, v.y * 3.6f + 1.5f, v.z * 2.1f * widen) * lump;
            }
            mesh.vertices = verts;
            mesh.triangles = sphere.triangles;
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var resin = new GameObject("Resin");
            resin.transform.SetParent(go.transform, false);
            resin.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mr = resin.AddComponent<MeshRenderer>();
            mr.sharedMaterial = refs.amber;
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            // the ancestor, wings spread, held in the resin
            AntRef ancestor = refs.Ant("Ant_003") ?? (refs.ants.Count > 0 ? refs.ants[0] : null);
            if (ancestor != null && ancestor.prefab != null)
            {
                var ant = Object.Instantiate(ancestor.prefab, go.transform);
                float s = 1.7f / Mathf.Max(1e-3f, ancestor.modelBodyLength);
                ant.transform.localScale = Vector3.one * s;
                ant.transform.localPosition = new Vector3(0, 1.3f, -ancestor.modelCenterZ * s);
                ant.transform.localRotation = Quaternion.Euler(-18, R(0, 360), 6);
                foreach (var a in ant.GetComponentsInChildren<Animator>()) a.enabled = false;
            }
            var light = new GameObject("ShrineLight").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0, 1.6f, 0);
            light.type = LightType.Point;
            light.color = new Color(1f, 0.62f, 0.25f);
            light.range = 9f;
            light.intensity = 1.6f;
            light.shadows = LightShadows.None;
            world.colliders.Add(new Circle(shrineSpot.x, shrineSpot.z, 2.2f, 4f));
            relics.Add(new Relic { id = "amber_shrine", pos = go.transform.position, reach = 3.2f, renderers = new[] { mr }, light = light, baseLight = 1.6f });
        }

        // ------------------------------------------------------------------ talk

        /// <summary>The relic within reach of a point (for the E prompt), or -1.</summary>
        public int Near(Vector3 p)
        {
            int best = -1;
            float bestD = float.MaxValue;
            for (int i = 0; i < relics.Count; i++)
            {
                float d = new Vector2(relics[i].pos.x - p.x, relics[i].pos.z - p.z).magnitude - relics[i].reach;
                if (d < 1.2f && d < bestD) { bestD = d; best = i; }
            }
            return best;
        }

        public int Count => relics.Count;
        public string IdOf(int i) => relics[i].id;
        public Vector3 PosOf(int i) => relics[i].pos;
        public float ReachOf(int i) => relics[i].reach;

        public string NameOf(int i) => Voices.Relics.TryGetValue(relics[i].id, out var sc) ? Loc.T(sc.zhName, sc.enName) : relics[i].id;

        /// <summary>The relic answers: its next line, a glow and a chime.</summary>
        public void Speak(int i, Hud hud)
        {
            Relic r = relics[i];
            r.flash = 1;
            if (!Voices.Relics.TryGetValue(r.id, out var sc)) return;
            said.TryGetValue(r.id, out int k);
            hud.ShowLine(Loc.T(sc.zhName, sc.enName), Voices.T(sc.lines[k % sc.lines.Length]));
            said[r.id] = k + 1;
            Voices.Chime(r.pos + Vector3.up, sc.pitch);
        }

        static readonly Vector3[] tips = new Vector3[2];

        /// <summary>Antenna touches, and the glow of relics that have just spoken fading back.</summary>
        public void Tick(float dt, AntController player, Hud hud)
        {
            tips[0] = player.AntennaTip(0);
            tips[1] = player.AntennaTip(1);
            for (int i = 0; i < relics.Count; i++)
            {
                Relic r = relics[i];
                float d = Mathf.Min(new Vector2(tips[0].x - r.pos.x, tips[0].z - r.pos.z).magnitude, new Vector2(tips[1].x - r.pos.x, tips[1].z - r.pos.z).magnitude);
                bool touch = d < r.reach - 0.25f;
                if (touch && !r.touching) Speak(i, hud);
                if (d > r.reach + 0.6f) r.touching = false;
                else if (touch) r.touching = true;
                if (r.flash <= 0) continue;
                r.flash = Mathf.Max(0, r.flash - dt / 3.5f);
                float f = r.flash * r.flash * (3 - 2 * r.flash);
                foreach (var mr in r.renderers)
                {
                    mr.GetPropertyBlock(block);
                    block.SetFloat("_GlowFlash", f);
                    block.SetFloat("_Flash", f);
                    mr.SetPropertyBlock(block);
                }
                if (r.light != null) r.light.intensity = r.baseLight * (1 + f * 2.5f);
            }
        }
    }
}

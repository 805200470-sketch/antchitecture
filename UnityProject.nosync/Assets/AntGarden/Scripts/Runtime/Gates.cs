using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>A light gate: an arch across the canyon. Portals are paired and jump the ant between regions.</summary>
    public class Gate
    {
        public Vector3 position;   // centre of the arch's base
        public Vector2 dir;        // path direction through the gate (for portals: pointing out of the region)
        public float radius;
        public int region;
        public bool portal;
        public Gate pair;
        public Region target;      // portals: the region the gate leads to
        public string world;       // world gates: the world (nest, ruin, archipelago) the gate leads to
        public string TargetLabel => world != null ? Gates.WorldName(world) : target != null ? Gates.ShortName(target) : "";
        public GameObject go;
    }

    /// <summary>
    /// Builds the gates: a seam gate wherever a stitched connector canyon crosses from one nest into the
    /// next, and a pair of portal gates between consecutive separate regions (at the exits facing each other).
    /// </summary>
    public static class Gates
    {
        public static List<Gate> Build(World w, RuntimeRefs refs, GateSettings cfg, Transform parent)
        {
            var gates = new List<Gate>();
            var root = new GameObject("Gates").transform;
            root.SetParent(parent, false);

            foreach (WEdge e in w.edges)
            {
                if (!e.connector || !Crossing(w, e, out Vector2 p, out Vector2 d)) continue;
                Region r = w.regions[e.region];
                float hw = Mathf.Max(2.5f, w.HalfWidth(p.x, p.y));
                // between the cells of a honeycomb the seam is marked by rising motes (SeamMotes), not an arch
                gates.Add(Make(w, refs, cfg, root, p, d, hw * cfg.archScale, r.biome.gateColor, r.biome.gateColor, e.region, false, !r.cell));
            }

            for (int i = 0; i + 1 < w.regions.Count; i++)
            {
                Region a = w.regions[i], b = w.regions[i + 1];
                if (a.cell && b.cell && a.field == b.field) continue;   // honeycomb neighbours are joined on foot
                WExit ea = BestExit(w, a, b), eb = BestExit(w, b, a);
                if (ea == null || eb == null) continue;
                // the arch shows where it leads; its light keeps the local colour so the region's mood holds
                var ga = Make(w, refs, cfg, root, new Vector2(ea.x, ea.z), new Vector2(ea.dirX, ea.dirZ),
                    Mathf.Max(2.5f, w.HalfWidth(ea.x, ea.z)) * cfg.archScale, b.biome.gateColor, a.biome.gateColor, a.index, true);
                var gb = Make(w, refs, cfg, root, new Vector2(eb.x, eb.z), new Vector2(eb.dirX, eb.dirZ),
                    Mathf.Max(2.5f, w.HalfWidth(eb.x, eb.z)) * cfg.archScale, a.biome.gateColor, b.biome.gateColor, b.index, true);
                ga.pair = gb;
                gb.pair = ga;
                ga.target = b;
                gb.target = a;
                gates.Add(ga);
                gates.Add(gb);
            }
            return gates;
        }

        /// <summary>A light gate at a canyon exit that leads to another world (it travels by reloading).</summary>
        public static Gate WorldGate(World w, RuntimeRefs refs, GateSettings cfg, Transform parent, WExit ex, string world, Color color)
        {
            Gate g = Make(w, refs, cfg, parent, new Vector2(ex.x, ex.z), new Vector2(ex.dirX, ex.dirZ),
                Mathf.Max(2.5f, w.HalfWidth(ex.x, ex.z)) * cfg.archScale, color, color, ex.region, true);
            g.portal = false;
            g.world = world;
            return g;
        }

        /// <summary>What a world is called on a gate: the ruin's title, the archipelago, or the nest's number.</summary>
        public static string WorldName(string id)
        {
            if (id.StartsWith("honeycomb:") || id.Contains("|") || id.StartsWith("stitch:")) return Loc.T("蚁穴群岛", "the Archipelago");
            if (id.Contains("#")) return RuinScene.Title(id.Substring(id.LastIndexOf('#') + 1));
            if (id.StartsWith("lab:")) return Loc.T("粒子蚁穴", "the Particle Nest");
            return Loc.T("蚁穴 ", "Nest ") + id.Replace("path-", "").Split('@')[0];
        }

        public static string ShortName(Region r)
        {
            string ids = string.Join("+", System.Array.ConvertAll(r.ids, s => s.Replace("path-", "")));
            return Loc.T($"{r.biome.Name} · 蚁穴 {ids}", $"{r.biome.Name} · {(r.ids.Length > 1 ? "nests" : "nest")} {ids}");
        }

        /// <summary>Where a connector canyon crosses a seam between two stitched nests.</summary>
        static bool Crossing(World w, WEdge e, out Vector2 point, out Vector2 dir)
        {
            point = dir = Vector2.zero;
            foreach (WFrameEdge fe in w.frameEdges)
            {
                // in a honeycomb the connector and the seam it crosses may be filed under the two different cells
                if (!fe.shared || (fe.region != e.region && !(w.regions[fe.region].cell && w.regions[e.region].cell))) continue;
                var q0 = new Vector2(fe.x0, fe.z0);
                var q1 = new Vector2(fe.x1, fe.z1);
                for (int k = 1; k < e.pts.Count; k++)
                {
                    if (!Intersect(e.pts[k - 1], e.pts[k], q0, q1, out Vector2 x)) continue;
                    point = x;
                    dir = (e.pts[k] - e.pts[k - 1]).normalized;
                    return true;
                }
            }
            // a connector that runs along a seam: use its middle
            if (e.pts.Count < 2) return false;
            int m = e.pts.Count / 2;
            point = e.pts[m];
            dir = (e.pts[Mathf.Min(m + 1, e.pts.Count - 1)] - e.pts[Mathf.Max(m - 1, 0)]).normalized;
            return false;
        }

        static bool Intersect(Vector2 p0, Vector2 p1, Vector2 q0, Vector2 q1, out Vector2 x)
        {
            x = Vector2.zero;
            Vector2 r = p1 - p0, s = q1 - q0;
            float den = r.x * s.y - r.y * s.x;
            if (Mathf.Abs(den) < 1e-6f) return false;
            Vector2 qp = q0 - p0;
            float t = (qp.x * s.y - qp.y * s.x) / den;
            float u = (qp.x * r.y - qp.y * r.x) / den;
            if (t < 0 || t > 1 || u < 0 || u > 1) return false;
            x = p0 + r * t;
            return true;
        }

        /// <summary>The exit of <paramref name="from"/> that faces <paramref name="to"/> best.</summary>
        static WExit BestExit(World w, Region from, Region to)
        {
            var c = new Vector2((from.minX + from.maxX) / 2, (from.minZ + from.maxZ) / 2);
            var towards = (new Vector2((to.minX + to.maxX) / 2, (to.minZ + to.maxZ) / 2) - c).normalized;
            WExit best = null;
            float bestScore = float.MinValue;
            foreach (WExit e in w.exits)
            {
                if (e.region != from.index) continue;
                float score = Vector2.Dot(new Vector2(e.x, e.z) - c, towards) + 20f * Vector2.Dot(new Vector2(e.dirX, e.dirZ), towards);
                if (score > bestScore) { bestScore = score; best = e; }
            }
            return best;
        }

        /// <param name="marker">false: no arch and veil, a softer light (the gate still names the nest ahead)</param>
        static Gate Make(World w, RuntimeRefs refs, GateSettings cfg, Transform parent, Vector2 p, Vector2 d, float radius, Color color, Color lightColor, int region, bool portal, bool marker = true)
        {
            var right = new Vector2(d.y, -d.x);
            float y = w.Height(p.x, p.y);
            y = Mathf.Min(y, w.Height(p.x + right.x * radius, p.y + right.y * radius));
            y = Mathf.Min(y, w.Height(p.x - right.x * radius, p.y - right.y * radius));
            var go = new GameObject(portal ? "PortalGate" : "SeamGate");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(p.x, y - 0.2f, p.y);
            go.transform.rotation = Quaternion.LookRotation(new Vector3(d.x, 0, d.y), Vector3.up);

            if (marker)
            {
                var arch = new GameObject("Arch");
                arch.transform.SetParent(go.transform, false);
                arch.AddComponent<MeshFilter>().sharedMesh = ArchMesh(radius, cfg.tube * (portal ? 1.4f : 1f));
                var mr = arch.AddComponent<MeshRenderer>();
                mr.sharedMaterial = refs.slotRing;
                mr.shadowCastingMode = ShadowCastingMode.Off;
                var block = new MaterialPropertyBlock();
                block.SetColor("_Color", color * cfg.glow);
                mr.SetPropertyBlock(block);
            }

            if (marker && refs.gateCurtain != null)
            {
                var curtain = new GameObject("Curtain");
                curtain.transform.SetParent(go.transform, false);
                curtain.AddComponent<MeshFilter>().sharedMesh = CurtainMesh(radius * 0.97f);
                var cr = curtain.AddComponent<MeshRenderer>();
                cr.sharedMaterial = refs.gateCurtain;
                cr.shadowCastingMode = ShadowCastingMode.Off;
                var cb = new MaterialPropertyBlock();
                cb.SetColor("_Color", color);
                cb.SetFloat("_Intensity", cfg.curtain * (portal ? 1.5f : 1f));
                cr.SetPropertyBlock(cb);
            }

            var light = new GameObject("GateLight").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.localPosition = new Vector3(0, radius * 0.55f, 0);
            light.type = LightType.Point;
            light.color = lightColor;
            light.range = cfg.lightRange * (portal ? 1.3f : 1f);
            light.intensity = cfg.lightIntensity * (marker ? 1f : 0.55f);
            light.shadows = LightShadows.None;

            return new Gate { position = new Vector3(p.x, y, p.y), dir = d, radius = radius, region = region, portal = portal, go = go };
        }

        /// <summary>A half torus standing in the local XY plane (feet slightly below the ground).</summary>
        static Mesh ArchMesh(float radius, float tube)
        {
            const int seg = 64, ring = 10;
            var v = new List<Vector3>();
            var n = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            float a0 = -0.08f, a1 = Mathf.PI + 0.08f;
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.Lerp(a0, a1, (float)i / seg);
                var c = new Vector3(Mathf.Cos(a) * radius, Mathf.Sin(a) * radius, 0);
                var outward = new Vector3(Mathf.Cos(a), Mathf.Sin(a), 0);
                for (int j = 0; j <= ring; j++)
                {
                    float b = j * Mathf.PI * 2 / ring;
                    Vector3 dir = outward * Mathf.Cos(b) + Vector3.forward * Mathf.Sin(b);
                    v.Add(c + dir * tube);
                    n.Add(dir);
                    uv.Add(new Vector2((float)i / seg, 0.5f + 0.5f * Mathf.Cos(b)));
                }
            }
            for (int i = 0; i < seg; i++)
            for (int j = 0; j < ring; j++)
            {
                int a = i * (ring + 1) + j, b2 = a + ring + 1;
                t.Add(a); t.Add(b2); t.Add(a + 1);
                t.Add(a + 1); t.Add(b2); t.Add(b2 + 1);
            }
            var mesh = new Mesh { name = "GateArch" };
            mesh.SetVertices(v);
            mesh.SetNormals(n);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>A half disc filling the arch; uv.x runs around the arch, uv.y from the centre (0) to the rim (1).</summary>
        static Mesh CurtainMesh(float radius)
        {
            const int seg = 48, rings = 6;
            var v = new List<Vector3>();
            var uv = new List<Vector2>();
            var t = new List<int>();
            for (int r = 0; r <= rings; r++)
            for (int i = 0; i <= seg; i++)
            {
                float a = Mathf.PI * i / seg, rr = radius * r / rings;
                v.Add(new Vector3(Mathf.Cos(a) * rr, Mathf.Sin(a) * rr, 0));
                uv.Add(new Vector2((float)i / seg, (float)r / rings));
            }
            for (int r = 0; r < rings; r++)
            for (int i = 0; i < seg; i++)
            {
                int a = r * (seg + 1) + i, b = a + seg + 1;
                t.Add(a); t.Add(b); t.Add(a + 1);
                t.Add(a + 1); t.Add(b); t.Add(b + 1);
            }
            var mesh = new Mesh { name = "GateCurtain" };
            mesh.SetVertices(v);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(t, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}

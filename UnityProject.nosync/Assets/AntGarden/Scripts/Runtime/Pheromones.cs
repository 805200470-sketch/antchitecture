using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's pheromones: a little swarm of arrows of light that leads the ant like a sprite
    /// (Shaders/Pheromone.shader). Its heart hovers a few units ahead of the ant along the glowing way, a soft glow
    /// with arrows circling it; more arrows stream out from beside the ant past it and on along the way, weaving,
    /// each pointing where it flies, fading in and out. When the ant stops the swarm waits ahead and keeps
    /// circling; off the way, it waits on the way nearest the ant; at the heart of the nest it settles there.
    /// </summary>
    public class Pheromones : MonoBehaviour
    {
        public int orbiters = 10;           // arrows circling the swarm's heart
        public int streamers = 40;          // arrows streaming on ahead along the way
        public float lead = 2.6f;           // how far ahead of the ant the swarm's heart hovers (units)
        public float reach = 12f;           // how far ahead the streaming arrows fly before they fade
        public float arrowLength = 0.085f, arrowWidth = 0.065f;
        public float gain = 2.1f;           // their brightness (HDR)
        public Color colour = new Color(0.55f, 0.9f, 1f), accent = new Color(0.78f, 0.72f, 1f);

        class Arrow
        {
            public bool orbit;
            public float phase, speed, radius, s, wobble, height, size, hue;
            public Vector3 axis, pos, dir;
            public bool started;
        }

        LabNest lab;
        Transform ant;
        Material mat;
        Mesh mesh;
        readonly List<Arrow> arrows = new List<Arrow>();
        Vector3[] verts, normals;
        Color[] colours;
        Vector2[] uvs, uv2s, uv3s;
        Vector3 core;
        bool coreSet;
        System.Random rng = new System.Random(808);

        public void Init(LabNest nest, Transform player, Material material)
        {
            lab = nest;
            ant = player;
            mat = material;
            if (mat == null || lab == null || lab.Route == null) { enabled = false; return; }
            mat.SetFloat("_Gain", gain);
            for (int i = 0; i < orbiters + streamers; i++)
            {
                var a = new Arrow
                {
                    orbit = i < orbiters,
                    phase = R() * 6.283f,
                    speed = i < orbiters ? (1.6f + 1.6f * R()) * (R() < 0.5f ? 1 : -1) : 2.2f + 1.4f * R(),
                    radius = 0.25f + 0.45f * R(),
                    s = R() * reach,
                    wobble = 0.25f + 0.45f * R(),
                    height = 0.22f + 0.4f * R(),
                    size = 0.75f + 0.5f * R(),
                    hue = R(),
                    axis = new Vector3(R() - 0.5f, 1.6f, R() - 0.5f).normalized,
                };
                arrows.Add(a);
            }
            int n = arrows.Count + 1;   // and the glow at the swarm's heart
            verts = new Vector3[n * 4];
            normals = new Vector3[n * 4];
            colours = new Color[n * 4];
            uvs = new Vector2[n * 4];
            uv2s = new Vector2[n * 4];
            uv3s = new Vector2[n * 4];
            var tris = new int[n * 6];
            for (int i = 0; i < n; i++)
            {
                uvs[i * 4] = new Vector2(-1, -1); uvs[i * 4 + 1] = new Vector2(1, -1); uvs[i * 4 + 2] = new Vector2(1, 1); uvs[i * 4 + 3] = new Vector2(-1, 1);
                tris[i * 6] = i * 4; tris[i * 6 + 1] = i * 4 + 2; tris[i * 6 + 2] = i * 4 + 1;
                tris[i * 6 + 3] = i * 4; tris[i * 6 + 4] = i * 4 + 3; tris[i * 6 + 5] = i * 4 + 2;
            }
            mesh = new Mesh { name = "Pheromones" };
            mesh.MarkDynamic();
            mesh.vertices = verts;
            mesh.uv = uvs;
            mesh.triangles = tris;
        }

        float R() => (float)rng.NextDouble();

        void LateUpdate()
        {
            if (mesh == null || ant == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f), t = Time.time;
            // the way ahead: the nearest glowing way to the ant, followed toward the heart
            List<Vector2> pts = null;
            int at = 0;
            float best = float.MaxValue;
            Vector2 a2 = new Vector2(ant.position.x, ant.position.z);
            foreach (var (tp, _, _) in lab.Route.tracks)
                for (int k = 0; k < tp.Count; k++)
                {
                    float d = (tp[k] - a2).sqrMagnitude;
                    if (d < best) { best = d; pts = tp; at = k; }
                }
            if (pts == null) return;
            bool done = lab.Done;
            // the swarm's heart: ahead of the ant on the way (waiting on the way if the ant has wandered off it), or at
            // the nest's heart once it is reached
            Vector3 coreWant = done ? Ground(lab.Heart, 0.9f) : Along(pts, at, Mathf.Sqrt(best) > 2.5f ? 0.6f : lead, 0.55f + 0.08f * Mathf.Sin(t * 1.3f));
            if (!coreSet) { core = coreWant; coreSet = true; }
            core = Vector3.Lerp(core, coreWant, 1 - Mathf.Exp(-dt * 2.2f));
            int v = 0;
            foreach (Arrow a in arrows)
            {
                Vector3 p;
                float alpha = 1f;
                if (a.orbit || done)
                {
                    // circling the swarm's heart, each on its own tilted ring, breathing in and out
                    a.phase += a.speed * dt;
                    Vector3 ax = a.axis, u = Vector3.Cross(ax, Vector3.right).normalized, w = Vector3.Cross(ax, u);
                    float r = a.radius * (done ? 2.2f : 1f) * (0.85f + 0.15f * Mathf.Sin(t * 0.9f + a.hue * 6f));
                    p = core + (u * Mathf.Cos(a.phase) + w * Mathf.Sin(a.phase)) * r;
                }
                else
                {
                    // streaming on ahead along the way from beside the ant, weaving, fading in and out
                    a.s += a.speed * dt;
                    if (a.s > reach) { a.s = 0; a.started = false; }
                    float sway = Mathf.Sin(t * (1.1f + a.hue) + a.phase) * a.wobble, bob = Mathf.Sin(t * (0.8f + a.hue * 0.7f) + a.phase * 1.7f) * 0.12f;
                    Vector3 on = Along(pts, at, a.s, a.height + bob);
                    Vector3 ahead = Along(pts, at, a.s + 0.4f, a.height + bob);
                    Vector3 fwd = ahead - on;
                    fwd.y = 0;
                    Vector3 side = fwd.sqrMagnitude > 1e-6f ? Vector3.Cross(Vector3.up, fwd.normalized) : Vector3.right;
                    // out of the ant's side at first, then onto the way
                    Vector3 from = ant.position + Vector3.up * (a.height * 0.6f) + side * (a.phase > 3.14f ? 0.25f : -0.25f);
                    p = Vector3.Lerp(from, on + side * sway, Mathf.SmoothStep(0, 1, a.s / 1.4f));
                    alpha = Mathf.SmoothStep(0, 1, a.s / 0.8f) * (1 - Mathf.SmoothStep(0, 1, (a.s - reach * 0.6f) / (reach * 0.4f)));
                }
                // pointing where it flies
                if (a.started)
                {
                    Vector3 vel = (p - a.pos) / Mathf.Max(dt, 1e-4f);
                    if (vel.sqrMagnitude > 1e-4f) a.dir = Vector3.Slerp(a.dir.sqrMagnitude > 0 ? a.dir : vel.normalized, vel.normalized, 1 - Mathf.Exp(-dt * 12f));
                }
                else { a.dir = Vector3.forward; a.started = true; }
                a.pos = p;
                Color c = Color.Lerp(colour, accent, a.hue > 0.85f ? 1f : 0f);
                c.a = alpha * (0.6f + 0.3f * Mathf.Sin(t * 3f + a.phase)) * (a.orbit && !done ? 0.75f : 1f);
                Quad(ref v, p, a.dir, arrowLength * a.size, arrowWidth * a.size, c, false);
            }
            // the glow at the swarm's heart
            Color g = colour * 0.3f;
            g.a = 0.28f + 0.08f * Mathf.Sin(t * 2.1f);
            Quad(ref v, core, Vector3.forward, 0.32f, 0.32f, g, true);
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.colors = colours;
            mesh.SetUVs(1, uv2s);
            mesh.SetUVs(2, uv3s);
            mesh.bounds = new Bounds(ant.position, Vector3.one * (reach * 2 + 6));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        void Quad(ref int v, Vector3 p, Vector3 dir, float length, float width, Color c, bool glow)
        {
            for (int k = 0; k < 4; k++)
            {
                verts[v + k] = p;
                normals[v + k] = dir;
                colours[v + k] = c;
                uv2s[v + k] = new Vector2(length, width);
                uv3s[v + k] = new Vector2(glow ? 1 : 0, 0);
            }
            v += 4;
        }

        /// <summary>The point on the way s units on from point at (toward the nest's heart), height above the floor.</summary>
        Vector3 Along(List<Vector2> pts, int at, float s, float height)
        {
            int k = at;
            Vector2 p = pts[k];
            while (s > 0 && k + 1 < pts.Count)
            {
                float seg = (pts[k + 1] - pts[k]).magnitude;
                if (seg >= s) { p = Vector2.Lerp(pts[k], pts[k + 1], s / Mathf.Max(seg, 1e-5f)); s = 0; break; }
                s -= seg;
                k++;
                p = pts[k];
            }
            return Ground(p, height);
        }

        Vector3 Ground(Vector2 p, float height) => new Vector3(p.x, lab.WalkHeight(p) + height, p.y);
    }
}

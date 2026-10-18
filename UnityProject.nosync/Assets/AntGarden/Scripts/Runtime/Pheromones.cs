using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's pheromones (Shaders/Pheromone.shader): a stream of many tiny arrows of light flowing along
    /// the way toward the heart, in the air a little ahead of the ant, never out of it: it rises out of nothing a couple
    /// of units ahead and runs on past ten more, every arrow pointing where it flies, so the way on reads at a glance.
    /// It is alive like a shoal: the stream as a whole snakes from side to side and rises and dips in slow travelling
    /// waves; inside it each arrow keeps its own lane and pace, weaving a little, the stream bunching and thinning as
    /// gusts run along it, now and then one turning a little loop, an eddy; arrows fade in and out, twinkling. When
    /// the ant stops the stream keeps flowing, a current
    /// pointing the way; at the heart of the nest it turns into a slow vortex over it.
    /// </summary>
    public class Pheromones : MonoBehaviour
    {
        public int arrows = 300;
        public float from = 0.9f, to = 12f;     // the stream runs from this far ahead of the ant along the way to this far
        public float speed = 2.8f;              // how fast the arrows fly along it (units/s)
        public float height = 0.75f;            // how high it flows above the floor
        public float halfWidth = 0.38f;         // how wide the stream is either side of its middle
        public float arrowLength = 0.05f, arrowWidth = 0.038f;
        public float gain = 1.9f;               // their brightness (HDR)
        public Color colour = new Color(0.55f, 0.9f, 1f), accent = new Color(0.8f, 0.72f, 1f);

        class Arrow
        {
            public float s, lane, up, pace, phase, size, hue;
            public float loop = -1, loopAt, loopSpin;   // now and then it turns a little loop in the stream (an eddy)
            public Vector3 pos, dir;
            public bool started;
        }

        LabNest lab;
        Transform ant;
        Material mat;
        Mesh mesh;
        readonly List<Arrow> list = new List<Arrow>();
        Vector3[] verts, normals;
        Color[] colours;
        Vector2[] uvs, uv2s, uv3s;
        readonly System.Random rng = new System.Random(808);

        public void Init(LabNest nest, Transform player, Material material)
        {
            lab = nest;
            ant = player;
            mat = material;
            if (mat == null || lab == null || lab.Route == null) { enabled = false; return; }
            mat.SetFloat("_Gain", gain);
            for (int i = 0; i < arrows; i++)
                list.Add(new Arrow
                {
                    s = Mathf.Lerp(from, to, R()),
                    lane = Gauss() * 0.45f,
                    up = Gauss() * 0.5f,
                    pace = 0.8f + 0.4f * R(),
                    phase = R() * 6.283f,
                    loopAt = 1f + 9f * R(),
                    loopSpin = R() < 0.5f ? 1 : -1,
                    size = 0.75f + 0.5f * R(),
                    hue = R(),
                });
            int n = list.Count;
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

        /// <summary>Roughly normal, mean 0, about -1..1 mostly.</summary>
        float Gauss() => (R() + R() + R() - 1.5f) * 1.15f;

        void LateUpdate()
        {
            if (mesh == null || ant == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f), t = Time.time;
            // the way: the nearest glowing way to the ant, followed toward the heart
            List<Vector2> pts = null;
            int at = 0;
            float best = float.MaxValue;
            var a2 = new Vector2(ant.position.x, ant.position.z);
            foreach (var (tp, _, _) in lab.Route.tracks)
                for (int k = 0; k < tp.Count; k++)
                {
                    float d = (tp[k] - a2).sqrMagnitude;
                    if (d < best) { best = d; pts = tp; at = k; }
                }
            if (pts == null) return;
            bool done = lab.Done;
            Vector3 heart = Ground(lab.Heart, 0);
            int v = 0;
            foreach (Arrow a in list)
            {
                Vector3 p;
                float alpha;
                if (!done)
                {
                    // along the way: gusts run along the stream, the arrows bunching and thinning as they pass
                    float gust = 1 + 0.35f * Mathf.Sin(0.7f * a.s - 2f * t + a.phase * 0.2f);
                    a.s += speed * a.pace * gust * dt;
                    if (a.s > to) { a.s = from + 0.6f * R(); a.started = false; }
                    Vector3 on = Along(pts, at, a.s), ahead = Along(pts, at, a.s + 0.3f);
                    Vector3 fwd = ahead - on;
                    fwd.y = 0;
                    fwd = fwd.sqrMagnitude > 1e-8f ? fwd.normalized : Vector3.forward;
                    Vector3 side = Vector3.Cross(Vector3.up, fwd);
                    // the stream snakes and rises in travelling waves; each arrow keeps its lane, weaving a little
                    float snake = 0.35f * Mathf.Sin(0.55f * a.s - 1.3f * t);
                    float lateral = snake + a.lane * halfWidth + 0.07f * Mathf.Sin(3.1f * t + a.phase);
                    float rise = height + 0.28f * Mathf.Sin(0.4f * a.s - 0.9f * t + 1.7f) + a.up * 0.18f + 0.05f * Mathf.Sin(2.3f * t + a.phase * 1.3f);
                    // now and then an arrow turns a little loop, an eddy in the stream, and runs on
                    a.loopAt -= dt;
                    if (a.loop < 0 && a.loopAt <= 0) { a.loop = 0; a.loopAt = 4f + 10f * R(); }
                    if (a.loop >= 0)
                    {
                        a.loop += dt / 1.1f;
                        float ang = Mathf.Clamp01(a.loop) * 6.283f, rr = 0.22f * Mathf.Sin(Mathf.Clamp01(a.loop) * Mathf.PI);
                        lateral += a.loopSpin * Mathf.Sin(ang) * rr;
                        rise += (1 - Mathf.Cos(ang)) * rr * 0.6f;
                        a.s -= speed * a.pace * dt * 0.6f * Mathf.Sin(Mathf.Clamp01(a.loop) * Mathf.PI);   // it hangs back as it turns
                        if (a.loop >= 1) a.loop = -1;
                    }
                    p = on + side * lateral + Vector3.up * rise;
                    alpha = Mathf.SmoothStep(0, 1, (a.s - from) / 1.2f) * (1 - Mathf.SmoothStep(0, 1, (a.s - (to - 3.5f)) / 3.5f));
                }
                else
                {
                    // at the heart: a slow vortex over it
                    a.s += speed * 0.25f * a.pace * dt;
                    float r = 0.7f + 0.9f * Mathf.Abs(a.lane), ang = a.s * 0.9f + a.phase;
                    p = heart + new Vector3(Mathf.Cos(ang) * r, 0.6f + 1.2f * Mathf.Abs(a.up) + 0.15f * Mathf.Sin(t + a.phase), Mathf.Sin(ang) * r);
                    alpha = 0.85f;
                }
                // it follows its place smoothly (a jump between ways is not seen) and points where it flies
                if (!a.started) { a.pos = p; a.dir = Vector3.forward; a.started = true; }
                Vector3 prev = a.pos;
                a.pos = Vector3.Lerp(a.pos, p, 1 - Mathf.Exp(-dt * 14f));
                Vector3 vel = (a.pos - prev) / Mathf.Max(dt, 1e-4f);
                if (vel.sqrMagnitude > 1e-4f) a.dir = Vector3.Slerp(a.dir, vel.normalized, 1 - Mathf.Exp(-dt * 12f));
                Color c = Color.Lerp(colour, accent, a.hue > 0.86f ? 1f : 0f);
                c.a = alpha * (0.6f + 0.3f * Mathf.Sin(t * (2.2f + a.hue * 2f) + a.phase * 3f));
                for (int k = 0; k < 4; k++)
                {
                    verts[v + k] = a.pos;
                    normals[v + k] = a.dir;
                    colours[v + k] = c;
                    uv2s[v + k] = new Vector2(arrowLength * a.size, arrowWidth * a.size);
                    uv3s[v + k] = Vector2.zero;
                }
                v += 4;
            }
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.colors = colours;
            mesh.SetUVs(1, uv2s);
            mesh.SetUVs(2, uv3s);
            mesh.bounds = new Bounds(ant.position, Vector3.one * (to * 2 + 8));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        /// <summary>The point on the floor s units on along the way from point at (toward the nest's heart).</summary>
        Vector3 Along(List<Vector2> pts, int at, float s)
        {
            int k = at;
            Vector2 p = pts[k];
            while (s > 0 && k + 1 < pts.Count)
            {
                float seg = (pts[k + 1] - pts[k]).magnitude;
                if (seg >= s) { p = Vector2.Lerp(pts[k], pts[k + 1], s / Mathf.Max(seg, 1e-5f)); break; }
                s -= seg;
                k++;
                p = pts[k];
            }
            return Ground(p, 0);
        }

        Vector3 Ground(Vector2 p, float h) => new Vector3(p.x, lab.WalkHeight(p) + h, p.y);
    }
}

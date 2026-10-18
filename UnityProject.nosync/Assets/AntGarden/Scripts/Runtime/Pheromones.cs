using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's pheromones: a sprite of little arrows of light that leads the ant (Shaders/Pheromone.shader).
    /// It hovers in the air a few units ahead along the way, never at the ant: its arrows dance round it on their own
    /// tilted, slowly turning loops, now and then one or two sweeping out ahead along the way and curving back, each
    /// arrow pointing where it flies, brightening and fading as it goes. As the ant comes near, the sprite drifts on
    /// ahead; when the ant stops it waits, dancing; off the way it waits over the way nearest the ant; at the heart of
    /// the nest it settles and circles there.
    /// </summary>
    public class Pheromones : MonoBehaviour
    {
        public int arrows = 34;
        public float lead = 4.2f;           // how far ahead of the ant along the way the sprite hovers (units)
        public float height = 1.25f;        // and how high above the floor
        public float arrowLength = 0.075f, arrowWidth = 0.058f;
        public float gain = 2.2f;           // their brightness (HDR)
        public Color colour = new Color(0.55f, 0.9f, 1f), accent = new Color(0.8f, 0.72f, 1f);

        class Arrow
        {
            public float phase, speed, radius, tilt, turn, size, hue, sweep, sweepAt;
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
        Vector3 centre, velocity;
        bool centreSet;
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
                    phase = R() * 6.283f,
                    speed = (0.9f + 1.4f * R()) * (R() < 0.5f ? 1 : -1),
                    radius = 0.35f + 0.85f * R(),
                    tilt = (R() - 0.5f) * 1.6f,
                    turn = R() * 6.283f,
                    size = 0.75f + 0.5f * R(),
                    hue = R(),
                    sweep = -1,
                    sweepAt = 2f + 10f * R(),
                });
            int n = list.Count + 1;   // and the faint glow at the sprite's heart
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
            // where the sprite wants to be: ahead along the way, in the air; over the heart once it is reached
            float sway = Mathf.Sin(t * 0.37f) * 0.6f;
            Vector3 want = done ? Ground(lab.Heart, height + 0.6f)
                : Along(pts, at, (Mathf.Sqrt(best) > 3f ? 2.5f : lead) + sway, height + 0.25f * Mathf.Sin(t * 0.7f));
            if (!centreSet) { centre = want; centreSet = true; }
            // it drifts there smoothly (a spring, critically damped), never darting
            const float k0 = 2.2f;
            Vector3 acc = (want - centre) * (k0 * k0) - velocity * (2f * k0);
            velocity += acc * dt;
            centre += velocity * dt;
            Vector3 fwdWay = Along(pts, at, lead + 1.5f, 0) - Along(pts, at, lead, 0);
            fwdWay.y = 0;
            fwdWay = fwdWay.sqrMagnitude > 1e-6f ? fwdWay.normalized : Vector3.forward;
            int v = 0;
            foreach (Arrow a in list)
            {
                // its own loop round the sprite's heart: a tilted ring that slowly turns, breathing in and out
                a.phase += a.speed * dt;
                float spin = a.turn + t * 0.23f * (a.hue - 0.5f);
                Vector3 ax = new Vector3(Mathf.Sin(a.tilt) * Mathf.Cos(spin), Mathf.Cos(a.tilt), Mathf.Sin(a.tilt) * Mathf.Sin(spin));
                Vector3 u = Vector3.Cross(ax, Vector3.right).normalized, w = Vector3.Cross(ax, u);
                float r = a.radius * (done ? 1.8f : 1f) * (0.8f + 0.2f * Mathf.Sin(t * 0.9f + a.hue * 6f));
                Vector3 p = centre + (u * Mathf.Cos(a.phase) + w * Mathf.Sin(a.phase)) * r;
                // now and then it sweeps out ahead along the way and curves back into the dance
                a.sweepAt -= dt;
                if (a.sweep < 0 && a.sweepAt <= 0 && !done) { a.sweep = 0; a.sweepAt = 6f + 10f * R(); }
                if (a.sweep >= 0)
                {
                    a.sweep += dt / 2.6f;
                    float s = Mathf.Sin(Mathf.Clamp01(a.sweep) * Mathf.PI);
                    Vector3 side = Vector3.Cross(Vector3.up, fwdWay);
                    p += fwdWay * (s * 3.2f) + side * (Mathf.Sin(a.sweep * 6.283f) * 0.6f * s) + Vector3.up * (0.3f * s);
                    if (a.sweep >= 1) a.sweep = -1;
                }
                // pointing where it flies
                if (a.started)
                {
                    Vector3 vel = (p - a.pos) / Mathf.Max(dt, 1e-4f);
                    if (vel.sqrMagnitude > 1e-4f) a.dir = Vector3.Slerp(a.dir, vel.normalized, 1 - Mathf.Exp(-dt * 10f));
                }
                else { a.dir = fwdWay; a.started = true; }
                a.pos = p;
                Color c = Color.Lerp(colour, accent, a.hue > 0.82f ? 1f : 0f);
                c.a = 0.55f + 0.35f * Mathf.Sin(t * (1.6f + a.hue) + a.phase * 2f);
                Quad(ref v, p, a.dir, arrowLength * a.size, arrowWidth * a.size, c, false);
            }
            // a faint glow at the sprite's heart
            Color g = colour * 0.25f;
            g.a = 0.3f + 0.1f * Mathf.Sin(t * 2.1f);
            Quad(ref v, centre, Vector3.forward, 0.45f, 0.45f, g, true);
            mesh.vertices = verts;
            mesh.normals = normals;
            mesh.colors = colours;
            mesh.SetUVs(1, uv2s);
            mesh.SetUVs(2, uv3s);
            mesh.bounds = new Bounds(centre, Vector3.one * 12f);
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
        Vector3 Along(List<Vector2> pts, int at, float s, float h)
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
            return Ground(p, h);
        }

        Vector3 Ground(Vector2 p, float h) => new Vector3(p.x, lab.WalkHeight(p) + h, p.y);
    }
}

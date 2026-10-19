using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's guiding threads of light (Shaders/LightThread.shader), after a forest at night where one thin
    /// line of light hangs down into the moss: they hang in the space ahead, never at the ant, and come and go.
    /// - Beacons: fine vertical threads dropping from the dark onto the way ahead, every few units along it toward the
    ///   heart; each lights a soft pool on the floor below it (_Beacons, NestField.cginc), breathes and now and then
    ///   all but goes out, and fades as the ant draws near and away as it falls behind, while the next ones further on
    ///   come up out of the dark. At the heart of the nest one hangs over it, steady.
    /// - Drifting threads: a few curling threads of light floating in the air over the way ahead, slowly turning and
    ///   drifting toward the heart, each living a few seconds; a pulse of light runs along each from its tail to its head,
    ///   the way on.
    /// </summary>
    public class GuideThreads : MonoBehaviour
    {
        public float spacing = 7f;                           // between beacons along the way (units)
        public Vector2 beaconFade = new Vector2(2.5f, 4.5f); // a beacon fades away as the ant comes within x..y of it
        public float beaconReach = 24f;                      // beacons show this far ahead
        public int drifters = 11;
        public float gain = 2.6f;
        public Color core = new Color(0.92f, 0.95f, 0.9f), halo = new Color(0.55f, 0.9f, 0.95f);

        class Drifter
        {
            public float along, side, height, length, tilt, phase, life, age, curl, speed;
            public Vector3 origin, dir;
        }

        LabNest lab;
        Transform ant;
        Camera cam;
        Material mat;
        Mesh mesh;
        readonly List<Drifter> drift = new List<Drifter>();
        readonly List<Vector3> verts = new List<Vector3>(4096);
        readonly List<Color> cols = new List<Color>(4096);
        readonly List<Vector2> uvs = new List<Vector2>(4096);
        readonly List<int> tris = new List<int>(8192);
        readonly Vector4[] beacons = new Vector4[4];
        readonly System.Random rng = new System.Random(4711);

        public void Init(LabNest nest, Transform player, Material material)
        {
            lab = nest;
            ant = player;
            mat = material;
            if (mat == null || lab == null || lab.Route == null) { enabled = false; return; }
            mat.SetFloat("_Gain", gain);
            mat.SetColor("_Core", core.linear);
            mat.SetColor("_Halo", halo.linear);
            mesh = new Mesh { name = "GuideThreads" };
            mesh.MarkDynamic();
            for (int i = 0; i < drifters; i++) drift.Add(new Drifter { age = 999 });
            Shader.SetGlobalVectorArray("_Beacons", beacons);
            Shader.SetGlobalColor("_BeaconColor", new Color(0.62f, 0.86f, 0.84f).linear);
        }

        float R() => (float)rng.NextDouble();

        /// <summary>Where the ant is on the ways: the track nearest it, the point there, and that point's distance to the heart.</summary>
        bool Locate(Vector2 a, out List<Vector2> pts, out List<float> ds, out int at)
        {
            pts = null;
            ds = null;
            at = 0;
            float best = float.MaxValue;
            foreach (var (tp, td, _) in lab.Route.tracks)
                for (int k = 0; k < tp.Count; k++)
                {
                    float d = (tp[k] - a).sqrMagnitude;
                    if (d < best) { best = d; pts = tp; ds = td; at = k; }
                }
            return pts != null;
        }

        /// <summary>The point at distance d from the heart along this track (it runs toward it), or, past this track's
        /// end (another way in joins the main one), along the main track; and the way on there.</summary>
        Vector2 AtDistance(List<Vector2> pts, List<float> ds, float d, out Vector2 dir)
        {
            if (OnTrack(pts, ds, d, out Vector2 p, out dir)) return p;
            foreach (var (tp, td, main) in lab.Route.tracks)
                if (main && OnTrack(tp, td, d, out p, out dir)) return p;
            dir = Vector2.up;
            return lab.Heart;
        }

        static bool OnTrack(List<Vector2> pts, List<float> ds, float d, out Vector2 p, out Vector2 dir)
        {
            p = Vector2.zero;
            dir = Vector2.up;
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                float d0 = ds[k], d1 = ds[k + 1];
                if ((d0 - d) * (d1 - d) > 0) continue;
                float t = Mathf.Abs(d0 - d1) > 1e-5f ? (d0 - d) / (d0 - d1) : 0;
                dir = (d1 < d0 ? pts[k + 1] - pts[k] : pts[k] - pts[k + 1]).normalized;
                p = Vector2.Lerp(pts[k], pts[k + 1], t);
                return true;
            }
            return false;
        }

        static float Noise1(float x)
        {
            int i = Mathf.FloorToInt(x);
            float f = x - i, u = f * f * (3 - 2 * f);
            return Mathf.Lerp(Hash(i), Hash(i + 1), u);
        }

        static float Hash(int i)
        {
            uint h = (uint)i * 2654435761u;
            h ^= h >> 15;
            h *= 2246822519u;
            h ^= h >> 13;
            return (h & 0xFFFFFF) / 16777216f;
        }

        void LateUpdate()
        {
            if (mesh == null || ant == null) return;
            if (cam == null) cam = Camera.main;
            if (cam == null) return;
            float dt = Mathf.Min(Time.deltaTime, 0.1f), t = Time.time;
            verts.Clear();
            cols.Clear();
            uvs.Clear();
            tris.Clear();
            var a2 = new Vector2(ant.position.x, ant.position.z);
            if (!Locate(a2, out var pts, out var ds, out int at)) return;
            float playerD = ds[at];
            int nb = 0;
            if (lab.Done)
            {
                // over the heart: one steady thread
                Vector2 h = lab.Heart;
                Beacon(ref nb, h, 0.9f + 0.1f * Mathf.Sin(t * 0.8f), 7f, t, 99);
            }
            else
            {
                // beacons at fixed places along the way (every spacing units from the heart): the ones ahead show
                int first = Mathf.FloorToInt((playerD - beaconFade.x) / spacing);
                for (int k = first; k >= 0 && nb < 4; k--)
                {
                    float d = k * spacing, ahead = playerD - d;
                    if (ahead > beaconReach) continue;
                    float show = Mathf.SmoothStep(0, 1, (ahead - beaconFade.x) / (beaconFade.y - beaconFade.x)) * (1 - Mathf.SmoothStep(0, 1, (ahead - beaconReach * 0.75f) / (beaconReach * 0.25f)));
                    if (show <= 0.01f) continue;
                    Vector2 p = AtDistance(pts, ds, d, out Vector2 _);
                    Beacon(ref nb, p, show, 8f + 3f * Hash(k * 7 + 3), t, k);
                }
            }
            for (int k = nb; k < 4; k++) beacons[k] = Vector4.zero;
            Shader.SetGlobalVectorArray("_Beacons", beacons);
            // the drifting threads over the way ahead
            foreach (Drifter f in drift)
            {
                f.age += dt;
                if (f.age > f.life) Spawn(f, pts, ds, playerD);
                f.along += f.speed * dt;
                Draw(f, t);
            }
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(ant.position, Vector3.one * (beaconReach * 2 + 20));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        /// <summary>A vertical thread from the dark down onto the floor at p, its pool of light below it; it breathes and
        /// now and then all but goes out.</summary>
        void Beacon(ref int nb, Vector2 p, float show, float height, float t, int seed)
        {
            float flicker = 0.55f + 0.45f * Noise1(t * 0.55f + seed * 13.1f);
            float dip = Noise1(t * 0.23f + seed * 7.7f);
            flicker *= Mathf.Lerp(0.15f, 1f, Mathf.SmoothStep(0.12f, 0.3f, dip));   // now and then it nearly goes out
            float alpha = show * flicker;
            float y0 = lab.WalkHeight(p);
            var bottom = new Vector3(p.x, y0 - 0.05f, p.y);
            var top = bottom + Vector3.up * height;
            // brighter toward the floor, the top fading into the dark
            const int n = 12;
            int start = verts.Count;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n;
                Vector3 q = Vector3.Lerp(bottom, top, u);
                float a = alpha * (1 - Mathf.SmoothStep(0.45f, 1f, u)) * (0.75f + 0.25f * (1 - u));
                Ribbon(q, Vector3.up, 0.06f, u, a, i == 0 ? -1 : start + (i - 1) * 2);
            }
            if (nb < 4) beacons[nb++] = new Vector4(p.x, y0, p.y, alpha * 1.3f);
        }

        void Spawn(Drifter f, List<Vector2> pts, List<float> ds, float playerD)
        {
            f.age = 0;
            f.life = 5f + 4f * R();
            f.along = Mathf.Max(0, playerD - (2f + 12f * R()));   // a distance to the heart, ahead of the ant
            f.side = (R() - 0.5f) * 2.6f;
            f.height = 0.4f + 2.2f * R() * R();
            f.length = 1.2f + 2.4f * R();
            f.tilt = (R() - 0.5f) * 0.6f;
            f.phase = R() * 6.283f;
            f.curl = 0.6f + 1.4f * R();
            f.speed = -(0.25f + 0.25f * R());   // drifting toward the heart: its distance to it shrinks
            Vector2 p = AtDistance(pts, ds, f.along, out Vector2 dir);
            var side = new Vector2(-dir.y, dir.x);
            Vector2 at = p + side * f.side;
            f.origin = new Vector3(at.x, lab.WalkHeight(at) + f.height, at.y);
            f.dir = new Vector3(dir.x, f.tilt * 0.3f, dir.y).normalized;
        }

        /// <summary>A drifting thread: a curling line along the way, alive for its life, a pulse running along it.</summary>
        void Draw(Drifter f, float t)
        {
            float life = Mathf.SmoothStep(0, 1, f.age / 1.4f) * (1 - Mathf.SmoothStep(0, 1, (f.age - (f.life - 2f)) / 2f));
            if (life <= 0.01f) return;
            Vector3 dir = f.dir, side = Vector3.Cross(Vector3.up, dir).normalized, up = Vector3.Cross(dir, side);
            Vector3 origin = f.origin + dir * (-f.speed * f.age);   // drifting on
            const int n = 28;
            int start = verts.Count;
            Vector3 prev = origin;
            for (int i = 0; i <= n; i++)
            {
                float u = i / (float)n, x = u * f.length;
                float w = f.curl * 0.18f;
                // a slowly turning curl
                float s1 = Mathf.Sin(x * 2.3f + f.phase + t * 0.6f) * w, s2 = Mathf.Cos(x * 1.7f + f.phase * 1.3f + t * 0.45f) * w * 0.7f;
                Vector3 q = origin + dir * x + side * s1 + up * s2;
                Vector3 tangent = i == 0 ? dir : (q - prev).normalized;
                prev = q;
                // thin at its ends; a pulse of light runs from its tail to its head, the way on
                float ends = Mathf.Sin(u * Mathf.PI);
                float pulse = Mathf.Exp(-Mathf.Pow(Mathf.Repeat(u - t * 0.55f - f.phase, 1f) - 0.5f, 2) * 60f);
                float a = life * ends * (0.35f + 0.9f * pulse) * (0.75f + 0.25f * Noise1(t * 1.3f + f.phase * 10));
                Ribbon(q, tangent, 0.045f, u, a, i == 0 ? -1 : start + (i - 1) * 2);
            }
        }

        /// <summary>Two vertices across a thread at q (facing the camera), joined to the previous pair.</summary>
        void Ribbon(Vector3 q, Vector3 tangent, float halfWidth, float u, float alpha, int prevPair)
        {
            Vector3 view = (q - cam.transform.position);
            float dist = view.magnitude;
            Vector3 across = Vector3.Cross(tangent, view / Mathf.Max(dist, 1e-4f)).normalized;
            // never thinner than about two pixels, so a far thread does not flicker away
            float pixel = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            float w = Mathf.Max(halfWidth, pixel * 3f);
            float thin = halfWidth / w;   // spread over more pixels: dimmer
            var c = new Color(1, 1, 1, alpha * Mathf.Lerp(0.35f, 1f, thin));
            int v = verts.Count;
            verts.Add(q - across * w);
            verts.Add(q + across * w);
            cols.Add(c);
            cols.Add(c);
            uvs.Add(new Vector2(u, -1));
            uvs.Add(new Vector2(u, 1));
            if (prevPair >= 0)
            {
                tris.Add(prevPair); tris.Add(prevPair + 1); tris.Add(v + 1);
                tris.Add(prevPair); tris.Add(v + 1); tris.Add(v);
            }
        }

        void OnDestroy() => Shader.SetGlobalVectorArray("_Beacons", new Vector4[4]);
    }
}

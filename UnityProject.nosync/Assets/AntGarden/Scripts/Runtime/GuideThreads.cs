using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's guiding filaments of light (Shaders/LightThread.shader), after a forest at night threaded with
    /// fine, angular lines of light: continuous filaments hanging in the air over the way, from the way in to the heart.
    /// Each is a line of straight runs that turn sharply now and then (a kink, a tooth), wandering from side to side and
    /// rising and falling between a little above the ant and well over its head; here and there a short fork leaves one
    /// and stops, and now and then one drops straight down to the floor and lights a pool there. One runs the whole way;
    /// others join it for a stretch, higher up, so in places there are two or three. They are made once, so they stay
    /// where they are: the ant follows them. Only the stretch ahead of the ant shows, coming up out of the dark far on
    /// and fading just behind it; light runs along them toward the heart, and they flicker a little. At the heart the
    /// last stretch stays lit.
    /// </summary>
    public class GuideThreads : MonoBehaviour
    {
        public float reach = 26f;            // how far ahead along the way the filaments show (units)
        public float gain = 2.4f;
        public Color core = new Color(0.92f, 0.95f, 0.9f), halo = new Color(0.55f, 0.9f, 0.95f);

        /// <summary>A line of light: its points, and each one's distance to the heart along the way (for the window
        /// ahead of the ant), how bright it is, and whether it drops to the floor (a pool of light there).</summary>
        class Filament
        {
            public readonly List<Vector3> pts = new List<Vector3>();
            public readonly List<float> d = new List<float>();
            public float bright = 1f, width = 0.022f, seed;
            public bool drop;
        }

        LabNest lab;
        Transform ant;
        Camera cam;
        Material mat;
        Mesh mesh;
        readonly List<Filament> lines = new List<Filament>();
        readonly List<Vector3> verts = new List<Vector3>(8192);
        readonly List<Color> cols = new List<Color>(8192);
        readonly List<Vector2> uvs = new List<Vector2>(8192), uv2s = new List<Vector2>(8192);
        readonly List<int> tris = new List<int>(16384);
        readonly Vector4[] beacons = new Vector4[4];
        readonly System.Random rng = new System.Random(4711);
        float lastD = 999f;

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
            Shader.SetGlobalVectorArray("_Beacons", beacons);
            Shader.SetGlobalColor("_BeaconColor", new Color(0.62f, 0.86f, 0.84f).linear);
            Build();
        }

        float R() => (float)rng.NextDouble();
        float U(float a, float b) => a + (b - a) * R();

        /// <summary>The filaments, once: one along every way (the main one whole, the others until they meet it), and a
        /// few higher ones joining the main one for a stretch.</summary>
        void Build()
        {
            List<Vector2> mainPts = null;
            List<float> mainDs = null;
            foreach (var (pts, ds, main) in lab.Route.tracks)
                if (main) { mainPts = pts; mainDs = ds; }
            foreach (var (pts, ds, main) in lab.Route.tracks)
            {
                if (pts.Count < 2) continue;
                float dFar = Mathf.Max(ds[0], ds[ds.Count - 1]), dNear = Mathf.Min(ds[0], ds[ds.Count - 1]);
                if (!main && mainPts != null)
                {
                    // a way in: until it comes near the main way
                    for (float d = dFar; d > dNear; d -= 1f)
                    {
                        Vector2 p = OnTrack(pts, ds, d, out _);
                        if (NearTrack(mainPts, p) < 1.6f) { dNear = d; break; }
                    }
                }
                Grow(pts, ds, dFar, Mathf.Max(0, dNear), 0.9f, 3.2f, 1.2f, 1f);
            }
            if (mainPts != null)
            {
                float dFar = Mathf.Max(mainDs[0], mainDs[mainDs.Count - 1]);
                for (float at = dFar - U(4, 14); at > 6; at -= U(10, 26))
                {
                    float len = U(8, 28), dEnd = Mathf.Max(1, at - len);
                    Grow(mainPts, mainDs, at, dEnd, 1.8f, 5.6f, 2f, 0.75f);
                }
            }
            int n = 0;
            foreach (Filament f in lines) n += f.pts.Count;
            Debug.Log($"[AntGarden] guiding filaments: {lines.Count} lines, {n} points");
        }

        /// <summary>
        /// A filament along a way from distance dFrom to dTo from the heart: straight runs of varied length, each turning
        /// sharply now and then (side, height or both), now and then a tooth (out and back over a short way); forks and
        /// drops to the floor here and there. Height between hMin and hMax over the floor, never further to a side than
        /// side.
        /// </summary>
        void Grow(List<Vector2> pts, List<float> ds, float dFrom, float dTo, float hMin, float hMax, float side, float bright)
        {
            var f = new Filament { bright = bright, seed = R() * 100, width = 0.018f + 0.01f * R() };
            float lat = U(-0.4f, 0.4f) * side, ht = U(hMin, (hMin + hMax) * 0.5f);
            float nextDrop = dFrom - U(4, 14);
            for (float d = dFrom; d > dTo; )
            {
                Vector2 p = OnTrack(pts, ds, d, out Vector2 dir);
                Vector2 q = p + new Vector2(-dir.y, dir.x) * lat;
                var v = new Vector3(q.x, lab.WalkHeight(q) + ht, q.y);
                f.pts.Add(v);
                f.d.Add(d);
                // a fork: a few short runs off to one side or up, and it stops
                if (R() < 0.06f) Fork(v, new Vector3(dir.x, 0, dir.y), d, bright * 0.6f, f.width * 0.8f);
                // a drop: down to the floor, a pool of light there
                if (d < nextDrop)
                {
                    nextDrop = d - U(5, 16);
                    var drop = new Filament { bright = bright * 0.85f, seed = R() * 100, width = f.width * 0.9f, drop = true };
                    float floor = lab.WalkHeight(q);
                    if (R() < 0.35f)
                    {
                        // it bends once on the way down
                        var mid = new Vector3(v.x + U(-0.4f, 0.4f), Mathf.Lerp(floor, v.y, U(0.35f, 0.7f)), v.z + U(-0.4f, 0.4f));
                        drop.pts.Add(v); drop.pts.Add(mid); drop.pts.Add(new Vector3(mid.x, floor - 0.05f, mid.z));
                        drop.d.Add(d); drop.d.Add(d); drop.d.Add(d);
                    }
                    else
                    {
                        drop.pts.Add(v); drop.pts.Add(new Vector3(v.x, floor - 0.05f, v.z));
                        drop.d.Add(d); drop.d.Add(d);
                    }
                    lines.Add(drop);
                }
                // the next run: mostly straight on, now and then a sharp turn, now and then a tooth
                float step = R() < 0.15f ? U(1.6f, 2.6f) : U(0.35f, 1.5f);
                if (R() < 0.3f)
                {
                    lat += U(-1f, 1f) * 0.55f * side;
                    ht += U(-1f, 1f) * 0.8f;
                }
                else
                {
                    lat += U(-1f, 1f) * 0.12f;
                    ht += U(-1f, 1f) * 0.1f;
                }
                if (R() < 0.07f)
                {
                    float half = Mathf.Min(step * 0.5f, 0.3f);
                    Vector2 p2 = OnTrack(pts, ds, d - half, out Vector2 dir2);
                    Vector2 q2 = p2 + new Vector2(-dir2.y, dir2.x) * (lat + U(-0.5f, 0.5f));
                    f.pts.Add(new Vector3(q2.x, lab.WalkHeight(q2) + ht + (R() < 0.5f ? 1 : -1) * U(0.35f, 0.8f), q2.y));
                    f.d.Add(d - half);
                }
                if (Mathf.Abs(lat) > side) lat = Mathf.Sign(lat) * (2 * side - Mathf.Abs(lat));
                ht = Mathf.Clamp(ht, hMin, hMax);
                d -= step;
            }
            if (f.pts.Count >= 2) lines.Add(f);
        }

        void Fork(Vector3 from, Vector3 along, float d, float bright, float width)
        {
            var f = new Filament { bright = bright, seed = R() * 100, width = width };
            Vector3 dir = (along * U(-0.3f, 0.6f) + Vector3.Cross(Vector3.up, along) * (R() < 0.5f ? 1 : -1) + Vector3.up * U(-0.3f, 0.9f)).normalized;
            Vector3 p = from;
            int runs = 2 + (int)(R() * 4);
            for (int i = 0; i <= runs; i++)
            {
                f.pts.Add(p);
                f.d.Add(d);
                // each run turns sharply from the last
                Vector3 turn = new Vector3(U(-1, 1), U(-0.6f, 1), U(-1, 1)).normalized;
                dir = (dir + turn * 0.9f).normalized;
                p += dir * U(0.25f, 0.75f);
            }
            lines.Add(f);
        }

        static Vector2 OnTrack(List<Vector2> pts, List<float> ds, float d, out Vector2 dir)
        {
            dir = Vector2.up;
            for (int k = 0; k + 1 < pts.Count; k++)
            {
                float d0 = ds[k], d1 = ds[k + 1];
                if ((d0 - d) * (d1 - d) > 0) continue;
                float t = Mathf.Abs(d0 - d1) > 1e-5f ? (d0 - d) / (d0 - d1) : 0;
                dir = (d1 < d0 ? pts[k + 1] - pts[k] : pts[k] - pts[k + 1]).normalized;
                return Vector2.Lerp(pts[k], pts[k + 1], t);
            }
            int last = ds[0] < ds[ds.Count - 1] ? 0 : ds.Count - 1;
            return pts[last];
        }

        static float NearTrack(List<Vector2> pts, Vector2 p)
        {
            float best = float.MaxValue;
            foreach (Vector2 q in pts) best = Mathf.Min(best, (q - p).sqrMagnitude);
            return Mathf.Sqrt(best);
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
            float t = Time.time;
            verts.Clear();
            cols.Clear();
            uvs.Clear();
            uv2s.Clear();
            tris.Clear();
            float playerD = GuideTrail.PlayerD(lab.Route, ant.position);
            bool done = lab.Done;
            if (playerD < 0) playerD = lastD; else lastD = playerD;
            int nb = 0;
            foreach (Filament f in lines)
            {
                float flick = 0.82f + 0.18f * Noise1(t * 0.9f + f.seed);
                Vector3 prevP = Vector3.zero;
                float prevA = 0;
                for (int i = 0; i < f.pts.Count; i++)
                {
                    float d = f.d[i];
                    float ahead = playerD - d;
                    float show = done ? 1 - Mathf.SmoothStep(0, 1, (d - 6f) / 6f)
                        : Mathf.SmoothStep(0, 1, (ahead + 1f) / 2.5f) * (1 - Mathf.SmoothStep(0, 1, (ahead - reach * 0.6f) / (reach * 0.4f)));
                    // the light running along it toward the heart
                    float pulse = Mathf.Exp(-Mathf.Pow(Mathf.Repeat((d + t * 3f + f.seed) / 7f, 1f) - 0.5f, 2) * 50f);
                    float a = show * f.bright * flick * (0.32f + 0.95f * pulse);
                    // a little tremor, the runs staying straight; a drop's foot stays on the floor
                    Vector3 p = f.pts[i];
                    if (!(f.drop && i == f.pts.Count - 1))
                        p += new Vector3(Noise1(t * 0.5f + i * 1.7f + f.seed) - 0.5f, Noise1(t * 0.4f + i * 2.3f + f.seed) - 0.5f, Noise1(t * 0.45f + i * 3.1f + f.seed) - 0.5f) * 0.05f;
                    if (i > 0 && (a > 0.004f || prevA > 0.004f)) Segment(prevP, p, prevA, a, f.width);
                    if (a > 0.004f && i > 0 && i < f.pts.Count - 1) Knot(p, a * 0.9f, f.width * 3.2f);
                    if (f.drop && i == f.pts.Count - 1 && a > 0.02f && nb < 4) beacons[nb++] = new Vector4(p.x, p.y, p.z, a * 1.2f);
                    prevP = p;
                    prevA = a;
                }
            }
            for (int k = nb; k < 4; k++) beacons[k] = Vector4.zero;
            Shader.SetGlobalVectorArray("_Beacons", beacons);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, uv2s);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(ant.position, Vector3.one * (reach * 2 + 20));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        /// <summary>A straight run from a to b, its brightness going from aa to ab, facing the camera.</summary>
        void Segment(Vector3 a, Vector3 b, float aa, float ab, float halfWidth)
        {
            Vector3 mid = (a + b) * 0.5f, tangent = (b - a).normalized;
            Vector3 view = mid - cam.transform.position;
            float dist = view.magnitude;
            Vector3 across = Vector3.Cross(tangent, view / Mathf.Max(dist, 1e-4f)).normalized;
            float pixel = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            float w = Mathf.Max(halfWidth, pixel * 2.5f), thin = Mathf.Lerp(0.35f, 1f, halfWidth / w);
            int v = verts.Count;
            verts.Add(a - across * w); verts.Add(a + across * w); verts.Add(b + across * w); verts.Add(b - across * w);
            var ca = new Color(1, 1, 1, aa * thin);
            var cb = new Color(1, 1, 1, ab * thin);
            cols.Add(ca); cols.Add(ca); cols.Add(cb); cols.Add(cb);
            uvs.Add(new Vector2(0, -1)); uvs.Add(new Vector2(0, 1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(1, -1));
            for (int k = 0; k < 4; k++) uv2s.Add(Vector2.zero);
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
        }

        /// <summary>A small round knot of light at p, where a filament turns.</summary>
        void Knot(Vector3 p, float alpha, float size)
        {
            Vector3 right = cam.transform.right, up = cam.transform.up;
            float dist = (p - cam.transform.position).magnitude;
            float pixel = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            float s = Mathf.Max(size, pixel * 3f);
            int v = verts.Count;
            verts.Add(p + (-right - up) * s); verts.Add(p + (right - up) * s); verts.Add(p + (right + up) * s); verts.Add(p + (-right + up) * s);
            var c = new Color(1, 1, 1, alpha * Mathf.Lerp(0.35f, 1f, size / s));
            for (int k = 0; k < 4; k++) { cols.Add(c); uv2s.Add(new Vector2(1, 0)); }
            uvs.Add(new Vector2(-1, -1)); uvs.Add(new Vector2(1, -1)); uvs.Add(new Vector2(1, 1)); uvs.Add(new Vector2(-1, 1));
            tris.Add(v); tris.Add(v + 1); tris.Add(v + 2);
            tris.Add(v); tris.Add(v + 2); tris.Add(v + 3);
        }

        void OnDestroy() => Shader.SetGlobalVectorArray("_Beacons", new Vector4[4]);
    }
}

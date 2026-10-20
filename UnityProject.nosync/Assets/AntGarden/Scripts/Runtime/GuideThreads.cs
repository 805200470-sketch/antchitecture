using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// The particle nest's guiding light (Shaders/LightThread.shader), after a night forest where one straight beam of
    /// light stands in the moss with fine, wandering tendrils round it. Beams stand beside the way from the way in to
    /// the heart, each lighting a pool on the floor at its foot and seen from far off over the walls, so the ant always
    /// has the next one to walk to. Between the beams, threads of light hang over the way and run along it toward the
    /// heart at different heights, two or three in places, braiding and parting, each wandering its own way, with now
    /// and then a sharp turn, a loop or a short branch ending in a hook. All are made once and stay where they are:
    /// the ant follows them. The threads show only ahead of the ant (coming up out of the dark far on, fading just
    /// behind it); light runs along them toward the heart, they flicker and tremble a little, and their breaks drift.
    /// At the heart the last of them stays lit.
    /// Round the foot of each beam, vines rise from a low tangle on the floor: solid things, not light (VineSplats,
    /// drawn with the floor's things), so the ant can tell what guides it from what grows there: wandering, kinking,
    /// curling into hooks, branching; a glowing vein or glowing pores on most, their tips glowing more.
    /// </summary>
    public class GuideThreads : MonoBehaviour
    {
        public float reach = 26f;            // how far ahead along the way the threads show (units)
        public float beamReach = 55f;        // and the beams, much further: they are what the ant walks toward
        public float gain = 2.4f;
        public float vineDensity = 2200f;    // the vines' splats per unit² of their surface
        public Color core = new Color(0.92f, 0.95f, 0.9f), halo = new Color(0.55f, 0.9f, 0.95f);

        /// <summary>A thread of light: its points, each one's distance to the heart along the way (for the window ahead
        /// of the ant) and along the thread from its start (for the light running along it and its breaks).</summary>
        class Filament
        {
            public readonly List<Vector3> pts = new List<Vector3>();
            public readonly List<float> d = new List<float>();
            public readonly List<float> s = new List<float>();
            public float bright = 1f, width = 0.014f, seed, dash;
            public float dMin = float.MaxValue, dMax = float.MinValue;
        }

        /// <summary>A vine: its path from the root and how far along it each point is, and its radius at the root and
        /// at the tip.</summary>
        class Vine
        {
            public readonly List<Vector3> pts = new List<Vector3>();
            public readonly List<float> s = new List<float>();
            public float r0, r1;
        }

        /// <summary>A straight beam of light standing on the floor beside the way.</summary>
        class Beam { public Vector3 foot; public float height, d, seed; }

        LabNest lab;
        Transform ant;
        Camera cam;
        Material mat;
        Mesh mesh;
        readonly List<Filament> lines = new List<Filament>();
        readonly List<Beam> beams = new List<Beam>();
        readonly List<Vine> vines = new List<Vine>();
        readonly List<Vector3> verts = new List<Vector3>(16384);
        readonly List<Color> cols = new List<Color>(16384);
        readonly List<Vector2> uvs = new List<Vector2>(16384), uv2s = new List<Vector2>(16384);
        readonly List<int> tris = new List<int>(24576);
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
            mesh = new Mesh { name = "GuideThreads", indexFormat = IndexFormat.UInt32 };
            mesh.MarkDynamic();
            Shader.SetGlobalVectorArray("_Beacons", beacons);
            Shader.SetGlobalColor("_BeaconColor", new Color(0.62f, 0.86f, 0.84f).linear);
            Build();
        }

        float R() => (float)rng.NextDouble();
        float U(float a, float b) => a + (b - a) * R();
        Vector3 Rand3() => new Vector3(U(-1f, 1f), U(-1f, 1f), U(-1f, 1f));

        /// <summary>A unit vector at random across v.</summary>
        Vector3 Across(Vector3 v)
        {
            Vector3 a = Vector3.Cross(v, Rand3());
            return a.sqrMagnitude > 1e-6f ? a.normalized : Vector3.Cross(v, Vector3.right).normalized;
        }

        /// <summary>The light, once: threads along every way (the main one whole, with a second braiding with it; the
        /// others until they come near it), higher ones over the main way for a stretch, and the beams, from the way in
        /// to the heart, with the vines round their feet.</summary>
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
                Along(pts, ds, dFar, Mathf.Max(0, dNear), 0.8f, 3.4f, 1.1f, 0.85f);
                if (main) Along(pts, ds, dFar - U(2f, 6f), Mathf.Max(0, dNear), 0.5f, 2.4f, 0.8f, 0.55f);
            }
            if (mainPts != null)
            {
                float dFar = Mathf.Max(mainDs[0], mainDs[mainDs.Count - 1]);
                for (float at = dFar - U(4f, 14f); at > 6f; at -= U(10f, 24f))
                {
                    float len = U(8f, 26f);
                    Along(mainPts, mainDs, at, Mathf.Max(1f, at - len), 2.2f, 6.5f, 1.6f, 0.5f);
                }
                for (float at = dFar - 7f; at > 9f; at -= U(16f, 24f)) Station(mainPts, mainDs, at);
                Station(mainPts, mainDs, 2.5f);
            }
            int n = 0;
            foreach (Filament f in lines) n += f.pts.Count;
            Debug.Log($"[AntGarden] guiding light: {beams.Count} beams, {lines.Count} threads, {n} points; {vines.Count} vines");
        }

        Filament Thread(float bright, float width, float dash) =>
            new Filament { bright = bright, width = width, dash = dash, seed = R() * 100f };

        static void Add(Filament f, Vector3 p, float d, float s)
        {
            f.pts.Add(p);
            f.d.Add(d);
            f.s.Add(s);
            if (d < f.dMin) f.dMin = d;
            if (d > f.dMax) f.dMax = d;
        }

        /// <summary>
        /// A thread along a way from distance dFrom to dTo from the heart, between hMin and hMax over the floor and never
        /// much further to a side than side: it swings slowly from side to side and up and down, with smaller quicker
        /// swings and a fine tremble over them; now and then it turns sharply (a corner it eases out of), loops once, or
        /// sends off a short branch that ends in a curl. Some are broken into dashes.
        /// </summary>
        void Along(List<Vector2> pts, List<float> ds, float dFrom, float dTo, float hMin, float hMax, float side, float bright)
        {
            if (dFrom - dTo < 1f) return;
            var f = Thread(bright, U(0.010f, 0.016f), R() < 0.35f ? U(0.3f, 0.5f) : 0f);
            float sa = R() * 100f, sb = R() * 100f, sc = R() * 100f;
            const float step = 0.08f;
            float nextLoop = dFrom - U(3f, 9f), nextBranch = dFrom - U(1.5f, 5f), nextKink = dFrom - U(0.8f, 3f);
            float kinkAt = float.MinValue, kinkIn = 0.2f, kinkOut = 0.6f;
            Vector2 kink = Vector2.zero;   // the corner's push (to the side, up)
            float s = 0;
            Vector3 last = Vector3.zero;
            bool first = true;
            for (float d = dFrom; d > dTo; d -= step)
            {
                Vector2 p = OnTrack(pts, ds, d, out Vector2 dir);
                var nrm = new Vector2(-dir.y, dir.x);
                if (d < nextKink)
                {
                    kinkIn = U(0.1f, 0.3f);
                    kinkOut = U(0.4f, 1.3f);
                    kinkAt = d - kinkIn;
                    kink = new Vector2(U(0.15f, 0.45f) * (R() < 0.5f ? -1 : 1), U(0.1f, 0.4f) * (R() < 0.5f ? -1 : 1));
                    nextKink = kinkAt - kinkOut - U(0.6f, 3f);
                }
                // the corner: straight out to it, then slowly back
                float k = d >= kinkAt ? Mathf.Clamp01((kinkAt + kinkIn - d) / kinkIn) : Mathf.Clamp01(1f - (kinkAt - d) / kinkOut);
                float lat = side * ((Noise1(d * 0.09f + sa) - 0.5f) * 1.5f + (Noise1(d * 0.42f + sb) - 0.5f) * 0.55f)
                            + (Noise1(d * 2.7f + sc) - 0.5f) * 0.07f + kink.x * k;
                float ht = Mathf.Lerp(hMin, hMax, Noise1(d * 0.06f + sb + 50f)) + (Noise1(d * 0.47f + sc + 20f) - 0.5f) * 0.7f
                           + (Noise1(d * 2.9f + sa + 7f) - 0.5f) * 0.06f + kink.y * k;
                Vector2 q = p + nrm * lat;
                var v = new Vector3(q.x, lab.WalkHeight(q) + Mathf.Max(0.25f, ht), q.y);
                if (!first) s += (v - last).magnitude;
                Vector3 travel = first ? new Vector3(dir.x, 0, dir.y) : (v - last).normalized;
                Add(f, v, d, s);
                if (!first && d < nextLoop)
                {
                    // now and then a loop, never quite closed, growing as it goes round (a hook more than a ring)
                    nextLoop = d - U(10f, 25f);
                    float r = U(0.12f, 0.32f), turn = U(3.8f, 5.6f);
                    Vector3 up = Across(travel);
                    int n = Mathf.CeilToInt(turn * r / 0.05f);
                    for (int i = 1; i <= n; i++)
                    {
                        float a = turn * i / n, ri = r * (0.75f + 0.5f * i / n);
                        Vector3 lp = v + up * (ri - ri * Mathf.Cos(a)) + travel * (ri * Mathf.Sin(a));
                        s += turn * r / n;
                        Add(f, lp, d, s);
                    }
                }
                if (!first && d < nextBranch)
                {
                    nextBranch = d - U(2f, 7f);
                    Vector3 bdir = Quaternion.AngleAxis(U(35f, 80f), Across(travel)) * travel;
                    Walk(v, bdir, U(0.5f, 2.2f), Vector3.up * U(-0.2f, 0.5f), bright * 0.7f, f.width * 0.7f, d, 1, false, 0.35f);
                }
                last = v;
                first = false;
            }
            if (f.pts.Count >= 2) lines.Add(f);
        }

        /// <summary>
        /// A thread walking freely from p: its heading wanders smoothly and is drawn toward pull; now and then it turns
        /// sharply or loops; a vine may branch (at the top level); it may end in a hook (curl: how likely). It never
        /// goes under the floor. A thread of light takes distance d (to the heart) at every point, so it shows all at
        /// once; a vine (width: its radius at the root) is kept for VineSplats.
        /// </summary>
        void Walk(Vector3 p, Vector3 dir, float length, Vector3 pull, float bright, float width, float d, int depth, bool vine, float curl)
        {
            var f = Thread(bright, width, !vine && R() < 0.3f ? U(0.25f, 0.5f) : 0f);
            float sa = R() * 100f, sb = R() * 100f, sc = R() * 100f;
            const float step = 0.06f;
            bool loops = R() < (vine ? 0.1f : 0.3f);   // a solid vine looping often reads as a knot
            float s = 0, nextKink = U(0.4f, 1.6f), nextLoop = U(0.8f, 3f), nextBranch = U(0.5f, 1.8f);
            int loopLeft = 0;
            float loopRate = 0;
            Vector3 loopAxis = Vector3.up;
            Add(f, p, d, 0);
            while (s < length)
            {
                if (loopLeft > 0)
                {
                    dir = Quaternion.AngleAxis(loopRate * step * Mathf.Rad2Deg, loopAxis) * dir;
                    loopLeft--;
                }
                else
                {
                    var wob = new Vector3(Noise1(s * 1.6f + sa) - 0.5f, Noise1(s * 1.4f + sb) - 0.5f, Noise1(s * 1.5f + sc) - 0.5f);
                    dir = (dir + wob * 0.25f + pull * step).normalized;
                    if (s > nextKink)
                    {
                        dir = Quaternion.AngleAxis(U(35f, 80f), Across(dir)) * dir;
                        nextKink = s + U(0.6f, 2.2f);
                    }
                    if (loops && s > nextLoop)
                    {
                        float r = U(0.12f, 0.3f);
                        loopAxis = Across(dir);
                        loopRate = 1f / r;
                        loopLeft = Mathf.CeilToInt(U(3.6f, 5.4f) * r / step);   // most of the way round, not closed
                        nextLoop = s + U(2.5f, 6f);
                    }
                    if (depth == 0 && vine && s > nextBranch)
                    {
                        nextBranch = s + U(0.8f, 2.5f);
                        Vector3 bdir = Quaternion.AngleAxis(U(30f, 70f), Across(dir)) * dir;
                        Walk(p, bdir, U(0.4f, 1.6f), pull * 0.6f, bright * 0.75f, width * 0.5f, d, depth + 1, true, 0.5f);
                    }
                }
                p += dir * step;
                s += step;
                float floor = lab.WalkHeight(new Vector2(p.x, p.z)) + 0.02f;
                if (p.y < floor)
                {
                    p.y = floor;
                    if (dir.y < 0) dir = new Vector3(dir.x, -dir.y * 0.5f, dir.z).normalized;
                }
                Add(f, p, d, s);
            }
            if (R() < curl)
            {
                // the tip: a hook, curling in a little
                float r = U(0.1f, 0.22f);
                Vector3 axis = Across(dir);
                int n = 12 + (int)(R() * 16);
                for (int i = 0; i < n; i++)
                {
                    dir = Quaternion.AngleAxis(step / r * Mathf.Rad2Deg, axis) * dir;
                    p += dir * step;
                    s += step;
                    r *= 0.97f;
                    Add(f, p, d, s);
                }
            }
            if (f.pts.Count < 2) return;
            if (!vine) { lines.Add(f); return; }
            var v = new Vine { r0 = width, r1 = U(0.005f, 0.008f) };
            v.pts.AddRange(f.pts);
            v.s.AddRange(f.s);
            vines.Add(v);
        }

        /// <summary>A beam beside the way at distance d from the heart (to one side, as far as the floor allows), and
        /// the vines rising round its foot from a low tangle on the floor.</summary>
        void Station(List<Vector2> pts, List<float> ds, float d)
        {
            Vector2 p = OnTrack(pts, ds, d, out Vector2 dir);
            var nrm = new Vector2(-dir.y, dir.x);
            float sideSign = R() < 0.5f ? -1 : 1;
            Vector2 foot = p;
            for (float off = U(1.1f, 1.6f); off > 0.15f; off -= 0.15f)
            {
                if (lab.OnFloor(p + nrm * sideSign * off, 0.25f)) { foot = p + nrm * sideSign * off; break; }
                if (lab.OnFloor(p - nrm * sideSign * off, 0.25f)) { foot = p - nrm * sideSign * off; break; }
            }
            var footAt = new Vector3(foot.x, lab.WalkHeight(foot), foot.y);
            beams.Add(new Beam { foot = footAt, height = U(11f, 16f), d = d, seed = R() * 100f });
            int n = 8 + (int)(R() * 7);
            for (int i = 0; i < n; i++)
            {
                float a = R() * 6.283f, rad = U(0.15f, 1.3f);
                var at = new Vector2(foot.x + Mathf.Cos(a) * rad, foot.y + Mathf.Sin(a) * rad);
                if (!lab.OnFloor(at, 0.05f)) continue;
                var outward = new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a));
                Vector3 dir0 = (Vector3.up + outward * U(0.1f, 0.6f) + Rand3() * 0.3f).normalized;
                float len = R() < 0.25f ? U(4f, 7f) : U(1.2f, 4f);
                Walk(new Vector3(at.x, lab.WalkHeight(at) + 0.02f, at.y), dir0, len, Vector3.up * U(0.3f, 0.9f) + outward * U(-0.1f, 0.25f),
                    1f, U(0.03f, 0.06f), d, 0, true, 0.5f);
            }
            for (int i = 0; i < 3; i++)
            {
                // the tangle on the floor at its foot
                float a = R() * 6.283f;
                Vector2 at = foot + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * U(0.05f, 0.5f);
                Walk(new Vector3(at.x, lab.WalkHeight(at) + 0.03f, at.y), new Vector3(Mathf.Cos(a + 1.6f), 0.15f, Mathf.Sin(a + 1.6f)),
                    U(0.8f, 2f), Vector3.up * 0.05f, 1f, U(0.018f, 0.03f), d, 1, true, 0f);
            }
        }

        /// <summary>
        /// The vines as solid splats (Shaders/ParticleSolid.shader, drawn by SolidThings with the floor's things), into
        /// the list: each a stem tapering from root to tip, a little swollen at its nodes, ringed with faint grooves and
        /// mottled, in its own colour from the nest's palette (darker than the plants); most with a glowing vein winding
        /// round it or glowing pores dotted along it, the last stretch to the tip glowing more; some with small leaves in
        /// pairs. Their glowing parts breathe with the plants'; they sway in the wind and light up as the ant walks past,
        /// as the plants do.
        /// </summary>
        public void VineSplats(List<SolidPoint> into, Color[] body, Color[] glowPal)
        {
            var vr = new System.Random(9001);
            float V() => (float)vr.NextDouble();
            int start = into.Count;
            int[] cool = { 0, 1, 2, 6 };
            foreach (Vine v in vines)
            {
                int n = v.pts.Count;
                if (n < 2) continue;
                float len = Mathf.Max(1e-3f, v.s[n - 1]);
                Color stem = body != null && body.Length >= 8 ? body[(int)(V() * 6) % 6].linear : new Color(0.32f, 0.42f, 0.38f);
                stem = Color.Lerp(stem, new Color(0.1f, 0.16f, 0.15f), 0.5f + 0.25f * V());   // darker than the plants
                Color light = glowPal != null && glowPal.Length >= 8 ? glowPal[cool[(int)(V() * 4) % 4]].linear : new Color(0.4f, 0.85f, 0.85f);
                bool vein = V() < 0.55f, leaves = V() < 0.45f;
                float pores = V() < 0.5f ? 0.012f + 0.025f * V() : 0.003f, tip = 0.1f + 0.15f * V();
                float veinAt = V() * 6.283f, veinTwist = (V() - 0.5f) * 14f, grooves = 5 + (int)(V() * 5), grooveTwist = (V() - 0.5f) * 6f;
                float nodes = 0.25f + 0.35f * V(), mot = 2f + 5f * V(), motSeed = V() * 50f;
                float nextLeaf = leaves ? 0.3f + 0.4f * V() : float.MaxValue;
                // a frame carried along the stem, so its grooves and vein run round it smoothly
                Vector3 t0 = (v.pts[1] - v.pts[0]).normalized;
                Vector3 nx = Vector3.Cross(t0, Mathf.Abs(t0.y) < 0.9f ? Vector3.up : Vector3.right).normalized;
                for (int i = 1; i < n; i++)
                {
                    Vector3 a = v.pts[i - 1], b = v.pts[i], tg = b - a;
                    float seg = tg.magnitude;
                    if (seg < 1e-5f) continue;
                    tg /= seg;
                    nx -= Vector3.Dot(nx, tg) * tg;
                    nx = nx.sqrMagnitude > 1e-8f ? nx.normalized : Vector3.Cross(tg, Vector3.right).normalized;
                    Vector3 bx = Vector3.Cross(tg, nx);
                    float s0 = v.s[i - 1], u = v.s[i] / len;
                    float node = 1f + 0.25f * Mathf.Pow(Mathf.Max(0f, Mathf.Cos(s0 / nodes * 6.283f)), 24f);
                    float r = Mathf.Lerp(v.r0, v.r1, Mathf.Pow(u, 0.8f)) * node;
                    float area = 6.283f * r * seg;
                    int k = Mathf.Max(3, Mathf.RoundToInt(area * vineDensity));
                    float rad = 0.62f * Mathf.Sqrt(area / k);
                    for (int j = 0; j < k; j++)
                    {
                        float th = V() * 6.283f, f = V(), sAt = s0 + f * seg;
                        Vector3 dir = nx * Mathf.Cos(th) + bx * Mathf.Sin(th);
                        Vector3 p = Vector3.Lerp(a, b, f) + dir * r;
                        float groove = 0.78f + 0.22f * Mathf.Cos(th * grooves + sAt * grooveTwist);
                        Color c = stem * (groove * (0.8f + 0.4f * Noise1(sAt * mot + motSeed + th * 0.8f)));
                        float glow = 0f;
                        if (vein && Mathf.Abs(Mathf.DeltaAngle(th * Mathf.Rad2Deg, (veinAt + sAt * veinTwist) * Mathf.Rad2Deg)) < 16f)
                        {
                            glow = 0.4f;
                            c = Color.Lerp(c, light * 0.45f, 0.7f);
                        }
                        else if (V() < pores)
                        {
                            glow = 0.75f + 0.25f * V();
                            c = light * (0.5f + 0.3f * V());
                        }
                        float tu = (u - (1f - tip)) / tip;
                        if (tu > 0)
                        {
                            glow = Mathf.Max(glow, 0.4f * tu);
                            c = Color.Lerp(c, light * 0.5f, 0.5f * tu);
                        }
                        into.Add(ThingPen.Splat(p, dir, c, rad * (0.8f + 0.4f * V()), glow));
                    }
                    // small leaves in pairs now and then, leaning toward the tip
                    if (v.s[i] > nextLeaf && u < 0.85f)
                    {
                        nextLeaf = v.s[i] + 0.3f + 0.45f * V();
                        float side = V() * 6.283f, size = (0.06f + 0.08f * V()) * Mathf.Lerp(1f, 0.6f, u);
                        for (int pair = 0; pair < 2; pair++)
                        {
                            Vector3 o = nx * Mathf.Cos(side + pair * Mathf.PI) + bx * Mathf.Sin(side + pair * Mathf.PI);
                            Leaf(into, b + o * r, (o + tg * 0.6f).normalized, tg, size, stem, V);
                        }
                    }
                }
            }
            Debug.Log($"[AntGarden] vines: {vines.Count}, {into.Count - start} splats");
        }

        /// <summary>A small leaf from at, reaching along, a little greener and lighter than its stem, its middle
        /// vein lighter, curling up a little at its end.</summary>
        void Leaf(List<SolidPoint> into, Vector3 at, Vector3 along, Vector3 stemDir, float length, Color stem, System.Func<float> V)
        {
            Vector3 across = Vector3.Cross(along, stemDir);
            if (across.sqrMagnitude < 1e-6f) across = Vector3.Cross(along, Vector3.up);
            across.Normalize();
            Vector3 nrm = Vector3.Cross(across, along).normalized;
            Color leaf = Color.Lerp(stem, new Color(0.3f, 0.42f, 0.26f), 0.5f) * 1.2f;
            float w = length * 0.42f;
            int k = Mathf.Max(8, Mathf.RoundToInt(length * w * 2.4f * vineDensity));
            float rad = 0.62f * Mathf.Sqrt(length * w * 1.6f / k);
            for (int j = 0; j < k; j++)
            {
                float x = V(), y = V() * 2f - 1f;
                float half = Mathf.Pow(Mathf.Sin(Mathf.PI * Mathf.Min(1f, x * 1.05f)), 0.8f);
                Vector3 p = at + along * (x * length) + across * (y * half * w) + nrm * (0.25f * w * x * x);
                float vein = Mathf.Abs(y) < 0.12f ? 1.15f : 1f;
                into.Add(ThingPen.Splat(p, nrm, leaf * (vein * (0.8f + 0.35f * V())), rad * (0.8f + 0.4f * V()), 0f));
            }
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
            foreach (Filament f in lines)
            {
                if (done ? f.dMin > 13f : playerD - f.dMax > reach + 1f || playerD - f.dMin < -2f) continue;
                float flick = 0.8f + 0.2f * Noise1(t * 0.9f + f.seed);
                Vector3 prevP = Vector3.zero;
                float prevA = 0;
                for (int i = 0; i < f.pts.Count; i++)
                {
                    float d = f.d[i], s = f.s[i];
                    float ahead = playerD - d;
                    float show = done ? 1 - Mathf.SmoothStep(0, 1, (d - 6f) / 6f)
                        : Mathf.SmoothStep(0, 1, (ahead + 1f) / 2.5f) * (1 - Mathf.SmoothStep(0, 1, (ahead - reach * 0.6f) / (reach * 0.4f)));
                    // the light running along it toward the heart
                    float pulse = Mathf.Exp(-Mathf.Pow(Mathf.Repeat((d + t * 3f + f.seed) / 7f, 1f) - 0.5f, 2) * 50f);
                    // its breaks, drifting
                    float whole = f.dash > 0 ? Mathf.SmoothStep(0, 1, (Noise1(s * 2.3f + f.seed + t * 0.25f) - f.dash + 0.08f) / 0.16f) : 1f;
                    float a = show * f.bright * flick * whole * (0.3f + 0.9f * pulse);
                    Vector3 p = f.pts[i];
                    // trembling a little
                    const float sway = 0.04f;
                    p += new Vector3(Noise1(t * 0.45f + s * 0.6f + f.seed) - 0.5f, (Noise1(t * 0.35f + s * 0.5f + f.seed + 9f) - 0.5f) * 0.5f,
                        Noise1(t * 0.4f + s * 0.7f + f.seed + 17f) - 0.5f) * sway;
                    if (i > 0 && (a > 0.004f || prevA > 0.004f)) Segment(prevP, p, prevA, a, f.width);
                    // now and then a bead of light on it, glinting
                    if (a > 0.03f && Hash(i * 31 + (int)f.seed) < 0.012f)
                        Knot(p, a * (0.4f + 0.6f * Noise1(t * 1.3f + i)), f.width * 3.5f);
                    prevP = p;
                    prevA = a;
                }
            }
            // the beams: seen from far off, fading once passed; a pool of light at the foot of the nearest
            int nb = 0;
            for (int k = 0; k < 4; k++) beacons[k] = Vector4.zero;
            foreach (Beam b in beams)
            {
                float ahead = playerD - b.d;
                float show = done ? (b.d < 6f ? 1f : 0f)
                    : Mathf.SmoothStep(0, 1, (ahead + 3f) / 4f) * (1 - Mathf.SmoothStep(0, 1, (ahead - beamReach * 0.7f) / (beamReach * 0.3f)));
                if (show < 0.005f) continue;
                float flick = 0.85f + 0.15f * Noise1(t * 0.7f + b.seed);
                const int pieces = 10;
                Vector3 prevP = b.foot;
                float prevA = show * flick;
                for (int i = 1; i <= pieces; i++)
                {
                    float h = (float)i / pieces;
                    Vector3 p = b.foot + Vector3.up * (b.height * h);
                    float a = show * flick * (1 - Mathf.Pow(h, 1.6f));
                    Segment(prevP, p, prevA, a, 0.011f);
                    prevP = p;
                    prevA = a;
                }
                // sparks sliding down it now and then
                for (int j = 0; j < 2; j++)
                {
                    float u = Mathf.Repeat(t * 0.08f + b.seed + j * 0.5f, 1f);
                    Knot(b.foot + Vector3.up * (b.height * (1 - u) * 0.7f), show * 0.6f * Mathf.Sin(u * Mathf.PI), 0.03f);
                }
                if (nb < 4 && ahead > -3f && ahead < reach) beacons[nb++] = new Vector4(b.foot.x, b.foot.y, b.foot.z, show * 1.1f);
            }
            Shader.SetGlobalVectorArray("_Beacons", beacons);
            mesh.Clear();
            mesh.SetVertices(verts);
            mesh.SetColors(cols);
            mesh.SetUVs(0, uvs);
            mesh.SetUVs(1, uv2s);
            mesh.SetTriangles(tris, 0);
            mesh.bounds = new Bounds(ant.position, Vector3.one * (beamReach * 2 + 40));
            Graphics.DrawMesh(mesh, Matrix4x4.identity, mat, 0, null, 0, null, ShadowCastingMode.Off, false);
        }

        /// <summary>A straight run from a to b, its brightness going from aa to ab, facing the camera.</summary>
        void Segment(Vector3 a, Vector3 b, float aa, float ab, float halfWidth)
        {
            Vector3 mid = (a + b) * 0.5f, tangent = (b - a).normalized;
            Vector3 view = mid - cam.transform.position;
            float dist = view.magnitude;
            Vector3 cross = Vector3.Cross(tangent, view / Mathf.Max(dist, 1e-4f));
            float sideways = cross.magnitude;
            Vector3 across = sideways > 1e-5f ? cross / sideways : cam.transform.right;
            float pixel = 2f * dist * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad) / Mathf.Max(1, cam.pixelHeight);
            float w = Mathf.Max(halfWidth, pixel * 2.5f), thin = Mathf.Lerp(0.35f, 1f, halfWidth / w);
            // seen end-on its light would pile up into one glaring line, and right at the lens it would be a smear
            thin *= Mathf.SmoothStep(0f, 1f, sideways / 0.35f) * Mathf.SmoothStep(0f, 1f, (dist - 0.3f) / 1.2f);
            if (thin <= 0.001f) return;
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

        /// <summary>A small round bead of light at p.</summary>
        void Knot(Vector3 p, float alpha, float size)
        {
            Vector3 right = cam.transform.right, up = cam.transform.up;
            float dist = (p - cam.transform.position).magnitude;
            alpha *= Mathf.SmoothStep(0f, 1f, (dist - 0.4f) / 1.6f);   // right at the lens a bead would be a big blur
            if (alpha <= 0.002f) return;
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

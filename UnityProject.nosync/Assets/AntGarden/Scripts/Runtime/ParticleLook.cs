using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// Toward the look of point clouds and Gaussian splats (Marshmallow Laser Feast's "Dream"): plants, ruin props
    /// and ground drawn as glowing points sampled from their surfaces (AssetBuilder.SurfacePoints), all in one
    /// DrawProcedural call from one buffer (Shaders/ParticleLook.shader). Two ways:
    /// - Overlay (P toggles; -particlelook [soft]): round the ant, a layer of points over the meshes, which dim.
    /// - Replace (the particle nest, chapter one): the plant meshes are hidden and the whole nest is made of dense
    ///   points, pale and nearly colourless like a scan, breathing a little. The walls and floor of the canyons
    ///   become points too (sampled from the terrain's own triangles, a few of them big soft splats), the terrain
    ///   itself sinking nearly black so it only hides what lies behind it; dust floats in the canyons' air. Points
    ///   are densest along the ways the ant is led (focus) and thin out away from them. Colliders, touches and
    ///   walking are unchanged.
    /// </summary>
    public class ParticleLook : MonoBehaviour
    {
        public enum Mode { Overlay, Replace }
        public Mode mode = Mode.Overlay;
        public float radius = 60f;          // overlay: plants and props this close to the ant become points
        public float groundRadius = 40f;    // overlay: and the ground this close
        public float regatherAfter = 20f;   // overlay: gather again once the ant has walked this far
        public float perUnit = 220f;        // overlay: points for a plant one unit tall (× height^1.6)
        public float perArea = 700f;        // replace: points for a plant one unit tall (× height², its surface)
        public int budget = 4000000;        // replace: at most this many plant points in all (fewer for every plant)
        public float groundStep = 0.5f;     // overlay: spacing of the ground's points
        public float pointSize = 0.02f;     // a point's radius (units)
        public float brightness = 1.2f;
        public float glowBrightness = 3f;
        public float shimmer = 0f;          // how far each point trembles (units)
        public float plantDim = 0.82f;      // overlay: how dark the plant meshes go behind their points
        public float groundDim = 0.5f;      // and the ground
        public float scanMix = 0.65f;       // how far a point's colour goes from its texture toward the pale scan colour
        public Color scanOverride = Color.clear;   // replace: one pale colour for every plant (alpha 0: each region's own)

        // replace: the ground as points
        public readonly List<MeshFilter> ground = new List<MeshFilter>();   // the terrain's meshes
        public float groundNear = 260f;     // ground points per unit² along the ways (walls a little more, floor less)
        public float groundFar = 18f;       // and far from them
        public int groundBudget = 1300000;
        public float splatShare = 0.06f;    // this share of them are big soft splats
        public Color groundColour = Color.clear;   // their colour (alpha 0: the scan colour)
        public float groundBrightness = 1f;
        public float dust = 0f;             // dust motes per unit³ of the canyons' air along the ways
        public float dustHeight = 3.2f;     // how high above the floor it floats

        // replace: where the ant is led (the glowing tracks): plants and ground are densest near them
        public List<List<Vector2>> focus;
        public float focusReach = 7f;       // how fast the density falls off away from them (units)
        public float farShare = 0.3f;       // plants far from them keep this share of their points

        struct Point { public Vector3 pos; public uint col; }

        // what a point is (the colour's fourth byte, top two bits; the low six: its size × 1/8, 0 = as it is)
        const uint Plant = 0, Dust = 1, Ground = 2, Glow = 3;

        World world;
        Transform follow;
        Material mat;
        ComputeBuffer buffer;
        Point[] uploaded = new Point[0];
        int count;
        Vector3 gatheredAt;
        bool on;
        readonly List<Point> points = new List<Point>(1 << 20);
        readonly List<Renderer> hidden = new List<Renderer>();
        readonly Dictionary<(PlantRef, float, float, Color, Color), uint[]> colours = new Dictionary<(PlantRef, float, float, Color, Color), uint[]>();
        float[] focusD2;   // squared distance to the nearest way, on a grid
        int fw, fh;
        float fx0, fz0;
        const float FocusCell = 1f;

        public bool On => on;
        public int Count => count;

        public void Init(World w, Transform ant, Material m)
        {
            world = w;
            follow = ant;
            mat = m;
            SetOn(false);
        }

        public void SetOn(bool value)
        {
            on = value && mat != null;
            if (mat != null)
            {
                mat.SetFloat("_Size", pointSize);
                mat.SetFloat("_Gain", brightness);
                mat.SetFloat("_GlowGain", glowBrightness);
                mat.SetFloat("_Shimmer", shimmer);
                mat.SetFloat("_MinPixels", mode == Mode.Replace ? 0.9f : 1.2f);
            }
            Shader.SetGlobalFloat("_PlantDim", on && mode == Mode.Overlay ? plantDim : 0f);
            Shader.SetGlobalFloat("_GroundDim", on ? groundDim : 0f);
            // replace: the meshes go; only their points are seen
            foreach (Renderer r in hidden) if (r != null) r.enabled = true;
            hidden.Clear();
            if (on && mode == Mode.Replace)
                foreach (PlantInstance pi in world.plantInstances)
                    if (pi.t != null)
                        foreach (Renderer r in pi.t.GetComponentsInChildren<Renderer>())
                            if (r.enabled) { r.enabled = false; hidden.Add(r); }
            if (on) Gather();
        }

        void Update()
        {
            if (!on || world == null || follow == null) return;
            if (mode == Mode.Overlay && (follow.position - gatheredAt).sqrMagnitude > regatherAfter * regatherAfter) Gather();
            if (count == 0) return;
            mat.SetBuffer("_Points", buffer);
            var bounds = new Bounds(new Vector3((world.minX + world.maxX) / 2, 0, (world.minZ + world.maxZ) / 2),
                new Vector3(world.maxX - world.minX + 400, 200, world.maxZ - world.minZ + 400));
            Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, count * 3, 1, null, null, ShadowCastingMode.Off, false, 0);
        }

        void OnDestroy()
        {
            buffer?.Release();
            Shader.SetGlobalFloat("_PlantDim", 0f);
            Shader.SetGlobalFloat("_GroundDim", 0f);
        }

        void Gather()
        {
            Vector3 c = follow.position;
            gatheredAt = c;
            points.Clear();
            bool replace = mode == Mode.Replace;
            if (replace && focusD2 == null) BuildFocus();
            float r2 = replace ? float.MaxValue : radius * radius;
            // how many points each plant gets; in replace mode all of them share the budget
            var picks = new List<(PlantInstance pi, int n)>();
            long wanted = 0;
            foreach (PlantInstance pi in world.plantInstances)
            {
                if (pi.t == null || pi.plant == null || !pi.plant.LoadPoints()) continue;
                if (!replace && pi.far) continue;
                Vector3 p0 = pi.t.position;
                float dx = p0.x - c.x, dz = p0.z - c.z;
                if (dx * dx + dz * dz > r2) continue;
                float h = pi.t.lossyScale.y;
                int n = replace ? Mathf.Clamp(Mathf.RoundToInt(perArea * h * h * Mathf.Lerp(farShare, 1f, Focus(p0.x, p0.z))), 200, pi.far ? 12000 : pi.plant.points.Length)
                                : Mathf.Clamp(Mathf.RoundToInt(perUnit * Mathf.Pow(h, 1.6f)), 40, pi.plant.points.Length);
                picks.Add((pi, n));
                wanted += n;
            }
            float share = replace && wanted > budget ? (float)budget / wanted : 1f;
            foreach (var (pi, n0) in picks)
            {
                int n = Mathf.Max(60, Mathf.RoundToInt(n0 * share));
                uint[] cols = Colours(pi);
                Matrix4x4 m = pi.t.localToWorldMatrix;
                for (int k = 0; k < n && k < cols.Length; k++) points.Add(new Point { pos = m.MultiplyPoint3x4(pi.plant.points[k]), col = cols[k] });
            }
            int plantPoints = points.Count;
            var rng = new System.Random(7);
            if (replace)
            {
                SampleGround(rng);
                int groundPoints = points.Count - plantPoints;
                FloatDust(rng);
                Debug.Log($"[AntGarden] particle look (Replace): {plantPoints} plant points from {picks.Count} plants" + (share < 1 ? $" (each at {share:P0})" : "")
                    + $", {groundPoints} ground, {points.Count - plantPoints - groundPoints} dust");
            }
            else
            {
                // the ground: a fine, slightly jittered grid of points over the floor, banks and plateau round the ant
                float step = groundStep, g2 = groundRadius * groundRadius;
                for (float x = c.x - groundRadius; x <= c.x + groundRadius; x += step)
                for (float z = c.z - groundRadius; z <= c.z + groundRadius; z += step)
                {
                    if ((x - c.x) * (x - c.x) + (z - c.z) * (z - c.z) > g2) continue;
                    float px = x + (float)(rng.NextDouble() - 0.5) * step, pz = z + (float)(rng.NextDouble() - 0.5) * step;
                    if (world.HexDist(px, pz) > -0.3f) continue;
                    Region r = world.RegionAt(px, pz);
                    bool floor = world.Dist(px, pz) < 0;
                    Color g = Color.Lerp(r.biome.groundColor, r.biome.rimColor, 0.35f) * (floor ? 1.3f : 0.9f) * (0.6f + 0.8f * (float)rng.NextDouble());
                    points.Add(new Point { pos = new Vector3(px, world.WalkHeight(px, pz) + 0.03f, pz), col = Pack(g, Ground) });
                }
                Debug.Log($"[AntGarden] particle look (Overlay): {points.Count} points from {picks.Count} plants round ({c.x:F0}, {c.z:F0})");
            }
            count = points.Count;
            if (count == 0) return;
            if (buffer == null || buffer.count < count)
            {
                buffer?.Release();
                buffer = new ComputeBuffer(Mathf.NextPowerOfTwo(count), 16);
            }
            if (uploaded.Length < count) uploaded = new Point[Mathf.NextPowerOfTwo(count)];
            points.CopyTo(uploaded);
            buffer.SetData(uploaded, 0, 0, count);
        }

        /// <summary>
        /// Replace: the walls and floor as points, sampled from the terrain's own triangles (its vertex colours say
        /// floor, bank, plateau and how open it is), lifted a little off the surface so the dark terrain does not
        /// hide them. Most are fine points; a few are big soft splats, a faint haze on the surfaces.
        /// </summary>
        void SampleGround(System.Random rng)
        {
            if (ground.Count == 0) return;
            var meshes = new List<(Vector3[] v, Vector3[] n, Color[] c, int[] t)>();
            foreach (MeshFilter mf in ground)
            {
                Mesh mesh = mf.sharedMesh;
                if (mesh == null || !mesh.isReadable) continue;
                Matrix4x4 m = mf.transform.localToWorldMatrix;
                Vector3[] v = mesh.vertices, n = mesh.normals;
                for (int i = 0; i < v.Length; i++) { v[i] = m.MultiplyPoint3x4(v[i]); n[i] = m.MultiplyVector(n[i]).normalized; }
                Color[] c = mesh.colors;
                if (c.Length != v.Length) { c = new Color[v.Length]; for (int i = 0; i < c.Length; i++) c[i] = new Color(0, 1, 0, 1); }
                meshes.Add((v, n, c, mesh.triangles));
            }
            // how many the ground wants, to keep to its budget
            double wanted = 0;
            foreach (var (v, _, c, t) in meshes)
                for (int k = 0; k < t.Length; k += 3)
                    wanted += Want(v[t[k]], v[t[k + 1]], v[t[k + 2]], c[t[k]], c[t[k + 1]], c[t[k + 2]]);
            float scale = wanted > groundBudget ? (float)(groundBudget / wanted) : 1f;
            Color tint = (groundColour.a > 0 ? groundColour : scanOverride.a > 0 ? scanOverride : new Color(0.7f, 0.8f, 1f)).linear;
            foreach (var (v, nr, c, t) in meshes)
                for (int k = 0; k < t.Length; k += 3)
                {
                    int ia = t[k], ib = t[k + 1], ic = t[k + 2];
                    float want = Want(v[ia], v[ib], v[ic], c[ia], c[ib], c[ic]) * scale;
                    int many = (int)want;
                    if (rng.NextDouble() < want - many) many++;
                    for (int j = 0; j < many; j++)
                    {
                        float u = (float)rng.NextDouble(), w = (float)rng.NextDouble();
                        if (u + w > 1) { u = 1 - u; w = 1 - w; }
                        float s = 1 - u - w;
                        Vector3 n = (nr[ia] * s + nr[ib] * u + nr[ic] * w).normalized;
                        Vector3 p = v[ia] * s + v[ib] * u + v[ic] * w + n * 0.035f;
                        Color lw = c[ia] * s + c[ib] * u + c[ic] * w;   // floor, bank, plateau, open
                        // the walls a little brighter than the floor, the plateau dimmer; the bank's top edge, where
                        // it meets the plateau, brightest: the outline of the passage; creases darker
                        float lum = 0.5f * lw.r + 0.75f * lw.g + 0.32f * lw.b + 1.4f * lw.g * lw.b;
                        lum *= (0.55f + 0.45f * lw.a) * (0.4f + 1.2f * (float)rng.NextDouble());
                        // not an even starfield: lighter and darker patches, and on the walls the soil's layers
                        float patch = Mathf.PerlinNoise(p.x * 0.31f + 17.3f, p.z * 0.31f + p.y * 0.45f + 5.1f);
                        float layers = 0.68f + 0.32f * Mathf.Sin(p.y * 6.5f + Mathf.PerlinNoise(p.x * 0.12f, p.z * 0.12f) * 6f);
                        lum *= (0.3f + 1.2f * patch) * Mathf.Lerp(1f, layers, lw.g);
                        bool splat = rng.NextDouble() < splatShare;
                        Color col = tint * lum * groundBrightness * (splat ? 0.16f : 1f);
                        points.Add(new Point { pos = p, col = Pack(col.gamma, Ground, splat ? 40u : 10u) });
                    }
                }
        }

        /// <summary>How many ground points a triangle wants: its area × the density there.</summary>
        float Want(Vector3 a, Vector3 b, Vector3 c, Color ca, Color cb, Color cc)
        {
            float area = 0.5f * Vector3.Cross(b - a, c - a).magnitude;
            Vector3 mid = (a + b + c) / 3f;
            if (world.HexDist(mid.x, mid.z) > -0.2f) return 0;   // the rim of the hexagon
            Color lw = (ca + cb + cc) / 3f;
            float kind = lw.r * 0.8f + lw.g * 1.25f + lw.b * 0.45f;
            return area * Mathf.Lerp(groundFar, groundNear, Focus(mid.x, mid.z)) * kind;
        }

        /// <summary>Replace: motes floating in the canyons' air, most of them along the ways; a few golden.</summary>
        void FloatDust(System.Random rng)
        {
            if (dust <= 0) return;
            float area = (world.maxX - world.minX) * (world.maxZ - world.minZ);
            int tries = Mathf.RoundToInt(area * dustHeight * dust);
            Color pale = new Color(0.62f, 0.75f, 1f).linear, gold = new Color(1f, 0.74f, 0.4f).linear;
            for (int i = 0; i < tries; i++)
            {
                float x = Mathf.Lerp(world.minX, world.maxX, (float)rng.NextDouble());
                float z = Mathf.Lerp(world.minZ, world.maxZ, (float)rng.NextDouble());
                if (rng.NextDouble() > 0.12f + 0.88f * Focus(x, z)) continue;
                if (world.HexDist(x, z) > -0.5f || world.Dist(x, z) > 0.5f) continue;   // in the canyons, not over the plateau
                float floor = world.WalkHeight(x, z);
                float y = floor + 0.12f + Mathf.Pow((float)rng.NextDouble(), 1.6f) * dustHeight;
                if (y > world.Ceiling(x, z) - 0.2f) continue;
                bool golden = rng.NextDouble() < 0.14;
                Color col = golden ? gold * (0.5f + 0.9f * (float)rng.NextDouble()) : pale * (0.25f + 0.6f * (float)rng.NextDouble());
                points.Add(new Point { pos = new Vector3(x, y, z), col = Pack(col.gamma, Dust, golden ? 10u : 0u) });
            }
        }

        /// <summary>The squared distance to the nearest way, on a coarse grid (for Focus).</summary>
        void BuildFocus()
        {
            fx0 = world.minX - 2;
            fz0 = world.minZ - 2;
            fw = Mathf.CeilToInt((world.maxX - world.minX + 4) / FocusCell) + 1;
            fh = Mathf.CeilToInt((world.maxZ - world.minZ + 4) / FocusCell) + 1;
            focusD2 = new float[fw * fh];
            for (int i = 0; i < focusD2.Length; i++) focusD2[i] = float.MaxValue;
            if (focus == null) return;
            int rc = Mathf.CeilToInt(focusReach * 3 / FocusCell);
            foreach (List<Vector2> track in focus)
            {
                if (track == null) continue;
                for (int k = 0; k < track.Count; k++)
                {
                    Vector2 a = track[k], b = k + 1 < track.Count ? track[k + 1] : a;
                    int steps = Mathf.Max(1, Mathf.CeilToInt((b - a).magnitude / 0.7f));
                    for (int s = 0; s < steps; s++)
                    {
                        Vector2 p = Vector2.Lerp(a, b, s / (float)steps);
                        int ci = Mathf.RoundToInt((p.x - fx0) / FocusCell), cj = Mathf.RoundToInt((p.y - fz0) / FocusCell);
                        for (int j = Mathf.Max(0, cj - rc); j <= Mathf.Min(fh - 1, cj + rc); j++)
                        for (int i = Mathf.Max(0, ci - rc); i <= Mathf.Min(fw - 1, ci + rc); i++)
                        {
                            float dx = fx0 + i * FocusCell - p.x, dz = fz0 + j * FocusCell - p.y;
                            float d2 = dx * dx + dz * dz;
                            if (d2 < focusD2[j * fw + i]) focusD2[j * fw + i] = d2;
                        }
                    }
                }
            }
        }

        /// <summary>1 on the ways the ant is led, falling toward 0 away from them (1 everywhere if there are none).</summary>
        float Focus(float x, float z)
        {
            if (focus == null || focus.Count == 0 || focusD2 == null) return 1f;
            int i = Mathf.Clamp(Mathf.RoundToInt((x - fx0) / FocusCell), 0, fw - 1);
            int j = Mathf.Clamp(Mathf.RoundToInt((z - fz0) / FocusCell), 0, fh - 1);
            float d2 = focusD2[j * fw + i];
            return d2 == float.MaxValue ? 0f : Mathf.Exp(-d2 / (focusReach * focusReach));
        }

        /// <summary>A plant's point colours as its region shows it (the Plant shader's hue rotation), packed.</summary>
        uint[] Colours(PlantInstance pi)
        {
            Color scanColour = scanOverride.a > 0 ? scanOverride * (pi.far ? 0.6f : 1f) : pi.scan;
            var key = (pi.plant, pi.hue, pi.sat, pi.glow, scanColour);
            if (colours.TryGetValue(key, out uint[] cached)) return cached;
            Color32[] src = pi.plant.pointColors;
            var cols = new uint[src.Length];
            float a = pi.hue * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            for (int k = 0; k < src.Length; k++)
            {
                Color lin = ((Color)src[k]).linear;
                // the hue rotation in YIQ, as AG_HueShift
                float y = lin.r * 0.299f + lin.g * 0.587f + lin.b * 0.114f;
                float i = lin.r * 0.596f - lin.g * 0.274f - lin.b * 0.322f;
                float q = lin.r * 0.211f - lin.g * 0.523f + lin.b * 0.312f;
                float i2 = (i * cs - q * sn) * pi.sat, q2 = (i * sn + q * cs) * pi.sat;
                var shifted = new Color(Mathf.Max(0, y + 0.956f * i2 + 0.621f * q2), Mathf.Max(0, y - 0.272f * i2 - 0.647f * q2), Mathf.Max(0, y - 1.106f * i2 + 1.703f * q2));
                bool glows = src[k].a > 127;
                // like a scan: pale points, brighter where the surface is lighter, a little of its own colour left
                float lum = shifted.r * 0.3f + shifted.g * 0.55f + shifted.b * 0.15f;
                Color scan = scanColour.linear * (0.18f + 1.3f * Mathf.Sqrt(lum));
                Color c = glows ? Color.Lerp(Color.Lerp(shifted, pi.glow.linear, 0.7f), scanColour.linear, mode == Mode.Replace ? 0.5f : 0f)
                                : Color.Lerp(shifted * 1.6f, scan, scanMix);
                cols[k] = Pack(c.gamma, glows ? Glow : Plant);
            }
            colours[key] = cols;
            return cols;
        }

        /// <summary>Gamma-space rgb in three bytes; the fourth says what the point is (top two bits) and how big
        /// (low six: × size/8; 0 = its own size).</summary>
        static uint Pack(Color c, uint kind, uint size = 0)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 255), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 255), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 255), 0, 255);
            return r | (g << 8) | (b << 16) | (((kind << 6) | (size & 63)) << 24);
        }
    }
}

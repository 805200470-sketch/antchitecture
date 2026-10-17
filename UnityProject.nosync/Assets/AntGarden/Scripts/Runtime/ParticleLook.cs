using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace AntGarden
{
    /// <summary>
    /// An experiment toward the look of Gaussian splats and point clouds (as in Marshmallow Laser Feast's
    /// "Dream"): the plants, ruin props and ground round the player's ant are drawn as glowing points sampled
    /// from their surfaces (AssetBuilder.SurfacePoints), and the meshes themselves sink to dark silhouettes. They
    /// still block, cast shadows and answer touches as before. P toggles it; -particlelook (or particleLook in
    /// game_config.json) starts with it on. Points are gathered within a radius of the ant, and gathered again
    /// once it has walked a good way; all of them are drawn in one call from one buffer
    /// (Shaders/ParticleLook.shader).
    /// </summary>
    public class ParticleLook : MonoBehaviour
    {
        public float radius = 60f;          // plants and props this close to the ant become points
        public float groundRadius = 40f;    // and the ground this close
        public float regatherAfter = 20f;   // gather again once the ant has walked this far
        public float perUnit = 220f;        // points for a plant one unit tall; more for taller ones (height^1.6, its surface)
        public float pointSize = 0.02f;     // a point's radius (units)
        public float brightness = 1.2f;
        public float groundStep = 0.5f;     // spacing of the ground's points
        public float plantDim = 0.93f;      // how dark the plant meshes go behind their points
        public float groundDim = 0.5f;      // and the ground
        public float scanMix = 0.65f;       // how far a point's colour goes from its texture toward the pale scan colour

        struct Point { public Vector3 pos; public uint col; }

        World world;
        Transform follow;
        Material mat;
        ComputeBuffer buffer;
        Point[] uploaded = new Point[0];
        int count;
        Vector3 gatheredAt;
        bool on;
        readonly List<Point> points = new List<Point>(1 << 20);
        readonly Dictionary<(PlantRef, float, float, Color, Color), uint[]> colours = new Dictionary<(PlantRef, float, float, Color, Color), uint[]>();

        public bool On => on;
        public int Count => count;

        public void Init(World w, Transform ant, Material m)
        {
            world = w;
            follow = ant;
            mat = m;
            if (mat != null) { mat.SetFloat("_Size", pointSize); mat.SetFloat("_Gain", brightness); }
            SetOn(false);
        }

        public void SetOn(bool value)
        {
            on = value && mat != null;
            Shader.SetGlobalFloat("_PlantDim", on ? plantDim : 0f);
            Shader.SetGlobalFloat("_GroundDim", on ? groundDim : 0f);
            if (on) Gather();
        }

        void Update()
        {
            if (!on || world == null || follow == null) return;
            if ((follow.position - gatheredAt).sqrMagnitude > regatherAfter * regatherAfter) Gather();
            if (count == 0) return;
            mat.SetBuffer("_Points", buffer);
            var bounds = new Bounds(gatheredAt, Vector3.one * (radius * 2 + 40));
            Graphics.DrawProcedural(mat, bounds, MeshTopology.Triangles, count * 6, 1, null, null, ShadowCastingMode.Off, false, 0);
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
            float r2 = radius * radius;
            foreach (PlantInstance pi in world.plantInstances)
            {
                if (pi.t == null || pi.plant == null || !pi.plant.LoadPoints()) continue;
                Vector3 p0 = pi.t.position;
                float dx = p0.x - c.x, dz = p0.z - c.z;
                if (dx * dx + dz * dz > r2) continue;
                int n = Mathf.Clamp(Mathf.RoundToInt(perUnit * Mathf.Pow(pi.t.lossyScale.y, 1.6f)), 40, pi.plant.points.Length);
                uint[] cols = Colours(pi);
                Matrix4x4 m = pi.t.localToWorldMatrix;
                for (int k = 0; k < n; k++) points.Add(new Point { pos = m.MultiplyPoint3x4(pi.plant.points[k]), col = cols[k] });
            }
            // the ground: a fine, slightly jittered grid of points over the floor, banks and plateau
            var rng = new System.Random(7);
            float g2 = groundRadius * groundRadius;
            for (float x = -groundRadius; x <= groundRadius; x += groundStep)
            for (float z = -groundRadius; z <= groundRadius; z += groundStep)
            {
                if (x * x + z * z > g2) continue;
                float px = c.x + x + (float)(rng.NextDouble() - 0.5) * groundStep, pz = c.z + z + (float)(rng.NextDouble() - 0.5) * groundStep;
                if (world.HexDist(px, pz) > -0.3f) continue;
                Region r = world.RegionAt(px, pz);
                bool floor = world.Dist(px, pz) < 0;
                Color g = Color.Lerp(r.biome.groundColor, r.biome.rimColor, 0.35f) * (floor ? 1.3f : 0.9f) * (0.6f + 0.8f * (float)rng.NextDouble());
                points.Add(new Point { pos = new Vector3(px, world.WalkHeight(px, pz) + 0.03f, pz), col = Pack(g, false, true) });
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
            Debug.Log($"[AntGarden] particle look: {count} points round ({c.x:F0}, {c.z:F0})");
        }

        /// <summary>A plant's point colours as its region shows it (the Plant shader's hue rotation), packed.</summary>
        uint[] Colours(PlantInstance pi)
        {
            var key = (pi.plant, pi.hue, pi.sat, pi.glow, pi.scan);
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
                Color scan = pi.scan.linear * (0.18f + 1.3f * Mathf.Sqrt(lum));
                Color c = glows ? Color.Lerp(shifted, pi.glow.linear, 0.7f) : Color.Lerp(shifted * 1.6f, scan, scanMix);
                cols[k] = Pack(c.gamma, glows);
            }
            colours[key] = cols;
            return cols;
        }

        /// <summary>Gamma-space rgb in three bytes; the fourth says what the point is: 0 plant, 128 ground, 255 glowing.</summary>
        static uint Pack(Color c, bool glows, bool ground = false)
        {
            uint r = (uint)Mathf.Clamp(Mathf.RoundToInt(c.r * 255), 0, 255);
            uint g = (uint)Mathf.Clamp(Mathf.RoundToInt(c.g * 255), 0, 255);
            uint b = (uint)Mathf.Clamp(Mathf.RoundToInt(c.b * 255), 0, 255);
            return r | (g << 8) | (b << 16) | ((glows ? 255u : ground ? 128u : 0u) << 24);
        }
    }
}

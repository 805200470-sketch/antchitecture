using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Paints the world map texture used by the minimap and the big map: hill-shaded ground, the walkable
    /// canyon floor in pale teal, recognized centrelines and the hexagon outline (outer rim bright, stitched
    /// seams faint). North (+Z) is up, so the map reads like the source photo.
    /// </summary>
    public class MapPainter
    {
        public readonly Texture2D texture;
        readonly World world;
        readonly int w, h;

        public MapPainter(World world, List<Gate> gates = null, int longSide = 1024)
        {
            this.world = world;
            float bw = world.maxX - world.minX, bh = world.maxZ - world.minZ;
            float s = longSide / Mathf.Max(bw, bh);
            w = Mathf.CeilToInt(bw * s);
            h = Mathf.CeilToInt(bh * s);
            texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, filterMode = FilterMode.Bilinear };
            var px = new Color32[w * h];
            var light = new Vector3(-0.5f, 0.75f, 0.45f).normalized;
            for (int y = 0; y < h; y++)
            for (int x = 0; x < w; x++)
            {
                Vector2 wp = ToWorld((x + 0.5f) / w, (y + 0.5f) / h);
                Color c;
                if (world.HexDist(wp.x, wp.y) > 0) c = new Color(0, 0, 0, 0);
                else
                {
                    Vector3 n = world.Normal(wp.x, wp.y);
                    float shade = Mathf.Clamp01(Vector3.Dot(n, light));
                    float d = world.PathDist(wp.x, wp.y);
                    Color ground = Color.Lerp(new Color(0.06f, 0.14f, 0.16f), new Color(0.2f, 0.33f, 0.35f), shade);
                    Color floor = Color.Lerp(new Color(0.32f, 0.58f, 0.58f), new Color(0.55f, 0.82f, 0.8f), shade);
                    c = Color.Lerp(floor, ground, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(-0.8f, 0.8f, d)));
                    Biome b = world.RegionAt(wp.x, wp.y).biome;
                    if (b.mapHue != 0) c = HueShift(c, b.mapHue, b.mapSaturation);
                    c.a = 0.95f;
                }
                px[y * w + x] = c;
            }
            texture.SetPixels32(px);
            foreach (WEdge e in world.edges)
                for (int i = 1; i < e.pts.Count; i++)
                    Line(e.pts[i - 1].x, e.pts[i - 1].y, e.pts[i].x, e.pts[i].y, new Color(0.85f, 0.97f, 0.95f, 0.55f));
            foreach (WFrameEdge fe in world.frameEdges)
            {
                Color rim = world.regions[fe.region].biome.gateColor;
                Line(fe.x0, fe.z0, fe.x1, fe.z1, fe.shared ? new Color(rim.r, rim.g, rim.b, 0.25f) : new Color(rim.r, rim.g, rim.b, 0.95f), 2);
            }
            if (gates != null)
                foreach (Gate g in gates)
                {
                    Color c = g.portal ? new Color(1f, 0.85f, 0.5f, 1f) : new Color(0.7f, 1f, 0.95f, 1f);
                    var right = new Vector2(g.dir.y, -g.dir.x) * g.radius;
                    Line(g.position.x - right.x, g.position.z - right.y, g.position.x + right.x, g.position.z + right.y, c, 3);
                    // portals: a dotted line to the paired gate in the other region
                    if (g.portal && g.pair != null && g.region < g.pair.region)
                    {
                        Vector2 a = new Vector2(g.position.x, g.position.z), b = new Vector2(g.pair.position.x, g.pair.position.z);
                        float len = Vector2.Distance(a, b);
                        for (float t = 0; t < len; t += 6f)
                        {
                            Vector2 p0 = Vector2.Lerp(a, b, t / len), p1 = Vector2.Lerp(a, b, Mathf.Min(1, (t + 3f) / len));
                            Line(p0.x, p0.y, p1.x, p1.y, new Color(1f, 0.85f, 0.5f, 0.8f), 2);
                        }
                    }
                }
            texture.Apply();
        }

        /// <summary>The same YIQ hue rotation as AG_HueShift in the shaders, so the map matches the ground.</summary>
        public static Color HueShift(Color c, float deg, float sat)
        {
            float y = 0.299f * c.r + 0.587f * c.g + 0.114f * c.b;
            float i = 0.596f * c.r - 0.274f * c.g - 0.322f * c.b;
            float q = 0.211f * c.r - 0.523f * c.g + 0.312f * c.b;
            float a = deg * Mathf.Deg2Rad, cs = Mathf.Cos(a), sn = Mathf.Sin(a);
            float i2 = (i * cs - q * sn) * sat, q2 = (i * sn + q * cs) * sat;
            return new Color(Mathf.Max(0, y + 0.956f * i2 + 0.621f * q2), Mathf.Max(0, y - 0.272f * i2 - 0.647f * q2),
                             Mathf.Max(0, y - 1.106f * i2 + 1.703f * q2), c.a);
        }

        /// <summary>World (x, z) to map UV (v = 1 at the top = north = +Z).</summary>
        public Vector2 ToUv(float x, float z)
        {
            return new Vector2((x - world.minX) / (world.maxX - world.minX), (z - world.minZ) / (world.maxZ - world.minZ));
        }

        public Vector2 ToWorld(float u, float v)
        {
            return new Vector2(world.minX + u * (world.maxX - world.minX), world.minZ + v * (world.maxZ - world.minZ));
        }

        public float Aspect => (float)w / h;

        void Line(float x0, float z0, float x1, float z1, Color c, int thick = 1)
        {
            Vector2 a = ToUv(x0, z0), b = ToUv(x1, z1);
            float ax = a.x * w, ay = a.y * h, bx = b.x * w, by = b.y * h;
            int steps = Mathf.CeilToInt(Mathf.Max(Mathf.Abs(bx - ax), Mathf.Abs(by - ay))) + 1;
            for (int i = 0; i <= steps; i++)
            {
                float t = (float)i / steps;
                int x = Mathf.RoundToInt(Mathf.Lerp(ax, bx, t)), y = Mathf.RoundToInt(Mathf.Lerp(ay, by, t));
                for (int oy = -thick + 1; oy < thick; oy++)
                for (int ox = -thick + 1; ox < thick; ox++)
                {
                    int px = x + ox, py = y + oy;
                    if (px < 0 || py < 0 || px >= w || py >= h) continue;
                    texture.SetPixel(px, py, Color.Lerp(texture.GetPixel(px, py), c, c.a));
                }
            }
        }
    }
}

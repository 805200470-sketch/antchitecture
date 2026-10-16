using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// The seams between neighbouring nests of a honeycomb, marked by light instead of a line: small motes rising
    /// slowly off the ground along each shared hexagon edge and fading, in each side's own glow colour (a few
    /// strays cross over), thickest where a canyon runs through (where the ant walks through them) and thinning
    /// and thickening along the edge. Drawn with Shaders/GuideTrail.shader (drifting particles); the ant passing
    /// brightens them a little.
    /// </summary>
    public static class SeamMotes
    {
        /// <param name="gain">brightness (0 = none)</param>
        /// <param name="density">how thickly they rise, relative (1 = as designed)</param>
        public static MeshRenderer Build(World world, List<Region> cells, Material mat, float gain, float density, Transform parent, int seed)
        {
            if (mat == null || gain <= 0 || cells.Count < 2) return null;
            var rng = new System.Random(seed);
            float Rand(float a, float b) => a + (float)rng.NextDouble() * (b - a);
            float Gauss()
            {
                double u1 = 1.0 - rng.NextDouble(), u2 = rng.NextDouble();
                return (float)(System.Math.Sqrt(-2.0 * System.Math.Log(u1)) * System.Math.Sin(2 * System.Math.PI * u2));
            }
            var glow = new GlowParticles();
            int seams = 0;
            for (int i = 0; i < cells.Count; i++)
            for (int j = i + 1; j < cells.Count; j++)
            {
                Region a = cells[i], b = cells[j];
                var ca = new Vector2(a.cellX, a.cellZ);
                var cb = new Vector2(b.cellX, b.cellZ);
                float apothem = a.cellR * 0.8660254f, dist = (cb - ca).magnitude;
                if (Mathf.Abs(dist - 2 * apothem) > apothem * 0.1f) continue;   // not neighbours
                seams++;
                // the shared edge: as long as the hexagon's side (= its circumradius), across the line between centres
                Vector2 mid = (ca + cb) / 2, n = (cb - ca) / dist, t = new Vector2(-n.y, n.x);
                float half = a.cellR / 2;
                bool differ = a.biome.id != b.biome.id;
                Color colA = Color.Lerp(a.biome.plantGlow, Color.white, 0.1f), colB = Color.Lerp(b.biome.plantGlow, Color.white, 0.1f);
                for (float s = -half + Rand(0, 0.3f); s < half;)
                {
                    float across = Mathf.Clamp(Gauss() * 0.9f, -2.4f, 2.4f);
                    Vector2 q = mid + t * (s + Rand(-0.15f, 0.15f)) + n * across;
                    // over a canyon floor (where the ant walks through) they rise thick; over the banks and the
                    // plateau sparser; and they thin and thicken along the edge
                    bool path = world.Dist(q.x, q.y) < 0;
                    float thick = Mathf.InverseLerp(0.25f, 0.75f, Mathf.PerlinNoise(q.x * 0.07f + 3.1f, q.y * 0.07f - 7.7f));
                    float rate = (path ? 6.5f : 2f) * (0.35f + thick) * (differ ? 1f : 0.7f) * density;
                    s += Mathf.Min(2f, -Mathf.Log(1f - (float)rng.NextDouble() * 0.999f) / Mathf.Max(0.05f, rate));
                    if (world.HexDist(q.x, q.y) > -0.5f || world.colliders.Overlaps(q.x, q.y, 0.05f)) continue;
                    var from = new Vector3(q.x, world.WalkHeight(q.x, q.y) + Rand(0f, 0.5f), q.y);
                    // they rise a couple of units and fade (under a tunnel roof, short of it)
                    float rise = Mathf.Min(Rand(1.2f, 2.8f), world.Ceiling(q.x, q.y) - from.y - 0.3f);
                    if (rise < 0.4f) continue;
                    Vector2 sway = t * Rand(-0.25f, 0.25f) + n * Rand(-0.2f, 0.2f);
                    bool sideB = across > 0 ^ rng.NextDouble() < 0.15;
                    Color col = (sideB ? colB : colA) * Mathf.Clamp01(Rand(0.45f, 1f) * (0.7f + 0.5f * thick));
                    glow.Add(from, new Vector3(sway.x, rise, sway.y), 2, 1e4f, Rand(0, 1), Rand(0.04f, 0.09f), Rand(4.5f, 8.5f), 0, col);
                }
            }
            if (glow.Count == 0) return null;
            var block = new MaterialPropertyBlock();
            block.SetFloat("_Gain", 1.4f * gain);
            block.SetFloat("_Base", 0.6f);
            block.SetFloat("_Near", 0.3f);
            block.SetFloat("_Ahead", 0f);
            block.SetFloat("_PlayerD", -1f);
            var mr = glow.Finish("SeamMotes", mat, parent, block);
            Debug.Log($"[AntGarden] seam motes: {glow.Count} along {seams} seams");
            return mr;
        }
    }
}

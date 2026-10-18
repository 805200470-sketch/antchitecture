using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// A measured plan of the particle nest, seen straight from above (written with the scene shots, -sceneshots):
    /// the canyon floor (where the ants walk) light, its banks darker, the plateau darkest, a grid every 5 units,
    /// the glowing ways (the main one gold), the heart, and the things left beside the way. Next to it a text file
    /// with the floor's width across the main way every 5 units along it, in units and in body lengths of the
    /// player's ant (1 unit), for judging whether the passages should be narrower.
    /// </summary>
    public static class LabMap
    {
        const float PixelsPerUnit = 10f;

        public static void Write(Game game, string dir)
        {
            LabNest lab = game.LabNest;
            World world = game.world;
            if (lab == null || world == null) return;
            Region region = world.regions[0];
            float x0 = region.minX - 2, z0 = region.minZ - 2, x1 = region.maxX + 2, z1 = region.maxZ + 2;
            int w = Mathf.CeilToInt((x1 - x0) * PixelsPerUnit), h = Mathf.CeilToInt((z1 - z0) * PixelsPerUnit);
            var px = new Color32[w * h];
            Color32 outside = new Color32(6, 8, 12, 255), plateau = new Color32(24, 34, 46, 255), bank = new Color32(58, 84, 104, 255), floor = new Color32(150, 200, 214, 255);
            for (int j = 0; j < h; j++)
            for (int i = 0; i < w; i++)
            {
                float x = x0 + (i + 0.5f) / PixelsPerUnit, z = z0 + (j + 0.5f) / PixelsPerUnit;
                Color32 c;
                if (world.HexDist(x, z) > 0) c = outside;
                else
                {
                    float d = world.Dist(x, z);
                    c = d < 0 ? floor : d < world.bankWidth ? Color32.Lerp(bank, plateau, d / world.bankWidth * 0.6f) : plateau;
                }
                // a grid every 5 units
                if (Mathf.Abs(Mathf.Repeat(x, 5f)) < 1f / PixelsPerUnit || Mathf.Abs(Mathf.Repeat(z, 5f)) < 1f / PixelsPerUnit)
                    c = Color32.Lerp(c, new Color32(120, 140, 160, 255), 0.25f);
                px[j * w + i] = c;
            }
            void Dot(Vector2 p, float r, Color32 col)
            {
                int ci = Mathf.RoundToInt((p.x - x0) * PixelsPerUnit), cj = Mathf.RoundToInt((p.y - z0) * PixelsPerUnit), rr = Mathf.CeilToInt(r * PixelsPerUnit);
                for (int j = cj - rr; j <= cj + rr; j++)
                for (int i = ci - rr; i <= ci + rr; i++)
                    if (i >= 0 && j >= 0 && i < w && j < h && (i - ci) * (i - ci) + (j - cj) * (j - cj) <= rr * rr) px[j * w + i] = col;
            }
            // the ways: the others pale, the main one gold
            GuideTrail.Route route = lab.Route;
            if (route != null)
            {
                foreach (var (pts, _, main) in route.tracks)
                    if (!main)
                        foreach (Vector2 p in pts) Dot(p, 0.12f, new Color32(210, 230, 235, 255));
                foreach (var (pts, _, main) in route.tracks)
                    if (main)
                        foreach (Vector2 p in pts) Dot(p, 0.16f, new Color32(255, 200, 90, 255));
            }
            Dot(lab.Heart, 1.2f, new Color32(255, 150, 110, 255));
            foreach (var (_, at, _) in lab.Objects) Dot(at, 0.5f, new Color32(255, 255, 255, 255));
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            tex.SetPixels32(px);
            tex.Apply();
            File.WriteAllBytes(Path.Combine(dir, "labmap.png"), tex.EncodeToPNG());
            Object.Destroy(tex);

            // the floor's width across the main way, every 5 units along it
            var sb = new StringBuilder();
            var inv = CultureInfo.InvariantCulture;
            sb.AppendLine($"rect {x0.ToString("F2", inv)} {z0.ToString("F2", inv)} {x1.ToString("F2", inv)} {z1.ToString("F2", inv)} ppu {PixelsPerUnit.ToString(inv)} bank {world.bankWidth.ToString("F2", inv)}");
            List<Vector2> track = route?.mainTrack;
            if (track != null && track.Count > 2)
            {
                float along = 0, next = 0;
                for (int k = 1; k < track.Count - 1; k++)
                {
                    along += (track[k] - track[k - 1]).magnitude;
                    if (along < next) continue;
                    next += 5f;
                    Vector2 p = track[k], way = (track[k + 1] - track[k - 1]).normalized, n = new Vector2(-way.y, way.x);
                    float floorW = Across(world, p, n, 0f), wallW = Across(world, p, n, world.bankWidth * 0.5f);
                    sb.AppendLine($"at {along.ToString("F0", inv)} x {p.x.ToString("F2", inv)} z {p.y.ToString("F2", inv)} nx {n.x.ToString("F3", inv)} nz {n.y.ToString("F3", inv)} floor {floorW.ToString("F2", inv)} walls {wallW.ToString("F2", inv)}");
                }
            }
            File.WriteAllText(Path.Combine(dir, "labmap.txt"), sb.ToString());
            Debug.Log($"[AntGarden] particle nest plan written ({w}×{h})");
        }

        /// <summary>How wide the ground below level (the distance field) is across p along n, in units.</summary>
        static float Across(World world, Vector2 p, Vector2 n, float level)
        {
            float a = 0, b = 0;
            while (a < 20 && world.Dist(p.x + n.x * a, p.y + n.y * a) < level) a += 0.05f;
            while (b < 20 && world.Dist(p.x - n.x * b, p.y - n.y * b) < level) b += 0.05f;
            return a + b;
        }
    }
}

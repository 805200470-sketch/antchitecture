using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Look check (-lookcheck &lt;dir&gt;): close-up screenshots for judging materials rather than motion —
    /// the canyon floor of every region from ant height, each plant kind (or those named with
    /// -plants a,b,c) from a few body lengths away, and a glowing plant before and after the ant touches it.
    /// Writes PNGs and quits.
    /// </summary>
    public static class LookCheck
    {
        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            game.demo = true;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            game.SetRingVisible(false);
            yield return new WaitForSeconds(2f);

            IEnumerator Shot(string name, int settle = 20)
            {
                for (int i = 0; i < settle; i++) yield return null;
                yield return new WaitForEndOfFrame();
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, name + ".png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }

            // the player stands at p, facing `heading`; the camera hangs behind and above
            void Stand(Vector2 p, float heading, float distance, float pitch)
            {
                game.player.Place(p.x, p.y, heading);
                game.rig.free = true;
                game.rig.distance = distance;
                game.rig.pitch = pitch;
                game.rig.yaw = heading + Mathf.PI;
                game.rig.SnapBehind(heading);
            }

            // 1 · the floor of each region, from ant height and from a little higher
            for (int r = 0; r < game.world.regions.Count; r++)
            {
                WEdge e = DemoMode.LongEdge(game, r);
                if (e == null) continue;
                Vector2 a = e.pts[e.pts.Count / 2], b = e.pts[Mathf.Min(e.pts.Count - 1, e.pts.Count / 2 + 3)];
                float h = Mathf.Atan2(b.x - a.x, b.y - a.y);
                Stand(a, h, 2.6f, 0.12f);
                yield return Shot($"ground_r{r}_low");
                Stand(a, h, 6f, 0.45f);
                yield return Shot($"ground_r{r}_high");
                // the bank, face-on from the far side of the canyon: its soil layers and relief
                Vector2 side = new Vector2(Mathf.Cos(h), -Mathf.Sin(h));   // perpendicular to the path
                Vector2 bankFoot = a + side * game.world.HalfWidth(a.x, a.y) * 0.4f;
                Stand(bankFoot, h + Mathf.PI / 2, 5.5f, 0.12f);
                yield return Shot($"bank_r{r}");
            }

            // 2 · each plant kind, nearest instance to a walkable spot
            string only = Game.Arg("-plants");
            var seen = new HashSet<string>();
            foreach (Placement pl in game.world.placements)
            {
                if (seen.Contains(pl.plant)) continue;
                if (!string.IsNullOrEmpty(only) && !GameConfig.InList(only, pl.plant)) continue;
                float back = Mathf.Clamp(pl.height * 1.3f + 1.2f, 2f, 16f);
                // look at the plant from the walkable ground in front of it
                bool found = false;
                Vector2 spot = default;
                for (int k = 0; k < 8 && !found; k++)
                {
                    float ang = k * Mathf.PI / 4;
                    found = game.world.SnapToWalkable(pl.x + Mathf.Sin(ang) * back, pl.z + Mathf.Cos(ang) * back, 1.5f, out spot);
                }
                if (!found) continue;
                seen.Add(pl.plant);
                // the camera stands on the walkable spot, at a third of the plant's height (at least ant eye
                // level), and looks at the middle of the plant; the ant steps out of the picture
                float baseY = game.world.Height(pl.x, pl.z);
                Vector3 target = new Vector3(pl.x, baseY + pl.height * 0.5f, pl.z);
                Vector3 eye = new Vector3(spot.x, game.world.Height(spot.x, spot.y) + Mathf.Max(0.5f, pl.height * 0.35f), spot.y);
                game.player.Place(spot.x, spot.y, Mathf.Atan2(pl.x - spot.x, pl.z - spot.y));
                game.player.SetVisible(false);
                game.rig.manual = true;
                Camera.main.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                yield return Shot($"plant_{pl.plant}");
                game.rig.manual = false;
                game.player.SetVisible(true);
            }

            // 2b · ruin scenes: every kind of relic, then a conversation with the first one
            foreach (RuinScene ruin in game.Ruins)
            {
                var kinds = new HashSet<string>();
                for (int i = 0; i < ruin.Count; i++)
                {
                    if (!kinds.Add(ruin.IdOf(i))) continue;
                    Vector3 c = ruin.PosOf(i);
                    float back = ruin.ReachOf(i) * 2.2f + 2f;
                    bool found = false;
                    Vector2 spot = default;
                    for (int k = 0; k < 12 && !found; k++)
                    {
                        float ang = k * Mathf.PI / 6;
                        found = game.world.SnapToWalkable(c.x + Mathf.Sin(ang) * back, c.z + Mathf.Cos(ang) * back, 1.5f, out spot);
                    }
                    if (!found) continue;
                    float h = Mathf.Max(1.2f, ruin.ReachOf(i));
                    Vector3 target = c + Vector3.up * h * 0.5f;
                    Vector3 eye = new Vector3(spot.x, game.world.Height(spot.x, spot.y) + Mathf.Min(h * 0.6f + 0.6f, game.world.Ceiling(spot.x, spot.y) - game.world.Height(spot.x, spot.y) - 0.6f), spot.y);
                    game.player.Place(spot.x, spot.y, Mathf.Atan2(c.x - spot.x, c.z - spot.y));
                    game.player.SetVisible(false);
                    game.rig.manual = true;
                    Camera.main.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
                    yield return Shot($"ruin_{ruin.kind}_{ruin.IdOf(i)}");
                    game.rig.manual = false;
                    game.player.SetVisible(true);
                }
                // walk up to the first relic and talk to it (E), twice
                for (int i = 0; i < ruin.Count; i++)
                {
                    Vector3 c = ruin.PosOf(i);
                    if (!game.world.SnapToWalkable(c.x, c.z, ruin.ReachOf(i) + 0.8f, out Vector2 near)) continue;
                    game.player.Place(near.x, near.y, Mathf.Atan2(c.x - near.x, c.z - near.y));
                    game.rig.free = true;
                    game.rig.distance = 4.5f;
                    game.rig.pitch = 0.25f;
                    game.rig.SnapBehind(game.player.heading);
                    for (int f = 0; f < 10; f++) yield return null;
                    game.Interact();
                    yield return Shot($"ruin_{ruin.kind}_talk1", 18);
                    game.Interact();
                    yield return Shot($"ruin_{ruin.kind}_talk2", 18);
                    break;
                }
            }

            // 3 · a glowing plant on the floor, before and after the ant touches it
            foreach (Placement pl in game.world.placements)
            {
                PlantRef pr = game.refs.Plant(pl.plant);
                var mr = pr != null && pr.prefab != null ? pr.prefab.GetComponentInChildren<MeshRenderer>() : null;
                if (mr == null || mr.sharedMaterial == null || !mr.sharedMaterial.HasProperty("_GlowStrength") || mr.sharedMaterial.GetFloat("_GlowStrength") <= 0) continue;
                if (pl.height > 3f || game.world.Dist(pl.x, pl.z) > -1.2f) continue;
                if (!game.world.SnapToWalkable(pl.x - 3f, pl.z, 1.2f, out Vector2 spot)) continue;
                float heading = Mathf.Atan2(pl.x - spot.x, pl.z - spot.y);
                Stand(spot, heading, 3.4f, 0.28f);
                yield return Shot($"touch_0_before_{pl.plant}");
                // walk into it
                for (float t = 0; t < 2.6f; t += Time.deltaTime)
                {
                    game.demoMove = new Vector2(Mathf.Sin(heading), Mathf.Cos(heading)) * 0.8f;
                    yield return null;
                }
                game.demoMove = Vector2.zero;
                yield return Shot("touch_1_flare", 6);
                yield return new WaitForSeconds(1.2f);
                yield return Shot("touch_2_ripple", 1);
                yield return new WaitForSeconds(4f);
                yield return Shot("touch_3_after", 1);
                break;
            }
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Application.Quit();
        }
    }
}

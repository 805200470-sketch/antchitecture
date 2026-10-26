using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Interaction check for the particle nests (-interactshots &lt;dir&gt;, with -world "lab:…"): the same four moments in
    /// every nest, so they can be laid side by side and seen to behave alike. A glowing thing an antenna can light, a
    /// quarter to three quarters of the way in, a little off the way: 1 idle (the ant stands back from it, it only
    /// breathes); 2 touched (it is lit as an antenna touching it would light it: it flares, its light runs on to its
    /// neighbours, the goal moves on); 3 walked past (the ant walks up beside it: what it passes lights up); 4 the heart
    /// (the ant walks into it: the chapter's end). Also writes how much there is to touch along the way. Quits when done.
    /// </summary>
    public static class InteractShots
    {
        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            game.demo = true;
            game.hud.SetStatsVisible(false);
            game.hud.SetHelpVisible(false);
            yield return new WaitForSeconds(3f);   // shaders compile, the NPCs spread out
            LabNest lab = game.LabNest;
            GlowPlants glow = game.world.glow;
            List<Vector2> t = lab?.Track;
            if (lab == null || glow == null || t == null || t.Count < 12)
            {
                Debug.LogWarning("[AntGarden] interactshots: not a particle nest");
                Application.Quit();
                yield break;
            }
            // how much there is to touch along the way (within 2 units of the main way, and in all)
            float length = 0;
            for (int i = 1; i < t.Count; i++) length += (t[i] - t[i - 1]).magnitude;
            int near = glow.CountNear(t, 2f);
            string theme = (NestTheme.Current ?? NestTheme.Cyan).id;
            string report = $"theme {theme}: way {length:F0} units, {glow.Count} things an antenna can light, {near} within 2 units of the way ({near / Mathf.Max(1f, length) * 10f:F1} per 10 units)";
            File.WriteAllText(Path.Combine(dir, "interact_report.txt"), report + "\n");
            Debug.Log("[AntGarden] interactshots " + report);

            // a touchable thing beside the way, a quarter to three quarters of the way in: a tall one if there is
            // one (a flare on a plant reads; on a pebble it does not)
            Vector3 thing = default;
            int at = -1;
            foreach (float tall in new[] { 0.7f, 0.35f, 0f })
            {
                for (int i = t.Count / 4; i < t.Count * 3 / 4 && at < 0; i++)
                    if (glow.NearestTouchable(new Vector3(t[i].x, 0, t[i].y), 1.6f, tall, out Vector3 g)) { thing = g; at = i; }
                if (at >= 0) break;
            }
            // (none in the middle half: anywhere along the way, a little further off it)
            for (int i = 4; i < t.Count - 4 && at < 0; i++)
                if (glow.NearestTouchable(new Vector3(t[i].x, 0, t[i].y), 2.4f, 0f, out Vector3 g)) { thing = g; at = i; }
            if (at < 0)
            {
                Debug.LogWarning("[AntGarden] interactshots: nothing to touch near the way");
                Application.Quit();
                yield break;
            }
            var target = new Vector2(thing.x, thing.z);
            int back = Back(t, at, 2.4f);

            // 1 idle: standing back from it, near enough for it to fill a good part of the picture
            DemoMode.PlaceFacing(game, t[back], target);
            DemoMode.Shot(game, game.cfg.camera.distance * 0.8f, game.cfg.camera.pitch);
            yield return new WaitForSeconds(2.5f);
            yield return SceneShots.ShellSettles();
            yield return Grab(Path.Combine(dir, "1_idle.png"));

            // 2 touched: lit as an antenna would light it (at the top of its flare)
            glow.TouchNearest(thing, 0.5f);
            yield return new WaitForSeconds(0.6f);
            yield return SceneShots.ShellSettles();
            yield return Grab(Path.Combine(dir, "2_touched.png"));
            yield return new WaitForSeconds(6f);   // its flare fades

            // 3 walked past: up beside it
            game.demoKeys = new Vector2(0, 1);
            for (float s = 0; s < 6f; s += Time.deltaTime)
            {
                Vector2 me = new Vector2(game.player.Position.x, game.player.Position.z);
                if ((me - target).magnitude < 1.1f || Vector2.Dot(target - me, (target - t[back]).normalized) < 0.2f) break;
                yield return null;
            }
            game.demoKeys = Vector2.zero;
            yield return new WaitForSeconds(1.0f);
            yield return SceneShots.ShellSettles();
            yield return Grab(Path.Combine(dir, "3_walked_past.png"));

            // 3b where a nest's buildings light only in parts (the kiln's temple): one of its spires, idle and touched
            if ((NestTheme.Current ?? NestTheme.Cyan).buildingsGlowInParts)
                foreach (var (id, c, a) in lab.Objects)
                {
                    if (!id.StartsWith("temple_spire")) continue;
                    DemoMode.PlaceFacing(game, a + (a - c).normalized * 1.2f, c);   // on the way, a little back from it
                    DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch * 0.7f);
                    yield return new WaitForSeconds(2.5f);
                    yield return SceneShots.ShellSettles();
            yield return Grab(Path.Combine(dir, "5_building_idle.png"));
                    glow.TouchNearest(new Vector3(c.x, 0, c.y), 1.5f);
                    yield return new WaitForSeconds(0.6f);
                    yield return SceneShots.ShellSettles();
            yield return Grab(Path.Combine(dir, "6_building_touched.png"));
                    yield return new WaitForSeconds(6f);
                    break;
                }

            // 4 the heart: walking the last stretch of the way into it (along the way, as a player would)
            Vector2 heart = lab.Heart;
            int hi = 0;
            for (int i = 1; i < t.Count; i++) if ((t[i] - heart).sqrMagnitude < (t[hi] - heart).sqrMagnitude) hi = i;
            int from = Back(t, hi, 7f), next = from;
            DemoMode.PlaceFacing(game, t[from], t[Mathf.Min(t.Count - 1, from + 3)]);
            DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
            yield return new WaitForSeconds(0.6f);
            game.demoKeys = null;
            bool settled = false;
            for (float s = 0; s < 9f; s += Time.deltaTime)
            {
                Vector2 me = DemoMode.Pos(game);
                if ((me - heart).magnitude < 3.0f) break;
                if (!settled && (me - heart).magnitude < 4.8f)
                {
                    // the plants' fine splats made before the chapter closes (its card shows five seconds; in a big
                    // nest the shell took longer than that to settle after it, and the still missed the card)
                    game.demoMove = Vector2.zero;
                    yield return SceneShots.ShellSettles();
                    settled = true;
                }
                while (next < hi && (t[next] - me).sqrMagnitude < 2.5f * 2.5f) next++;
                Vector2 d = (next < hi ? t[next] : heart) - me;
                game.demoMove = d.sqrMagnitude < 0.25f ? Vector2.zero : d.normalized;
                yield return null;
            }
            game.demoMove = Vector2.zero;
            game.demoKeys = Vector2.zero;
            yield return new WaitForSeconds(1.0f);
            yield return SceneShots.ShellSettles();
            yield return Grab(Path.Combine(dir, "4_heart.png"));

            File.WriteAllText(Path.Combine(dir, "done.txt"), game.world.label + "\n");
            Debug.Log("[AntGarden] interactshots done");
            Application.Quit();
        }

        /// <summary>The index of the way's point about dist units before index i (toward the way in).</summary>
        static int Back(List<Vector2> t, int i, float dist)
        {
            float acc = 0;
            int k = i;
            while (k > 0 && acc < dist)
            {
                acc += (t[k] - t[k - 1]).magnitude;
                k--;
            }
            return k;
        }

        static IEnumerator Grab(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }
    }
}

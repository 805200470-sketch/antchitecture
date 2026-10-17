using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Check film of the ruin tunnels (-trailfilm &lt;dir&gt;): in each ruin the ant crawls along the glowing
    /// specks, the camera following as in play: coming in at the way in nearest the player's start, halfway,
    /// and up to the heart. The game clock runs at a fixed 30 fps and every frame is saved (&lt;dir&gt;/&lt;kind&gt;/f00000.jpg), so
    /// the wave of light along the track plays at its real speed. Tools/frames_to_video.sh makes the MP4.
    /// </summary>
    public static class TrailFilm
    {
        const float Lookahead = 2.5f;

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            Application.targetFrameRate = -1;
            Time.captureFramerate = DemoMode.Fps;
            game.demo = true;
            game.demoKeys = null;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            yield return new WaitForSeconds(3f);   // shaders compile, the NPCs spread out
            var end = new WaitForEndOfFrame();
            var walks = new List<(string name, List<Vector2> track)>();
            foreach (RuinScene ruin in game.Ruins) walks.Add((ruin.kind, ruin.TrailMainFull ?? ruin.TrailMain));
            if (game.LabNest != null) walks.Add(("lab", game.LabNest.Track));
            foreach (var (name, track) in walks)
            {
                // from where the main track starts (in a honeycomb across the seam, in the neighbouring nest)
                if (track == null || track.Count < 4) continue;
                string sub = Path.Combine(dir, name);
                Directory.CreateDirectory(sub);
                float length = 0;
                for (int i = 1; i < track.Count; i++) length += (track[i] - track[i - 1]).magnitude;
                // three stretches: coming in across the seam, halfway, and up to the heart
                var parts = new[] { (from: 0.5f, seconds: 9f), (from: length * 0.45f, seconds: 5f), (from: Mathf.Max(0, length - 5.5f), seconds: 7f) };
                int f = 0;
                foreach (var (from, seconds) in parts)
                {
                    int at = 0;
                    for (float acc = 0; at < track.Count - 1 && acc < from; at++) acc += (track[at + 1] - track[at]).magnitude;
                    at = Mathf.Min(at, track.Count - 2);
                    DemoMode.PlaceFacing(game, track[at], track[Mathf.Min(track.Count - 1, at + 3)]);
                    DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
                    game.demoMove = Vector2.zero;
                    game.demoFast = false;
                    for (int i = 0; i < 20; i++) yield return null;   // the camera settles behind the ant
                    int next = at;
                    for (int k = 0, n = Mathf.RoundToInt(seconds * DemoMode.Fps); k < n; k++, f++)
                    {
                        // crawl toward a point a little way ahead on the track (at its end: stop at the ring)
                        Vector2 pos = DemoMode.Pos(game);
                        while (next < track.Count - 1 && (track[next] - pos).sqrMagnitude < Lookahead * Lookahead) next++;
                        Vector2 d = track[next] - pos;
                        game.demoMove = d.sqrMagnitude < 0.5f ? Vector2.zero : d.normalized;
                        yield return end;
                        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(sub, $"f{f:00000}.jpg"), tex.EncodeToJPG(90));
                        Object.Destroy(tex);
                        yield return null;
                    }
                }
                game.demoMove = Vector2.zero;
                Debug.Log($"[AntGarden] trail film {name}: {f} frames, track {length:F0} units");
            }
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Application.Quit();
        }
    }
}

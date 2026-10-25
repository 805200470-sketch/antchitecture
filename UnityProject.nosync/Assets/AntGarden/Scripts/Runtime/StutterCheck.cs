using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Frame-time check for the particle nests (-stuttercheck &lt;dir&gt;, with -world "lab:…"; -stutterwalk &lt;seconds&gt;,
    /// default 30): the ant walks the main way in real time, at the frame rate a player gets (no fixed capture rate), and
    /// every frame's time is kept with what came with it (a garbage collection, the fine plant shell remade or swapped
    /// in). Writes stutter_report.txt (the spread of the frame times, how even they are, the long frames and what came
    /// with them) and frames.csv. Quits when done.
    /// </summary>
    public static class StutterCheck
    {
        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            game.demo = true;
            game.demoKeys = null;
            game.hud.SetStatsVisible(false);
            game.hud.SetHelpVisible(false);
            yield return new WaitForSeconds(4f);   // shaders compile, the NPCs spread out, the first shell is made
            LabNest lab = game.LabNest;
            List<Vector2> t = lab?.Track;
            if (t == null || t.Count < 4)
            {
                Debug.LogWarning("[AntGarden] stuttercheck: not a particle nest");
                Application.Quit();
                yield break;
            }
            float seconds = float.TryParse(Game.Arg("-stutterwalk"), out float sw) ? sw : 30f;
            DemoMode.PlaceFacing(game, t[1], t[Mathf.Min(t.Count - 1, 4)]);
            DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
            for (int i = 0; i < 30; i++) yield return null;   // the camera settles behind the ant

            var frames = new List<(float dt, bool gc, int made, int shown, float x, float z)>();
            int gc0 = System.GC.CollectionCount(0), made0 = SolidThings.ShellsMade, shown0 = SolidThings.ShellsShown;
            int next = 1;
            float time = 0;
            while (time < seconds)
            {
                // crawl toward a point a little way ahead on the way (at its end, stop)
                Vector2 pos = DemoMode.Pos(game);
                while (next < t.Count - 1 && (t[next] - pos).sqrMagnitude < 2.5f * 2.5f) next++;
                Vector2 d = t[next] - pos;
                game.demoMove = d.sqrMagnitude < 0.5f ? Vector2.zero : d.normalized;
                yield return null;
                float dt = Time.unscaledDeltaTime;
                time += dt;
                int gc = System.GC.CollectionCount(0);
                frames.Add((dt, gc != gc0, SolidThings.ShellsMade - made0, SolidThings.ShellsShown - shown0, pos.x, pos.y));
                gc0 = gc;
                made0 = SolidThings.ShellsMade;
                shown0 = SolidThings.ShellsShown;
            }
            game.demoMove = Vector2.zero;

            // the spread, and how even: a frame that differs from the one before by more than 4 ms shows as a hitch in
            // anything moving (the eye reads the uneven steps, most of all in small bright things on a dark ground)
            var ms = new List<float>();
            foreach (var f in frames) ms.Add(f.dt * 1000f);
            var sorted = new List<float>(ms);
            sorted.Sort();
            float P(float q) => sorted[Mathf.Clamp(Mathf.RoundToInt(q * (sorted.Count - 1)), 0, sorted.Count - 1)];
            float sum = 0;
            foreach (float m in ms) sum += m;
            int uneven = 0, over25 = 0, over40 = 0;
            for (int i = 1; i < ms.Count; i++) if (Mathf.Abs(ms[i] - ms[i - 1]) > 4f) uneven++;
            foreach (float m in ms) { if (m > 25f) over25++; if (m > 40f) over40++; }
            var report = new StringBuilder();
            double hz = Screen.currentResolution.refreshRateRatio.value;
            report.AppendLine($"theme {(NestTheme.Current ?? NestTheme.Cyan).id}, {Screen.width}x{Screen.height} at {hz:F0} Hz, vSyncCount {QualitySettings.vSyncCount}, targetFrameRate {Application.targetFrameRate}, quality {game.Preset.name}");
            report.AppendLine($"{ms.Count} frames in {sum / 1000f:F1} s: {ms.Count / (sum / 1000f):F1} fps; frame ms p50 {P(0.5f):F1}, p90 {P(0.9f):F1}, p99 {P(0.99f):F1}, max {sorted[sorted.Count - 1]:F1}");
            report.AppendLine($"uneven (more than 4 ms from the frame before): {uneven} ({100f * uneven / Mathf.Max(1, ms.Count - 1):F1}%); over 25 ms: {over25}; over 40 ms: {over40}");
            int gcs = 0, made = 0, shown = 0;
            foreach (var f in frames) { if (f.gc) gcs++; made += f.made; shown += f.shown; }
            report.AppendLine($"garbage collections {gcs}, shells made {made}, swapped in {shown}");
            report.AppendLine("long frames (over 25 ms), with what came with them:");
            float at = 0;
            for (int i = 0; i < frames.Count; i++)
            {
                at += frames[i].dt;
                var f = frames[i];
                if (f.dt * 1000f <= 25f) continue;
                report.AppendLine($"  {at:F2}s frame {i}: {f.dt * 1000f:F1} ms{(f.gc ? ", gc" : "")}{(f.made > 0 ? ", shell made" : "")}{(f.shown > 0 ? ", shell swapped" : "")} at ({f.x:F1}, {f.z:F1})");
            }
            File.WriteAllText(Path.Combine(dir, "stutter_report.txt"), report.ToString());
            var csv = new StringBuilder("frame,ms,gc,shell_made,shell_shown,x,z\n");
            for (int i = 0; i < frames.Count; i++)
                csv.AppendLine($"{i},{frames[i].dt * 1000f:F2},{(frames[i].gc ? 1 : 0)},{frames[i].made},{frames[i].shown},{frames[i].x:F2},{frames[i].z:F2}");
            File.WriteAllText(Path.Combine(dir, "frames.csv"), csv.ToString());
            Debug.Log("[AntGarden] stuttercheck\n" + report);
            Application.Quit();
        }
    }
}

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
        static IEnumerator Grab(string dir, string name, int quality = 88)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, name + ".jpg"), tex.EncodeToJPG(quality));
            Object.Destroy(tex);
        }

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
            var shell = new List<(float upload, bool busy, float edge)>();   // (11-03) the fine shell's upload, its worker, its far edge
            int gc0 = System.GC.CollectionCount(0), made0 = SolidThings.ShellsMade, shown0 = SolidThings.ShellsShown;
            int next = 1;
            float time = 0, worstEdge = 99f;
            bool sprint = Game.Arg("-stuttersprint") != null, stills = Game.Arg("-stutterstill") != null;
            game.demoFast = sprint;   // (-stuttersprint: run, to see whether the fine splats' far edge keeps up)
            // (-handover, 11-04: at three places along the way the same view four ways, for the coarse / fine handover: as it
            // is, the plants coarse everywhere, the ground and walls drawn everywhere as they are far off, nothing brightened
            // round the ant; -stutterclip N: every sixth frame of the first N seconds, as the ant walks)
            bool handover = Game.Arg("-handover") != null;
            float clipFor = float.TryParse(Game.Arg("-stutterclip"), out float cf) ? cf : 0f;
            int stop = 0, clipFrame = 0;
            float[] stops = { 7f, 15f, 23f };
            while (time < seconds)
            {
                if (handover && stop < stops.Length && time >= stops[stop])
                {
                    stop++;
                    game.demoMove = Vector2.zero;
                    for (float w = 0; w < 1.2f || (SolidThings.Instance != null && !SolidThings.Instance.ShellReady() && w < 4f); w += Time.unscaledDeltaTime) yield return null;
                    for (int f = 0; f < 8; f++) yield return null;
                    yield return Grab(dir, $"h{stop}_a_as_is");
                    ParticleLook.DebugShellNoPlants = true; SolidThings.DebugPlantsCoarse = true;
                    SolidThings.Instance?.DebugRebuild();
                    for (int f = 0; f < 4; f++) yield return null;
                    yield return Grab(dir, $"h{stop}_b_plants_coarse");
                    ParticleLook.DebugShellNoPlants = false; SolidThings.DebugPlantsCoarse = false;
                    ParticleLook.DebugGroundFar = true; ParticleLook.DebugShellNoWalls = true;
                    SolidThings.Instance?.DebugRebuild();
                    for (int f = 0; f < 4; f++) yield return null;
                    yield return Grab(dir, $"h{stop}_c_ground_far");
                    ParticleLook.DebugGroundFar = false; ParticleLook.DebugShellNoWalls = false;
                    SolidThings.Instance?.DebugRebuild();
                    SolidThings.DebugNoLift = true;
                    if (WalkLight.Instance != null) WalkLight.Instance.enabled = false;
                    Shader.SetGlobalFloat("_WalkLit", 0f);
                    for (int f = 0; f < 4; f++) yield return null;
                    yield return Grab(dir, $"h{stop}_d_no_lift");
                    SolidThings.DebugNoLift = false;
                    if (WalkLight.Instance != null) WalkLight.Instance.enabled = true;
                    for (int f = 0; f < 4; f++) yield return null;
                }
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
                shell.Add((SolidThings.ShellUploadMs, SolidThings.ShellBusy, SolidThings.ShellEdge));
                if (clipFor > 0 && time <= clipFor && frames.Count % 6 == 0) yield return Grab(dir, $"clip_{clipFrame++:000}", 60);
                // (-stutterstill: a still looking ahead whenever the far edge is the shortest yet, and one at 20 s; the
                // frames they cost are not counted, so use it apart from the frame-time runs)
                if (stills && time > 3f && (SolidThings.ShellEdge < worstEdge - 0.05f || (time >= 20f && time - dt < 20f)))
                {
                    bool worst = SolidThings.ShellEdge < worstEdge - 0.05f;
                    if (worst) worstEdge = SolidThings.ShellEdge;
                    yield return new WaitForEndOfFrame();
                    Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, worst ? "edge_worst.jpg" : "ahead_20s.jpg"), tex.EncodeToJPG(88));
                    Object.Destroy(tex);
                }
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
            // with the screen on, most frames sit right at the cap (76-97 % on 10-27); with the screen locked or dark
            // the system paces the game itself (20-23 %, at 56-57 fps however much is drawn), and the times say nothing
            if (Application.targetFrameRate > 0)
            {
                float cap = 1000f / Application.targetFrameRate;
                int atCap = 0;
                foreach (float m in ms) if (Mathf.Abs(m - cap) < 0.15f) atCap++;
                report.AppendLine($"at the cap ({cap:F1} ms, within 0.15): {100f * atCap / Mathf.Max(1, ms.Count):F0}% of frames; focused {Application.isFocused}");
            }
            int gcs = 0, made = 0, shown = 0;
            foreach (var f in frames) { if (f.gc) gcs++; made += f.made; shown += f.shown; }
            report.AppendLine($"garbage collections {gcs}, shells made {made}, swapped in {shown}");
            // (11-03) the long frames by what ran in them: the shell's upload, its worker, neither; how even the rest are;
            // and the fine splats' far edge (how far past the ant's it still reached; below 0 the plants there thinned)
            int longUp = 0, longBusy = 0, longNone = 0, upFrames = 0, busyFrames = 0, behind = 0;
            float upMax = 0, upSum = 0, edgeMin = 99f;
            double mean = sum / Mathf.Max(1, ms.Count), var = 0;
            for (int i = 0; i < ms.Count; i++)
            {
                var sh = shell[i];
                var += (ms[i] - mean) * (ms[i] - mean);
                if (sh.upload > 0) { upFrames++; upSum += sh.upload; upMax = Mathf.Max(upMax, sh.upload); }
                if (sh.busy) busyFrames++;
                if (i > 30) { edgeMin = Mathf.Min(edgeMin, sh.edge); if (sh.edge < 0) behind++; }
                if (ms[i] <= 25f) continue;
                if (sh.upload > 0 || (i > 0 && shell[i - 1].upload > 0)) longUp++;
                else if (sh.busy) longBusy++;
                else longNone++;
            }
            int near = 0;
            foreach (float m in ms) if (Mathf.Abs(m - 1000f / 60f) < 1f) near++;
            report.AppendLine($"frame ms spread: sd {System.Math.Sqrt(var / Mathf.Max(1, ms.Count)):F2}, p5 {P(0.05f):F1}, p95 {P(0.95f):F1}; within 1 ms of 16.7: {100f * near / Mathf.Max(1, ms.Count):F0}%");
            report.AppendLine($"long frames with the shell's upload (or right after it) {longUp}, with its worker {longBusy}, with neither {longNone}");
            report.AppendLine($"shell upload: {upFrames} frames, {upSum / Mathf.Max(1, upFrames):F2} ms a frame on average, most {upMax:F2} ms; worker busy {busyFrames} frames");
            report.AppendLine($"fine splats' far edge past the ant's: at least {edgeMin:F2} units; behind it (below 0) in {behind} frames{(sprint ? " (running)" : "")}");
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
            var csv = new StringBuilder("frame,ms,gc,shell_made,shell_shown,x,z,upload_ms,worker,edge\n");
            for (int i = 0; i < frames.Count; i++)
                csv.AppendLine($"{i},{frames[i].dt * 1000f:F2},{(frames[i].gc ? 1 : 0)},{frames[i].made},{frames[i].shown},{frames[i].x:F2},{frames[i].z:F2},{shell[i].upload:F2},{(shell[i].busy ? 1 : 0)},{shell[i].edge:F2}");
            File.WriteAllText(Path.Combine(dir, "frames.csv"), csv.ToString());
            Debug.Log("[AntGarden] stuttercheck\n" + report);
            Application.Quit();
        }
    }
}

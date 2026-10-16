using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Leg check (-legcheck &lt;dir&gt; [-legdt 60|30|jitter]): the player's ant walks straight, zigzags on tapped
    /// keys, pivots round, starts and stops, pushes into a wall, sprints while steering and stands in a crowd,
    /// at a steady 60 or 30 fps or with uneven frame times like a busy PC. Every frame it measures how the
    /// procedural legs move: the step rate, the shortest swing, how fast the leg bones turn, how hard the
    /// visible feet jerk, how much planted feet slip, and how fast the body clip runs. Writes leg_report.json
    /// and a few frames, then quits.
    /// </summary>
    public static class LegCheck
    {
        class Seg
        {
            public string name;
            public float seconds;
            public int frames, swings, jerks;
            public float minSwing = 1e9f, cadenceMax, boneMax, jerkMax, slipMax;
            public double cadenceSum, slipSum, boneSum;
            public readonly List<float> bone = new List<float>(), jerk = new List<float>();
        }

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            game.demo = true;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            string mode = Game.Arg("-legdt") ?? "60";
            // -legframes 1: also film every frame from a close camera beside the ant, for a video
            bool film = Game.Arg("-legframes") == "1";
            string frameDir = Path.Combine(dir, "frames");
            if (film) Directory.CreateDirectory(frameDir);
            var labels = new StringBuilder();
            int frameNo = 0;
            Vector3 camPos = Vector3.zero;
            bool camPlaced = false;
            var rng = new System.Random(5);
            Time.captureDeltaTime = 1 / 60f;
            yield return new WaitForSeconds(1.5f);
            AntController ant = game.player;
            LegRig rig = ant.Legs;   // null with -proclegs 0: the clip drives the legs, measured from the bones
            var femurs = new Transform[6];
            var tibias = new Transform[6];
            var tarsi = new Transform[6];
            string[] pairs = { "front", "mid", "hind" };
            foreach (Transform t in ant.GetComponentsInChildren<Transform>(true))
                for (int i = 0; i < 6; i++)
                {
                    string side = i % 2 == 0 ? "L" : "R", pair = pairs[i / 2];
                    if (t.name == $"leg_{pair}_femur.{side}") femurs[i] = t;
                    if (t.name == $"leg_{pair}_tibia.{side}") tibias[i] = t;
                    if (t.name == $"leg_{pair}_tarsus.{side}") tarsi[i] = t;
                }
            if (femurs[0] == null || tibias[0] == null || tarsi[0] == null) { File.WriteAllText(Path.Combine(dir, "leg_report.json"), "{ \"error\": \"no leg bones\" }\n"); Application.Quit(); yield break; }
            WEdge e = DemoMode.LongEdge(game, 0);
            int i0 = Mathf.Clamp(e.pts.Count / 3, 0, e.pts.Count - 4);
            var segs = new List<Seg>();
            float bl = ant.cfg.bodyLength;
            string climbReport = "";

            IEnumerator Do(string name, float seconds, System.Func<float, (Vector2 keys, bool fast)> input)
            {
                var s = new Seg { name = name, seconds = seconds };
                segs.Add(s);
                var prevF = new Quaternion[6];
                var prevT = new Quaternion[6];
                var tip1 = new Vector3[6];
                var tip2 = new Vector3[6];
                var swingStart = new float[6];
                var wasSwing = new bool[6];
                // a step already in the air when the segment starts is not timed (its start was not seen)
                for (int i = 0; i < 6; i++) { wasSwing[i] = rig != null && rig.Swinging(i); swingStart[i] = -1; }
                float lastPhase = rig != null ? rig.Phase : 0, t = 0;
                int f = 0;
                while (t < seconds)
                {
                    Time.captureDeltaTime = mode == "30" ? 1 / 30f : mode == "jitter" ? (float)(1 / 90.0 + rng.NextDouble() * (1 / 22.0 - 1 / 90.0)) : 1 / 60f;
                    var (keys, fast) = input(t);
                    game.demoKeys = keys;
                    game.demoFast = fast;
                    yield return null;
                    float dt = Time.deltaTime;
                    t += dt;
                    float cad = rig != null ? Mathf.Repeat(rig.Phase - lastPhase, 1f) / Mathf.Max(dt, 1e-4f) : 0;
                    lastPhase = rig != null ? rig.Phase : 0;
                    for (int i = 0; i < 6; i++)
                    {
                        Quaternion qf = femurs[i].localRotation, qt = tibias[i].localRotation;
                        Vector3 tip = rig != null ? rig.Tip(i) : tarsi[i].position;
                        bool sw = rig != null ? rig.Swinging(i) : tip.y > game.world.WalkHeight(tip.x, tip.z) + 0.06f * bl;
                        if (f >= 2)
                        {
                            float w = Mathf.Max(Quaternion.Angle(prevF[i], qf), Quaternion.Angle(prevT[i], qt)) / dt;
                            s.bone.Add(w);
                            s.boneSum += w;
                            s.boneMax = Mathf.Max(s.boneMax, w);
                            // how hard the visible foot changes speed (body lengths / s²)
                            float j = (tip - 2 * tip1[i] + tip2[i]).magnitude / (dt * dt) / bl;
                            s.jerk.Add(j);
                            s.jerkMax = Mathf.Max(s.jerkMax, j);
                            if (j > 400f) s.jerks++;
                            if (!sw && !wasSwing[i])
                            {
                                float slip = new Vector2(tip.x - tip1[i].x, tip.z - tip1[i].z).magnitude / dt / bl;
                                s.slipSum += slip;
                                s.slipMax = Mathf.Max(s.slipMax, slip);
                            }
                        }
                        if (sw && !wasSwing[i]) { s.swings++; swingStart[i] = t; }
                        if (!sw && wasSwing[i] && swingStart[i] >= 0) s.minSwing = Mathf.Min(s.minSwing, t - swingStart[i]);
                        wasSwing[i] = sw;
                        prevF[i] = qf;
                        prevT[i] = qt;
                        tip2[i] = tip1[i];
                        tip1[i] = tip;
                    }
                    if (f >= 2) { s.frames++; s.cadenceSum += cad; s.cadenceMax = Mathf.Max(s.cadenceMax, cad); }
                    if (film && name != "settle")
                    {
                        // a close camera beside the ant, a little in front, easing after it so it never jerks itself
                        Camera cam = game.rig.cam;
                        game.rig.manual = true;
                        Vector3 c = ant.Position + Vector3.up * 0.22f * bl;
                        float ang = ant.heading + 1.9f;
                        Vector3 want = c + new Vector3(Mathf.Sin(ang), 0, Mathf.Cos(ang)) * 2.1f * bl + Vector3.up * 0.75f * bl;
                        camPos = camPlaced ? Vector3.Lerp(camPos, want, 1 - Mathf.Exp(-4f * dt)) : want;
                        camPlaced = true;
                        cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(c - camPos));
                        yield return new WaitForEndOfFrame();
                        Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(frameDir, $"f{frameNo:00000}.jpg"), shot.EncodeToJPG(88));
                        Object.Destroy(shot);
                        labels.Append(frameNo).Append(' ').Append(name).Append('\n');
                        frameNo++;
                    }
                    else if (f == 40)
                    {
                        yield return new WaitForEndOfFrame();
                        Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                        File.WriteAllBytes(Path.Combine(dir, $"{name}.jpg"), tex.EncodeToJPG(85));
                        Object.Destroy(tex);
                    }
                    f++;
                }
                if (!film)
                {
                    yield return new WaitForEndOfFrame();
                    Texture2D end = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, $"{name}_end.jpg"), end.EncodeToJPG(85));
                    Object.Destroy(end);
                }
                game.demoKeys = Vector2.zero;
                game.demoFast = false;
            }

            void Start(int at)
            {
                DemoMode.PlaceFacing(game, e.pts[at], e.pts[Mathf.Min(e.pts.Count - 1, at + 3)]);
                DemoMode.Shot(game, 2.6f, 0.25f);
            }
            Vector2 none = Vector2.zero, w = new Vector2(0, 1);
            Start(i0);
            yield return Do("settle", 1f, _ => (none, false));
            yield return Do("straight_walk", 4f, _ => (w, false));
            yield return Do("tap_steer", 6f, t => { float c = t % 1.6f; return (c < 0.25f ? new Vector2(1, 1) : c > 0.8f && c < 1.05f ? new Vector2(-1, 1) : w, false); });
            yield return Do("sprint_steer", 5f, t => { float c = t % 1.6f; return (c < 0.25f ? new Vector2(1, 1) : c > 0.8f && c < 1.05f ? new Vector2(-1, 1) : w, true); });
            yield return Do("start_stop", 5.6f, t => ((t % 1.4f) < 0.7f ? w : none, false));
            // turning round from a standstill: back (S) and forth (W), with a pause between
            yield return Do("pivot", 4.4f, t => { float c = t % 2.2f; return (c < 0.6f ? new Vector2(0, -1) : c > 1.1f && c < 1.7f ? w : none, false); });
            // into the canyon wall: face the nearest edge and keep pushing
            Vector2 p = new Vector2(ant.Position.x, ant.Position.z);
            Vector2 g = game.world.GradDist(p.x, p.y);
            ant.heading = Mathf.Atan2(g.x, g.y);
            game.rig.SnapBehind(ant.heading);
            yield return Do("wall_push", 4f, _ => (w, false));
            yield return Do("idle", 3f, _ => (none, false));
            // up the bank as far as the invisible wall, across the slope, and back down to the floor: from a stretch of
            // canyon edge with no plants in the way uphill
            if (ClearSlope(game, out Vector2 foot, out Vector2 uphill))
            {
                ant.Place(foot.x, foot.y, Mathf.Atan2(uphill.x, uphill.y));
                game.rig.SnapBehind(ant.heading);
                for (int k = 0; k < 20; k++) yield return null;
            }
            else game.rig.SnapBehind(ant.heading);
            float h0 = ant.Position.y;
            {
                Vector3 a0 = ant.Position;
                var g0 = new Vector2(Mathf.Sin(ant.heading), Mathf.Cos(ant.heading));
                var dbg = new StringBuilder("[AntGarden] climb probe:");
                for (float t = 0; t <= 5f; t += 1f)
                {
                    float x = a0.x + g0.x * t, z = a0.z + g0.y * t;
                    dbg.Append($" t{t:F0} path {game.world.PathDist(x, z):F2} dist {game.world.Dist(x, z):F2} climb {game.world.ClimbDist(x, z, ant.cfg.climb):F2} h {game.world.Height(x, z):F2} hex {game.world.HexDist(x, z):F2};");
                }
                Debug.Log(dbg.ToString());
            }
            float logT = 0;
            yield return Do("climb_up", 7f, t =>
            {
                if (t >= logT) { logT += 0.5f; Debug.Log($"[AntGarden] climb t{t:F1} pos ({ant.Position.x:F2},{ant.Position.y:F2},{ant.Position.z:F2}) speed {ant.speed:F2} heading {ant.heading:F2} path {game.world.PathDist(ant.Position.x, ant.Position.z):F2} climbDist {game.world.ClimbDist(ant.Position.x, ant.Position.z, ant.cfg.climb):F2}"); }
                return (w, false);
            });
            climbReport = $"\"climb\": {{ \"bankDistReached\": {game.world.PathDist(ant.Position.x, ant.Position.z):F2}, \"allowed\": {ant.cfg.climb:F2}, \"heightGained\": {ant.Position.y - h0:F2} }},\n";
            yield return Do("along_slope", 4f, _ => (new Vector2(1, 0), false));
            yield return Do("climb_down", 4f, _ => (new Vector2(0, -1), false));
            Time.captureDeltaTime = 0;

            var sb = new StringBuilder();
            sb.Append($"{{\n  \"dt\": \"{mode}\",\n  {climbReport}  \"segments\": [\n");
            for (int k = 0; k < segs.Count; k++)
            {
                Seg s = segs[k];
                s.bone.Sort();
                s.jerk.Sort();
                float P(List<float> l, float q) => l.Count == 0 ? 0 : l[Mathf.Min(l.Count - 1, (int)(q * l.Count))];
                float secs = Mathf.Max(0.01f, s.seconds);
                sb.Append($"    {{ \"name\": \"{s.name}\", \"stepsPerSecPerLeg\": {s.swings / 6f / secs:F2}, \"minSwingSec\": {(s.minSwing > 1e8f ? 0 : s.minSwing):F3},")
                  .Append($" \"cadenceMean\": {s.cadenceSum / Mathf.Max(1, s.frames):F2}, \"cadenceMax\": {s.cadenceMax:F2},")
                  .Append($" \"boneDegPerSecP95\": {P(s.bone, 0.95f):F0}, \"boneDegPerSecMax\": {s.boneMax:F0},")
                  .Append($" \"footJerkP95\": {P(s.jerk, 0.95f):F0}, \"footJerkMax\": {s.jerkMax:F0}, \"jerkFrames\": {s.jerks},")
                  .Append($" \"plantedSlipMean\": {s.slipSum / Mathf.Max(1, s.frames * 3):F3}, \"plantedSlipMax\": {s.slipMax:F2} }}")
                  .Append(k < segs.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "leg_report.json"), sb.ToString());
            if (film) File.WriteAllText(Path.Combine(dir, "frames.txt"), labels.ToString());
            Debug.Log("[AntGarden] legcheck " + sb.ToString().Replace("\n", " "));
            Application.Quit();
        }

        /// <summary>A point just inside the canyon edge with nothing growing for five units uphill, and that direction.</summary>
        static bool ClearSlope(Game game, out Vector2 foot, out Vector2 uphill)
        {
            World world = game.world;
            foreach (WEdge e in world.edges)
            {
                if (e.region != 0) continue;
                for (int i = 2; i < e.pts.Count - 2; i += 3)
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Vector2 a = e.pts[Mathf.Max(0, i - 1)], b = e.pts[Mathf.Min(e.pts.Count - 1, i + 1)];
                        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * side;
                        // walk out from the path to just short of the edge
                        Vector2 p = e.pts[i];
                        for (int k = 0; k < 120 && world.Dist(p.x, p.y) < -0.7f; k++) p += n * 0.2f;
                        if (world.Dist(p.x, p.y) > -0.4f) p -= n * 0.3f;
                        Vector2 g = world.GradDist(p.x, p.y).normalized;
                        float edge = world.Dist(p.x, p.y);
                        bool clear = edge < -0.3f && edge > -1.2f;   // right at the foot of the bank
                        for (float t = 0; t < 5f && clear; t += 0.4f)
                            if (world.colliders.Overlaps(p.x + g.x * t, p.y + g.y * t, 0.6f)) clear = false;
                        if (!clear) continue;
                        foot = p;
                        uphill = g;
                        return true;
                    }
            }
            foot = uphill = Vector2.zero;
            return false;
        }
    }
}

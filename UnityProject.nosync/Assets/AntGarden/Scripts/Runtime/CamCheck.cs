using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Camera check (-camcheck &lt;dir&gt;): a keyboard "player" runs every region of the current world the way a
    /// person would (W held and A/D tapped to follow the canyon, then the same with Shift), and in the first
    /// region also holds D alone, backs up on S, lets go of a mouse drag mid-run and stands still. Every frame the
    /// camera is measured against the ant: how fast its offset from the ant changes, how hard that offset jolts,
    /// how often the boom is pulled in, how much the ant jumps on screen and what shortened the boom. Writes
    /// cam_report.json and a frame every two seconds, then quits. -camstyle classic measures the previous camera.
    /// </summary>
    public static class CamCheck
    {
        const int Fps = 30;

        class Seg
        {
            public string name;
            public int frames, jolts, pullIns, flips;
            public double offPath, yawPath, bobPath, screenPath, travelled, antTurn, boomSum;
            public float accMax, screenJumpMax, boomMin = 1e9f;
            public readonly List<float> acc = new List<float>();
            public readonly Dictionary<string, int> blocked = new Dictionary<string, int>();
        }

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            game.demo = true;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            Time.captureFramerate = Fps;   // a fixed clock: the same run however fast the machine is
            yield return new WaitForSeconds(1.5f);
            var segs = new List<Seg>();
            for (int r = 0; r < game.world.regions.Count; r++)
            {
                List<Vector2> route = Route(game.world, r, 220f);
                if (route == null) continue;
                Vector2 d0 = route[1] - route[0];
                game.player.Place(route[0].x, route[0].y, Mathf.Atan2(d0.x, d0.y));
                game.rig.SnapBehind(game.player.heading);
                for (int i = 0; i < 20; i++) yield return null;
                var bot = new Bot(route);
                yield return Measure(game, dir, segs, $"r{r}_walk", 18f, () => bot.Keys(game), false);
                yield return Measure(game, dir, segs, $"r{r}_sprint", 14f, () => bot.Keys(game), true);
                // W alone, no steering: the canyon bends and the ant runs along its wall
                yield return Measure(game, dir, segs, $"r{r}_W_only", 6f, () => new Vector2(0, 1), false);
                if (r > 0) continue;
                // the moves that used to swing the camera: D alone, S, and a drag let go of while walking
                yield return Measure(game, dir, segs, "hold_WD", 4f, () => new Vector2(1, 1), false);
                yield return Measure(game, dir, segs, "hold_D", 5f, () => new Vector2(1, 0), false);
                yield return Measure(game, dir, segs, "hold_S", 3f, () => new Vector2(0, -1), false);
                yield return Measure(game, dir, segs, "idle", 2f, () => Vector2.zero, false);
                bot.Resync(game);
                game.rig.Nudge(1.1f);
                yield return Measure(game, dir, segs, "drag_then_W", 5f, () => new Vector2(0, 1), false);
            }
            game.demoKeys = null;

            var sb = new StringBuilder();
            sb.Append("{\n  \"style\": \"").Append(game.rig.classic ? "classic" : "follow").Append("\",\n  \"segments\": [\n");
            for (int i = 0; i < segs.Count; i++)
            {
                Seg s = segs[i];
                float secs = s.frames / (float)Fps;
                s.acc.Sort();
                float P(float q) => s.acc.Count == 0 ? 0 : s.acc[Mathf.Min(s.acc.Count - 1, (int)(q * s.acc.Count))];
                var why = new List<string>();
                foreach (var kv in s.blocked) why.Add($"{kv.Key} {kv.Value * 100f / Mathf.Max(1, s.frames):F0}%");
                sb.Append($"    {{ \"name\": \"{s.name}\", \"seconds\": {secs:F1}, \"travelled\": {s.travelled:F1},")
                  .Append($" \"offsetSpeed\": {s.offPath / secs:F3}, \"yawDegPerSec\": {s.yawPath / secs * Mathf.Rad2Deg:F1},")
                  .Append($" \"yawFlips\": {s.flips}, \"bobPerSec\": {s.bobPath / secs:F3},")
                  .Append($" \"offsetAccP50\": {P(0.5f):F2}, \"offsetAccP95\": {P(0.95f):F2}, \"offsetAccMax\": {s.accMax:F1}, \"jolts\": {s.jolts},")
                  .Append($" \"pullIns\": {s.pullIns}, \"boomMean\": {s.boomSum / Mathf.Max(1, s.frames):F2}, \"boomMin\": {s.boomMin:F2},")
                  .Append($" \"screenDriftPerSec\": {s.screenPath / secs:F3}, \"screenJumpMax\": {s.screenJumpMax:F3},")
                  .Append($" \"antTurnDegPerSec\": {s.antTurn / secs * Mathf.Rad2Deg:F1}, \"blockedBy\": \"{string.Join(", ", why)}\" }}")
                  .Append(i < segs.Count - 1 ? ",\n" : "\n");
            }
            sb.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "cam_report.json"), sb.ToString());
            Debug.Log("[AntGarden] camcheck " + sb.ToString().Replace("\n", " "));
            Application.Quit();
        }

        /// <summary>Hold the given keys for a while and measure the camera every frame.</summary>
        static IEnumerator Measure(Game game, string dir, List<Seg> segs, string name, float seconds, System.Func<Vector2> keys, bool fast)
        {
            var s = new Seg { name = name };
            segs.Add(s);
            CameraRig rig = game.rig;
            Camera cam = rig.cam;
            Vector3 off1 = Vector3.zero, off2 = Vector3.zero;
            Vector2 lastScreen = Vector2.zero, lastAnt = Vector2.zero;
            float lastYaw = 0, lastYawRate = 0, lastHeading = game.player.heading;
            var boomHist = new Queue<float>();
            int cooldown = 0, n = (int)(seconds * Fps), skip = name.StartsWith("drag") ? 12 : 2;
            for (int f = 0; f < n; f++)
            {
                game.demoKeys = keys();
                game.demoFast = fast;
                yield return null;   // the game moves the ant and the camera
                Vector3 ant = game.player.Position;
                Vector3 off = cam.transform.position - ant;
                Vector3 vp = cam.WorldToViewportPoint(ant + Vector3.up * 0.2f);
                var screen = new Vector2(vp.x, vp.y);
                var ant2 = new Vector2(ant.x, ant.z);
                Vector3 fw = cam.transform.forward;
                float yaw = Mathf.Atan2(fw.x, fw.z);
                float boom = off.magnitude;
                if (f >= skip)
                {
                    s.frames++;
                    s.offPath += (off - off1).magnitude;
                    s.bobPath += Mathf.Abs(off.y - off1.y);
                    s.screenPath += (screen - lastScreen).magnitude;
                    s.screenJumpMax = Mathf.Max(s.screenJumpMax, (screen - lastScreen).magnitude);
                    s.travelled += (ant2 - lastAnt).magnitude;
                    s.antTurn += Mathf.Abs(Mathf.DeltaAngle(lastHeading * Mathf.Rad2Deg, game.player.heading * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    float dy = Mathf.DeltaAngle(lastYaw * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                    s.yawPath += Mathf.Abs(dy);
                    float rate = dy * Fps;
                    // the camera turning one way, then quickly the other: a wobble
                    if (Mathf.Abs(rate) > 0.35f && Mathf.Abs(lastYawRate) > 0.35f && Mathf.Sign(rate) != Mathf.Sign(lastYawRate)) s.flips++;
                    if (Mathf.Abs(rate) > 0.35f) lastYawRate = rate;
                    // how hard the camera's offset from the ant changes speed (units / s²): a jolt above 25
                    float acc = (off - 2 * off1 + off2).magnitude * Fps * Fps;
                    s.acc.Add(acc);
                    s.accMax = Mathf.Max(s.accMax, acc);
                    if (acc > 25f) s.jolts++;
                    s.boomSum += boom;
                    s.boomMin = Mathf.Min(s.boomMin, boom);
                    // the boom pulled in by more than half a unit within a third of a second
                    boomHist.Enqueue(boom);
                    while (boomHist.Count > 10) boomHist.Dequeue();
                    float peak = 0;
                    foreach (float b in boomHist) peak = Mathf.Max(peak, b);
                    if (cooldown > 0) cooldown--;
                    else if (peak - boom > 0.5f) { s.pullIns++; cooldown = 15; }
                    if (!string.IsNullOrEmpty(rig.blockedBy)) s.blocked[rig.blockedBy] = s.blocked.TryGetValue(rig.blockedBy, out int c) ? c + 1 : 1;
                }
                off2 = off1;
                off1 = off;
                lastScreen = screen;
                lastAnt = ant2;
                lastYaw = yaw;
                lastHeading = game.player.heading;
                if (f % (2 * Fps) == Fps)
                {
                    yield return new WaitForEndOfFrame();
                    Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, $"{name}_{f / Fps:00}.jpg"), tex.EncodeToJPG(85));
                    Object.Destroy(tex);
                }
            }
            game.demoKeys = Vector2.zero;
        }

        /// <summary>
        /// A long walk along the region's canyons from the start of its longest path, preferring paths not yet
        /// walked and never turning into a dead end when there is another way.
        /// </summary>
        static List<Vector2> Route(World world, int region, float length)
        {
            var adj = new Dictionary<string, List<WEdge>>();
            WEdge first = null;
            foreach (WEdge e in world.edges)
            {
                if (e.region != region || e.pts == null || e.pts.Count < 2) continue;
                if (!adj.TryGetValue(e.source, out var a)) adj[e.source] = a = new List<WEdge>();
                if (!adj.TryGetValue(e.target, out var b)) adj[e.target] = b = new List<WEdge>();
                a.Add(e);
                b.Add(e);
                // start on the longest real canyon (the straight connectors between stitched nests test nothing)
                if (!e.connector && (first == null || e.pts.Count > first.pts.Count)) first = e;
            }
            if (first == null) return null;
            var route = new List<Vector2>(first.pts);
            var used = new HashSet<WEdge> { first };
            string at = first.target;
            float total = 0;
            for (int i = 1; i < route.Count; i++) total += (route[i] - route[i - 1]).magnitude;
            var rng = new System.Random(7 + region);
            for (int guard = 0; total < length && guard < 400; guard++)
            {
                List<WEdge> opts = adj[at];
                WEdge pick = null;
                int Score(WEdge e)
                {
                    string end = e.source == at ? e.target : e.source;
                    return (used.Contains(e) ? 0 : 4) + (adj[end].Count > 1 ? 2 : 0) + (e.connector ? 0 : 1);
                }
                foreach (WEdge e in opts)
                    if (pick == null || Score(e) > Score(pick) || (Score(e) == Score(pick) && rng.NextDouble() < 0.5)) pick = e;
                used.Add(pick);
                bool fwd = pick.source == at;
                for (int i = 1; i < pick.pts.Count; i++)
                {
                    Vector2 q = fwd ? pick.pts[i] : pick.pts[pick.pts.Count - 1 - i];
                    total += (q - route[route.Count - 1]).magnitude;
                    route.Add(q);
                }
                at = fwd ? pick.target : pick.source;
            }
            return route;
        }

        /// <summary>
        /// Steers like a person on a keyboard: looks a little way down the route and holds whichever of the eight
        /// W/A/S/D combinations points closest to it relative to the camera, re-deciding five times a second and
        /// only changing keys once the way ahead is clearly off the current ones.
        /// </summary>
        class Bot
        {
            readonly List<Vector2> route;
            int idx, sector;
            float wait;

            public Bot(List<Vector2> route) { this.route = route; }

            public void Resync(Game game)
            {
                Vector2 p = new Vector2(game.player.Position.x, game.player.Position.z);
                float best = 1e9f;
                for (int i = 0; i < route.Count; i++)
                {
                    float d = (route[i] - p).sqrMagnitude;
                    if (d < best) { best = d; idx = i; }
                }
            }

            public Vector2 Keys(Game game)
            {
                Vector2 p = new Vector2(game.player.Position.x, game.player.Position.z);
                float best = (route[idx] - p).sqrMagnitude;
                for (int i = idx + 1; i < Mathf.Min(route.Count, idx + 40); i++)
                {
                    float d = (route[i] - p).sqrMagnitude;
                    if (d < best) { best = d; idx = i; }
                }
                // a point 1.6 units further along the route
                float need = 1.6f;
                int j = idx;
                Vector2 aim = route[j];
                while (j < route.Count - 1 && need > 0)
                {
                    float l = (route[j + 1] - route[j]).magnitude;
                    if (l >= need) { aim = route[j] + (route[j + 1] - route[j]) * (need / l); need = 0; }
                    else { need -= l; j++; aim = route[j]; }
                }
                Vector2 want = aim - p;
                var (fwd, right) = game.rig.Basis();
                float a = Mathf.Atan2(Vector2.Dot(want, right), Vector2.Dot(want, fwd)) * Mathf.Rad2Deg;
                wait -= Time.deltaTime;
                if (wait <= 0)
                {
                    wait = 0.2f;
                    if (Mathf.Abs(Mathf.DeltaAngle(a, sector * 45f)) > 30f) sector = Mathf.RoundToInt(a / 45f);
                }
                float s = sector * 45f * Mathf.Deg2Rad;
                float x = Mathf.Sin(s), z = Mathf.Cos(s);
                return new Vector2(Mathf.Abs(x) > 0.3f ? Mathf.Sign(x) : 0, Mathf.Abs(z) > 0.3f ? Mathf.Sign(z) : 0);
            }
        }
    }
}

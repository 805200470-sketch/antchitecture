using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Film only (-storyfilm &lt;dir&gt;, 11-04): shots for the presentation video ("ANTCHITECTURE", storyboard III and IV), each
    /// frame-exact at 30 a second (Time.captureFramerate) into &lt;dir&gt;/&lt;shot&gt;_&lt;nest&gt;/f00000.jpg, at 1920×1080 whatever the
    /// window (FilmScreen), for Tools/frames_to_video.sh. The shots, in the order -storyplan lists them (a comma list; a step
    /// that takes the ant into another nest ends this world's steps, the next ones are taken there):
    ///   map      the ant in its nest, the big map opening on the nest, then one step further the honeycomb's ring (S33);
    ///            then stills of both steps at 3840×2160, as in the game and clean (no title, no hints)
    ///   aerial   the nest's real paths from high above at a slant, its guiding light shown along all its way, a slow turn (S33)
    ///   crane    straight down on the whole nest, north (its line drawing's top) up, easing down to behind the ant standing on
    ///            the light at the way in, then the ant walks along the light (S28); with a still of the plan (3840×2160 and
    ///            the clip's first frame) and plan.json: where the line drawing's corners lie on that frame
    ///   walk     the ant walks along the light from the way in, the normal camera (B-roll)
    ///   depart   the ant walks the last stretch of the light to the way out and through its light gate (S31a); the next nest
    ///            loads, where the next step should be arrive
    ///   arrive   the ant comes out of the light gate it came through and walks in, seen from inside the nest (S31a)
    ///   goto:&lt;world&gt;   on to that nest by the map's ring, no film
    /// The screen's words are hidden unless a step ends in +ui (map: always). Nothing here runs in play.
    /// </summary>
    public static class StoryFilm
    {
        const int Fps = DemoMode.Fps;
        const float Lookahead = 2.5f;
        static List<string> plan;
        static int next;
        static string root;
        static float baseExposure = float.NaN;
        static bool stuck;   // a gate or the map did not take the ant on: the run ends there
        static readonly WaitForEndOfFrame end = new WaitForEndOfFrame();

        public static IEnumerator Run(Game game, string dir)
        {
            root = dir;
            Directory.CreateDirectory(dir);
            if (plan == null)
            {
                plan = new List<string>();
                foreach (string s in (Game.Arg("-storyplan") ?? "walk").Split(','))
                    if (!string.IsNullOrWhiteSpace(s)) plan.Add(s.Trim());
                next = 0;
            }
            Begin(game);
            baseExposure = float.NaN;
            yield return Settle(3f);   // shaders compile, the ants spread out
            while (next < plan.Count)
            {
                string step = plan[next++];
                bool ui = step.EndsWith("+ui");
                string name = ui ? step.Substring(0, step.Length - 3) : step;
                Debug.Log($"[AntGarden] story film: {name}{(ui ? " with its words" : "")} in {Nest()} ({next} of {plan.Count})");
                // (a step that leaves this nest: when the nest is left this coroutine ends with its world, and Run starts again
                // in the next; only when the gate or the map did not take the ant on does it come back here)
                if (name == "depart") { yield return Depart(game, ui); if (stuck) break; continue; }
                if (name.StartsWith("goto:")) { yield return Travel(game, name.Substring(5)); break; }
                if (game.LabNest == null || game.LabNest.Track == null || game.LabNest.Track.Count < 4) { Debug.LogWarning("[AntGarden] story film: no glowing way here"); continue; }
                if (name == "map") yield return Map(game);
                else if (name == "nestmap") yield return NestMap(game);
                else if (name == "aerial") yield return Aerial(game, ui);
                else if (name == "crane") yield return Crane(game, ui);
                else if (name == "walk") yield return Walk(game, ui);
                else if (name == "arrive") yield return Arrive(game, ui);
                else Debug.LogWarning("[AntGarden] story film: no step " + name);
            }
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Debug.Log("[AntGarden] story film: done");
            Time.captureFramerate = 0;
            Application.Quit();
        }

        /// <summary>Film time from now on (30 a second, every frame), the game's input off, the screen's words held back.</summary>
        internal static void Begin(Game game)
        {
            Application.targetFrameRate = -1;
            Time.captureFramerate = Fps;
            game.demo = true;
            game.demoKeys = null;
            game.demoMove = Vector2.zero;
            game.demoFast = false;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            if (game.LabNest != null) game.LabNest.Hold = true;   // (no chapter card over the shots)
            FilmScreen.On(game);
        }

        /// <summary>Some film time passes, not recorded.</summary>
        internal static IEnumerator Settle(float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                Application.targetFrameRate = -1;   // (Game drops it when the window is covered: not while filming)
                yield return null;
            }
        }

        static string Nest()
        {
            string s = Game.Selection ?? "";
            return s.Contains("@") ? s.Substring(s.LastIndexOf('@') + 1).Split('#')[0] : s.Replace(":", "_");
        }

        static string Sub(string shot)
        {
            string d = Path.Combine(root, shot + "_" + Nest());
            for (int k = 2; Directory.Exists(d) && File.Exists(Path.Combine(d, "f00000.jpg")); k++) d = Path.Combine(root, $"{shot}_{Nest()}_{k}");
            Directory.CreateDirectory(d);
            return d;
        }

        internal static string Frame(string sub, int k) => Path.Combine(sub, $"f{k:00000}.jpg");

        // ---------------------------------------------------------------- the way

        internal static int PointAlong(List<Vector2> pts, float dist)
        {
            float acc = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += (pts[i] - pts[i - 1]).magnitude;
                if (acc >= dist) return i;
            }
            return pts.Count - 1;
        }

        static float Length(List<Vector2> pts)
        {
            float l = 0;
            for (int i = 1; i < pts.Count; i++) l += (pts[i] - pts[i - 1]).magnitude;
            return l;
        }

        /// <summary>The ant on the glowing way this far along it, facing along it, the camera behind it as in play.</summary>
        static void StandOnWay(Game game, float along)
        {
            List<Vector2> t = game.LabNest.Track;
            int a = PointAlong(t, along), b = PointAlong(t, along + 4.5f);
            if (b <= a) b = Mathf.Min(t.Count - 1, a + 1);
            DemoMode.PlaceFacing(game, t[a], t[b]);
            game.rig.manual = false;
            DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
            game.demoMove = Vector2.zero;
        }

        /// <summary>Walks the ant toward a point a little way ahead on the way (stops at its end).</summary>
        internal static void Steer(Game game, List<Vector2> t, ref int i, float pace = 1f)
        {
            Vector2 pos = DemoMode.Pos(game);
            while (i < t.Count - 1 && (t[i] - pos).sqrMagnitude < Lookahead * Lookahead) i++;
            Vector2 d = t[i] - pos;
            game.demoMove = d.sqrMagnitude < 0.5f ? Vector2.zero : d.normalized * pace;
        }

        /// <summary>The plants standing within reach of at that rise over an ant: their foot, the radius of their box and
        /// their height (as placed: the particle look bends and stretches each a little, hence the margins in Blocked).</summary>
        internal static List<(Vector3 foot, float r, float h)> TallNear(Game game, Vector3 at, float within)
        {
            var list = new List<(Vector3, float, float)>();
            foreach (PlantInstance pi in game.world.plantInstances)
            {
                if (pi.t == null || pi.plant == null || pi.far) continue;
                Vector3 p = pi.t.position;
                if (new Vector2(p.x - at.x, p.z - at.z).sqrMagnitude > within * within) continue;
                float sc = pi.t.lossyScale.y, h = sc * Mathf.Max(0.05f, pi.plant.size.y);
                if (h < 1.2f) continue;
                list.Add((p, 0.5f * sc * Mathf.Max(pi.plant.size.x, pi.plant.size.z), h));
            }
            return list;
        }

        /// <summary>How badly a camera at cam looking at target is placed: inside a plant's box (1), or below the ground there
        /// (0.6); its line of sight through a plant's upper part or into a bank (0.25 each).</summary>
        internal static float Blocked(Game game, Vector3 cam, Vector3 target, List<(Vector3 foot, float r, float h)> tall)
        {
            float bad = 0f;
            if (cam.y < game.world.Height(cam.x, cam.z) + 0.25f) bad += 0.6f;
            foreach (var (foot, r, h) in tall)
            {
                float flat = new Vector2(cam.x - foot.x, cam.z - foot.z).magnitude;
                if (flat < r * 1.15f + 0.4f && cam.y > foot.y - 0.3f && cam.y < foot.y + h * 1.25f + 0.4f) { bad += 1f; break; }
            }
            bool seen = false, ground = false;
            for (int k = 1; k < 10; k++)
            {
                Vector3 q = Vector3.Lerp(cam, target, k / 10f);
                if (!ground && q.y < game.world.Height(q.x, q.z) - 0.05f) { bad += 0.25f; ground = true; }
                if (seen) continue;
                foreach (var (foot, r, h) in tall)
                    if (new Vector2(q.x - foot.x, q.z - foot.z).magnitude < r && q.y > foot.y + 0.45f * h && q.y < foot.y + h * 1.1f) { bad += 0.25f; seen = true; break; }
            }
            return bad;
        }

        static readonly List<GameObject> offLens = new List<GameObject>();

        /// <summary>The other ants that come right up to the lens step out of the picture (a flying one filled the frame),
        /// and back once clear of it.</summary>
        internal static void AntsOffLens(Game game)
        {
            Vector3 c = game.cam.transform.position;
            foreach (NpcAnt n in game.npcs)
            {
                GameObject go = n.ant.gameObject;
                float d = Vector3.Distance(n.ant.Position + Vector3.up * 0.3f, c);
                if (go.activeSelf && d < 2.2f) { go.SetActive(false); offLens.Add(go); }
                else if (!go.activeSelf && d > 3.2f && offLens.Remove(go)) go.SetActive(true);
            }
        }

        internal static void AntsBack()
        {
            foreach (GameObject go in offLens) if (go != null) go.SetActive(true);
            offLens.Clear();
        }

        /// <summary>The floor's height along the main way (where the line drawing's lines lie).</summary>
        static float FloorHeight(Game game)
        {
            List<Vector2> t = game.LabNest.Track;
            float sum = 0;
            int n = 0;
            for (int i = 0; i < t.Count; i += 3) { sum += game.world.Height(t[i].x, t[i].y); n++; }
            return n > 0 ? sum / n : 0f;
        }

        static float Smoother(float x)
        {
            x = Mathf.Clamp01(x);
            return x * x * x * (x * (6f * x - 15f) + 10f);
        }

        /// <summary>The night lifted this many stops above the nest's own (as SceneShots' view from above: from far off a
        /// night scene's shape does not read).</summary>
        static void Lift(float stops)
        {
            var volume = Object.FindObjectOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            if (volume == null || volume.profile == null || !volume.profile.TryGetSettings(out UnityEngine.Rendering.PostProcessing.ColorGrading grading)) return;
            if (float.IsNaN(baseExposure)) baseExposure = grading.postExposure.value;
            grading.postExposure.Override(baseExposure + stops);
        }

        // ---------------------------------------------------------------- the shots

        /// <summary>B-roll: the ant walks along the light from the way in, 18 s, the camera as in play.</summary>
        static IEnumerator Walk(Game game, bool ui)
        {
            string sub = Sub("B_walk");
            StandOnWay(game, 0.8f);
            FilmScreen.Ui(game, ui);
            yield return Settle(1.5f);
            yield return SceneShots.ShellSettles();
            yield return Settle(0.3f);
            List<Vector2> t = game.LabNest.Track;
            int i = PointAlong(t, 0.8f);
            for (int k = 0, n = 18 * Fps; k < n; k++)
            {
                Steer(game, t, ref i);
                AntsOffLens(game);
                yield return end;
                FilmScreen.Save(Frame(sub, k));
                yield return null;
            }
            AntsBack();
            game.demoMove = Vector2.zero;
            Debug.Log($"[AntGarden] story film: walk {Nest()} {18 * Fps} frames");
        }

        /// <summary>
        /// S28: straight down on the whole nest as its line drawing lies (north up: the world's z is the drawing's up), easing
        /// down over 9 s to behind the ant standing on the light at the way in, where the camera is as in play; then the ant
        /// walks along the light for 3.6 s. The camera's distance eases in its logarithm (an even zoom), the point it looks at
        /// comes onto the ant as the camera nears it, and it turns from looking down to looking along the way only once low.
        /// </summary>
        static IEnumerator Crane(Game game, bool ui)
        {
            string sub = Sub("S28_crane");
            Region reg = game.world.regions[0];
            StandOnWay(game, 0.8f);
            FilmScreen.Ui(game, ui);
            yield return Settle(2f);   // the camera settles behind the ant
            yield return SceneShots.ShellSettles();
            yield return Settle(0.5f);
            Camera cam = game.cam;
            Vector3 endPos = cam.transform.position;
            Quaternion endRot = cam.transform.rotation;
            Vector3 antTarget = game.player.Position + Vector3.up * game.cfg.camera.height;
            Vector3 endLook = endPos + endRot * Vector3.forward * Vector3.Distance(endPos, antTarget);
            Vector3 endOff = endPos - endLook;
            float endDist = endOff.magnitude, endPitch = Mathf.Asin(Mathf.Clamp(endOff.y / endDist, -1f, 1f)), endYaw = Mathf.Atan2(endOff.x, endOff.z);
            float floorY = FloorHeight(game);
            var centre = new Vector3(reg.offset.x, floorY, reg.offset.y);   // (the hexagon's middle is the drawing's)
            float R = (float)reg.field.R;
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float high = 1.05f * R / tanHalf;   // the pointy-top hexagon, 2R tall, top to bottom in the frame
            float far = cam.farClipPlane;
            cam.farClipPlane = Mathf.Max(far, high * 3f);

            // straight down, the plan turning so the ant's way points up the screen, then from well up already tilting toward
            // the ant, so the camera comes in low from a side and sees the ant under whatever stands over it (straight down
            // onto blue's way in it looked through a big glowing bulb standing right over the ant), and ends where the camera
            // is in play
            float tilt = Mathf.Min(high * 0.4f, 55f);
            float lHigh = Mathf.Log(high), lTilt = Mathf.Log(tilt), lEnd = Mathf.Log(endDist);
            float side = 0f;   // the turn round the ant from behind it that the tilt starts from (chosen below)
            (Vector3, Quaternion) Pose(float s)
            {
                float ld = Mathf.Lerp(lHigh, lEnd, s), dist = Mathf.Exp(ld);
                Vector3 look = Vector3.Lerp(centre, endLook, Smoother(Mathf.InverseLerp(high, tilt * 0.9f, dist)));
                float yawDeg = ld > lTilt
                    ? Mathf.LerpAngle(180f, endYaw * Mathf.Rad2Deg + side, Smoother(Mathf.InverseLerp(lHigh - 0.1f * (lHigh - lTilt), lTilt, ld)))
                    : endYaw * Mathf.Rad2Deg + side * (1f - Smoother(Mathf.InverseLerp(lTilt, lEnd + 0.15f * (lTilt - lEnd), ld)));
                float yaw = yawDeg * Mathf.Deg2Rad;
                float x = Mathf.InverseLerp(lTilt, lEnd, ld);
                float pitch = Mathf.Lerp(Mathf.PI / 2f, endPitch, 1f - Mathf.Pow(1f - x, 2.5f));
                var horiz = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                Vector3 pos = look + (horiz * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * dist;
                float ground = game.world.Height(pos.x, pos.z) + 0.3f;
                if (s < 1f && pos.y < ground) pos.y = ground;
                Vector3 up = Vector3.up * Mathf.Cos(pitch) - horiz * Mathf.Sin(pitch);
                return (pos, Quaternion.LookRotation(look - pos, up));
            }

            // how it comes down: of two tilts (late: straight down to about 18 above the ant, then the arc, as long a plan as
            // can be; early: from well up) and the turns round the ant tried, the one whose way down keeps clearest of the
            // plants standing there (inside one, its splats filled the frame; through one, it hid the ant: blue's way in has
            // a big glowing bulb right over it) and of the banks; the late tilt, and nearest behind the ant, where it is as clear
            var tall = TallNear(game, game.player.Position, tilt + 6f);
            float bestBlock = float.MaxValue, bestSide = 0f, bestTilt = tilt;
            foreach (float tryTilt in new[] { Mathf.Max(18f, endDist * 4f), tilt })
                foreach (float turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f, 125f, -125f, 160f, -160f })
                {
                    side = turn;
                    tilt = tryTilt;
                    lTilt = Mathf.Log(tryTilt);
                    float from = (lHigh - Mathf.Log(Mathf.Min(high * 0.9f, tryTilt * 2.5f))) / (lHigh - lEnd);
                    float block = Mathf.Abs(turn) * 0.002f + (tryTilt > 20f ? 0.6f : 0f);
                    for (int q = 0; q <= 80; q++)
                    {
                        var (pq, _) = Pose(Mathf.Lerp(from, 0.995f, q / 80f));
                        block += Blocked(game, pq, endLook, tall);
                    }
                    if (block < bestBlock) { bestBlock = block; bestSide = turn; bestTilt = tryTilt; }
                }
            side = bestSide;
            tilt = bestTilt;
            lTilt = Mathf.Log(tilt);
            float lMid = lTilt;
            Debug.Log($"[AntGarden] story film: crane {Nest()} tilts from {tilt:F0} up, comes down {side:F0}° round from behind the ant ({tall.Count} plants near, blocked {bestBlock:F2})");
            game.rig.manual = true;
            var (p0, r0) = Pose(0f);
            cam.transform.SetPositionAndRotation(p0, r0);
            Lift(0.8f);
            // the plan, large, and where the line drawing lies on the clip's first frame
            yield return FilmScreen.Still(Path.Combine(root, $"S28_plan_{Nest()}_4k.png"), 2);
            cam.transform.SetPositionAndRotation(p0, r0);
            yield return null;
            WritePlan(game, reg, cam, floorY);
            int craneN = 9 * Fps, holdN = Mathf.RoundToInt(0.4f * Fps), walkN = Mathf.RoundToInt(3.6f * Fps);
            List<Vector2> t = game.LabNest.Track;
            int i = PointAlong(t, 0.8f);
            float lensAtEnd = game.rig.lensClear, lClose = Mathf.Log(6f);
            for (int k = 0, n = craneN + holdN + walkN; k < n; k++)
            {
                if (k <= craneN)
                {
                    float s = Smoother(k / (float)craneN);
                    var (p, r) = Pose(s);
                    cam.transform.SetPositionAndRotation(p, r);
                    float ld = Mathf.Lerp(lHigh, lEnd, s);
                    // the night lifted while high (as SceneShots' view from above), the nest's own by mid-way down
                    Lift(0.8f * Smoother(Mathf.InverseLerp(lMid, lHigh, ld)));
                    // low among the plants, what stands right at the lens clears (as the steady camera does in play), handing
                    // over to the camera's own at the end
                    float lens = ld > lMid ? 0f : ld > lClose ? Smoother(Mathf.InverseLerp(lMid, lMid - 0.4f, ld)) : Mathf.Lerp(1f, lensAtEnd, Smoother(Mathf.InverseLerp(lClose, lEnd, ld)));
                    Shader.SetGlobalVector("_LensClear", new Vector4(lens, 0f, 0f, 0f));
                }
                if (k == craneN + holdN) { cam.transform.SetPositionAndRotation(endPos, endRot); game.rig.manual = false; }   // the camera as in play from here
                if (k >= craneN + holdN) Steer(game, t, ref i);
                AntsOffLens(game);
                yield return end;
                FilmScreen.Save(Frame(sub, k));
                if (k == 0) FilmScreen.SavePng(Path.Combine(root, $"S28_plan_{Nest()}_1080.png"));
                yield return null;
            }
            AntsBack();
            game.demoMove = Vector2.zero;
            Lift(0f);
            cam.farClipPlane = far;
            game.rig.manual = false;
            Debug.Log($"[AntGarden] story film: crane {Nest()} from {high:F0} up (R {R:F1}) to {endDist:F1} behind the ant, {craneN + holdN + walkN} frames");
        }

        /// <summary>Where the line drawing (its corners, in its own pixels) lies on the plan's frame (1920×1080, from the top
        /// left), the floor's height; a drawing turned in the game (path-011~m: mirrored) is said so.</summary>
        static void WritePlan(Game game, Region reg, Camera cam, float floorY)
        {
            var f = reg.field;
            float s = (float)f.Scale, W = (float)f.ImageWidth, H = (float)f.ImageHeight;
            string id = reg.ids != null && reg.ids.Length > 0 ? reg.ids[0] : "";
            string how = id.Contains("~") ? id.Substring(id.IndexOf('~') + 1) : "";
            bool fx = how.Contains("m") || how.Contains("r"), fy = how.Contains("v") || how.Contains("r");
            Vector2 Screen(float px, float py)
            {
                // the drawing's pixel, where the game laid it (mirrored as its id says), on the floor
                float lx = fx ? W - px : px, ly = fy ? H - py : py;
                var w = new Vector3((lx - W / 2f) * s + reg.offset.x, floorY, -((ly - H / 2f) * s) + reg.offset.y);
                Vector3 sp = cam.WorldToScreenPoint(w);
                return new Vector2(sp.x, FilmScreen.H - sp.y);
            }
            Vector2 a = Screen(0, 0), b = Screen(W, 0), c = Screen(0, H), d = Screen(W, H);
            string F(Vector2 v) => $"[{v.x:F2}, {v.y:F2}]";
            File.WriteAllText(Path.Combine(root, $"S28_plan_{Nest()}.json"),
                $"{{\n  \"nest\": \"{Nest()}\",\n  \"path\": \"{id}\",\n  \"mirrored\": {(fx ? "true" : "false")},\n  \"flipped\": {(fy ? "true" : "false")},\n"
                + $"  \"image\": [{W:F0}, {H:F0}],\n  \"frame\": [{FilmScreen.W}, {FilmScreen.H}],\n  \"floor\": {floorY:F3},\n"
                + $"  \"corners\": {{ \"top_left\": {F(a)}, \"top_right\": {F(b)}, \"bottom_left\": {F(c)}, \"bottom_right\": {F(d)} }}\n}}\n");
        }

        /// <summary>
        /// S33: the nest from high above at a slant, the whole of its glowing way lit (the guiding thread shows all the way
        /// from the way in instead of only ahead of the ant: GuideThreads' reach), its light running along it toward the way
        /// out; a slow turn, a little closer as it turns, 10 s.
        /// </summary>
        static IEnumerator Aerial(Game game, bool ui)
        {
            string sub = Sub("S33_aerial");
            Region reg = game.world.regions[0];
            StandOnWay(game, 0.8f);
            FilmScreen.Ui(game, ui);
            var threads = Object.FindObjectOfType<GuideThreads>();
            float reach0 = threads != null ? threads.reach : 0f, beam0 = threads != null ? threads.beamReach : 0f;
            if (threads != null) { threads.reach = 4000f; threads.beamReach = 4000f; }
            yield return Settle(1.5f);
            yield return SceneShots.ShellSettles();
            Camera cam = game.cam;
            float floorY = FloorHeight(game), R = (float)reg.field.R;
            var centre = new Vector3(reg.offset.x, floorY, reg.offset.y);
            float tanHalf = Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            float dist0 = 1.12f * R / tanHalf, dist1 = dist0 * 0.88f;
            float far = cam.farClipPlane;
            cam.farClipPlane = Mathf.Max(far, dist0 * 3f);
            game.rig.manual = true;
            Lift(0.8f);
            for (int k = 0, n = 10 * Fps; k < n; k++)
            {
                float u = k / (float)(n - 1);
                float yaw = (180f - 14f + 28f * u) * Mathf.Deg2Rad, pitch = Mathf.Lerp(64f, 58f, u) * Mathf.Deg2Rad;
                float dist = Mathf.Lerp(dist0, dist1, u);   // (even: easy to cut anywhere)
                var horiz = new Vector3(Mathf.Sin(yaw), 0f, Mathf.Cos(yaw));
                Vector3 pos = centre + (horiz * Mathf.Cos(pitch) + Vector3.up * Mathf.Sin(pitch)) * dist;
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(centre - pos, Vector3.up * Mathf.Cos(pitch) - horiz * Mathf.Sin(pitch)));
                yield return end;
                FilmScreen.Save(Frame(sub, k));
                if (k == 0) FilmScreen.SavePng(Path.Combine(root, $"S33_aerial_{Nest()}_first.png"));
                yield return null;
            }
            Lift(0f);
            cam.farClipPlane = far;
            game.rig.manual = false;
            if (threads != null) { threads.reach = reach0; threads.beamReach = beam0; }
            Debug.Log($"[AntGarden] story film: aerial {Nest()} from {dist0:F0} (R {R:F1})");
        }

        /// <summary>
        /// S33: the ant in its nest, the big map opening on the nest (its first step), then one step further the honeycomb's
        /// ring with the six nests' own canyons; as a player sees it (the map's words), 11 s. Then stills of both steps at
        /// 3840×2160, as in the game and clean (no title, no hints).
        /// </summary>
        static IEnumerator Map(Game game)
        {
            string sub = Sub("S33_map");
            for (float t = 0; HoneyMap.Shared != null && !HoneyMap.Shared.Done && t < 40f; t += Time.unscaledDeltaTime) yield return null;
            for (int f = 0; !game.bigmap.HoneyShown && f < 120; f++) yield return null;   // (the map is built again once its picture is drawn)
            StandOnWay(game, 0.8f);
            FilmScreen.Ui(game, true);
            yield return Settle(2f);
            yield return SceneShots.ShellSettles();
            int k = 0;
            int open = Mathf.RoundToInt(1.2f * Fps), ring = open + Mathf.RoundToInt(3.8f * Fps), last = ring + Mathf.RoundToInt(6f * Fps);
            for (; k < last; k++)
            {
                if (k == open) game.bigmap.Toggle(true);
                if (k == ring) game.bigmap.ShowNests();
                FilmScreen.OnlyWords(game, "BigMap", "Minimap");
                yield return end;
                FilmScreen.Save(Frame(sub, k));
                yield return null;
            }
            // the stills: the ring, then the nest, each as in the game and clean
            yield return Settle(0.5f);
            yield return MapStill(game, $"S33_map_ring_{Nest()}");
            game.bigmap.Back();
            yield return Settle(1f);
            yield return MapStill(game, $"S28_map_nest_{Nest()}");
            game.bigmap.Toggle(false);
            yield return Settle(0.5f);
            FilmScreen.Ui(game, false);
            Debug.Log($"[AntGarden] story film: map {Nest()} {k} frames");
        }

        /// <summary>The nest's own map (the big map's first step, the nest whole) as stills: 3840×2160, as in the game and
        /// clean (no title, no hints).</summary>
        static IEnumerator NestMap(Game game)
        {
            for (float t = 0; HoneyMap.Shared != null && !HoneyMap.Shared.Done && t < 40f; t += Time.unscaledDeltaTime) yield return null;
            for (int f = 0; !game.bigmap.HoneyShown && f < 120; f++) yield return null;
            StandOnWay(game, 0.8f);
            FilmScreen.Ui(game, true);
            yield return Settle(0.5f);
            game.bigmap.Toggle(true);
            yield return Settle(1f);
            yield return MapStill(game, $"S28_map_nest_{Nest()}");
            game.bigmap.Toggle(false);
            yield return Settle(0.4f);
            FilmScreen.Ui(game, false);
        }

        static IEnumerator MapStill(Game game, string name)
        {
            FilmScreen.OnlyWords(game, "BigMap");
            yield return FilmScreen.Still(Path.Combine(root, name + "_game_4k.png"), 2, () => FilmScreen.OnlyWords(game, "BigMap"));
            yield return FilmScreen.Still(Path.Combine(root, name + "_clean_4k.png"), 2, () =>
            {
                FilmScreen.OnlyWords(game, "BigMap");
                foreach (Transform tr in game.hud.canvas.GetComponentsInChildren<Transform>(true))
                    if (tr.name == "Hints" || tr.name == "Title" || tr.name == "Note" || tr.name == "PadCursor") tr.gameObject.SetActive(false);
            });
            foreach (Transform tr in game.hud.canvas.GetComponentsInChildren<Transform>(true))
                if (tr.name == "Hints" || tr.name == "Title" || tr.name == "Note") tr.gameObject.SetActive(true);
        }

        /// <summary>
        /// S31a, leaving: from 9 units short of the end of the light, the ant walks the last of it to the way out and on
        /// through its light gate, the camera as in play; a second after it is in the gate's light the clip ends and the gate
        /// takes it on (the next nest loads; the load is cut from the film).
        /// </summary>
        static IEnumerator Depart(Game game, bool ui)
        {
            if (game.LabNest == null || game.LabNest.Track == null) yield break;
            string sub = Sub("S31a_depart");
            List<Vector2> t = game.LabNest.Track;
            float len = Length(t);
            float from = Mathf.Max(0.8f, len - 9f);
            StandOnWay(game, from);
            if (ui && game.LabNest != null)
            {
                // with the screen's words: the chapter begun (its first card comes and goes before the shot), so its last
                // card comes at the way out
                game.LabNest.Hold = false;
                FilmScreen.Ui(game, false);
                yield return Settle(8.5f);
            }
            FilmScreen.Ui(game, ui);
            yield return Settle(1.5f);
            yield return SceneShots.ShellSettles();
            Vector2 heart = game.LabNest.Heart;
            Gate gate = null;
            foreach (Gate g in game.gates)
                if (g.world != null && (gate == null || (new Vector2(g.position.x, g.position.z) - heart).sqrMagnitude < (new Vector2(gate.position.x, gate.position.z) - heart).sqrMagnitude)) gate = g;
            if (gate == null) { Debug.LogWarning("[AntGarden] story film: no gate at the way out"); yield break; }
            var gp = new Vector2(gate.position.x, gate.position.z);
            // (the gate stands where the canyon runs out of the nest's frame, and the floor ends a little short of it: the ant
            // walks on toward the gate's middle until it is at the gate as the game has it, E's reach, and gets no nearer)
            float reach = Mathf.Max(game.cfg.gates.portalRadius, gate.radius * 0.8f);
            int i = PointAlong(t, from);
            int k = 0, inLight = -1, max = 22 * Fps, stall = 0;
            float nearest = float.MaxValue;
            for (; k < max; k++)
            {
                Vector2 pos = DemoMode.Pos(game);
                float dg = (pos - gp).magnitude;
                bool onWay = i < t.Count - 1 || (t[t.Count - 1] - pos).sqrMagnitude > 1.5f * 1.5f;
                if (inLight >= 0 && stall > 6) game.demoMove = Vector2.zero;   // (at the floor's end: it stops, no turning on the spot)
                else if (onWay && dg > reach) Steer(game, t, ref i);
                else { Vector2 d = gp - pos; game.demoMove = d.sqrMagnitude < 0.04f ? Vector2.zero : d.normalized; }
                if (dg < nearest - 0.01f) { nearest = dg; stall = 0; } else stall++;
                if (inLight < 0 && dg < reach && (stall > 10 || dg < 1.0f || Vector2.Dot(pos - gp, gate.dir) > -0.2f)) inLight = k;
                AntsOffLens(game);
                yield return end;
                FilmScreen.Save(Frame(sub, k));
                yield return null;
                if (inLight >= 0 && k - inLight >= Fps) break;
            }
            AntsBack();
            game.demoMove = Vector2.zero;
            Debug.Log($"[AntGarden] story film: depart {Nest()} through the gate to {gate.world}, {k + 1} frames (in its light at {inLight})");
            File.WriteAllText(Path.Combine(sub, "gate.txt"), $"{gate.world}\n{inLight}\n");
            if (next >= plan.Count) yield break;   // (nothing to film there: the run ends here)
            // through the gate (as E does there; else as the map's ring does: the same way on, arriving at the same gate)
            Time.captureFramerate = 0;
            FilmScreen.Off(game);
            game.Interact();
            yield return new WaitForSecondsRealtime(1.5f);
            game.bigmap.ClickNest(gate.world);
            yield return new WaitForSecondsRealtime(5f);
            Debug.LogWarning("[AntGarden] story film: the gate did not take the ant on");
            stuck = true;
            Begin(game);
        }

        /// <summary>
        /// S31a, arriving: the ant just inside the light gate it came through, walking in along the way from it; seen from
        /// inside the nest, a few steps in and to the side, looking back at the gate and turning to follow the ant, 6 s.
        /// </summary>
        static IEnumerator Arrive(Game game, bool ui)
        {
            string sub = Sub("S31a_arrive");
            Vector2 at = DemoMode.Pos(game);
            Gate gate = null;
            foreach (Gate g in game.gates)
                if (g.world != null && (gate == null || (new Vector2(g.position.x, g.position.z) - at).sqrMagnitude < (new Vector2(gate.position.x, gate.position.z) - at).sqrMagnitude)) gate = g;
            if (gate == null) { Debug.LogWarning("[AntGarden] story film: no gate to arrive by"); yield break; }
            var gp = new Vector2(gate.position.x, gate.position.z);
            Vector2 inward = -gate.dir;
            // the way in from that gate: the glowing track that starts nearest it, else straight in
            List<Vector2> way = null;
            float best = 36f;
            foreach (List<Vector2> tr in game.LabNest.Tracks)
            {
                if (tr == null || tr.Count < 4) continue;
                float d = Mathf.Min((tr[0] - gp).sqrMagnitude, (tr[tr.Count - 1] - gp).sqrMagnitude);
                if (d < best) { best = d; way = (tr[0] - gp).sqrMagnitude <= (tr[tr.Count - 1] - gp).sqrMagnitude ? tr : Reversed(tr); }
            }
            if (way == null) way = new List<Vector2> { gp, gp + inward * 4f, gp + inward * 8f, gp + inward * 12f };
            // where the floor begins inside the gate (the gate stands at the frame, the floor ends short of it): the first
            // point of the way in on the open floor
            int j0 = 0;
            float in0 = 0f;
            for (int j = 0; j < way.Count; j++)
            {
                if (j > 0) in0 += (way[j] - way[j - 1]).magnitude;
                if (game.LabNest.OnFloor(way[j], 0.45f)) { j0 = j; break; }
            }
            Vector2 snapped = way[j0];
            DemoMode.PlaceFacing(game, snapped, way[Mathf.Min(way.Count - 1, PointAlong(way, in0 + 4f))]);
            game.rig.manual = false;
            DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
            FilmScreen.Ui(game, ui);
            // the camera: 6 further along the way in, a little up, to whichever side sees the gate and the ant clearest (in the
            // kiln a fire morel stood between), looking back at the gate
            Vector2 a = way[PointAlong(way, in0 + 6f)], b = way[PointAlong(way, in0 + 7f)];
            Vector2 along = (b - a).sqrMagnitude > 1e-4f ? (b - a).normalized : inward, sideV = new Vector2(-along.y, along.x);
            var gateAt = new Vector3(gp.x, game.world.Height(gp.x, gp.y) + 1.1f, gp.y);
            var antAt = new Vector3(snapped.x, game.world.Height(snapped.x, snapped.y) + 0.3f, snapped.y);
            var tallA = TallNear(game, new Vector3(a.x, 0f, a.y), 14f);
            float bestA = float.MaxValue;
            var camPos = new Vector3(a.x, game.world.Height(a.x, a.y) + 1.2f, a.y);
            foreach (float off in new[] { 1.6f, -1.6f, 2.6f, -2.6f, 0.8f, -0.8f })
            {
                Vector2 c2 = a + sideV * off;
                if (!game.LabNest.OnFloor(c2, 0.3f)) continue;
                var cp = new Vector3(c2.x, game.world.Height(c2.x, c2.y) + 1.2f, c2.y);
                float bl = Blocked(game, cp, gateAt, tallA) + Blocked(game, cp, antAt, tallA) + Mathf.Abs(off) * 0.05f;
                if (bl < bestA) { bestA = bl; camPos = cp; }
            }
            Camera cam = game.cam;
            game.rig.manual = true;
            Vector3 look = new Vector3(gp.x, game.world.Height(gp.x, gp.y) + 1.1f, gp.y), lookVel = Vector3.zero;
            cam.transform.SetPositionAndRotation(camPos, Quaternion.LookRotation(look - camPos));
            yield return Settle(1.2f);
            yield return SceneShots.ShellSettles();
            DemoMode.PlaceFacing(game, snapped, way[Mathf.Min(way.Count - 1, PointAlong(way, in0 + 4f))]);
            int i = j0;
            int k = 0;
            for (int n = 6 * Fps; k < n; k++)
            {
                if (k >= Mathf.RoundToInt(0.4f * Fps)) Steer(game, way, ref i, 0.9f);
                if (k > 3 * Fps && new Vector2(game.player.Position.x - camPos.x, game.player.Position.z - camPos.z).magnitude < 2.2f) break;   // (close enough: it walks past)
                // the look comes off the gate onto the ant as it walks out of the light
                Vector3 ant = game.player.Position + Vector3.up * 0.35f;
                Vector3 want = Vector3.Lerp(look, ant, Smoother(k / (1.2f * Fps)));
                look = Vector3.SmoothDamp(look, want, ref lookVel, 0.35f, Mathf.Infinity, 1f / Fps);
                Vector3 pos = Vector3.Lerp(camPos, camPos + new Vector3(along.x, 0f, along.y) * 0.8f, Smoother(k / (float)n));
                cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(look - pos, Vector3.up));
                AntsOffLens(game);
                yield return end;
                FilmScreen.Save(Frame(sub, k));
                yield return null;
            }
            AntsBack();
            game.demoMove = Vector2.zero;
            game.rig.manual = false;
            DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
            Debug.Log($"[AntGarden] story film: arrive {Nest()} by the gate to {gate.world}, {k} frames");
        }

        static List<Vector2> Reversed(List<Vector2> pts)
        {
            var r = new List<Vector2>(pts);
            r.Reverse();
            return r;
        }

        /// <summary>On to another nest by the map's ring (as clicking it there does).</summary>
        static IEnumerator Travel(Game game, string world)
        {
            for (float t = 0; HoneyMap.Shared != null && !HoneyMap.Shared.Done && t < 40f; t += Time.unscaledDeltaTime) yield return null;
            for (int f = 0; !game.bigmap.HoneyShown && f < 120; f++) yield return null;
            Time.captureFramerate = 0;
            FilmScreen.Off(game);
            game.bigmap.ClickNest(world);
            yield return new WaitForSecondsRealtime(5f);
            Debug.LogWarning("[AntGarden] story film: the map did not take the ant to " + world);
            stuck = true;
            Begin(game);
        }
    }

    /// <summary>
    /// Film only: the film's own screen, 1920×1080 whatever the window (the Mac's screen, 1728×1117 points, holds no 1920×1080
    /// window): the camera renders into a texture of that size, and the screen's words (the HUD canvas, made a camera's
    /// for the film) are drawn over it by a second camera into the same texture, at their own size there; each frame is read
    /// back from it. Stills at twice that.
    /// </summary>
    public static class FilmScreen
    {
        public const int W = 1920, H = 1080;
        const int UiLayer = 5;
        static RenderTexture rt;
        static Texture2D read;
        static Camera uiCam;
        static Canvas canvas;

        public static void On(Game game)
        {
            if (rt == null)
            {
                rt = new RenderTexture(W, H, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { name = "FilmScreen", antiAliasing = 1 };
                rt.Create();
            }
            if (read == null) read = new Texture2D(W, H, TextureFormat.RGB24, false);
            game.cam.targetTexture = rt;
            game.cam.ResetAspect();
            if (uiCam == null)
            {
                uiCam = new GameObject("FilmUiCamera").AddComponent<Camera>();
                uiCam.clearFlags = CameraClearFlags.Depth;
                uiCam.cullingMask = 1 << UiLayer;
                uiCam.orthographic = true;
                uiCam.nearClipPlane = 0.1f;
                uiCam.farClipPlane = 50f;
                uiCam.allowHDR = false;
                uiCam.allowMSAA = false;
                uiCam.transform.position = new Vector3(0f, -5000f, 0f);
            }
            uiCam.depth = game.cam.depth + 10f;
            uiCam.targetTexture = rt;
            canvas = game.hud.canvas;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = uiCam;
            canvas.planeDistance = 10f;
            Layer();
            canvas.enabled = false;
        }

        public static void Off(Game game)
        {
            if (game != null && game.cam != null) { game.cam.targetTexture = null; game.cam.ResetAspect(); }
            if (canvas != null) { canvas.renderMode = RenderMode.ScreenSpaceOverlay; canvas.enabled = true; }
        }

        static readonly List<GameObject> hidden = new List<GameObject>();

        /// <summary>The screen's words shown (as the game has them: any OnlyWords put away come back) or not.</summary>
        public static void Ui(Game game, bool on)
        {
            if (canvas != null) canvas.enabled = on;
            if (!on) return;
            foreach (GameObject go in hidden) if (go != null) go.SetActive(true);
            hidden.Clear();
        }

        /// <summary>Of the screen's words only these (the canvas's own children by name), every frame (the game shows its
        /// own again as it goes); Ui(true) brings the others back.</summary>
        public static void OnlyWords(Game game, params string[] keep)
        {
            if (canvas == null) return;
            canvas.enabled = true;
            foreach (Transform child in canvas.transform)
            {
                if (System.Array.IndexOf(keep, child.name) >= 0 || !child.gameObject.activeSelf) continue;
                child.gameObject.SetActive(false);
                if (!hidden.Contains(child.gameObject)) hidden.Add(child.gameObject);
            }
        }

        static void Layer()
        {
            if (canvas == null) return;
            foreach (Transform t in canvas.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = UiLayer;
        }

        /// <summary>This frame (call after WaitForEndOfFrame) as a JPEG.</summary>
        public static void Save(string path)
        {
            Grab(rt, read);
            File.WriteAllBytes(path, read.EncodeToJPG(95));
            Layer();   // (words made since, the visitors' cards: drawn from the next frame)
        }

        /// <summary>This frame as a PNG.</summary>
        public static void SavePng(string path)
        {
            Grab(rt, read);
            File.WriteAllBytes(path, read.EncodeToPNG());
        }

        static void Grab(RenderTexture from, Texture2D into)
        {
            RenderTexture before = RenderTexture.active;
            RenderTexture.active = from;
            into.ReadPixels(new Rect(0, 0, from.width, from.height), 0, 0, false);
            into.Apply(false);
            RenderTexture.active = before;
        }

        /// <summary>A still, the camera and the words rendered at scale times the film's size (one frame of film time).</summary>
        public static IEnumerator Still(string path, int scale, System.Action each = null)
        {
            Camera main = Camera.main;
            var big = new RenderTexture(W * scale, H * scale, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB) { antiAliasing = 1 };
            big.Create();
            if (main != null) { main.targetTexture = big; main.ResetAspect(); }
            if (uiCam != null) uiCam.targetTexture = big;
            Layer();
            each?.Invoke();
            yield return null;
            each?.Invoke();
            yield return new WaitForEndOfFrame();
            var tex = new Texture2D(W * scale, H * scale, TextureFormat.RGB24, false);
            Grab(big, tex);
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            if (main != null) { main.targetTexture = rt; main.ResetAspect(); }
            if (uiCam != null) uiCam.targetTexture = rt;
            big.Release();
            Object.Destroy(big);
            Debug.Log("[AntGarden] story film: still " + Path.GetFileName(path));
        }
    }
}

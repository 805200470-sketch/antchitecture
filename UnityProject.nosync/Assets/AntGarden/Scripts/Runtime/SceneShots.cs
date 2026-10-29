using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Review stills of every region in the current world (-sceneshots &lt;dir&gt;): playing it (the normal camera
    /// behind the walking ant, with the game's UI), the same spot through the ant's own eyes, the whole nest from
    /// high above, and its map. Spots are a canyon and a junction chamber, or in a ruin the way in (looking down the
    /// glowing specks) and the relics themselves.
    /// Then the game quits.
    /// </summary>
    public static class SceneShots
    {
        public static IEnumerator Run(Game game, string dir)
        {
            if (game.LabNest != null) { Directory.CreateDirectory(dir); LabMap.Write(game, dir); }   // the particle nest's measured plan
            Directory.CreateDirectory(dir);
            game.demo = true;
            game.hud.SetStatsVisible(false);
            game.hud.SetHelpVisible(false);
            yield return new WaitForSeconds(3f);   // shaders compile, the NPCs spread out
            // -shotregions 4,5: only these regions (a honeycomb has fifteen)
            var only = new HashSet<int>();
            foreach (string part in (Game.Arg("-shotregions") ?? "").Split(','))
                if (int.TryParse(part.Trim(), out int ri)) only.Add(ri);
            for (int r = 0; r < game.world.regions.Count; r++)
            {
                if (only.Count > 0 && !only.Contains(r)) continue;
                int k = 0;
                foreach (var (name, at, toward) in Spots(game, r))
                {
                    k++;
                    // walk in, so the ant is mid-stride and the camera has settled behind it
                    DemoMode.PlaceFacing(game, at, toward);
                    DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
                    game.demoKeys = new Vector2(0, 1);
                    for (float t = 0; t < 1.3f; t += Time.deltaTime) yield return null;
                    game.demoKeys = Vector2.zero;
                    yield return ShellSettles();
                    yield return Grab(Path.Combine(dir, $"r{r}_{k}_play_{name}.png"));
                    game.demoKeys = Vector2.zero;
                    // at a relic, look at the relic itself; elsewhere, wherever the view runs furthest
                    bool relic = name.StartsWith("relic") || name.StartsWith("temple") || name.StartsWith("amber") || name.StartsWith("lab_own_")
                        || name.StartsWith("lab_temple") || name.StartsWith("lab_relic") || name.StartsWith("lab_ember") || name.StartsWith("lab_char") || name == "lab_heart_close"
                        || name == "lab_morel";   // (the kiln's morels: looking down the way, its eye shot missed them, k6)
                    yield return AntEye(game, Path.Combine(dir, $"r{r}_{k}_eye_{name}.png"), relic ? toward : (Vector2?)null);
                    // a plant's close-up: a photograph of the plant itself too
                    if (PlantShots.Contains(name) || Tall.ContainsKey(name) || name.StartsWith("lab_temple") || name.StartsWith("lab_ember") || name.StartsWith("lab_char"))
                        yield return PlantPhoto(game, Path.Combine(dir, $"r{r}_{k}_photo_{name}.png"), at, toward, Tall.TryGetValue(name, out float tall) ? tall : -1);
                }
                // the player's ant close up, as a player sees it zoomed right in (is it made of points?)
                yield return AntClose(game, Path.Combine(dir, $"r{r}_8_ant_close.png"), 1.9f, 35f);
                yield return AntClose(game, Path.Combine(dir, $"r{r}_8_ant_side.png"), 2.8f, 80f);
                yield return Aerial(game, r, Path.Combine(dir, $"r{r}_9_aerial.png"));
            }
            var map = new MapPainter(game.world, game.gates, 1600);
            File.WriteAllBytes(Path.Combine(dir, "map.png"), map.texture.EncodeToPNG());
            File.WriteAllText(Path.Combine(dir, "done.txt"), game.world.label + "\n");
            Debug.Log("[AntGarden] sceneshots done");
            Application.Quit();
        }

        /// <summary>Where to stand: up to three relics in a ruin, otherwise a canyon and a junction chamber.</summary>
        static List<(string name, Vector2 at, Vector2 toward)> Spots(Game game, int r)
        {
            var spots = new List<(string, Vector2, Vector2)>();
            foreach (RuinScene ruin in game.Ruins)
            {
                if (ruin.region.index != r) continue;
                // just inside the way in, looking down the glowing track
                List<Vector2> track = ruin.TrailMain;
                if (track != null && track.Count > 4)
                {
                    int a = PointAlong(track, 4f), b = PointAlong(track, 13f);
                    if (b > a) spots.Add(("trail_entry", track[a], track[b]));
                }
                var kinds = new HashSet<string>();
                for (int i = 0; i < ruin.Count && spots.Count < 4; i++)
                {
                    if (!kinds.Add(ruin.IdOf(i))) continue;
                    Vector3 c = ruin.PosOf(i);
                    Vector2 target = new Vector2(c.x, c.z);
                    if (Approach(game, r, target, ruin.ReachOf(i) * 1.5f + 3.2f, out Vector2 at)) spots.Add((ruin.IdOf(i), at, target));
                }
                return spots;
            }
            if (game.LabNest != null && r == 0 && game.LabNest.Track != null && game.LabNest.Track.Count > 4)
            {
                // the particle nest: the way in, halfway, and the heart
                List<Vector2> t = game.LabNest.Track;
                int a0 = PointAlong(t, 3f), a1 = PointAlong(t, 12f);
                if (a1 > a0) spots.Add(("lab_way_in", t[a0], t[a1]));
                int m = t.Count / 2;
                if (m + 6 < t.Count) spots.Add(("lab_halfway", t[m], t[m + 6]));
                // (from the way in, as a player comes: Approach alone could stand behind a bank on another canyon)
                if (OnWayIn(game, 3.4f, out Vector2 at) || Approach(game, r, game.LabNest.Heart, 7f, out at)) spots.Add(("lab_heart", at, game.LabNest.Heart));
                // a curled plant and a glass-bulbed one close up, seen from the way (for judging the plants' detail)
                void CloseUp(string kind, string name, float back)
                {
                    // the one of its kind nearest the way (not on it), seen from the way's open floor: so the camera
                    // stands where the ant walks, with nothing between it and the plant
                    var cands = new List<(float dw, Vector2 plant, Vector2 onWay)>();
                    foreach (Placement pl in game.world.placements)
                    {
                        if (pl.plant != kind) continue;
                        var pp = new Vector2(pl.x, pl.z);
                        float nearest = float.MaxValue;
                        Vector2 q0 = pp;
                        foreach (Vector2 q in t) { float d = (q - pp).sqrMagnitude; if (d < nearest) { nearest = d; q0 = q; } }
                        float dw = Mathf.Sqrt(nearest);
                        // (one standing up a bank, seen from the way, is behind the bank's slope: tried last; one at a
                        // guiding beam's foot, in its vines and its pool of light, before that)
                        bool atBeam = false;
                        foreach (Vector2 b in GuideThreads.BeamFeet) atBeam |= (b - pp).sqrMagnitude < 2.5f * 2.5f;
                        // (and one whose foot is off the floor, at a wall's foot in its dark, read as hanging in the air:
                        // after those on the floor)
                        if (dw >= 0.9f) cands.Add((dw + (game.world.Dist(pp.x, pp.y) > 0.6f ? 1000f : 0f) + (atBeam ? 500f : 0f)
                            + (game.LabNest.OnFloor(pp, 0.3f) ? 0f : 200f), pp, q0));
                    }
                    cands.Sort((a, b) => a.dw.CompareTo(b.dw));
                    // from the way's side first, else turned round it a little at a time (a big plant among others)
                    foreach (float turn in new[] { 0f, 30f, -30f, 60f, -60f, 90f, -90f })
                        foreach (var (dw, plant, onWay) in cands)
                        {
                            Vector2 dir = (plant - onWay).normalized, d = Quaternion.Euler(0, 0, turn) * dir;
                            Vector2 at = plant - d * Mathf.Max(back, Mathf.Min(dw, back + 1.5f));
                            if (!game.LabNest.OnFloor(at, 0.2f) || !InSight(game, at, plant)) continue;
                            spots.Add((name, at, plant));
                            return;
                        }
                }
                CloseUp("curl_glow", "lab_curl", 2.6f);
                CloseUp("spore_stalk", "lab_spore", 2.2f);
                CloseUp("curl_a_crozier", "lab_crozier", 2.4f);
                CloseUp("puff_b_lantern", "lab_lantern", 2.6f);
                CloseUp("curl_c_hook", "lab_hook", 2.4f);
                CloseUp("coral_b_branching", "lab_coral", 2.2f);
                CloseUp("frond_a_feather", "lab_frond", 2.6f);
                CloseUp("curl_d_cluster", "lab_cluster", 2.2f);
                CloseUp("curl_b_double", "lab_double", 2.4f);
                CloseUp("curl_e_corkscrew", "lab_corkscrew", 2.4f);
                CloseUp("puff_a_stalked", "lab_puff", 2.4f);
                CloseUp("dry_mushroom", "lab_mushroom", 4.5f);
                CloseUp("amber_resin", "lab_resin", 2.4f);
                CloseUp("thorn_succulent", "lab_thorn", 2.4f);
                // two of the things left beside the way, seen from the way (in the temple, an arch and a spire)
                var shown = new HashSet<string>();
                // (of the kiln's burnt logs the one nearest the way, not the first: the first lay up on the rim, a bank
                // between it and the way, k6)
                float logNear = float.MaxValue;
                foreach (var (id, pos, from) in game.LabNest.Objects)
                    if (id == "ember_log") logNear = Mathf.Min(logNear, (pos - from).sqrMagnitude);
                foreach (var (id, pos, from) in game.LabNest.Objects)
                    if ((id == "relic_teacup" || id == "relic_key" || id.StartsWith("temple_arch") || id.StartsWith("temple_spire") || (id == "ember_log" && (pos - from).sqrMagnitude <= logNear) || id == "char_stump_temple" || id == "resin_cone") && shown.Add(id))
                    {
                        // (the kiln's stump temple, large, from further back; the first of each along the way; its log close, so
                        // its carved end and its cracks show, 1.5: from 4.2 it was 12.8 away, a tenth of its points drawn, k6)
                        Vector2 back = from - (pos - from).normalized * (id.StartsWith("temple") ? 2.4f : id == "ember_log" ? 1.5f : id == "char_stump_temple" ? 5.5f : id == "resin_cone" ? 5f : 3.5f);
                        spots.Add(("lab_" + id, back, pos));
                    }
                // this nest's own things close up (of each kind the one nearest the way), and the thing at its heart
                Tall.Clear();
                var owns = new List<(float dw, string kind, Vector2 pos, float height, Vector2 onWay)>();
                foreach (var (kind, pos, height) in SolidThings.Owned)
                {
                    float nearest = float.MaxValue;
                    Vector2 q0 = pos;
                    foreach (Vector2 q in t) { float d = (q - pos).sqrMagnitude; if (d < nearest) { nearest = d; q0 = q; } }
                    owns.Add((Mathf.Sqrt(nearest), kind, pos, height, q0));
                }
                owns.Sort((a, b) => a.dw.CompareTo(b.dw));
                // the heart thing, so a close-up neither stands inside it nor looks through it (the way ends at the heart:
                // for a thing beside it the camera stood in the leafcutters' fungus garden)
                Vector2 heartAt = default;
                float heartSize = 0f;
                foreach (var (kind, pos, height) in SolidThings.Owned) if (kind == "Heart") { heartAt = pos; heartSize = height; }
                var refused = new Dictionary<string, int[]>();   // (checks) why a kind has no close-up
                foreach (var (dw, kind, pos, height, onWay) in owns)
                {
                    string name = kind == "Heart" ? "lab_heart_close" : "lab_own_" + kind;
                    if (Tall.ContainsKey(name)) continue;
                    if (!refused.TryGetValue(kind, out int[] why)) refused[kind] = why = new int[4];
                    if (kind != "Heart" && dw < 0.6f && !LookDown.Contains(kind)) { why[0]++; continue; }   // (a flat thing may lie on the way)
                    // (a flat thing not under a story spot's ring: seen from above, the ring hid half a bitten leaf)
                    bool underRing = false;
                    if (LookDown.Contains(kind))
                        foreach (StorySlot sl in game.world.slots) underRing |= (new Vector2(sl.position.x, sl.position.z) - pos).sqrMagnitude < 1.6f * 1.6f;
                    if (underRing) { why[1]++; continue; }
                    Vector2 dir = dw > 0.1f ? (pos - onWay).normalized : (game.LabNest.Heart - t[0]).normalized;
                    Vector2 stand = pos - dir * Mathf.Max(Mathf.Clamp(height * 1.8f, 1.5f, 3.4f), dw);
                    if (kind != "Heart" && heartSize > 0f && (Vector2.Distance(stand, heartAt) < heartSize * 0.62f || OffSegment(heartAt, stand, pos) < heartSize * 0.5f)) { why[1]++; continue; }
                    if (kind == "Heart" ? !OnWayIn(game, 1.6f, out stand) && !Approach(game, r, pos, 4.6f, out stand) : !game.LabNest.OnFloor(stand, 0.2f) || !InSight(game, stand, pos))
                    {
                        why[kind == "Heart" || !game.LabNest.OnFloor(stand, 0.2f) ? 2 : 3]++;
                        continue;
                    }
                    Tall[name] = height;
                    spots.Add((name, stand, pos));
                }
                foreach (var kv in refused)
                    if (!Tall.ContainsKey(kv.Key == "Heart" ? "lab_heart_close" : "lab_own_" + kv.Key))
                        Debug.Log($"[AntGarden] no close-up of {kv.Key}: {kv.Value[0]} too near the way, {kv.Value[1]} by the heart or a story spot, {kv.Value[2]} off the floor, {kv.Value[3]} hidden");
                // and last (so every shot before keeps its number): the crystals by the way, and the world's plants a
                // nest has fixed for itself (the leafcutters' corals' soft change to their tips, the mushrooms' soil)
                CloseUp("crystal_a_spray", "lab_crystal_spray", 2.2f);
                CloseUp("crystal_b_fan", "lab_crystal_fan", 2.0f);
                CloseUp("crystal_c_mixed", "lab_crystal_mixed", 2.2f);
                CloseUp("coral_fungus", "lab_coral_fungus", 2.2f);
                CloseUp("tiny_mushrooms", "lab_tiny_mushrooms", 1.6f);
                // (the moss forest's own Tripo plants, after everything else so no other nest's numbers change)
                CloseUp("liverwort_parasols", "lab_liverwort", 2.6f);
                CloseUp("pixie_cup_lichen", "lab_lichen", 2.2f);
                CloseUp("moss_capsules", "lab_capsules", 2.6f);
                // (the kiln temple's own, after the moss forest's for the same reason)
                CloseUp("ember_cups", "lab_cups", 1.3f);   // (closer: at 2.0 the small cups were lost against the bank)
                // (its frame as for a plant 1.2 tall, about 40°: at its own 0.4 the lens closed to 14° and took a patch of the
                // slope, the cups in a corner, k6)
                Tall["lab_cups"] = 1.2f;
                CloseUp("fire_morel", "lab_morel", 2.6f);
                // (the amber nest's own, after the kiln temple's for the same reason)
                CloseUp("tear_tree", "lab_tear_tree", 3.0f);
                CloseUp("gumweed", "lab_gumweed", 2.2f);
                return spots;
            }
            WEdge e = DemoMode.LongEdge(game, r);
            if (e != null)
            {
                int i = Mathf.Clamp(e.pts.Count / 3, 0, e.pts.Count - 4);
                spots.Add(("canyon", e.pts[i], e.pts[i + 3]));
            }
            WNode chamber = null;
            foreach (WNode n in game.world.nodes)
                if (n.region == r && n.degree >= 3 && n.inside && (chamber == null || n.clear > chamber.clear)) chamber = n;
            if (chamber != null)
            {
                Vector2 c = new Vector2(chamber.x, chamber.z);
                if (Approach(game, r, c, chamber.clear + 4f, out Vector2 at)) spots.Add(("chamber", at, c));
            }
            return spots;
        }

        /// <summary>The index of the point about <paramref name="dist"/> units along a polyline (its last one if shorter).</summary>
        static int PointAlong(List<Vector2> pts, float dist)
        {
            float acc = 0;
            for (int i = 1; i < pts.Count; i++)
            {
                acc += (pts[i] - pts[i - 1]).magnitude;
                if (acc >= dist) return i;
            }
            return pts.Count - 1;
        }

        /// <summary>A walkable point on a path about <paramref name="back"/> units from the target, on its way in.</summary>
        /// <summary>Where on the main way the ant stands back units (along it) short of where its glowing track ends
        /// at the heart: there it sees the heart as a player coming in does.</summary>
        static bool OnWayIn(Game game, float back, out Vector2 at)
        {
            at = default;
            List<Vector2> t = game.LabNest?.Track;
            if (t == null || t.Count < 2) return false;
            Vector2 heart = game.LabNest.Heart;
            int hi = 0;
            for (int i = 1; i < t.Count; i++) if ((t[i] - heart).sqrMagnitude < (t[hi] - heart).sqrMagnitude) hi = i;
            int step = hi > t.Count / 2 ? -1 : 1;   // toward the way in
            float left = back;
            for (int i = hi; i + step >= 0 && i + step < t.Count; i += step)
            {
                float seg = (t[i + step] - t[i]).magnitude;
                if (seg >= left) { at = Vector2.Lerp(t[i], t[i + step], left / Mathf.Max(seg, 1e-4f)); return game.world.Dist(at.x, at.y) < -0.3f; }
                left -= seg;
            }
            return false;
        }

        static bool Approach(Game game, int r, Vector2 target, float back, out Vector2 at)
        {
            at = default;
            float best = float.MaxValue;
            foreach (WEdge e in game.world.edges)
            {
                if (e.region != r || e.pts.Count < 2) continue;
                foreach (Vector2 p in e.pts)
                {
                    float miss = Mathf.Abs((p - target).magnitude - back);
                    if (miss < best && game.world.Dist(p.x, p.y) < -0.6f) { best = miss; at = p; }
                }
            }
            return best < 2f;
        }

        /// <summary>Waits (up to 12 s) until the plants' fine splats are made for where the ant and the camera now are.</summary>
        internal static IEnumerator ShellSettles()
        {
            for (float t = 0; t < 12f; t += Time.unscaledDeltaTime)
            {
                if (SolidThings.Instance == null || SolidThings.Instance.ShellReady()) yield break;
                yield return null;
            }
        }

        /// <summary>The moment every still is taken at (NestClock): the fireflies, spores, pheromones, the threads'
        /// flicker and the plants' breath where they were in every other run, so two versions compare fairly.</summary>
        const float ShotMoment = 31.5f;

        static IEnumerator Grab(string path)
        {
            NestClock.Fix(ShotMoment);
            Game.Instance?.SettleSpores(ShotMoment);
            yield return null;   // a frame for what moves on the CPU to take the moment
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
            NestClock.Release();
            Game.Instance?.ResumeSpores();
        }

        /// <summary>
        /// Through the ant's eyes: just above its head, looking a little up at the plants, turned towards whichever
        /// way near ahead is clearest; ants right in front of the lens step out of the picture for the shot.
        /// </summary>
        /// <summary>The player's ant close up: from behind it, turned this far round to its side (degrees), this far
        /// from its middle and a little above, looking at it; its ring and the screen's text left out.</summary>
        static IEnumerator AntClose(Game game, string path, float dist, float turn)
        {
            AntController ant = game.player;
            Camera cam = game.rig.cam;
            float fov = cam.fieldOfView;
            var fwd = new Vector3(Mathf.Sin(ant.heading), 0, Mathf.Cos(ant.heading));
            Vector3 dir = Quaternion.Euler(0, turn, 0) * -fwd;
            Vector3 target = ant.Position + Vector3.up * 0.2f;
            // (from above enough that the pebbles round its feet stay out of the lens)
            Vector3 eye = target + (dir + Vector3.up * 0.85f).normalized * dist;
            game.rig.manual = true;
            game.SetRingVisible(false);
            game.hud.canvas.enabled = false;
            cam.fieldOfView = 28f;   // (a long lens from further off: close up, a wide one bulged the ant's near parts)
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            for (int f = 0; f < 4; f++) yield return null;
            yield return Grab(path);
            cam.fieldOfView = fov;
            game.hud.canvas.enabled = true;
            game.SetRingVisible(true);
            game.rig.manual = false;
        }

        static IEnumerator AntEye(Game game, string path, Vector2? lookAt)
        {
            AntController ant = game.player;
            Camera cam = game.rig.cam;
            float fov = cam.fieldOfView, bestYaw = ant.heading, bestClear = -1;
            if (lookAt.HasValue) bestYaw = Mathf.Atan2(lookAt.Value.x - ant.Position.x, lookAt.Value.y - ant.Position.z);
            else
                for (int k = 0; k < 9; k++)
                {
                    float yaw = ant.heading + (k % 2 == 0 ? 1 : -1) * ((k + 1) / 2) * 0.3f;
                    float clear = Clearance(game, ant.Position, yaw);
                    if (clear > bestClear + 0.5f) { bestClear = clear; bestYaw = yaw; }
                }
            Vector3 fwd = new Vector3(Mathf.Sin(bestYaw), 0, Mathf.Cos(bestYaw));
            Vector3 eye = ant.Position + fwd * 0.45f + Vector3.up * 0.38f;
            // (switched off rather than hidden: an NPC shows itself again every frame it is near the camera)
            var hidden = new List<GameObject>();
            foreach (NpcAnt n in game.npcs)
            {
                Vector3 d = n.ant.Position - eye;
                if (d.magnitude < 4f && Vector3.Dot(d, fwd) > -0.5f && n.ant.gameObject.activeSelf) { n.ant.gameObject.SetActive(false); hidden.Add(n.ant.gameObject); }
            }
            game.rig.manual = true;
            ant.SetVisible(false);
            game.SetRingVisible(false);
            game.hud.canvas.enabled = false;
            cam.fieldOfView = 50f;   // (72° stretched what was near the lens)
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(fwd + Vector3.up * 0.14f));
            yield return ShellSettles();
            for (int f = 0; f < 3; f++) yield return null;
            yield return Grab(path);
            foreach (GameObject a in hidden) a.SetActive(true);
            cam.fieldOfView = fov;
            game.hud.canvas.enabled = true;
            game.SetRingVisible(true);
            ant.SetVisible(true);
            game.rig.manual = false;
        }

        /// <summary>True when nothing standing up (another plant, a rock) is in the way from a camera at from to
        /// the plant at plant.</summary>
        static bool InSight(Game game, Vector2 from, Vector2 plant)
        {
            Vector2 d = plant - from;
            float len = d.magnitude;
            if (len < 1e-3f) return true;
            d /= len;
            // nothing standing right at the camera (a bush there filled a close-up with its splats)
            foreach (Placement pl in game.world.placements)
            {
                var q = new Vector2(pl.x, pl.z);
                if (pl.height >= 0.15f && (q - from).sqrMagnitude < 0.8f * 0.8f && (q - plant).sqrMagnitude > 0.04f) return false;
            }
            // nor the ground rising between them over the line from the camera's height to the plant's middle (the crystal
            // spray's close-up looked into a bank, its crystal behind it; a plant up a bank's foot is still seen)
            float h0 = game.world.Height(from.x, from.y) + 0.5f, h1 = game.world.Height(plant.x, plant.y) + 0.4f;
            for (float u = 0.1f; u < 0.91f; u += 0.1f)
            {
                Vector2 q = from + d * (len * u);
                if (game.world.Height(q.x, q.y) > Mathf.Lerp(h0, h1, u) + 0.05f) return false;
            }
            foreach (Placement pl in game.world.placements)
            {
                if (pl.height < 0.4f) continue;
                var p = new Vector2(pl.x, pl.z);
                if ((p - plant).sqrMagnitude < 0.04f) continue;   // the plant itself
                float along = Vector2.Dot(p - from, d);
                if (along < 0.2f || along > len - 0.3f) continue;
                float off = Mathf.Abs(d.x * (p.y - from.y) - d.y * (p.x - from.x));
                if (off < Mathf.Max(0.35f, pl.height * 0.3f)) return false;
            }
            // nor a nest's own thing standing up between them (a cut leaf sail hid half a crystal)
            foreach (var (kind, p, height) in SolidThings.Owned)
            {
                if (height < 0.4f || LookDown.Contains(kind) || (p - plant).sqrMagnitude < 0.04f) continue;   // (a flat thing does not hide)
                float along = Vector2.Dot(p - from, d);
                if (along < 0.2f || along > len - 0.3f) continue;
                if (Mathf.Abs(d.x * (p.y - from.y) - d.y * (p.x - from.x)) < Mathf.Max(0.35f, height * 0.4f)) return false;
            }
            return true;
        }

        /// <summary>How far the point p is from the segment a..b.</summary>
        static float OffSegment(Vector2 p, Vector2 a, Vector2 b)
        {
            Vector2 ab = b - a;
            float t = ab.sqrMagnitude < 1e-6f ? 0f : Mathf.Clamp01(Vector2.Dot(p - a, ab) / ab.sqrMagnitude);
            return (a + ab * t - p).magnitude;
        }

        /// <summary>The close-ups of things that are not plants (a nest's own, its heart): how tall each is.</summary>
        static readonly Dictionary<string, float> Tall = new Dictionary<string, float>();

        /// <summary>A nest's own things lying flat on the floor, photographed from above.</summary>
        static readonly HashSet<string> LookDown = new HashSet<string> { "BittenLeaf", "LeafBits", "AshDrift" };   // (the kiln's cold ash too, k11)

        static readonly HashSet<string> PlantShots = new HashSet<string>
            { "lab_curl", "lab_spore", "lab_crozier", "lab_lantern", "lab_hook", "lab_coral", "lab_frond", "lab_cluster", "lab_double", "lab_corkscrew", "lab_puff", "lab_mushroom", "lab_resin", "lab_thorn",
              "lab_crystal_spray", "lab_crystal_fan", "lab_crystal_mixed", "lab_coral_fungus", "lab_tiny_mushrooms", "lab_cups", "lab_morel",
              "lab_liverwort", "lab_lichen", "lab_capsules", "lab_tear_tree", "lab_gumweed" };

        /// <summary>
        /// A plant close up, as a photograph: from the way, raised to about half the plant's height and aimed at its
        /// middle, the lens just wide enough for the whole plant; the ant, its ring and the screen's text left out, and
        /// ants near the lens stepped out of the picture.
        /// </summary>
        static IEnumerator PlantPhoto(Game game, string path, Vector2 from, Vector2 plant, float tall = -1)
        {
            float h = 1.5f, best = float.MaxValue;
            if (tall > 0) h = Mathf.Max(0.5f, tall);
            else
                foreach (Placement pl in game.world.placements)
                {
                    float d = (new Vector2(pl.x, pl.z) - plant).sqrMagnitude;
                    if (d < best) { best = d; h = pl.height; }
                }
            Camera cam = game.rig.cam;
            float fov = cam.fieldOfView;
            var eye = new Vector3(from.x, game.world.WalkHeight(from.x, from.y) + Mathf.Clamp(h * 0.55f, 0.45f, 2f), from.y);
            // (aimed lower at the kiln's morels, whose caps a view at their middle cut off below, their stems unseen, k6; and
            // at its cups' own middle, framed as if 1.2 tall: aimed at 0.6 the cluster fell off the bottom, k7)
            float aim = path.EndsWith("_photo_lab_morel.png") ? 0.3f : path.EndsWith("_photo_lab_cups.png") ? 0.15f : 0.5f;
            float ground = game.world.WalkHeight(plant.x, plant.y);
            // (from the kiln's morel's own foot: at a bank's foot it stands below the floor's height at its middle, and
            // aimed from that the frame took the bank above it, k8)
            if (path.EndsWith("_photo_lab_morel.png"))
                foreach (PlantInstance pi in game.world.plantInstances)
                    if (pi.t != null && pi.plant != null && pi.plant.name == "fire_morel" && (new Vector2(pi.t.position.x, pi.t.position.z) - plant).sqrMagnitude < 0.04f)
                        ground = pi.t.position.y;
            var target = new Vector3(plant.x, ground + h * aim, plant.y);
            // a thing lying flat, seen from above (from level with the floor a bitten leaf was a line in it)
            string stem = Path.GetFileNameWithoutExtension(path);
            if (stem.Contains("_own_") && LookDown.Contains(stem.Substring(stem.IndexOf("_own_") + 5)))
            {
                Vector2 back = (from - plant).sqrMagnitude > 1e-4f ? (from - plant).normalized : Vector2.up, at = plant + back * 1.9f;
                eye = new Vector3(at.x, game.world.WalkHeight(at.x, at.y) + 1.5f, at.y);
                target = new Vector3(plant.x, game.world.WalkHeight(plant.x, plant.y) + 0.05f, plant.y);
                h = Mathf.Max(2.2f, tall / 0.6f * 0.8f);   // the frame holds it whole (a leaf's tall is 0.6 of its length)
            }
            float dist = Mathf.Max(0.5f, (target - eye).magnitude);
            game.rig.manual = true;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            yield return ShellSettles();   // the plants' fine splats made for this view
            // ants near the lens, or between it and the thing (or just past it), step out of the picture
            var hidden = new List<GameObject>();
            foreach (NpcAnt n in game.npcs)
                if ((n.ant.Position - eye).magnitude < Mathf.Max(3f, dist + 1.5f) && n.ant.gameObject.activeSelf) { n.ant.gameObject.SetActive(false); hidden.Add(n.ant.gameObject); }
            game.rig.manual = true;
            game.player.SetVisible(false);
            game.SetRingVisible(false);
            game.hud.canvas.enabled = false;
            // no wider than 46° (wider stretched the near parts): stepped back along the line to the plant as far as needs be,
            // up to half as far again
            float want = 2f * Mathf.Atan(h * 0.65f / dist) * Mathf.Rad2Deg;
            if (want > 46f)
            {
                float back = Mathf.Min(h * 0.65f / Mathf.Tan(23f * Mathf.Deg2Rad), dist * 1.5f);
                eye = target + (eye - target).normalized * back;
                dist = back;
            }
            cam.fieldOfView = Mathf.Clamp(2f * Mathf.Atan(h * 0.65f / dist) * Mathf.Rad2Deg, 14f, 46f);
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            for (int f = 0; f < 3; f++) yield return null;
            yield return Grab(path);
            // (a nest's own thing is not a plant: say what it is, not which plant stands nearest)
            string file = Path.GetFileName(path);
            if (file.Contains("_own_") || file.Contains("heart_close"))
                Debug.Log($"[AntGarden] photo {file}: the nest's own " + (file.Contains("heart_close") ? "heart thing" : file.Substring(file.IndexOf("_own_") + 5).Replace(".png", ""))
                    + $", {h:F2} tall, {dist:F1} from the camera");
            else if (game.Look != null) Debug.Log($"[AntGarden] photo {file}: " + game.Look.DescribeNear(new Vector3(plant.x, 0, plant.y), eye, cam.fieldOfView, Screen.height));
            foreach (GameObject a in hidden) a.SetActive(true);
            cam.fieldOfView = fov;
            game.hud.canvas.enabled = true;
            game.SetRingVisible(true);
            game.player.SetVisible(true);
            game.rig.manual = false;
        }

        /// <summary>How far the view runs along a direction before a plant, a rock or the ground fills it.</summary>
        static float Clearance(Game game, Vector3 from, float yaw)
        {
            var dir = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw));
            float y0 = from.y + 0.38f;
            for (float d = 0.6f; d < 8f; d += 0.3f)
            {
                Vector2 p = new Vector2(from.x, from.z) + dir * d;
                if (game.world.Height(p.x, p.y) > y0 + d * 0.14f - 0.1f) return d;
                foreach (Placement pl in game.world.placements)
                {
                    if (pl.height < 0.3f) continue;
                    float dx = pl.x - p.x, dz = pl.z - p.y, r = Mathf.Max(0.5f, pl.height * 0.35f);
                    if (dx * dx + dz * dz < r * r) return d;
                }
            }
            return 8f;
        }

        /// <summary>The whole region from high above at an angle, with the night haze thinned so the far side shows.</summary>
        static IEnumerator Aerial(Game game, int r, string path)
        {
            Region reg = game.world.regions[r];
            Camera cam = game.rig.cam;
            Vector3 c = new Vector3((reg.minX + reg.maxX) / 2, 0, (reg.minZ + reg.maxZ) / 2);
            c.y = game.world.Height(c.x, c.z);
            float size = Mathf.Max(reg.maxX - reg.minX, reg.maxZ - reg.minZ);
            float pitch = 55f * Mathf.Deg2Rad, yaw = 200f * Mathf.Deg2Rad, dist = size * 0.95f;
            Vector3 pos = c + new Vector3(Mathf.Sin(yaw) * Mathf.Cos(pitch), Mathf.Sin(pitch), Mathf.Cos(yaw) * Mathf.Cos(pitch)) * dist;
            float far = cam.farClipPlane, fog = RenderSettings.fogDensity, fov = cam.fieldOfView, shadow = QualitySettings.shadowDistance;
            Exposure(0.8f);   // a night scene seen from far away: lift it so the nest's shape reads
            game.rig.manual = true;
            game.hud.canvas.enabled = false;
            cam.farClipPlane = dist * 2.5f;
            cam.fieldOfView = 50f;
            RenderSettings.fogDensity = fog * 0.25f;
            QualitySettings.shadowDistance = dist * 2f;
            cam.transform.SetPositionAndRotation(pos, Quaternion.LookRotation(c - pos));
            for (int f = 0; f < 4; f++) yield return null;
            yield return Grab(path);
            cam.farClipPlane = far;
            cam.fieldOfView = fov;
            RenderSettings.fogDensity = fog;
            QualitySettings.shadowDistance = shadow;
            Exposure(-0.8f);
            game.hud.canvas.enabled = true;
            game.rig.manual = false;
        }

        static void Exposure(float stops)
        {
            var volume = Object.FindObjectOfType<UnityEngine.Rendering.PostProcessing.PostProcessVolume>();
            if (volume != null && volume.profile != null && volume.profile.TryGetSettings(out UnityEngine.Rendering.PostProcessing.ColorGrading grading))
                grading.postExposure.Override(grading.postExposure.value + stops);
        }
    }
}

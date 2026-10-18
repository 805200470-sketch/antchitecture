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
                    yield return Grab(Path.Combine(dir, $"r{r}_{k}_play_{name}.png"));
                    game.demoKeys = Vector2.zero;
                    // at a relic, look at the relic itself; elsewhere, wherever the view runs furthest
                    bool relic = name.StartsWith("relic") || name.StartsWith("temple") || name.StartsWith("amber");
                    yield return AntEye(game, Path.Combine(dir, $"r{r}_{k}_eye_{name}.png"), relic ? toward : (Vector2?)null);
                }
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
                if (Approach(game, r, game.LabNest.Heart, 7f, out Vector2 at)) spots.Add(("lab_heart", at, game.LabNest.Heart));
                // two of the things left beside the way, seen from the way
                foreach (var (id, pos, from) in game.LabNest.Objects)
                    if (id == "relic_teacup" || id == "relic_key")
                    {
                        Vector2 back = from - (pos - from).normalized * 3.5f;
                        spots.Add(("lab_" + id, back, pos));
                    }
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

        static IEnumerator Grab(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.Destroy(tex);
        }

        /// <summary>
        /// Through the ant's eyes: just above its head, looking a little up at the plants, turned towards whichever
        /// way near ahead is clearest; ants right in front of the lens step out of the picture for the shot.
        /// </summary>
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
            cam.fieldOfView = 72f;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(fwd + Vector3.up * 0.14f));
            for (int f = 0; f < 3; f++) yield return null;
            yield return Grab(path);
            foreach (GameObject a in hidden) a.SetActive(true);
            cam.fieldOfView = fov;
            game.hud.canvas.enabled = true;
            game.SetRingVisible(true);
            ant.SetVisible(true);
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

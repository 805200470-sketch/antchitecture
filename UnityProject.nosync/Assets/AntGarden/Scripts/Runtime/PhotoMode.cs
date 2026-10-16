using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering.PostProcessing;

namespace AntGarden
{
    /// <summary>
    /// Presentation stills (-photos &lt;dir&gt;), for slides:
    /// - composed shots rendered straight from the camera at 3840×2160 (no UI, no window-size limit), a little
    ///   brighter than in game so they hold up on a projector, saved as high-quality JPEGs;
    /// - screenshots with the game's UI (run with -lang en for English), captured at twice the window size;
    /// - the world map as a large PNG with a transparent background.
    /// Then the game quits.
    /// </summary>
    public static class PhotoMode
    {
        public const int Width = 3840, Height = 2160;

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            game.demo = true;          // the script places the ant; the keyboard does nothing
            game.SetRingVisible(false);
            game.hud.SetStatsVisible(false);
            Brighten(0.45f);
            yield return new WaitForSeconds(2.5f); // shaders compile, ants spread out

            // -photoset stills | ui | all (default): render only part of the set
            string set = Game.Arg("-photoset") ?? "all";
            var shots = new List<(string name, Action setup, float wait)>();
            WEdge blue = DemoMode.LongEdge(game, 0), amber = DemoMode.LongEdge(game, 1);
            if (blue != null)
            {
                int i = blue.pts.Count / 4;
                shots.Add(("01_blue_canyon", () => { Face(game, blue, i); DemoMode.Shot(game, 7f, 0.16f, true); }, 2.5f));
            }
            Vector2? busy = BusiestTrailPoint(game, 0);
            if (busy.HasValue)
                shots.Add(("02_ant_trail", () => { PlaceOnTrail(game, busy.Value); DemoMode.Shot(game, 4.6f, 0.13f); }, 2f));
            WNode chamber = game.world.nodes.Find(n => n.region == 0 && n.degree >= 3 && n.inside && n.clear >= 1.8f);
            if (chamber != null)
                shots.Add(("03_chamber_colony", () => { Stand(game, new Vector2(chamber.x, chamber.z)); DemoMode.Shot(game, 9f, 0.4f, true); }, 2.5f));
            NpcAnt friend = game.npcs.Find(n => n.region == 0 && n.ant.altitude < 0.1f);
            if (friend != null && blue != null)
                shots.Add(("04_antenna_touch", () => Meeting(game, friend, blue), 1.2f));
            Gate seam = game.gates.Find(g => !g.portal);
            if (seam != null)
                shots.Add(("05_light_gate", () => BeforeGate(game, seam, 6f, 5f), 2f));
            Gate portal = game.gates.Find(g => g.portal && g.region == 0);
            if (portal != null)
                shots.Add(("06_portal_gate", () => BeforeGate(game, portal, 8f, 6.5f), 2f));
            WNode amberChamber = game.world.nodes.Find(n => n.region == 1 && n.degree >= 3 && n.inside);
            if (amberChamber != null)
                shots.Add(("07_amber_nest", () => { Stand(game, new Vector2(amberChamber.x, amberChamber.z)); Wide(game, 15f, 0.55f); }, 4.5f));
            if (amber != null)
            {
                int i = amber.pts.Count / 3;
                shots.Add(("08_amber_canyon", () => { Face(game, amber, i); DemoMode.Shot(game, 6.5f, 0.15f, true); }, 2.5f));
            }
            AddPlant(game, shots, "09_amber_resin", 1, "amber_resin");
            AddPlant(game, shots, "10_lantern_pods", 0, "lantern_pods", "bracket_shelf");
            AddCanopy(game, shots, "11_fungal_canopy");
            if (blue != null)
            {
                int i = blue.pts.Count / 2;
                shots.Add(("12_aerial_view", () => { Face(game, blue, i); Wide(game, 30f, 0.72f); }, 2.5f));
            }

            if (set == "ui") shots.Clear();
            if (set == "poster") shots = PosterShots(game, blue);
            foreach (var shot in shots)
            {
                shot.setup();
                yield return new WaitForSeconds(shot.wait);
                yield return new WaitForEndOfFrame();
                Save(game.cam, Path.Combine(dir, shot.name + ".jpg"));
                Debug.Log($"[AntGarden] photo {shot.name}");
            }

            // the same world with its UI (English when run with -lang en), captured at twice the window size
            game.SetRingVisible(true);
            game.hud.SetHelpVisible(true);
            foreach (var ui in set == "stills" || set == "poster" ? new List<(string name, Action setup, float wait)>() : UiShots(game, blue))
            {
                ui.setup();
                yield return new WaitForSeconds(ui.wait);
                ScreenCapture.CaptureScreenshot(Path.Combine(dir, ui.name + ".png"), 2);
                yield return null;
                yield return null;
                yield return new WaitForSeconds(0.3f);
                game.hud.HideSlot();
                game.bigmap.Toggle(false);
                Debug.Log($"[AntGarden] ui shot {ui.name}");
            }

            // the archipelago map: north up, transparent outside the nests
            if (set != "ui" && set != "poster")
            {
                var map = new MapPainter(game.world, game.gates, 3200);
                File.WriteAllBytes(Path.Combine(dir, "13_map.png"), map.texture.EncodeToPNG());
            }
            Debug.Log($"[AntGarden] photos done: {shots.Count} + map");
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        /// <summary>Render the camera alone (post effects included, no UI) at 4K and save a JPEG.</summary>
        static void Save(Camera cam, string path)
        {
            var rt = RenderTexture.GetTemporary(Width, Height, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.sRGB);
            RenderTexture previous = cam.targetTexture;
            cam.targetTexture = rt;
            cam.Render();
            cam.targetTexture = previous;
            RenderTexture active = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Width, Height, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, Width, Height), 0, 0);
            tex.Apply();
            RenderTexture.active = active;
            RenderTexture.ReleaseTemporary(rt);
            File.WriteAllBytes(path, tex.EncodeToJPG(95));
            UnityEngine.Object.Destroy(tex);
        }

        /// <summary>Key-visual candidates for a poster: low, close, the ant in the foreground and light behind it.</summary>
        static List<(string name, Action setup, float wait)> PosterShots(Game game, WEdge blue)
        {
            var list = new List<(string, Action, float)>();
            Gate portal = game.gates.Find(g => g.portal && g.region == 0);
            if (portal != null)
                list.Add(("poster_a_portal", () =>
                {
                    BeforeGate(game, portal, 4.5f, 3f);
                    game.rig.pitch = 0.05f;
                    game.rig.yaw += 0.55f; // three-quarter view of the ant, the golden portal behind
                }, 2.5f));
            Gate seam = game.gates.Find(g => !g.portal);
            if (seam != null)
                list.Add(("poster_b_light_gate", () =>
                {
                    BeforeGate(game, seam, 3.5f, 3f);
                    game.rig.pitch = 0.06f;
                    game.rig.yaw -= 0.6f;
                }, 2.5f));
            NpcAnt friend = game.npcs.Find(n => n.region == 0 && !n.species.canFly && n.ant.altitude < 0.1f);
            if (friend != null && blue != null)
                list.Add(("poster_c_meeting", () =>
                {
                    Meeting(game, friend, blue);
                    game.rig.distance = 2.6f;
                    game.rig.pitch = 0.05f;
                }, 1.3f));
            Vector2? busy = BusiestTrailPoint(game, 0);
            if (busy.HasValue)
                list.Add(("poster_d_trail", () =>
                {
                    PlaceOnTrail(game, busy.Value);
                    DemoMode.Shot(game, 3.4f, 0.04f);
                }, 2f));
            return list;
        }

        /// <summary>Lift the exposure a little for slides (stills are viewed on projectors, not in a dark room).</summary>
        static void Brighten(float stops)
        {
            var volume = UnityEngine.Object.FindObjectOfType<PostProcessVolume>();
            if (volume != null && volume.profile != null && volume.profile.TryGetSettings(out ColorGrading grading))
                grading.postExposure.Override(grading.postExposure.value + stops);
        }

        /// <summary>A wide free-camera shot from the side with the clearest view (no mushroom cap in the lens).</summary>
        static void Wide(Game game, float distance, float pitch)
        {
            DemoMode.Shot(game, distance, pitch, true);
            Vector3 target = game.player.Position + Vector3.up * 0.6f;
            float bestYaw = game.rig.yaw, bestClear = float.MinValue;
            for (int k = 0; k < 16; k++)
            {
                float yaw = k * Mathf.PI / 8, cp = Mathf.Cos(pitch);
                Vector3 cam = target + new Vector3(Mathf.Sin(yaw) * cp, Mathf.Sin(pitch), Mathf.Cos(yaw) * cp) * distance;
                float clear = 50f;
                foreach (Placement pl in game.world.placements)
                {
                    if (pl.height < 3f) continue;
                    float dx = cam.x - pl.x, dz = cam.z - pl.z;
                    if (dx * dx + dz * dz > 900f) continue;
                    // tall plants: their tops spread to about half their height
                    float top = game.world.Height(pl.x, pl.z) + pl.height;
                    float reach = Mathf.Sqrt(dx * dx + dz * dz) - pl.height * 0.55f;
                    if (cam.y < top + 1f) clear = Mathf.Min(clear, reach);
                }
                if (clear > bestClear) { bestClear = clear; bestYaw = yaw; }
            }
            game.rig.yaw = bestYaw;
        }

        /// <summary>Stand under a fan tree near the canyon and look up into its canopy.</summary>
        static void AddCanopy(Game game, List<(string, Action, float)> shots, string name)
        {
            Placement best = default;
            float bestScore = float.MaxValue;
            foreach (Placement pl in game.world.placements)
            {
                if (pl.region != 0 || pl.plant != "fan_tree") continue;
                float score = Mathf.Abs(game.world.PathDist(pl.x, pl.z) - 2f);
                if (score < bestScore) { bestScore = score; best = pl; }
            }
            if (best.plant == null) return;
            shots.Add((name, () =>
            {
                Vector2 tree = new Vector2(best.x, best.z);
                if (!game.world.SnapToWalkable(best.x, best.z, 0.9f, out Vector2 p)) return;
                DemoMode.PlaceFacing(game, p, tree);
                DemoMode.Shot(game, 3.2f, -0.3f);
            }, 4.5f));
        }

        /// <summary>Screens that show the interface: HUD with a prompt, the map, a story slot, a portal, an arrival.</summary>
        static List<(string name, Action setup, float wait)> UiShots(Game game, WEdge blue)
        {
            var list = new List<(string, Action, float)>();
            NpcAnt friend = game.npcs.Find(n => n.region == 0 && n.ant.altitude < 0.1f);
            if (friend != null && blue != null)
                list.Add(("ui_01_hud_greeting", () =>
                {
                    int i = blue.pts.Count / 3;
                    DemoMode.PlaceFacing(game, blue.pts[i], blue.pts[Mathf.Min(blue.pts.Count - 1, i + 3)]);
                    float h = game.player.heading;
                    Vector2 front = DemoMode.Pos(game) + new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * 1.9f;
                    friend.ant.Place(front.x, front.y, h + Mathf.PI);
                    friend.Greet(game.player);
                    DemoMode.Shot(game, 3.8f, 0.2f);
                }, 4.5f));
            list.Add(("ui_02_map", () => game.bigmap.Toggle(true), 1f));
            StorySlot slot = game.world.slots.Find(s => game.world.RegionAt(s.position.x, s.position.z)?.index == 0);
            if (slot != null)
                list.Add(("ui_03_story_slot", () =>
                {
                    Vector2 sp = new Vector2(slot.position.x, slot.position.z);
                    Vector2 from = game.world.SnapToWalkable(sp.x + 2.5f, sp.y, 0.9f, out Vector2 q) ? q : sp;
                    DemoMode.PlaceFacing(game, from, sp);
                    DemoMode.Shot(game, 4.2f, 0.3f);
                    game.OpenSlot(slot);
                }, 1.2f));
            Gate portal = game.gates.Find(g => g.portal && g.region == 0);
            if (portal != null)
                list.Add(("ui_04_portal_prompt", () =>
                {
                    Vector2 gp = new Vector2(portal.position.x, portal.position.z);
                    DemoMode.PlaceFacing(game, gp - portal.dir * 2.6f, gp);
                    DemoMode.Shot(game, 5.5f, 0.16f);
                }, 1.5f));
            Gate arrival = game.gates.Find(g => g.portal && g.region > 0);
            if (arrival != null)
                list.Add(("ui_05_arrival_banner", () => { game.TeleportToGate(arrival); game.rig.distance = 4.6f; game.rig.pitch = 0.2f; }, 2.4f));
            return list;
        }

        static void Face(Game game, WEdge e, int i)
        {
            i = Mathf.Clamp(i, 0, e.pts.Count - 4);
            DemoMode.PlaceFacing(game, e.pts[i], e.pts[i + 3]);
        }

        static void Stand(Game game, Vector2 at)
        {
            if (game.world.SnapToWalkable(at.x, at.y, 0.9f, out Vector2 p)) game.player.Place(p.x, p.y, game.player.heading);
        }

        /// <summary>A canyon point with the most ants around it, to frame a busy trail.</summary>
        static Vector2? BusiestTrailPoint(Game game, int region)
        {
            Vector2? best = null;
            int bestCount = 0;
            foreach (WEdge e in game.world.edges)
            {
                if (e.region != region || e.pts.Count < 6) continue;
                for (int k = 2; k < e.pts.Count - 2; k += 3)
                {
                    int count = 0;
                    foreach (NpcAnt n in game.npcs)
                        if ((new Vector2(n.ant.Position.x, n.ant.Position.z) - e.pts[k]).sqrMagnitude < 36f) count++;
                    if (count > bestCount) { bestCount = count; best = e.pts[k]; }
                }
            }
            return best;
        }

        static void PlaceOnTrail(Game game, Vector2 p)
        {
            // stand a few steps away and look along the trail towards the crowd
            Vector2 from = p + new Vector2(4f, 0);
            if (game.world.SnapToWalkable(p.x + 4f, p.y + 1f, 0.9f, out Vector2 s)) from = s;
            DemoMode.PlaceFacing(game, from, p);
        }

        static void Meeting(Game game, NpcAnt friend, WEdge e)
        {
            int i = e.pts.Count * 3 / 5;
            DemoMode.PlaceFacing(game, e.pts[i], e.pts[Mathf.Min(e.pts.Count - 1, i + 3)]);
            float h = game.player.heading;
            Vector2 front = DemoMode.Pos(game) + new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * 1.7f;
            friend.ant.Place(front.x, front.y, h + Mathf.PI);
            friend.Greet(game.player);
            game.player.PlayTalk();
            DemoMode.Shot(game, 3.2f, 0.12f);
            game.rig.yaw += Mathf.PI * 0.42f; // from the side, both faces in view
        }

        static void BeforeGate(Game game, Gate g, float back, float distance)
        {
            Vector2 gp = new Vector2(g.position.x, g.position.z);
            DemoMode.PlaceFacing(game, gp - g.dir * back, gp);
            DemoMode.Shot(game, distance, 0.15f);
        }

        static void AddPlant(Game game, List<(string, Action, float)> shots, string name, int region, params string[] kinds)
        {
            Placement best = default;
            float bestScore = float.MaxValue;
            foreach (Placement pl in game.world.placements)
            {
                if (pl.region != region || Array.IndexOf(kinds, pl.plant) < 0) continue;
                float score = Mathf.Abs(game.world.PathDist(pl.x, pl.z) - 1.5f) - pl.height * 0.05f;
                if (score < bestScore) { bestScore = score; best = pl; }
            }
            if (best.plant == null) return;
            shots.Add((name, () =>
            {
                Vector2 plant = new Vector2(best.x, best.z);
                if (!game.world.SnapToWalkable(best.x, best.z, 0.9f, out Vector2 p)) return;
                Vector2 to = plant - p;
                float back = Mathf.Max(0, best.height * 1.2f + 2f - to.magnitude);
                if (to.sqrMagnitude > 1e-4f && back > 0 && game.world.SnapToWalkable(p.x - to.normalized.x * back, p.y - to.normalized.y * back, 0.9f, out Vector2 q)) p = q;
                DemoMode.PlaceFacing(game, p, plant);
                DemoMode.Shot(game, 4.2f, 0.14f);
            }, 4.5f)); // long enough for the mood to settle after switching regions
        }
    }
}

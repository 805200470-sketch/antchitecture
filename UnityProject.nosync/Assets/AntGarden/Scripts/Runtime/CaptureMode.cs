using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Automated evidence run (-capture &lt;dir&gt;): takes screenshots from a set of viewpoints, measures the
    /// frame rate at each, writes capture_report.json, then quits. Lets the build be checked without a person.
    /// </summary>
    public static class CaptureMode
    {
        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            var report = new StringBuilder();
            report.Append("{\n  \"world\": \"").Append(Escape(game.world.label)).Append("\",\n");
            report.Append($"  \"quality\": \"{game.Preset.name}\",\n  \"screen\": \"{Screen.width}x{Screen.height}\",\n");
            report.Append($"  \"gpu\": \"{Escape(SystemInfo.graphicsDeviceName)}\",\n  \"npcs\": {game.npcs.Count},\n  \"views\": [\n");
            yield return new WaitForSeconds(2f); // let shaders compile and the camera settle

            var views = new List<(string name, System.Action setup)>
            {
                ("01_behind_ant", () => { game.rig.SnapBehind(game.player.heading); game.rig.distance = 3.6f; game.rig.pitch = 0.2f; }),
                ("02_high_view", () => { game.rig.distance = 11f; game.rig.pitch = 0.55f; }),
                ("03_look_up", () => { game.rig.distance = 3f; game.rig.pitch = -0.35f; }),
            };
            // a chamber with its landmark tree, if there is one
            WNode chamber = null;
            foreach (WNode n in game.world.nodes) if (n.degree >= 3 && n.inside) { chamber = n; break; }
            if (chamber != null)
                views.Add(("04_chamber", () =>
                {
                    if (game.world.SnapToWalkable(chamber.x, chamber.z, 0.9f, out Vector2 p)) game.player.Place(p.x, p.y, game.player.heading);
                    game.rig.SnapBehind(game.player.heading);
                    game.rig.distance = 9f;
                    game.rig.pitch = 0.3f;
                }));
            if (game.npcs.Count > 0)
                views.Add(("05_npc_close", () =>
                {
                    var npc = game.npcs[3 % game.npcs.Count].ant;
                    Vector3 q = npc.Position;
                    if (game.world.SnapToWalkable(q.x - Mathf.Sin(npc.heading) * 2.2f, q.z - Mathf.Cos(npc.heading) * 2.2f, 0.9f, out Vector2 p))
                        game.player.Place(p.x, p.y, npc.heading);
                    game.rig.SnapBehind(game.player.heading);
                    game.rig.distance = 3.2f;
                    game.rig.pitch = 0.25f;
                }));
            // gates: look through a seam gate, then a portal, then jump into the other region
            Gate seam = game.gates.Find(g => !g.portal);
            if (seam != null)
                views.Add(("07_seam_gate", () => LookAt(game, seam, 7f)));
            Gate portal = game.gates.Find(g => g.portal && g.region == 0);
            if (portal != null)
                views.Add(("08_portal_gate", () => LookAt(game, portal, 8f)));
            Gate arrival = game.gates.Find(g => g.portal && g.region > 0);
            if (arrival != null)
            {
                views.Add(("09_other_region", () => { game.TeleportToGate(arrival); game.rig.distance = 4.5f; game.rig.pitch = 0.22f; }));
                views.Add(("10_other_region_high", () => { game.rig.distance = 12f; game.rig.pitch = 0.5f; }));
                WNode islandChamber = game.world.nodes.Find(n => n.region == arrival.region && n.degree >= 3 && n.inside);
                if (islandChamber != null)
                    views.Add(("11_other_region_chamber", () =>
                    {
                        if (game.world.SnapToWalkable(islandChamber.x, islandChamber.z, 0.9f, out Vector2 p)) game.player.Place(p.x, p.y, game.player.heading);
                        game.rig.SnapBehind(game.player.heading);
                        game.rig.distance = 9f;
                        game.rig.pitch = 0.3f;
                    }));
            }
            views.Add(("12_bigmap", () => game.bigmap.Toggle(true)));
            // the other UI language, switched live with the map open, then back
            views.Add(("12b_bigmap_other_language", () => game.ToggleLanguage()));
            views.Add(("12c_language_back", () => { game.ToggleLanguage(); game.bigmap.Toggle(false); }));
            // the newer assets up close: one view per region, plus the story husk
            AddPlantView(game, views, "13_new_assets_region0", 0, "lantern_pods", "bracket_shelf", "coral_fungus", "jelly_fungus");
            AddPlantView(game, views, "14_new_assets_region1", 1, "amber_resin", "soil_spire", "dry_mushroom");
            AddPlantView(game, views, "15_seed_husk_region", -1, "seed_husk");

            for (int i = 0; i < views.Count; i++)
            {
                if (i > 0 && views[i - 1].name.Contains("bigmap") && !views[i].name.Contains("bigmap")) game.bigmap.Toggle(false);
                views[i].setup();
                yield return new WaitForSeconds(views[i].name.Contains("region") ? 3.5f : 1.5f);
                int frames = 0;
                float t = 0;
                while (t < 2f) { t += Time.unscaledDeltaTime; frames++; yield return null; }
                string file = Path.Combine(dir, views[i].name + ".png");
                ScreenCapture.CaptureScreenshot(file);
                yield return null;
                yield return null;
                report.Append($"    {{ \"view\": \"{views[i].name}\", \"fps\": {frames / t:F1} }}{(i < views.Count - 1 ? "," : "")}\n");
                Debug.Log($"[AntGarden] capture {views[i].name}: {frames / t:F1} fps");
            }
            game.bigmap.Toggle(false);
            report.Append("  ]\n}\n");
            File.WriteAllText(Path.Combine(dir, "capture_report.json"), report.ToString());
            yield return new WaitForSeconds(0.5f);
            Application.Quit();
        }

        /// <summary>
        /// Stand the ant near one of the given plants (the one closest to the canyon edge, so it is in view
        /// from the floor), facing it, with the camera behind. Region -1 = any region.
        /// </summary>
        static void AddPlantView(Game game, List<(string, System.Action)> views, string name, int region, params string[] kinds)
        {
            Placement best = default;
            float bestScore = float.MaxValue;
            foreach (Placement pl in game.world.placements)
            {
                if (region >= 0 && pl.region != region || System.Array.IndexOf(kinds, pl.plant) < 0) continue;
                float score = Mathf.Abs(game.world.PathDist(pl.x, pl.z) - 1.5f) - pl.height * 0.05f;
                if (score < bestScore) { bestScore = score; best = pl; }
            }
            if (best.plant == null) return;
            views.Add((name, () =>
            {
                if (!game.world.SnapToWalkable(best.x, best.z, 0.9f, out Vector2 p)) return;
                Vector2 to = new Vector2(best.x, best.z) - p;
                float back = Mathf.Max(0, best.height * 1.2f + 2f - to.magnitude);
                if (to.sqrMagnitude > 1e-4f && back > 0 && game.world.SnapToWalkable(p.x - to.normalized.x * back, p.y - to.normalized.y * back, 0.9f, out Vector2 q)) p = q;
                to = new Vector2(best.x, best.z) - p;
                game.player.Place(p.x, p.y, Mathf.Atan2(to.x, to.y));
                game.rig.SnapBehind(game.player.heading);
                game.rig.distance = 4f;
                game.rig.pitch = 0.12f;
            }));
        }

        /// <summary>Stand the ant in front of a gate, facing it, with the camera behind.</summary>
        static void LookAt(Game game, Gate g, float back)
        {
            Vector2 d = g.portal ? g.dir : g.dir;
            Vector2 from = new Vector2(g.position.x, g.position.z) - d * back;
            if (game.world.SnapToWalkable(from.x, from.y, 0.9f, out Vector2 p)) game.player.Place(p.x, p.y, Mathf.Atan2(d.x, d.y));
            game.rig.SnapBehind(game.player.heading);
            game.rig.distance = 5f;
            game.rig.pitch = 0.18f;
        }

        static string Escape(string s) => (s ?? "").Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}

using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Map check (-mapcheck &lt;dir&gt;, only when asked for; 11-04). At the screen size the game was started with
    /// (-screen-width, -screen-height, -screen-fullscreen), in each nest of the honeycomb in turn (GameConfig.labWorld, on
    /// clockwise round the ring by the big map's own ring; -mapnests N: only the first N), its state kept across the
    /// worlds' reloads: a still of the screen with its minimap; the big map open, checked to lie wholly on the screen (the
    /// title as written, the ring, the nest's map and each row of buttons, every border inside the screen's), and a still
    /// of it; in the first nest the big map in the other language too, and with the pad's cursor on the far side of the
    /// ring. Checks too that the honeycomb's map (HoneyMap, drawn off the main thread from the nests' path data) knows the
    /// nest's gates as the nest has them (Game.gates, GatesBack, LabNest.Heart). The chapter's card is held back (no card
    /// over the stills). Writes map_report.json, puts the language preference back as it was, and quits.
    /// </summary>
    public static class MapCheck
    {
        static readonly StringBuilder checks = new StringBuilder();
        static int count, failed, step;
        static string langPref;

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            bool first = step == 0;
            if (first) langPref = PlayerPrefs.GetString("antgarden.lang", "zh");
            game.demo = true;   // the check steers: no keys, no pad
            game.hud.SetStatsVisible(false);
            if (game.LabNest != null) game.LabNest.Hold = true;   // (no chapter card over the stills)
            string tag = $"{step:00}_{Short(Game.Selection)}";
            if (first) Check("screen", true, $"{Screen.width}x{Screen.height} {Screen.fullScreenMode}, canvas {((RectTransform)game.hud.canvas.transform).rect.size}");

            // the honeycomb's map drawn (a second or so off the main thread, while the nest itself took longer)
            float t = 0f;
            while (HoneyMap.Shared != null && !HoneyMap.Shared.Done && t < 30f) { t += Time.unscaledDeltaTime; yield return null; }
            HoneyMap honey = HoneyMap.Shared;
            int here = honey != null && honey.Ready ? honey.IndexOf(Game.Selection) : -1;
            // (outside the particle nests, the old map: only its stills and its fit)
            if (Game.Lab) Check(tag + "_honeycomb_map_drawn", here >= 0, honey == null ? "no honeycomb map" : honey.Error ?? $"nest {here + 1} of {honey.Count}");
            if (here >= 0) Agree(game, honey, here, tag);

            yield return new WaitForSeconds(first ? 2.5f : 1.5f);
            yield return Grab(Path.Combine(dir, tag + "_hud.jpg"));

            // the big map's first step: this nest, whole
            game.bigmap.Toggle(true);
            yield return new WaitForSeconds(0.8f);
            string lang = Loc.En ? "en" : "zh";
            if (Game.Lab) Check(tag + "_bigmap_draws_the_honeycomb", game.bigmap.HoneyShown, "");
            Check(tag + "_opens_on_the_nest", game.bigmap.Level == 1, $"step {game.bigmap.Level}");
            Fits(game, tag + "_nest_" + lang);
            yield return Grab(Path.Combine(dir, tag + "_nest_" + lang + ".jpg"));
            if (first && game.bigmap.HoneyShown)
            {
                // one step further: the whole honeycomb; the other language on each step; one step back at a time
                game.bigmap.ShowNests();
                yield return new WaitForSeconds(0.8f);
                Check(tag + "_one_step_further_the_ring", game.bigmap.Level == 2, $"step {game.bigmap.Level}");
                Fits(game, tag + "_nests_" + lang);
                yield return Grab(Path.Combine(dir, tag + "_nests_" + lang + ".jpg"));
                game.ToggleLanguage();   // (the map is built again in the other language, on the step it was on)
                yield return new WaitForSeconds(0.8f);
                string other = Loc.En ? "en" : "zh";
                Check(tag + "_language_keeps_the_step", game.bigmap.Level == 2, $"step {game.bigmap.Level}");
                Fits(game, tag + "_nests_" + other);
                yield return Grab(Path.Combine(dir, tag + "_nests_" + other + ".jpg"));
                game.bigmap.Back();
                yield return new WaitForSeconds(0.8f);
                Check(tag + "_back_to_the_nest", game.bigmap.Level == 1, $"step {game.bigmap.Level}");
                Fits(game, tag + "_nest_" + other);
                yield return Grab(Path.Combine(dir, tag + "_nest_" + other + ".jpg"));
                game.ToggleLanguage();
                yield return new WaitForSeconds(0.5f);
                // the pad's cursor (from the ant's nest) onto the far side of the ring
                game.bigmap.ShowNests();
                yield return new WaitForSeconds(0.5f);
                var labs = Ring(game);
                int at = labs.IndexOf(Game.Selection);
                if (at >= 0)
                {
                    string far = labs[(at + labs.Count / 2) % labs.Count];
                    float s = 0f;
                    game.bigmap.PadTick(new Vector2(0.05f, 0f), false, Time.deltaTime);
                    while ((game.bigmap.RingHover != far || (game.bigmap.RingCentre(far) - game.bigmap.PadCursor).magnitude > 20f) && s < 8f)
                    {
                        Vector2 to = game.bigmap.RingCentre(far) - game.bigmap.PadCursor;
                        game.bigmap.PadTick(to.magnitude > 80f ? to.normalized : to / 80f, false, Time.deltaTime);
                        s += Time.deltaTime;
                        yield return null;
                    }
                    Check(tag + "_pad_cursor_onto_" + Short(far), game.bigmap.RingHover == far, $"after {s:F1}s");
                    yield return new WaitForSeconds(0.3f);
                    yield return Grab(Path.Combine(dir, tag + "_nests_pad_cursor.jpg"));
                }
                game.bigmap.Back();
                yield return new WaitForSeconds(0.4f);
                game.bigmap.Back();
                yield return new WaitForSeconds(0.4f);
                Check(tag + "_back_twice_closes", !game.bigmap.IsOpen, $"step {game.bigmap.Level}");
            }
            game.bigmap.Toggle(false);

            // on round the ring, by the map's ring
            step++;
            Write(dir, false);
            var ring = Ring(game);
            int max = int.TryParse(Game.Arg("-mapnests"), out int m) ? m : ring.Count;
            int i = ring.IndexOf(Game.Selection);
            if (i < 0 || step >= max) { yield return Finish(dir); yield break; }
            game.bigmap.ClickNest(ring[(i + 1) % ring.Count]);
            yield return new WaitForSeconds(3f);
            // (only reached when the world did not change)
            Check(tag + "_map_took_the_ant_on", false, "still in " + Short(Game.Selection));
            yield return Finish(dir);
        }

        static List<string> Ring(Game game)
        {
            var labs = new List<string>();
            foreach (string w in game.cfg.labWorld.Split(',')) if (!string.IsNullOrWhiteSpace(w)) labs.Add(w.Trim());
            return labs;
        }

        /// <summary>The map wholly on the screen: each of its parts inside the screen's rectangle (half a pixel's grace).</summary>
        static void Fits(Game game, string name)
        {
            var parts = game.bigmap.ScreenRects();
            var off = new List<string>();
            var all = new StringBuilder();
            foreach (var (part, r) in parts)
            {
                all.Append($"{part} {r.xMin:F0}..{r.xMax:F0} x {r.yMin:F0}..{r.yMax:F0}; ");
                if (r.xMin < -0.5f || r.yMin < -0.5f || r.xMax > Screen.width + 0.5f || r.yMax > Screen.height + 0.5f) off.Add(part);
            }
            Check(name + "_fits_the_screen", off.Count == 0 && parts.Count > 0, (off.Count > 0 ? "off the screen: " + string.Join(", ", off) + " · " : "") + $"screen {Screen.width}x{Screen.height}: " + all);
        }

        /// <summary>The honeycomb's map knows this nest's gates as the nest has them: each gate at one of its doors (within
        /// a unit and a half) leading where the gate leads, its way out where the light leads, the gate back where an ant
        /// from the previous nest arrives.</summary>
        static void Agree(Game game, HoneyMap honey, int here, string tag)
        {
            HoneyMap.Nest nest = honey.nests[here];
            var wrong = new List<string>();
            int matched = 0;
            foreach (Gate g in game.gates)
            {
                if (g.world == null) continue;
                var at = new Vector2(g.position.x, g.position.z);
                HoneyMap.Door door = null;
                float best = float.MaxValue;
                foreach (HoneyMap.Door d in nest.doors) { float dd = (d.at - at).magnitude; if (dd < best) { best = dd; door = d; } }
                if (door == null || best > 1.5f || door.to != g.world) wrong.Add($"gate ({at.x:F0}, {at.y:F0}) to {Short(g.world)}: map {(door == null ? "none" : $"({door.at.x:F0}, {door.at.y:F0}) to {Short(door.to)}")}");
                else matched++;
            }
            if (matched != nest.doors.Count) wrong.Add($"{nest.doors.Count} doors on the map, {matched} gates matched");
            Check(tag + "_map_knows_its_gates", wrong.Count == 0, wrong.Count == 0 ? $"{matched} gates" : string.Join("; ", wrong));
            if (game.LabNest != null)
            {
                float d = nest.wayOut.HasValue ? (nest.wayOut.Value - game.LabNest.Heart).magnitude : -1f;
                Check(tag + "_map_knows_its_way_out", d >= 0f && d < 1f, $"{d:F2} from where the light leads");
            }
            string prev = honey.nests[(here + honey.Count - 1) % honey.Count].id;
            HoneyMap.Door back = nest.Arrival((here + honey.Count - 1) % honey.Count);
            bool known = game.GatesBack.TryGetValue(prev, out Gate gb);
            float off = known && back != null ? (back.at - new Vector2(gb.position.x, gb.position.z)).magnitude : -1f;
            Check(tag + "_map_knows_its_gate_back", known ? off >= 0f && off < 1.5f : back == null, known ? $"{off:F2} from the gate back to {Short(prev)}" : "no gate back");
        }

        static IEnumerator Grab(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, shot.EncodeToJPG(92));
            Object.Destroy(shot);
        }

        static void Check(string name, bool ok, string detail)
        {
            checks.Append(count++ > 0 ? ",\n" : "").Append($"    {{ \"name\": \"{name}\", \"ok\": {(ok ? "true" : "false")}, \"detail\": \"{detail.Replace("\"", "'")}\" }}");
            if (!ok) failed++;
            Debug.Log($"[AntGarden] mapcheck {(ok ? "ok" : "FAILED")}: {name} ({detail})");
        }

        static void Write(string dir, bool done) =>
            File.WriteAllText(Path.Combine(dir, "map_report.json"), $"{{\n  \"checks\": [\n{checks}\n  ],\n  \"failed\": {failed},\n  \"done\": {(done ? "true" : "false")}\n}}\n");

        static IEnumerator Finish(string dir)
        {
            if (langPref != null) { PlayerPrefs.SetString("antgarden.lang", langPref); PlayerPrefs.Save(); }
            Write(dir, true);
            Debug.Log($"[AntGarden] mapcheck done: {count} checks, {failed} failed");
            yield return new WaitForSeconds(0.3f);
            Application.Quit(failed > 0 ? 1 : 0);
        }

        static string Short(string world) => world == null ? "none" : world.Contains("@") ? world.Substring(world.LastIndexOf('@') + 1) : world;
    }
}

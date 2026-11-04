using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Honeycomb check (-honeycombcheck &lt;dir&gt;, only when asked for; 11-03). Tours the ring of particle nests
    /// (GameConfig.labWorld, Game.RingGates, BigMap's ring) as a player would, checking each step across the worlds'
    /// reloads (its state is static): in each nest its gates lead to its two neighbours and nowhere else, its way out to
    /// the next; on through each way out round the whole ring, then one gate back, the ant arrives at the gate that leads
    /// back where it came from; then the map's ring, by the pad's cursor and A and by a click in turn, to every nest once
    /// (from a nest that is no neighbour the ant arrives at the nest's start). In the first nest it takes stills: its two
    /// gates naming their neighbours, the map in 中文 and in English, a gate in English; on the first map jump, the pad's
    /// cursor on the ring. Writes honeycomb_report.json, puts the language preference back as it was, and quits.
    /// </summary>
    public static class HoneycombCheck
    {
        static readonly StringBuilder checks = new StringBuilder();
        static int count, failed, hop;
        static string expectTo, expectFrom, expectHow;   // the hop under way: where to, from where, by what (gate, pad, click)
        static string langPref;                            // the language preference before the check (it switches the language)
        static readonly HashSet<string> mapVisited = new HashSet<string>();

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            bool first = hop == 0 && expectTo == null;
            if (first) langPref = PlayerPrefs.GetString("antgarden.lang", "zh");
            Vector3 arrived = game.player.Position;   // where the ant was put on arrival, before anything moves it
            game.demo = true;   // the check steers: no keys, no pad
            game.hud.SetStatsVisible(false);
            var labs = new List<string>();
            foreach (string w in game.cfg.labWorld.Split(',')) if (!string.IsNullOrWhiteSpace(w)) labs.Add(w.Trim());
            int n = labs.Count, here = labs.IndexOf(Game.Selection);
            string tag = $"{hop:00}_{Short(Game.Selection)}";
            if (n < 2 || here < 0)
            {
                Check(tag + "_in_the_ring", false, Game.Selection);
                yield return Finish(dir);
                yield break;
            }
            string next = labs[(here + 1) % n], prev = labs[(here + n - 1) % n];

            // 1 · its gates lead to its two neighbours and nowhere else; its way out (where its light leads) to the next
            var dests = new List<string>();
            foreach (Gate g in game.gates) if (g.world != null) dests.Add(g.world);
            Check(tag + "_gates_lead_to_its_two_neighbours", dests.TrueForAll(d => d == next || d == prev) && dests.Contains(next) && dests.Contains(prev),
                $"{dests.Count} gates: {string.Join(" ", dests.ConvertAll(Short))}; neighbours {Short(prev)}, {Short(next)}");
            game.GatesBack.TryGetValue(next, out Gate wayOut);
            game.GatesBack.TryGetValue(prev, out Gate wayBack);
            float toHeart = wayOut != null && game.LabNest != null ? Vector2.Distance(Flat(wayOut.position), game.LabNest.Heart) : -1f;
            Check(tag + "_way_out_opens_on_the_next", wayOut != null && toHeart >= 0f && toHeart < 1f, $"the gate to {Short(next)} {toHeart:F1} from where the light leads");
            Check(tag + "_end_card_names_the_next", game.LabNest != null && game.LabNest.WayOutTo == next, $"the chapter's last card names {Short(game.LabNest?.WayOutTo)}");

            // 2 · where the hop under way put the ant
            if (expectTo != null)
            {
                Check(tag + "_arrived_by_" + expectHow, Game.Selection == expectTo, $"meant for {Short(expectTo)} from {Short(expectFrom)}");
                if ((expectFrom == next || expectFrom == prev) && game.GatesBack.TryGetValue(expectFrom, out Gate back))
                {
                    Gate nearest = null;
                    float nd = float.MaxValue;
                    foreach (Gate g in game.gates)
                        if (g.world != null && Vector2.Distance(Flat(g.position), Flat(arrived)) < nd) { nd = Vector2.Distance(Flat(g.position), Flat(arrived)); nearest = g; }
                    float d = Vector2.Distance(Flat(back.position), Flat(arrived));
                    Check(tag + "_at_the_gate_back", nearest == back && d < 9f, $"{d:F1} inside the gate back to {Short(expectFrom)}; the nearest gate leads to {Short(nearest?.world)}");
                }
                else
                {
                    var sp = game.world.SpawnPoint(0);
                    float d = Vector2.Distance(new Vector2(sp.x, sp.z), Flat(arrived));
                    Check(tag + "_at_the_start", d < 1.5f, $"{d:F1} from the nest's start (from {Short(expectFrom)}, no neighbour)");
                }
            }
            expectTo = null;

            // 3 · the first nest: stills of its way out (the chapter's last card naming the next nest, and the gate's prompt)
            // in both languages, of its gate back, and of the map in both languages
            if (first)
            {
                yield return new WaitForSeconds(7f);   // the chapter's first card comes and goes
                if (wayOut != null)
                {
                    yield return GateShot(game, wayOut, Path.Combine(dir, "gate_next_zh.jpg"));   // (at the way out: the last card)
                    game.ToggleLanguage();   // (the card and the prompt written again in English)
                    yield return new WaitForSeconds(0.8f);
                    yield return Grab(Path.Combine(dir, "gate_next_en.jpg"));
                    game.ToggleLanguage();
                    yield return new WaitForSeconds(0.5f);
                }
                if (wayBack != null) yield return GateShot(game, wayBack, Path.Combine(dir, "gate_prev_zh.jpg"));
                game.bigmap.Toggle(true);
                yield return new WaitForSeconds(1.2f);
                Check(tag + "_map_ring", game.bigmap.RingCount == n && game.bigmap.RingHere == Game.Selection, $"{game.bigmap.RingCount} nests, marked {Short(game.bigmap.RingHere)}");
                yield return Grab(Path.Combine(dir, "map_zh.jpg"));
                game.ToggleLanguage();
                yield return new WaitForSeconds(1.2f);
                Check(tag + "_map_ring_in_english", Loc.En && game.bigmap.IsOpen && game.bigmap.RingCount == n && game.bigmap.RingHere == Game.Selection, $"open {game.bigmap.IsOpen}, marked {Short(game.bigmap.RingHere)}");
                yield return Grab(Path.Combine(dir, "map_en.jpg"));
                game.bigmap.Toggle(false);
                game.ToggleLanguage();
                yield return new WaitForSeconds(0.5f);
            }
            else yield return new WaitForSeconds(1.5f);

            // 4 · on: through each way out round the ring, one gate back, then the map's ring to every nest once
            Write(dir, false);
            if (hop < n) yield return Through(game, wayOut, next, dir);
            else if (hop == n) yield return Through(game, wayBack, prev, dir);
            else
            {
                string target = Pick(labs, here);
                if (target == null) { yield return Finish(dir); yield break; }
                mapVisited.Add(target);
                game.bigmap.Toggle(true);
                game.bigmap.ShowNests();   // (11-04: the map opens on the nest; its ring is one step further)
                yield return new WaitForSeconds(0.8f);
                if (mapVisited.Count == 1)
                {
                    // the nest the ant is in, chosen, keeps it there
                    string stay = Game.Selection;
                    game.bigmap.ClickNest(stay);
                    yield return new WaitForSeconds(0.3f);
                    Check(tag + "_choosing_its_own_nest_stays", Game.Selection == stay && game.bigmap.IsOpen, "");
                }
                if (mapVisited.Count % 2 == 1)
                {
                    // the pad: the left stick takes the cursor (from the ant) onto the nest, then A
                    float t = 0f;
                    game.bigmap.PadTick(new Vector2(0.05f, 0f), false, Time.deltaTime);
                    // (on into the cell's middle, as a player aims, not only over its edge)
                    while ((game.bigmap.RingHover != target || (game.bigmap.RingCentre(target) - game.bigmap.PadCursor).magnitude > 20f) && t < 8f)
                    {
                        Vector2 to = game.bigmap.RingCentre(target) - game.bigmap.PadCursor;
                        game.bigmap.PadTick(to.magnitude > 80f ? to.normalized : to / 80f, false, Time.deltaTime);
                        t += Time.deltaTime;
                        yield return null;
                    }
                    Check(tag + "_pad_cursor_onto_" + Short(target), game.bigmap.RingHover == target, $"after {t:F1}s");
                    if (mapVisited.Count == 1)
                    {
                        yield return new WaitForSeconds(0.4f);
                        yield return Grab(Path.Combine(dir, "map_pad_cursor_zh.jpg"));
                    }
                    Leave(dir, target, "pad");
                    game.bigmap.PadTick(Vector2.zero, true, Time.deltaTime);   // A
                }
                else
                {
                    Leave(dir, target, "click");
                    game.bigmap.ClickNest(target);
                }
                yield return new WaitForSeconds(2.5f);
                // (only reached when the world did not change)
                Check(tag + "_map_took_the_ant_to_" + Short(target), false, "still in " + Short(Game.Selection));
                yield return Finish(dir);
            }
        }

        /// <summary>A hop about to start: what the next world should find.</summary>
        static void Leave(string dir, string to, string how)
        {
            expectFrom = Game.Selection;
            expectTo = to;
            expectHow = how;
            hop++;
            Write(dir, false);
        }

        /// <summary>Stand just inside the gate, as a player walking up to it does, and press E.</summary>
        static IEnumerator Through(Game game, Gate g, string dest, string dir)
        {
            if (g == null)
            {
                Check($"{hop:00}_{Short(Game.Selection)}_has_a_gate_to_{Short(dest)}", false, "");
                yield return Finish(dir);
                yield break;
            }
            Stand(game, g, 1.4f);
            for (int i = 0; i < 4; i++) yield return null;   // the prompt finds the gate (Game.UpdatePrompt)
            Leave(dir, dest, "gate");
            game.Interact();
            yield return new WaitForSeconds(2.5f);
            // (only reached when the world did not change)
            Check($"{hop:00}_gate_took_the_ant_to_{Short(dest)}", false, "still in " + Short(Game.Selection));
            yield return Finish(dir);
        }

        static void Stand(Game game, Gate g, float inside)
        {
            Vector2 at = Flat(g.position) - g.dir * inside;
            if (!game.world.SnapToWalkable(at.x, at.y, 0.9f, out Vector2 p)) p = at;
            game.player.Place(p.x, p.y, Mathf.Atan2(g.dir.x, g.dir.y));   // facing out through the gate
            game.rig.SnapBehind(game.player.heading);
        }

        /// <summary>The ant a little inside a gate, facing it, the camera behind: the gate and its prompt naming where it leads.</summary>
        static IEnumerator GateShot(Game game, Gate g, string path)
        {
            Stand(game, g, 2.6f);
            yield return new WaitForSeconds(1.6f);
            yield return Grab(path);
        }

        static IEnumerator Grab(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, shot.EncodeToJPG(90));
            Object.Destroy(shot);
        }

        /// <summary>The next nest to reach by the map: one not yet reached that way, the far side of the ring first.</summary>
        static string Pick(List<string> labs, int here)
        {
            int n = labs.Count;
            foreach (int off in new[] { n / 2, 2, n - 2, 1, n - 1 })
            {
                string t = labs[(here + off) % n];
                if (off > 0 && off < n && !mapVisited.Contains(t)) return t;
            }
            foreach (string t in labs) if (t != labs[here] && !mapVisited.Contains(t)) return t;
            return null;
        }

        static void Check(string name, bool ok, string detail)
        {
            checks.Append(count++ > 0 ? ",\n" : "").Append($"    {{ \"name\": \"{name}\", \"ok\": {(ok ? "true" : "false")}, \"detail\": \"{detail.Replace("\"", "'")}\" }}");
            if (!ok) failed++;
            Debug.Log($"[AntGarden] honeycombcheck {(ok ? "ok" : "FAILED")}: {name} ({detail})");
        }

        static void Write(string dir, bool done) =>
            File.WriteAllText(Path.Combine(dir, "honeycomb_report.json"), $"{{\n  \"checks\": [\n{checks}\n  ],\n  \"failed\": {failed},\n  \"done\": {(done ? "true" : "false")}\n}}\n");

        static IEnumerator Finish(string dir)
        {
            if (langPref != null) { PlayerPrefs.SetString("antgarden.lang", langPref); PlayerPrefs.Save(); }
            Write(dir, true);
            Debug.Log($"[AntGarden] honeycombcheck done: {count} checks, {failed} failed");
            yield return new WaitForSeconds(0.3f);
            Application.Quit(failed > 0 ? 1 : 0);
        }

        static Vector2 Flat(Vector3 p) => new Vector2(p.x, p.z);

        static string Short(string world) => world == null ? "none" : world.Contains("@") ? world.Substring(world.LastIndexOf('@') + 1) : world;
    }
}

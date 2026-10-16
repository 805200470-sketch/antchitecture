using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// NPC movement check (-npccheck &lt;dir&gt;): watches every NPC for 40 seconds of game time and counts how
    /// often they spin (a full turn within 3 seconds while barely moving) and turn back (facing the other way
    /// within 1.5 seconds), per ant-minute, plus their mean turning speed; then photographs the busiest
    /// chamber from above and quits.
    /// Run it with -colony 0 and without to compare the old wandering with the ant-colony trails.
    /// </summary>
    public static class NpcCheck
    {
        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            game.demo = true;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            Time.captureFramerate = 30;   // a fixed clock: the same simulation however fast the machine is
            yield return new WaitForSeconds(2f);

            // per ant, the last 3 seconds of turning: a full turn within them while barely getting anywhere is
            // a spin; facing the opposite way within 1.5 seconds is a U-turn
            const int Window = 90, Half = 45;   // frames at 30 fps
            var hist = new Dictionary<NpcAnt, (Queue<float> turns, Queue<Vector2> pos, Queue<float> heading, float sum)>();
            long samples = 0, spins = 0, uturns = 0;
            var spinWhere = new Dictionary<string, int>();   // role / state when the spin was noticed
            double turn = 0;
            float t = 0;
            while (t < 40f)
            {
                yield return null;
                float dt = Time.deltaTime;
                t += dt;
                foreach (NpcAnt n in game.npcs)
                {
                    if (n.ant.altitude > 0.05f) { hist.Remove(n); continue; }   // flying is not walking
                    Vector2 here = new Vector2(n.ant.Position.x, n.ant.Position.z);
                    if (!hist.TryGetValue(n, out var h))
                    {
                        h = (new Queue<float>(), new Queue<Vector2>(), new Queue<float>(), 0f);
                        h.pos.Enqueue(here);
                        h.heading.Enqueue(n.ant.heading);
                        hist[n] = h;
                        continue;
                    }
                    float prev = 0;
                    foreach (float x in h.heading) prev = x;   // the latest heading
                    float d = Mathf.Abs(Mathf.DeltaAngle(prev * Mathf.Rad2Deg, n.ant.heading * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
                    samples++;
                    turn += d / Mathf.Max(dt, 1e-4f);
                    h.turns.Enqueue(d);
                    h.sum += d;
                    h.pos.Enqueue(here);
                    h.heading.Enqueue(n.ant.heading);
                    while (h.turns.Count > Window) h.sum -= h.turns.Dequeue();
                    while (h.pos.Count > Window + 1) h.pos.Dequeue();
                    while (h.heading.Count > Window + 1) h.heading.Dequeue();
                    bool reset = false;
                    if (h.sum >= Mathf.PI * 2 && (here - h.pos.Peek()).magnitude < 1.5f * n.ant.cfg.bodyLength) { spins++; reset = true; string k = $"{n.role}/{n.StateName}"; spinWhere[k] = spinWhere.TryGetValue(k, out int c) ? c + 1 : 1; }
                    else if (h.heading.Count > Half)
                    {
                        float[] arr = h.heading.ToArray();
                        float back = arr[arr.Length - 1 - Half];
                        if (Mathf.Abs(Mathf.DeltaAngle(back * Mathf.Rad2Deg, n.ant.heading * Mathf.Rad2Deg)) > 150f && n.ant.speed > 0.1f) { uturns++; reset = true; }
                    }
                    if (reset)
                    {
                        h = (new Queue<float>(), new Queue<Vector2>(), new Queue<float>(), 0f);
                        h.pos.Enqueue(here);
                        h.heading.Enqueue(n.ant.heading);
                    }
                    hist[n] = h;
                }
            }
            bool colony = game.world.trails != null;
            double antMinutes = samples / 30.0 / 60.0;
            string report = "{\n" +
                $"  \"colony\": {(colony ? "true" : "false")},\n  \"npcs\": {game.npcs.Count},\n  \"antMinutes\": {antMinutes:F1},\n" +
                $"  \"meanTurnRadPerSec\": {(samples > 0 ? turn / samples : 0):F3},\n" +
                $"  \"spinsPerAntMinute\": {(antMinutes > 0 ? spins / antMinutes : 0):F3},\n" +
                $"  \"uTurnsPerAntMinute\": {(antMinutes > 0 ? uturns / antMinutes : 0):F3},\n" +
                $"  \"spinsBy\": \"{string.Join(", ", System.Linq.Enumerable.Select(spinWhere, kv => kv.Key + " " + kv.Value))}\",\n" +
                $"  \"trails\": \"{(colony ? game.world.trails.Summary() : "off")}\"\n}}\n";
            File.WriteAllText(Path.Combine(dir, "npc_report.json"), report);
            Debug.Log("[AntGarden] npccheck " + report.Replace("\n", " "));

            // the busiest place: the chamber with the most ants around it, seen from above
            WNode best = null;
            int bestCount = -1;
            foreach (WNode nd in game.world.nodes)
            {
                if (nd.degree < 3 || !nd.inside) continue;
                int c = 0;
                foreach (NpcAnt n in game.npcs)
                    if ((new Vector2(n.ant.Position.x - nd.x, n.ant.Position.z - nd.z)).sqrMagnitude < 144) c++;
                if (c > bestCount) { bestCount = c; best = nd; }
            }
            if (best != null && game.world.SnapToWalkable(best.x, best.z, 1f, out Vector2 p))
            {
                game.player.Place(p.x, p.y, game.player.heading);
                game.rig.free = true;
                game.rig.SnapBehind(game.player.heading);
                game.rig.distance = 16f;
                game.rig.pitch = 0.95f;
                for (int i = 0; i < 40; i++) yield return null;
                yield return new WaitForEndOfFrame();
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, "busiest_chamber.png"), tex.EncodeToPNG());
                Object.Destroy(tex);
            }
            Application.Quit();
        }
    }
}

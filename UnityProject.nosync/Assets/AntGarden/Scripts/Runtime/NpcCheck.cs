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
    /// Then encounters: 8 times the player's ant is set down still, right in front of a walking NPC, for 4 seconds;
    /// it counts the frames an NPC touches it and how far it is pushed (run with -giveway 0 to compare with NPCs that
    /// do not steer round it).
    /// (11-04) No ant may be moved by another (AntController.KeepClear). Over the 40 seconds it also measures how far the
    /// standing ants were moved (the player's ant stands all through; an NPC stopped to wait, meet or greet), the frames
    /// two ants on the ground overlapped, how often an NPC held up turned away, and the flights (takeoffs, landings, most
    /// in the air at once). And 6 more encounters with the player's ant walking: up behind a walking NPC and head-on into
    /// one, counting the frames they overlapped and the closest they came.
    /// Then portraits of the ants' colours (11-04): the player's ant close, one NPC of each kind close, the player's ant
    /// beside an NPC as the game's camera sees them, and a winged one taking off, flying and landing (made to take off
    /// near the player's ant). -npcportraits: only the portraits (the six nests' colours, quickly).
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
            if (Game.Arg("-npcportraits") == null)
            {
                yield return Movement(game, dir);
                yield return Encounters(game, dir);
            }
            yield return Portraits(game, dir);

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

        static float Flat(Vector3 a, Vector3 b) => Vector2.Distance(new Vector2(a.x, a.z), new Vector2(b.x, b.z));

        /// <summary>Somewhere the player's ant can be set down: off the stems and clear of the other ants.</summary>
        static bool Clear(Game game, Vector3 at) => !game.world.colliders.Overlaps(at.x, at.z, game.player.Radius * 0.75f) && game.player.ClearOfAnts(at.x, at.z, game.player.Radius + 0.05f);

        /// <summary>Every ant on the ground, the player's first.</summary>
        static List<AntController> Grounded(Game game)
        {
            var all = new List<AntController> { game.player };
            foreach (NpcAnt n in game.npcs) all.Add(n.ant);
            all.RemoveAll(a => a.altitude > 1f);
            return all;
        }

        /// <summary>40 seconds of watching: turning, and (11-04) pushing, overlaps, turning away, flights.</summary>
        static IEnumerator Movement(Game game, string dir)
        {
            // per ant, the last 3 seconds of turning: a full turn within them while barely getting anywhere is
            // a spin; facing the opposite way within 1.5 seconds is a U-turn
            const int Window = 90, Half = 45;   // frames at 30 fps
            var hist = new Dictionary<NpcAnt, (Queue<float> turns, Queue<Vector2> pos, Queue<float> heading, float sum)>();
            long samples = 0, spins = 0, uturns = 0;
            var spinWhere = new Dictionary<string, int>();   // role / state when the spin was noticed
            double turn = 0;
            // (11-04) the standing ants moved, overlaps, flights
            var was = new Dictionary<NpcAnt, (Vector3 pos, string state, float speed)>();
            Vector3 playerFrom = game.player.Position;
            float standMoved = 0, standMax = 0;
            int standFrames = 0, overlapFrames = 0, takeoffs = 0, landings = 0, mostFlying = 0;
            float closest = float.MaxValue;
            int heldBefore = NpcAnt.HeldTurns;
            float t = 0;
            while (t < 40f)
            {
                yield return null;
                float dt = Time.deltaTime;
                t += dt;
                int flying = 0;
                foreach (NpcAnt n in game.npcs)
                {
                    string st = n.StateName;
                    if (st == "TakeOff" || st == "Fly" || st == "Land") flying++;
                    if (was.TryGetValue(n, out var w))
                    {
                        if (w.state != "TakeOff" && st == "TakeOff") takeoffs++;
                        if (w.state == "Land" && st != "Land") landings++;
                        // standing (waiting, meeting, greeting, resting), at rest this frame and the last (not still easing to a
                        // stop on its own): it should not have moved at all
                        bool standing = (st == "Pause" || st == "Meet" || st == "Greet") && (w.state == "Pause" || w.state == "Meet" || w.state == "Greet")
                            && n.ant.speed < 0.002f && w.speed < 0.002f;
                        if (standing)
                        {
                            float m = Flat(n.ant.Position, w.pos);
                            standMoved += m;
                            standMax = Mathf.Max(standMax, m);
                            standFrames++;
                        }
                    }
                    was[n] = (n.ant.Position, st, n.ant.speed);
                }
                mostFlying = Mathf.Max(mostFlying, flying);
                var ground = Grounded(game);
                for (int i = 0; i < ground.Count; i++)
                    for (int j = i + 1; j < ground.Count; j++)
                    {
                        float d = Flat(ground[i].Position, ground[j].Position), min = ground[i].Radius + ground[j].Radius;
                        closest = Mathf.Min(closest, d - min);
                        if (d < min - 0.03f) overlapFrames++;
                    }
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
            var kinds = new Dictionary<string, int>();
            foreach (NpcAnt n in game.npcs) kinds[n.species.id] = kinds.TryGetValue(n.species.id, out int c) ? c + 1 : 1;
            string report = "{\n" +
                $"  \"colony\": {(colony ? "true" : "false")},\n  \"npcs\": {game.npcs.Count},\n  \"antMinutes\": {antMinutes:F1},\n" +
                $"  \"meanTurnRadPerSec\": {(samples > 0 ? turn / samples : 0):F3},\n" +
                $"  \"spinsPerAntMinute\": {(antMinutes > 0 ? spins / antMinutes : 0):F3},\n" +
                $"  \"uTurnsPerAntMinute\": {(antMinutes > 0 ? uturns / antMinutes : 0):F3},\n" +
                $"  \"spinsBy\": \"{string.Join(", ", System.Linq.Enumerable.Select(spinWhere, kv => kv.Key + " " + kv.Value))}\",\n" +
                $"  \"playerStandingMoved\": {Flat(game.player.Position, playerFrom):F4},\n" +
                $"  \"npcStandingMoved\": {standMoved:F4}, \"npcStandingMaxStep\": {standMax:F5}, \"npcStandingFrames\": {standFrames},\n" +
                $"  \"overlapPairFrames\": {overlapFrames}, \"closestGap\": {closest:F3},\n" +
                $"  \"heldTurnedAway\": {NpcAnt.HeldTurns - heldBefore},\n" +
                $"  \"takeoffs\": {takeoffs}, \"landings\": {landings}, \"mostInTheAir\": {mostFlying},\n" +
                $"  \"kinds\": \"{string.Join(", ", System.Linq.Enumerable.Select(kinds, kv => kv.Key + " " + kv.Value))}\",\n" +
                $"  \"trails\": \"{(colony ? game.world.trails.Summary() : "off")}\"\n}}\n";
            File.WriteAllText(Path.Combine(dir, "npc_report.json"), report);
            Debug.Log("[AntGarden] npccheck " + report.Replace("\n", " "));
        }

        /// <summary>The player's ant set down in a walking NPC's way: still (8 times), then walking (11-04: 3 times up behind
        /// one, 3 times head-on into one).</summary>
        static IEnumerator Encounters(Game game, string dir)
        {
            int encounters = 0, touchFrames = 0, lensFrames = 0;
            var touchBy = new Dictionary<string, int>();   // (11-04) the NPC's state in the frames it touched
            float pushed = 0, closest = float.MaxValue;
            var rnd = new System.Random(5);
            for (int e = 0, tries = 0; encounters < 8 && tries < 40; e++, tries++)
            {
                var walking = game.npcs.FindAll(n => n.StateName == "Walk" && n.ant.speed > 0.2f && n.ant.altitude < 0.5f);
                if (walking.Count == 0) { yield return new WaitForSeconds(1f); continue; }
                NpcAnt pick = walking[rnd.Next(walking.Count)];
                Vector3 fwd = new Vector3(Mathf.Sin(pick.ant.heading), 0, Mathf.Cos(pick.ant.heading));
                Vector3 at = pick.ant.Position + fwd * 1.6f;
                if (game.world.Dist(at.x, at.z) > -0.3f) continue;   // not on the open floor
                if (!Clear(game, at)) continue;   // (11-04: nor on a stem or another ant: set down on one, it was pushed off the stem)
                game.player.Place(at.x, at.z, pick.ant.heading + Mathf.PI);
                encounters++;
                yield return null;
                Vector3 p0 = game.player.Position;
                for (float s = 0; s < 4f; s += Time.deltaTime)
                {
                    yield return null;
                    foreach (NpcAnt n in game.npcs)
                    {
                        if (n.ant.altitude > 1f) continue;
                        float d = Vector2.Distance(new Vector2(n.ant.Position.x, n.ant.Position.z), new Vector2(game.player.Position.x, game.player.Position.z));
                        closest = Mathf.Min(closest, d - n.ant.Radius - game.player.Radius);
                        if (d < n.ant.Radius + game.player.Radius) { touchFrames++; touchBy[n.StateName] = touchBy.TryGetValue(n.StateName, out int tb) ? tb + 1 : 1; }
                        if (NpcAnt.CameraAt.HasValue && (n.ant.Position - NpcAnt.CameraAt.Value).magnitude < 1.2f) lensFrames++;
                    }
                }
                pushed += Vector2.Distance(new Vector2(p0.x, p0.z), new Vector2(game.player.Position.x, game.player.Position.z));
            }
            Debug.Log($"[AntGarden] npccheck encounters: {encounters}, touching frames {touchFrames} ({(encounters > 0 ? touchFrames / (float)encounters : 0):F1} per encounter), " +
                      $"player pushed {pushed:F2} units in all, closest gap {closest:F2}, NPC frames within 1.2 of the lens {lensFrames}, give way {(NpcAnt.GiveWayOn ? "on" : "off")}" +
                      (touchBy.Count > 0 ? $", touching by state: {string.Join(", ", System.Linq.Enumerable.Select(touchBy, kv => kv.Key + " " + kv.Value))}" : ""));

            // (11-04) walking into them: up behind one, and head-on
            int walks = 0, overlap = 0, blocked = 0;
            var overBy = new Dictionary<string, int>();
            float gap = float.MaxValue;
            for (int e = 0, tries = 0; walks < 6 && tries < 30; e++, tries++)
            {
                var walking = game.npcs.FindAll(n => n.StateName == "Walk" && n.ant.speed > 0.2f && n.ant.altitude < 0.5f);
                if (walking.Count == 0) { yield return new WaitForSeconds(1f); continue; }
                NpcAnt pick = walking[rnd.Next(walking.Count)];
                bool behind = e % 2 == 0;
                Vector3 fwd = new Vector3(Mathf.Sin(pick.ant.heading), 0, Mathf.Cos(pick.ant.heading));
                Vector3 at = pick.ant.Position + fwd * (behind ? -1.4f : 3f);
                if (game.world.Dist(at.x, at.z) > -0.3f || !Clear(game, at)) continue;
                game.player.Place(at.x, at.z, pick.ant.heading + (behind ? 0 : Mathf.PI));
                game.rig.SnapBehind(game.player.heading);
                walks++;
                game.demoKeys = new Vector2(0, 1);
                bool held = false, shot = false;
                for (float s = 0; s < 4f; s += Time.deltaTime)
                {
                    yield return null;
                    // a still as the game's camera sees it, once it has come up to the NPC (or at 2.5 seconds)
                    if (!shot && (s > 2.5f || game.player.WalkFree < 0.5f && s > 0.8f))
                    {
                        shot = true;
                        string nest = NestTheme.Current != null ? NestTheme.Current.id : "nest";
                        yield return Grab(Path.Combine(dir, $"{nest}_walk_{(behind ? "behind" : "headon")}_{e}.jpg"));
                    }
                    foreach (NpcAnt n in game.npcs)
                    {
                        if (n.ant.altitude > 1f) continue;
                        float d = Flat(n.ant.Position, game.player.Position), min = n.ant.Radius + game.player.Radius;
                        gap = Mathf.Min(gap, d - min);
                        if (d < min - 0.03f) { overlap++; overBy[n.StateName] = overBy.TryGetValue(n.StateName, out int ob) ? ob + 1 : 1; }
                    }
                    held |= game.player.WalkFree < 0.5f;
                }
                if (held) blocked++;
                game.demoKeys = null;
            }
            Debug.Log($"[AntGarden] npccheck walking into them: {walks} walks (behind and head-on), overlapping frames {overlap}, closest gap {gap:F3}, the player's ant held up in {blocked}" +
                      (overBy.Count > 0 ? $", overlapping by the NPC's state: {string.Join(", ", System.Linq.Enumerable.Select(overBy, kv => kv.Key + " " + kv.Value))}" : ""));
        }

        /// <summary>(11-04) The ants' colours: the player's ant close, an NPC of each kind close, the two side by side as the
        /// game's camera sees them, and a winged one taking off, flying and landing.</summary>
        static IEnumerator Portraits(Game game, string dir)
        {
            string nest = NestTheme.Current != null ? NestTheme.Current.id : "nest";
            Camera cam = game.rig.cam;
            float fov = cam.fieldOfView;
            game.SetRingVisible(false);
            game.hud.canvas.enabled = false;
            // the player's ant where it stands, close, from behind and from the side
            yield return Close(game, game.player, Path.Combine(dir, $"{nest}_player_close.jpg"), 1.9f, 35f);
            yield return Close(game, game.player, Path.Combine(dir, $"{nest}_player_side.jpg"), 2.6f, 80f);
            // one NPC of each kind, close (the player's ant set down a little way off, so it is drawn)
            var seen = new HashSet<string>();
            foreach (NpcAnt n in game.npcs)
            {
                if (n.ant.altitude > 0.5f || !seen.Add(n.species.id)) continue;
                Vector3 q = n.ant.Position;
                if (!game.world.SnapToWalkable(q.x + 2.6f, q.z, 0.6f, out Vector2 at)) continue;
                game.player.Place(at.x, at.y, Mathf.Atan2(q.x - at.x, q.z - at.y));
                for (int f = 0; f < 18; f++) yield return null;   // drawn again near the player's ant (it fades in)
                yield return Close(game, n.ant, Path.Combine(dir, $"{nest}_npc_{n.species.id}.jpg"), 1.9f, 50f);
            }
            // the player's ant beside an NPC, as the game's camera sees them
            NpcAnt mate = game.npcs.Find(n => n.ant.altitude < 0.5f && !n.species.canFly);
            if (mate != null)
            {
                Vector3 q = mate.ant.Position;
                Vector3 side = new Vector3(Mathf.Cos(mate.ant.heading), 0, -Mathf.Sin(mate.ant.heading));
                if (game.world.SnapToWalkable(q.x + side.x * 1.3f, q.z + side.z * 1.3f, 0.6f, out Vector2 at))
                {
                    game.player.Place(at.x, at.y, mate.ant.heading);
                    game.SetRingVisible(true);
                    game.hud.canvas.enabled = true;
                    DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
                    for (int f = 0; f < 20; f++) yield return null;
                    yield return Grab(Path.Combine(dir, $"{nest}_side_by_side.jpg"));
                    game.SetRingVisible(false);
                    game.hud.canvas.enabled = false;
                    // and the two close together, from in front and to the side, a little above (the player's ant's ring left out)
                    Vector3 mid = (game.player.Position + mate.ant.Position) * 0.5f + Vector3.up * 0.25f;
                    Vector3 ahead = new Vector3(Mathf.Sin(mate.ant.heading), 0, Mathf.Cos(mate.ant.heading));
                    Vector3 eye = mid + (Quaternion.Euler(0, 40f, 0) * ahead + Vector3.up * 0.75f).normalized * 3.4f;
                    eye.y = Mathf.Max(eye.y, game.world.Height(eye.x, eye.z) + 0.5f);
                    game.rig.manual = true;
                    cam.fieldOfView = 36f;
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(mid - eye));
                    for (int f = 0; f < 3; f++) yield return null;
                    yield return Grab(Path.Combine(dir, $"{nest}_pair_close.jpg"));
                    cam.fieldOfView = fov;
                    game.rig.manual = false;
                }
            }
            // (11-04) and where it is brightest, at the way in under the guiding light's first pool: an NPC set down beside the
            // player's ant, as the game sees them and close (no ant may blow out to white under the bloom)
            List<Vector2> track = game.LabNest?.Track;
            NpcAnt other = game.npcs.Find(n => n.ant.altitude < 0.5f && !n.species.canFly && n != mate) ?? mate;
            if (track != null && track.Count > 8 && other != null)
            {
                DemoMode.PlaceFacing(game, track[2], track[6]);
                Vector3 pp = game.player.Position;
                Vector3 side = new Vector3(Mathf.Cos(game.player.heading), 0, -Mathf.Sin(game.player.heading));
                if (game.world.SnapToWalkable(pp.x + side.x * 1.2f, pp.z + side.z * 1.2f, 0.5f, out Vector2 at))
                {
                    other.ant.Place(at.x, at.y, game.player.heading);
                    game.SetRingVisible(true);
                    game.hud.canvas.enabled = true;
                    DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);
                    for (int f = 0; f < 20; f++) yield return null;
                    yield return Grab(Path.Combine(dir, $"{nest}_bright_way_in.jpg"));
                    game.SetRingVisible(false);
                    game.hud.canvas.enabled = false;
                    Vector3 mid = (game.player.Position + other.ant.Position) * 0.5f + Vector3.up * 0.25f;
                    Vector3 ahead = new Vector3(Mathf.Sin(game.player.heading), 0, Mathf.Cos(game.player.heading));
                    Vector3 eye = mid + (Quaternion.Euler(0, 40f, 0) * ahead + Vector3.up * 0.75f).normalized * 3.4f;
                    eye.y = Mathf.Max(eye.y, game.world.Height(eye.x, eye.z) + 0.5f);
                    game.rig.manual = true;
                    cam.fieldOfView = 36f;
                    cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(mid - eye));
                    for (int f = 0; f < 3; f++) yield return null;
                    yield return Grab(Path.Combine(dir, $"{nest}_bright_pair_close.jpg"));
                    cam.fieldOfView = fov;
                    game.rig.manual = false;
                }
            }
            // a flight: a winged one walking takes off a little way in front of the player's ant
            NpcAnt flyer = game.npcs.Find(n => n.species.canFly && n.StateName == "Walk");
            if (flyer != null)
            {
                Vector3 q = flyer.ant.Position;
                Vector3 fwd = new Vector3(Mathf.Sin(flyer.ant.heading), 0, Mathf.Cos(flyer.ant.heading));
                if (game.world.SnapToWalkable(q.x - fwd.x * 4f, q.z - fwd.z * 4f, 0.6f, out Vector2 at))
                    game.player.Place(at.x, at.y, flyer.ant.heading);
                DemoMode.Shot(game, game.cfg.camera.distance, game.cfg.camera.pitch);   // (the game's camera behind it, looking at the flyer)
                game.SetRingVisible(true);
                bool off = false;
                for (int f = 0; f < 120 && !off; f++) { yield return null; off = f >= 18 && flyer.TakeOffNow(); }   // (once it walks again)
                if (off)
                {
                    int shot = 0;
                    float t = 0, next = 0.4f;
                    while (t < 30f && flyer.StateName != "Walk")
                    {
                        yield return null;
                        t += Time.deltaTime;
                        if (t < next) continue;
                        next = t + (flyer.StateName == "Fly" ? 1.5f : 0.6f);
                        // first as the game's camera sees it from behind the player's ant
                        if (shot % 2 == 0) yield return Grab(Path.Combine(dir, $"{nest}_flight_{shot:00}_{flyer.StateName}_game.jpg"));
                        // then from behind it and a little to its side and above, looking at its body (its clips lift the body
                        // above where the ant stands: between its antennae's tips and a body length back)
                        AntController a = flyer.ant;
                        Vector3 back = new Vector3(Mathf.Sin(a.heading), 0, Mathf.Cos(a.heading));
                        Vector3 body = (a.AntennaTip(0) + a.AntennaTip(1)) * 0.5f - back * 0.7f * a.cfg.bodyLength;
                        Vector3 eye = body - back * 2.6f + new Vector3(back.z, 0, -back.x) * 1.2f + Vector3.up * 0.7f;
                        eye.y = Mathf.Max(eye.y, game.world.Height(eye.x, eye.z) + 0.4f);
                        game.rig.manual = true;
                        cam.fieldOfView = 40f;
                        cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(body - eye));
                        yield return Grab(Path.Combine(dir, $"{nest}_flight_{shot++:00}_{flyer.StateName}.jpg"));
                        cam.fieldOfView = fov;
                        game.rig.manual = false;
                        if (shot >= 14) break;
                    }
                    Debug.Log($"[AntGarden] npccheck flight: {shot} stills over {t:F1} s, now {flyer.StateName}");
                }
            }
            game.SetRingVisible(true);
            game.hud.canvas.enabled = true;
        }

        /// <summary>An ant close up: from behind it, turned this far round to its side (degrees), this far from its middle
        /// and a little above, looking at it (as SceneShots' AntClose, for any ant).</summary>
        static IEnumerator Close(Game game, AntController ant, string path, float dist, float turn)
        {
            Camera cam = game.rig.cam;
            float fov = cam.fieldOfView;
            var fwd = new Vector3(Mathf.Sin(ant.heading), 0, Mathf.Cos(ant.heading));
            Vector3 dir = Quaternion.Euler(0, turn, 0) * -fwd;
            Vector3 target = ant.Position + Vector3.up * 0.2f;
            Vector3 eye = target + (dir + Vector3.up * 0.85f).normalized * dist;
            game.rig.manual = true;
            cam.fieldOfView = 28f;
            cam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye));
            for (int f = 0; f < 3; f++) yield return null;
            yield return Grab(path);
            cam.fieldOfView = fov;
            game.rig.manual = false;
        }

        static IEnumerator Grab(string path)
        {
            yield return new WaitForEndOfFrame();
            Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(path, tex.EncodeToJPG(90));
            Object.Destroy(tex);
        }
    }
}

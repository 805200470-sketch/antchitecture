using System;
using System.Collections;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Animation check (-animcheck &lt;dir&gt;): the player ant walks, runs and turns along a canyon while the
    /// camera watches from the side and from three-quarter front, close up. Every frame is saved at a fixed
    /// 30 fps game clock, so leg timing, foot sliding and body bounce can be studied frame by frame.
    /// The first two segments play the clips exactly as authored, at the speed they were made for (what the
    /// preview videos show), as the reference for the game's own walk. -species Ant_001,Ant_003 runs the
    /// whole check once per species (default: the player's), -proclegs 0 turns the procedural legs off.
    /// </summary>
    public static class AnimCheck
    {
        static IEnumerator Markers(Game game, GameObject[] marks)
        {
            while (true)
            {
                LegRig legs = game.player.Legs;
                for (int m = 0; m < 6; m++)
                {
                    bool on = legs != null && legs.Weight > 0;
                    marks[m].SetActive(on);
                    if (!on) continue;
                    marks[m].transform.position = legs.Foot(m);
                    marks[m].GetComponent<Renderer>().material.color = legs.Swinging(m) ? new Color(1f, 0.5f, 0.1f) : new Color(0.2f, 1f, 0.3f);
                }
                yield return null;
            }
        }

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            Application.targetFrameRate = -1;
            Time.captureFramerate = DemoMode.Fps;
            game.demo = true;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            game.SetRingVisible(false);
            // the other ants step aside: hidden, and no longer updated (so nothing shows them again)
            foreach (NpcAnt n in game.npcs) n.ant.SetVisible(false);
            game.npcs.Clear();
            // markers on the six foot targets: green planted, orange in the air
            var marks = new GameObject[6];
            for (int m = 0; m < 6; m++)
            {
                marks[m] = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                UnityEngine.Object.Destroy(marks[m].GetComponent<Collider>());
                marks[m].transform.localScale = Vector3.one * 0.07f;
            }
            if (Game.Arg("-markers") != null) game.StartCoroutine(Markers(game, marks)); // foot targets, for debugging
            else foreach (var m in marks) m.SetActive(false);
            yield return new WaitForSeconds(1.5f);

            WEdge e = DemoMode.LongEdge(game, 0);
            if (e == null) { Application.Quit(); yield break; }
            int i = e.pts.Count / 3;
            int frame = 0;

            AntController ant = game.player;
            string species = game.cfg.ant.playerSpecies;
            string[] list = (Game.Arg("-species") ?? species).Split(new[] { ',' }, StringSplitOptions.RemoveEmptyEntries);
            IEnumerator Segment(string name, float seconds, float yawOffset, float distance, float pitch, float speed, bool fast, float turn,
                                bool authored = false, AntController.Clip? clip = null)
            {
                DemoMode.PlaceFacing(game, e.pts[i], e.pts[Mathf.Min(e.pts.Count - 1, i + 4)]);
                game.rig.free = true;
                game.rig.distance = distance;
                game.rig.pitch = pitch;
                ant.authoredPlayback = authored;
                ant.clipOverride = clip;
                bool logged = false;
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    float h = game.player.heading + turn * Time.deltaTime;
                    game.demoMove = new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * speed;
                    game.demoFast = fast;
                    yield return null;
                    // the camera keeps a fixed angle to the ant (no auto swing behind it)
                    game.rig.yaw = game.player.heading + Mathf.PI + yawOffset;
                    yield return new WaitForEndOfFrame();
                    Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                    File.WriteAllBytes(Path.Combine(dir, $"{species}_{name}_{frame++:0000}.jpg"), tex.EncodeToJPG(90));
                    UnityEngine.Object.Destroy(tex);
                    if (!logged && t > seconds * 0.6f)
                    {
                        Debug.Log($"[AntGarden] animcheck {species} {name}: {ant.AnimInfo()}");
                        logged = true;
                    }
                }
                game.demoMove = Vector2.zero;
                game.demoFast = false;
                ant.authoredPlayback = false;
                ant.clipOverride = null;
                frame = 0;
            }

            foreach (string sp in list)
            {
                if (sp != species) game.SwapPlayerSpecies(sp);
                ant = game.player;
                species = sp;
                yield return new WaitForSeconds(0.5f);
                // reference: the clips as authored, at their own speed (the preview videos)
                float crawlRef = ant.crawlCycleSpeed / game.cfg.ant.crawlSpeed, fastRef = ant.fastCycleSpeed / game.cfg.ant.crawlSpeed;
                yield return Segment("r1_authored_crawl", 4f, Mathf.PI / 2, 2.4f, 0.08f, crawlRef, false, 0, true);
                yield return Segment("r2_authored_fast", 3f, Mathf.PI / 2, 2.4f, 0.08f, fastRef, false, 0, true, AntController.Clip.Fast);
                // the game
                yield return Segment("a_walk_side", 3f, Mathf.PI / 2, 2.4f, 0.08f, 1f, false, 0);
                yield return Segment("c_run_side", 2.5f, Mathf.PI / 2, 3.2f, 0.1f, 1f, true, 0);
                yield return Segment("d_slow_side", 3f, Mathf.PI / 2, 2.4f, 0.08f, 0.35f, false, 0);
                yield return Segment("b_walk_front", 3f, Mathf.PI * 0.8f, 2.8f, 0.2f, 1f, false, 0);
                yield return Segment("e_circle", 3f, Mathf.PI * 0.75f, 2.6f, 0.35f, 0.6f, false, 1.4f);
            }
            File.WriteAllText(Path.Combine(dir, "done.txt"), "ok\n");
            Application.Quit();
        }
    }
}

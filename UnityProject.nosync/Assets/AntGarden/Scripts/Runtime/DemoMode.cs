using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Scripted demo recording (-demo &lt;dir&gt;): a guided tour of the features with captions. The game clock
    /// runs at a fixed 30 fps (Time.captureFramerate) and every frame is saved as a JPEG, so the video plays
    /// smoothly however long each frame takes to render. Tools/frames_to_video.sh (Blender) makes the MP4.
    /// </summary>
    public static class DemoMode
    {
        public const int Fps = 30;
        static bool recording;
        static int frames;

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            Application.targetFrameRate = -1;
            Time.captureFramerate = Fps;
            game.demo = true;
            game.hud.SetHelpVisible(false);
            game.hud.SetStatsVisible(false);
            game.hud.SetFade(1, true);
            yield return new WaitForSeconds(1.5f); // shaders compile, NPCs spread out
            recording = true;
            game.StartCoroutine(Capture(dir));

            var tour = new List<Func<Game, IEnumerator>>
            {
                Opening, Crawl, Sprint, LookAround, MeetAnt, StorySlot, SeamGate, MapAndPortal, AmberNest, AmberChamber, PortalHome, Ending,
            };
            foreach (var shot in tour) yield return shot(game);

            recording = false;
            yield return null;
            yield return null;
            File.WriteAllText(Path.Combine(dir, "demo_done.txt"), $"{frames} frames at {Fps} fps\n");
            Debug.Log($"[AntGarden] demo recorded: {frames} frames");
            Application.Quit();
        }

        static IEnumerator Capture(string dir)
        {
            var end = new WaitForEndOfFrame();
            while (recording)
            {
                yield return end;
                if (!recording) break;
                Texture2D tex = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, $"f{frames:00000}.jpg"), tex.EncodeToJPG(92));
                UnityEngine.Object.Destroy(tex);
                frames++;
            }
        }

        // ---------- helpers ----------

        /// <summary>Run <paramref name="each"/> every frame for a number of seconds (argument: 0..1 progress).</summary>
        static IEnumerator For(float seconds, Action<float> each = null)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                each?.Invoke(t / seconds);
                yield return null;
            }
        }

        static IEnumerator Cut(Game game, Action setup)
        {
            game.rig.free = false;
            game.demoMove = Vector2.zero;
            game.demoFast = false;
            game.hud.SetCaption("");
            game.hud.SetFade(1);
            yield return For(0.4f);
            setup();
            yield return null;
            game.hud.SetFade(0);
        }

        internal static void Shot(Game game, float distance, float pitch, bool free = false)
        {
            game.rig.free = free;
            game.rig.SnapBehind(game.player.heading);
            game.rig.distance = distance;
            game.rig.pitch = pitch;
        }

        internal static void PlaceFacing(Game game, Vector2 at, Vector2 target)
        {
            if (!game.world.SnapToWalkable(at.x, at.y, 0.9f, out Vector2 p)) p = at;
            Vector2 d = target - p;
            game.player.Place(p.x, p.y, Mathf.Atan2(d.x, d.y));
        }

        internal static Vector2 Pos(Game game) => new Vector2(game.player.Position.x, game.player.Position.z);

        static Vector2 Steer(Game game, Vector2 target)
        {
            Vector2 d = target - Pos(game);
            return d.sqrMagnitude < 0.04f ? Vector2.zero : d.normalized;
        }

        /// <summary>A long canyon edge in a region, to crawl along.</summary>
        internal static WEdge LongEdge(Game game, int region)
        {
            WEdge best = null;
            foreach (WEdge e in game.world.edges)
                if (e.region == region && !e.connector && (best == null || e.pts.Count > best.pts.Count)) best = e;
            return best;
        }

        // ---------- the tour ----------

        static IEnumerator Opening(Game game)
        {
            WEdge e = LongEdge(game, 0);
            if (e != null) PlaceFacing(game, e.pts[e.pts.Count / 4], e.pts[e.pts.Count / 4 + 3]);
            Shot(game, 13f, 0.48f, true);
            game.hud.SetFade(0);
            game.hud.SetCaption(Loc.T("<b>Ant Garden · 蚂蚁花园</b>\n<size=22>由真实蚁穴的路径生成的峡谷世界</size>", "<b>Ant Garden</b>\n<size=22>A canyon world grown from the tunnels of real ant nests</size>"));
            yield return For(6f, k => game.rig.yaw += Time.deltaTime * 0.12f);
        }

        static IEnumerator Crawl(Game game)
        {
            WEdge e = LongEdge(game, 0);
            if (e == null) yield break;
            int i = e.pts.Count / 4;
            game.rig.free = false;
            game.rig.distance = 3.8f;
            game.rig.pitch = 0.2f;
            game.hud.SetCaption(Loc.T("WASD 爬行：峡谷沿着蚁穴的路径延伸，地面有真实的起伏", "WASD to crawl · the canyon follows the nest's tunnels, and the ground rolls"));
            yield return For(8f, k =>
            {
                while (i < e.pts.Count - 1 && Vector2.Distance(e.pts[i], Pos(game)) < 2.2f) i++;
                game.demoMove = Steer(game, e.pts[i]);
                game.rig.distance = Mathf.Lerp(game.rig.distance, 3.8f, Time.deltaTime);
                game.rig.pitch = Mathf.Lerp(game.rig.pitch, 0.2f, Time.deltaTime);
            });
        }

        static IEnumerator Sprint(Game game)
        {
            WEdge e = LongEdge(game, 0);
            if (e == null) yield break;
            int i = 0;
            float best = float.MaxValue;
            for (int k = 0; k < e.pts.Count; k++)
            {
                float d = Vector2.Distance(e.pts[k], Pos(game));
                if (d < best) { best = d; i = k; }
            }
            game.hud.SetCaption(Loc.T("按住 Shift 快爬", "Hold Shift to run"));
            yield return For(6f, k =>
            {
                while (i < e.pts.Count - 1 && Vector2.Distance(e.pts[i], Pos(game)) < 3f) i++;
                game.demoMove = Steer(game, e.pts[i]);
                game.demoFast = true;
            });
            game.demoFast = false;
            game.demoMove = Vector2.zero;
        }

        static IEnumerator LookAround(Game game)
        {
            game.hud.SetCaption(Loc.T("鼠标拖动转镜头 · 滚轮拉近拉远 · 抬头能看到菌林的穹顶", "Drag to look around · scroll to zoom · look up into the fungal canopy"));
            float yaw0 = game.rig.yaw;
            yield return For(7f, k =>
            {
                game.demoMove = Vector2.zero;
                game.rig.yaw = yaw0 + Mathf.SmoothStep(0, 1, k) * Mathf.PI * 1.1f;
                game.rig.pitch = Mathf.Lerp(0.25f, -0.32f, Mathf.SmoothStep(0, 1, Mathf.InverseLerp(0.45f, 1f, k)));
                game.rig.distance = Mathf.Lerp(5f, 3.2f, k);
            });
        }

        static IEnumerator MeetAnt(Game game)
        {
            NpcAnt target = null;
            foreach (NpcAnt n in game.npcs)
                if (game.world.RegionAt(n.ant.Position.x, n.ant.Position.z)?.index == 0 && n.ant.altitude < 0.1f) { target = n; break; }
            WEdge e = LongEdge(game, 0);
            if (target == null || e == null) yield break;
            yield return Cut(game, () =>
            {
                // stand on the canyon floor and bring the other ant to just in front, facing us
                int i = e.pts.Count * 3 / 5;
                PlaceFacing(game, e.pts[i], e.pts[Mathf.Min(e.pts.Count - 1, i + 3)]);
                float h = game.player.heading;
                Vector2 front = Pos(game) + new Vector2(Mathf.Sin(h), Mathf.Cos(h)) * 1.8f;
                target.ant.Place(front.x, front.y, h + Mathf.PI);
                Shot(game, 3.6f, 0.24f);
                target.Greet(game.player);
            });
            game.hud.SetCaption(Loc.T("靠近其他蚂蚁按 E 碰触角打招呼 · 5 种蚂蚁，颜色各不相同", "Press E beside another ant to touch antennae · five species, five colours"));
            bool greeted = false;
            yield return For(6f, k =>
            {
                game.demoMove = Vector2.zero;
                if (!greeted && k > 0.2f) { game.Interact(); greeted = true; }
                if (k > 0.55f) game.rig.yaw += Time.deltaTime * 0.35f; // swing round to see both faces
            });
        }

        static IEnumerator StorySlot(Game game)
        {
            StorySlot slot = null;
            foreach (StorySlot s in game.world.slots)
                if (game.world.RegionAt(s.position.x, s.position.z)?.index == 0) { slot = s; break; }
            if (slot == null) yield break;
            Vector2 sp = new Vector2(slot.position.x, slot.position.z);
            yield return Cut(game, () =>
            {
                Vector2 from = sp + new Vector2(4.5f, 0);
                if (game.world.SnapToWalkable(sp.x + 4.5f, sp.y, 0.9f, out Vector2 p)) from = p;
                PlaceFacing(game, from, sp);
                Shot(game, 4.2f, 0.3f);
            });
            game.hud.SetCaption(Loc.T("发光的圆环是故事物件位：以后会放用户上传的照片和故事", "Glowing rings are story slots for visitors' photos and stories"));
            bool opened = false;
            yield return For(7f, k =>
            {
                float d = Vector2.Distance(sp, Pos(game));
                game.demoMove = d > 1.2f && !opened ? Steer(game, sp) : Vector2.zero;
                if (d < 1.8f && !opened && k > 0.25f) { game.OpenSlot(slot); opened = true; }
            });
            game.hud.HideSlot();
        }

        static IEnumerator SeamGate(Game game)
        {
            Gate gate = game.gates.Find(g => !g.portal);
            if (gate == null) yield break;
            Vector2 gp = new Vector2(gate.position.x, gate.position.z);
            yield return Cut(game, () =>
            {
                PlaceFacing(game, gp - gate.dir * 9f, gp);
                Shot(game, 4.6f, 0.16f);
            });
            game.hud.SetCaption(Loc.T("相连的两个蚁穴之间有一道光门，穿过去就进入下一个蚁穴", "A light gate marks the seam between two connected nests"));
            yield return For(7f, k => game.demoMove = Steer(game, gp + gate.dir * 8f));
        }

        static IEnumerator MapAndPortal(Game game)
        {
            Gate arrival = game.gates.Find(g => g.portal && g.region > 0);
            yield return Cut(game, () => game.bigmap.Toggle(true));
            game.hud.SetCaption(Loc.T("按 M 打开地图：点任意位置即可传送 · 右边是另一个独立的琥珀蚁穴", "Press M for the map · click anywhere to teleport · right: a separate amber nest"));
            yield return For(5f);
            if (arrival == null) { game.bigmap.Toggle(false); yield break; }
            game.hud.SetCaption(Loc.T("点地图上的「光门」按钮，或者走到传送光门前按 E，就能去另一个蚁穴", "A gate button on the map, or E at a portal gate, takes you across"));
            yield return For(2.5f);
            game.TeleportToGate(arrival);
            game.rig.distance = 4.4f;
            game.rig.pitch = 0.2f;
            yield return For(3.5f); // the mood blends from blue to amber
        }

        static IEnumerator AmberNest(Game game)
        {
            WEdge e = LongEdge(game, 1);
            if (e == null) yield break;
            game.hud.SetCaption(Loc.T("琥珀蚁穴：另一种风格 —— 地面、植物、雾和蚂蚁都换成了暖色", "The amber nest: ground, plants, fog and ants all turn warm"));
            int i = 0;
            float best = float.MaxValue;
            for (int k = 0; k < e.pts.Count; k++)
            {
                float d = Vector2.Distance(e.pts[k], Pos(game));
                if (d < best) { best = d; i = k; }
            }
            int dir = i < e.pts.Count / 2 ? 1 : -1;
            yield return For(8f, k =>
            {
                while (i > 0 && i < e.pts.Count - 1 && Vector2.Distance(e.pts[i], Pos(game)) < 2.4f) i += dir;
                game.demoMove = Steer(game, e.pts[i]);
            });
        }

        static IEnumerator AmberChamber(Game game)
        {
            WNode chamber = game.world.nodes.Find(n => n.region == 1 && n.degree >= 3 && n.inside);
            if (chamber == null) yield break;
            yield return Cut(game, () =>
            {
                if (game.world.SnapToWalkable(chamber.x, chamber.z, 0.9f, out Vector2 p)) game.player.Place(p.x, p.y, game.player.heading);
                Shot(game, 22f, 0.95f, true); // high enough to clear the mushroom caps
            });
            game.hud.SetCaption(Loc.T("新素材：干枯巨菇、土塔蚁丘和发光的琥珀树脂，只长在琥珀蚁穴里", "Its own assets: dried giant mushrooms, termite spires, glowing amber resin"));
            float yaw0 = game.rig.yaw;
            yield return For(7f, k =>
            {
                game.demoMove = Vector2.zero;
                game.rig.yaw = yaw0 + k * 1.2f;
            });
        }

        static IEnumerator PortalHome(Game game)
        {
            Gate portal = game.gates.Find(g => g.portal && g.region > 0);
            if (portal == null || portal.pair == null) yield break;
            Vector2 gp = new Vector2(portal.position.x, portal.position.z);
            yield return Cut(game, () =>
            {
                PlaceFacing(game, gp - portal.dir * 9f, gp);
                Shot(game, 5f, 0.18f);
            });
            game.hud.SetCaption(Loc.T("走到传送光门前按 E，回到蓝色蚁穴", "Walk up to the portal gate and press E to return to the blue nests"));
            bool used = false;
            yield return For(6f, k =>
            {
                float d = Vector2.Distance(gp, Pos(game));
                game.demoMove = used || d < 3f ? Vector2.zero : Steer(game, gp);
                if (!used && d < 3.4f && k > 0.4f)
                {
                    game.Interact();
                    used = true;
                    game.rig.distance = 4.4f;
                }
            });
            yield return For(2.5f);
        }

        static IEnumerator Ending(Game game)
        {
            yield return Cut(game, () =>
            {
                // back where the tour began, for a closing orbit over the blue canyon
                WEdge e = LongEdge(game, 0);
                if (e != null) PlaceFacing(game, e.pts[e.pts.Count / 4], e.pts[e.pts.Count / 4 + 3]);
                Shot(game, 13f, 0.48f, true);
            });
            game.hud.SetCaption(Loc.T("<b>Ant Garden · 蚂蚁花园</b>\n<size=22>Unity 演示版 · 3 个相连的蚁穴 + 1 个独立的琥珀蚁穴</size>", "<b>Ant Garden</b>\n<size=22>Unity demo · three connected nests + one separate amber nest</size>"));
            yield return For(5f, k => game.rig.yaw += Time.deltaTime * 0.1f);
            game.hud.SetCaption("");
            game.hud.SetFade(1);
            yield return For(1f);
        }
    }
}

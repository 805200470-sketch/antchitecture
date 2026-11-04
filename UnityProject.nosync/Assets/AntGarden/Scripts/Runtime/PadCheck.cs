using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// Pad check (-padcheck &lt;dir&gt;): plugs in a virtual game pad and plays with it the way a person would: walks on
    /// the left stick, runs with RT, turns the camera with the right stick, zooms with LB, opens the map with Y,
    /// moves its cursor and closes it with B, touches antennae with an ant on A, reaches out with B. Each step is
    /// checked against what it should have done; writes pad_report.json and a screenshot, then quits. With -tutorial 1 it
    /// walks the tutorial instead (WalkTutorial).
    /// </summary>
    public static class PadCheck
    {
        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            if (Tutorial.Instance != null) { yield return WalkTutorial(game, dir); yield break; }
            yield return new WaitForSeconds(1.5f);
            // read with the window behind too (else every pad check fails when someone works in another window, 11-03), the
            // machine's own mice and keyboards left out (read in the background, they would turn the prompts to the keys)
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (InputDevice d in new List<InputDevice>(InputSystem.devices))
                if (d is Mouse || d is Keyboard) InputSystem.DisableDevice(d);
            Gamepad pad = InputSystem.AddDevice<Gamepad>("VirtualPad");
            var report = new StringBuilder("{\n  \"checks\": [\n");
            int failed = 0, count = 0;
            void Check(string name, bool ok, string detail)
            {
                report.Append(count++ > 0 ? ",\n" : "").Append($"    {{ \"name\": \"{name}\", \"ok\": {(ok ? "true" : "false")}, \"detail\": \"{detail}\" }}");
                if (!ok) failed++;
            }
            IEnumerator Hold(GamepadState st, float seconds)
            {
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    InputSystem.QueueStateEvent(pad, st);
                    yield return null;
                }
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
            }
            IEnumerator Press(GamepadButton b)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b, true));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                yield return null;
            }

            AntController ant = game.player;
            WEdge e = DemoMode.LongEdge(game, 0);
            int i0 = Mathf.Clamp(e.pts.Count / 3, 0, e.pts.Count - 4);
            DemoMode.PlaceFacing(game, e.pts[i0], e.pts[i0 + 3]);
            game.rig.SnapBehind(ant.heading);
            yield return Hold(new GamepadState(), 0.5f);

            Vector3 p0 = ant.Position;
            yield return Hold(new GamepadState { leftStick = new Vector2(0, 1) }, 1.5f);
            float walked = Vector3.Distance(p0, ant.Position);
            Check("left_stick_walks", walked > 0.6f, $"moved {walked:F2}");

            float topSpeed = 0;
            bool sprinted = false;
            var run = new GamepadState { leftStick = new Vector2(0, 1), rightTrigger = 1 };
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                InputSystem.QueueStateEvent(pad, run);
                yield return null;
                topSpeed = Mathf.Max(topSpeed, ant.speed);
                sprinted |= ant.sprinting;
            }
            yield return Hold(new GamepadState(), 0.6f);
            Check("rt_runs", sprinted && topSpeed > ant.cfg.crawlSpeed * 1.2f, $"top speed {topSpeed:F2}, crawl {ant.cfg.crawlSpeed:F2}");
            Check("release_stops", ant.speed < 0.1f, $"speed {ant.speed:F2}");

            float half = 0;
            p0 = ant.Position;
            for (float t = 0; t < 1.5f; t += Time.deltaTime)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState { leftStick = new Vector2(0, 0.45f) });
                yield return null;
                half = Mathf.Max(half, ant.speed);
            }
            yield return Hold(new GamepadState(), 0.4f);
            Check("gentle_tilt_walks_slowly", half > 0.05f && half < ant.cfg.crawlSpeed * 0.8f, $"speed {half:F2} at 45% tilt");

            float yaw0 = game.rig.yaw;
            yield return Hold(new GamepadState { rightStick = new Vector2(1, 0) }, 1f);
            float turned = Mathf.Abs(Mathf.DeltaAngle(yaw0 * Mathf.Rad2Deg, game.rig.yaw * Mathf.Rad2Deg));
            Check("right_stick_turns_camera", turned > 60f, $"turned {turned:F0} deg");

            float d0 = game.rig.distance;
            yield return Hold(new GamepadState().WithButton(GamepadButton.LeftShoulder, true), 0.6f);
            Check("lb_zooms_out", game.rig.distance > d0 * 1.3f, $"{d0:F2} -> {game.rig.distance:F2}");
            yield return Hold(new GamepadState().WithButton(GamepadButton.RightShoulder, true), 0.6f);
            Check("rb_zooms_in", game.rig.distance < d0 * 1.3f, $"back to {game.rig.distance:F2}");

            yield return Press(GamepadButton.North);
            Check("y_opens_map", game.bigmap.IsOpen, "");
            yield return Hold(new GamepadState { leftStick = new Vector2(1, 0.3f) }, 0.6f);
            yield return new WaitForEndOfFrame();
            Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, "map_with_pad_cursor.jpg"), shot.EncodeToJPG(85));
            Object.Destroy(shot);
            yield return Press(GamepadButton.East);
            Check("b_closes_map", !game.bigmap.IsOpen, "");
            Check("prompts_name_pad_buttons", Pad.LastUsed, "");

            // A beside an ant: antennae touch
            NpcAnt friend = null;
            foreach (NpcAnt n in game.npcs)
                if (n.region == 0 && n.ant.altitude < 0.05f && (friend == null || (n.ant.Position - ant.Position).sqrMagnitude < (friend.ant.Position - ant.Position).sqrMagnitude)) friend = n;
            if (friend != null)
            {
                Vector3 f = friend.ant.Position;
                Vector3 back = new Vector3(Mathf.Sin(friend.ant.heading), 0, Mathf.Cos(friend.ant.heading));
                ant.Place(f.x + back.x * 1.3f, f.z + back.z * 1.3f, Mathf.Atan2(-back.x, -back.z));
                game.rig.SnapBehind(ant.heading);
                yield return null;
                yield return null;
                yield return Press(GamepadButton.South);
                Check("a_touches_antennae", friend.StateName == "Greet" || ant.Talking, $"npc state {friend.StateName}, player talking {ant.Talking}");
            }
            else Check("a_touches_antennae", false, "no ant nearby");

            yield return Hold(new GamepadState(), 2.6f);   // the touch finishes
            ant.Place(ant.Position.x, ant.Position.z, ant.heading);
            yield return Press(GamepadButton.East);
            Check("b_reaches_out", ant.Talking, "");

            yield return new WaitForEndOfFrame();
            shot = ScreenCapture.CaptureScreenshotAsTexture();
            File.WriteAllBytes(Path.Combine(dir, "after_pad.jpg"), shot.EncodeToJPG(85));
            Object.Destroy(shot);
            Check("tutorial_off_in_check_runs", Tutorial.Instance == null && !Tutorial.Active, "");   // (unless -tutorial 1)
#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
            yield return MacWiredXbox(Check);   // the wired Xbox pad as macOS shows it (Pad.WiredXboxOnMac), 11-03
#endif
            report.Append($"\n  ],\n  \"failed\": {failed}\n}}\n");
            File.WriteAllText(Path.Combine(dir, "pad_report.json"), report.ToString());
            Debug.Log("[AntGarden] padcheck " + report.ToString().Replace("\n", " "));
            InputSystem.RemoveDevice(pad);
            Application.Quit();
        }

        /// <summary>
        /// -padcheck &lt;dir&gt; -tutorial 1: the tutorial (Tutorial) walked through with the virtual pad as a visitor would, a
        /// still at each step (教程_*.jpg: in Chinese, one after a key is touched, named for the keyboard; X turns it to
        /// English), then the rest of the screen switched with X (英文_*.jpg: the chapter's card and goal, the help, the
        /// map, a spoken line, a story slot's panel, each also switched back). With -tutorialskip: only a short ☰ (the help)
        /// and ☰ held a second (skipped, the chapter follows). With -controls (11-04): ⧉ puts the controls top left away at
        /// the first step and the tutorial goes on without them, ⧉ brings them back after the second, they name the keys once
        /// a key is touched, and they fold away by themselves when the tutorial ends, done or skipped. Writes
        /// tutorial_report.json, then quits.
        /// </summary>
        static IEnumerator WalkTutorial(Game game, string dir)
        {
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;   // (a check's window may be behind)
            // the machine's own mice and keyboards left out: read in the background too, a hand on the trackpad turned the
            // prompts to the keys mid-check (11-03)
            foreach (InputDevice d in new List<InputDevice>(InputSystem.devices))
                if (d is Mouse || d is Keyboard) InputSystem.DisableDevice(d);
            Gamepad pad = InputSystem.AddDevice<Gamepad>("VirtualPad");
            string langPref = PlayerPrefs.GetString("antgarden.lang", "zh");   // (Loc.Toggle saves it: put back at the end)
            var report = new StringBuilder("{\n  \"checks\": [\n");
            int failed = 0, count = 0;
            void Check(string name, bool ok, string detail)
            {
                detail = (detail ?? "").Replace("\"", "'").Replace("\n", " ");
                report.Append(count++ > 0 ? ",\n" : "").Append($"    {{ \"name\": \"{name}\", \"ok\": {(ok ? "true" : "false")}, \"detail\": \"{detail}\" }}");
                if (!ok) failed++;
            }
            IEnumerator Hold(GamepadState st, float seconds)
            {
                for (float t = 0; t < seconds; t += Time.deltaTime)
                {
                    InputSystem.QueueStateEvent(pad, st);
                    yield return null;
                }
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
            }
            IEnumerator HoldUntil(GamepadState st, System.Func<bool> until, float most)
            {
                for (float t = 0; t < most && !until(); t += Time.deltaTime)
                {
                    InputSystem.QueueStateEvent(pad, st);
                    yield return null;
                }
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
            }
            IEnumerator Press(GamepadButton b)
            {
                InputSystem.QueueStateEvent(pad, new GamepadState().WithButton(b, true));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(pad, new GamepadState());
                yield return null;
                yield return null;
            }
            IEnumerator Until(System.Func<bool> ok, float most)
            {
                for (float t = 0; t < most && !ok(); t += Time.deltaTime) yield return null;
            }
            IEnumerator Still(string name)
            {
                yield return new WaitForEndOfFrame();
                Texture2D shot = ScreenCapture.CaptureScreenshotAsTexture();
                File.WriteAllBytes(Path.Combine(dir, name + ".jpg"), shot.EncodeToJPG(90));
                Object.Destroy(shot);
                Debug.Log($"[AntGarden] tutorialcheck still {name}");
            }
            Transform canvas = game.hud.canvas.transform;
            string TextOf(string path) { Transform t = canvas.Find(path); Text x = t != null ? t.GetComponent<Text>() : null; return x != null ? x.text : ""; }
            bool Visible(string path) { Transform t = canvas.Find(path); return t != null && t.gameObject.activeInHierarchy; }
            void Finish()
            {
                report.Append($"\n  ],\n  \"failed\": {failed}\n}}\n");
                File.WriteAllText(Path.Combine(dir, "tutorial_report.json"), report.ToString());
                Debug.Log("[AntGarden] tutorialcheck " + report.ToString().Replace("\n", " "));
                PlayerPrefs.SetString("antgarden.lang", langPref);
                PlayerPrefs.Save();
                InputSystem.RemoveDevice(pad);
                Application.Quit();
            }

            Tutorial tut = Tutorial.Instance;
            AntController ant = game.player;
            bool Up(int s) => tut.StepShown == s && tut.Showing;
            // the way ahead: the nest's glowing track (elsewhere a long canyon), to stand on facing along it
            List<Vector2> way = game.LabNest?.Track;
            if (way == null || way.Count < 12) way = DemoMode.LongEdge(game, 0).pts;
            int Nearest()
            {
                int n = 0;
                var p = new Vector2(ant.Position.x, ant.Position.z);
                for (int i = 1; i < way.Count; i++) if ((way[i] - p).sqrMagnitude < (way[n] - p).sqrMagnitude) n = i;
                return n;
            }
            void Ahead(int from)
            {
                int i = Mathf.Clamp(from, 0, way.Count - 7);
                DemoMode.PlaceFacing(game, way[i], way[i + 6]);
                game.rig.SnapBehind(ant.heading);
            }

            yield return Until(() => Up(0), 15f);
            yield return Hold(new GamepadState { rightStick = new Vector2(0.4f, 0f) }, 0.05f);   // the pad in hand
            Ahead(4);
            yield return Hold(new GamepadState(), 1.2f);   // the camera settles behind the ant

            if (Game.Arg("-tutorialskip") != null)
            {
                yield return Press(GamepadButton.Start);   // a short ☰: the help, as always
                yield return Hold(new GamepadState(), 0.4f);
                Check("short_press_opens_help", Visible("Help") && Tutorial.Active, "");
                yield return Still("教程_短按菜单键_说明");
                yield return Press(GamepadButton.Start);
                yield return Hold(new GamepadState(), 0.4f);
                Check("short_press_again_closes_it", !Visible("Help"), "");
                var start = new GamepadState().WithButton(GamepadButton.Start, true);
                for (float t = 0; t < 0.6f; t += Time.deltaTime) { InputSystem.QueueStateEvent(pad, start); yield return null; }
                InputSystem.QueueStateEvent(pad, start);
                yield return Still("教程_按住菜单键");   // the thin line filling under the words
                Check("not_skipped_yet", Tutorial.Active, "");
                yield return Hold(start, 0.8f);
                Check("held_a_second_skips", tut.Finished && tut.Skipped && !Tutorial.Active, "");
                Check("help_stays_shut", !Visible("Help"), "");
                yield return Until(() => Visible("Objective"), 4f);
                Check("chapter_follows", Visible("Objective"), TextOf("Objective/ObjectiveText"));
                Check("controls_fold_away_when_skipped", !Visible("Controls") && !game.hud.ControlsOpen, "");   // (11-04)
                yield return Hold(new GamepadState(), 0.7f);
                yield return Still("教程_跳过后_第一章");
                Finish();
                yield break;
            }

            yield return Hold(new GamepadState { rightStick = new Vector2(0.4f, 0f) }, 0.05f);   // (the pad in hand, at the still)
            Check("crawl_first", Up(0), tut.Words);
            Check("names_the_pad", tut.Words.Contains("左摇杆"), tut.Words);
            Check("chapter_waits", !Visible("Objective"), "");
            Check("controls_show_x", TextOf("Controls/ControlsText").Contains("X"), "");
            yield return Still("教程_1_爬行");
            // (11-04) ⧉ puts the controls top left away; the tutorial goes on without them, and ⧉ brings them back after its
            // second step
            yield return Press(GamepadButton.Select);
            yield return Hold(new GamepadState(), 0.3f);
            Check("view_puts_the_controls_away", !Visible("Controls") && Up(0), tut.Words);
            yield return Still("教程_1_爬行_操作收起");
            yield return HoldUntil(new GamepadState { leftStick = new Vector2(0f, 1f) }, () => tut.Done, 8f);
            Check("left_stick_ticks_it", tut.Done, tut.Words);
            yield return Still("教程_1_爬行_完成");
            yield return Until(() => Up(1), 4f);
            yield return Still("教程_2_转镜头");
            yield return HoldUntil(new GamepadState { rightStick = new Vector2(1f, 0f) }, () => tut.Done, 5f);
            Check("right_stick_ticks_it", tut.Done, tut.Words);
            yield return Press(GamepadButton.Select);
            Check("view_brings_the_controls_back", Visible("Controls"), "");
            yield return Until(() => Up(2), 4f);
            Ahead(Nearest());   // facing along the way again
            yield return Hold(new GamepadState(), 0.8f);
            yield return Still("教程_3_快爬");
            // a key touched: the same step names the keyboard's Shift
            Keyboard kb = InputSystem.AddDevice<Keyboard>("VirtualKeyboard");
            InputSystem.QueueStateEvent(kb, new KeyboardState(Key.RightCtrl));
            yield return null;
            yield return null;
            InputSystem.QueueStateEvent(kb, new KeyboardState());
            yield return null;
            yield return null;
            Check("keyboard_named_after_a_key", tut.Words.Contains("Shift"), tut.Words);
            Check("controls_name_the_keys_too", TextOf("Controls/ControlsText").Contains("W A S D"), "");   // (11-04)
            yield return Still("教程_3_快爬_键盘");
            InputSystem.RemoveDevice(kb);
            yield return HoldUntil(new GamepadState { leftStick = new Vector2(0f, 1f), rightTrigger = 1f }, () => tut.Done, 6f);
            Check("rt_ticks_it", tut.Done, $"sprinting {ant.sprinting}");
            yield return Hold(new GamepadState(), 1f);   // stops
            yield return Until(() => Up(3), 4f);
            Check("pad_named_again", tut.Words.Contains("B"), tut.Words);
            yield return Until(() => ant.speed < 0.1f, 3f);
            yield return Still("教程_4_触角");
            yield return Press(GamepadButton.East);
            yield return Until(() => tut.Done, 2f);
            Check("b_ticks_it", tut.Done, $"talking {ant.Talking}");
            yield return Until(() => Up(4), 4f);
            yield return Still("教程_5_地图");
            yield return Press(GamepadButton.North);
            yield return Until(() => Up(5), 4f);
            Check("y_ticks_it", game.bigmap.IsOpen && tut.StepShown == 5, "");
            yield return Still("教程_6_关地图");
            yield return Press(GamepadButton.East);
            yield return Until(() => Up(6), 4f);
            Check("b_closed_it", !game.bigmap.IsOpen && tut.StepShown == 6, "");
            yield return Still("教程_7_换语言");
            yield return Press(GamepadButton.West);
            Check("x_switches_to_english", Loc.En, "");
            yield return Until(() => tut.Done, 1f);
            yield return Still("英文_教程_7_换语言_完成");
            yield return Until(() => Up(7), 4f);
            Check("hand_over_in_english", tut.Words.Contains("Follow"), tut.Words);
            yield return Still("英文_教程_8_跟着光");
            yield return Until(() => tut.Finished, 10f);
            Check("finishes", tut.Finished && !tut.Skipped, "");
            yield return Until(() => Visible("Objective"), 4f);
            yield return Hold(new GamepadState(), 0.7f);
            Check("chapter_after_it", TextOf("Objective/ObjectiveText").Contains("Follow"), TextOf("Objective/ObjectiveText"));
            Check("controls_fold_away_after_it", !Visible("Controls") && !game.hud.ControlsOpen, "");   // (11-04)
            yield return Still("英文_第一章");
            // X while the chapter's card shows: written again in place (not replaced by the nest's banner)
            yield return Press(GamepadButton.West);
            yield return Hold(new GamepadState(), 0.3f);
            Check("chapter_card_switches_in_place", TextOf("Region/RegionTitle") == "第一章 · 入口" && TextOf("Objective/ObjectiveText").Contains("跟着"), TextOf("Region/RegionTitle"));
            yield return Still("英文_第一章_换回中文");
            yield return Press(GamepadButton.West);
            yield return Hold(new GamepadState(), 0.3f);
            // the full help (a short ☰), in English
            yield return Press(GamepadButton.Start);
            yield return Hold(new GamepadState(), 0.4f);
            Check("help_in_english", Visible("Help") && TextOf("Help/Title").Contains("X 中文"), "");
            Check("help_under_the_goal", ((RectTransform)canvas.Find("Help")).anchoredPosition.y < -60f, "");
            yield return Still("英文_说明");
            yield return Press(GamepadButton.Start);
            yield return Hold(new GamepadState(), 0.3f);
            yield return Press(GamepadButton.Select);   // (11-04: folded away when the tutorial ended; ⧉ brings them back)
            yield return Hold(new GamepadState(), 0.3f);
            Check("controls_in_english", Visible("Controls") && TextOf("Controls/ControlsText").Contains("Pad controls"), "");
            // the map, and X while it is open
            yield return Press(GamepadButton.North);
            yield return Hold(new GamepadState(), 0.5f);
            yield return Still("英文_地图");
            yield return Press(GamepadButton.West);
            yield return Hold(new GamepadState(), 0.5f);
            Check("map_switches_open", game.bigmap.IsOpen && !Loc.En, "");
            yield return Still("英文_地图_换回中文");
            yield return Press(GamepadButton.West);
            yield return Press(GamepadButton.East);
            yield return Hold(new GamepadState(), 0.3f);
            // a spoken line (a memory plant's story), and X while it shows
            var story = Voices.Stories[1];
            game.hud.ShowLine("留在这里的话 · 一位访客", "Left here · by a visitor", story.zh, story.en);
            yield return Hold(new GamepadState(), 0.7f);
            yield return Still("英文_一句话");
            yield return Press(GamepadButton.West);
            yield return Hold(new GamepadState(), 0.3f);
            Check("line_switches", TextOf("Line/LineText") == story.zh, TextOf("Line/LineText"));
            yield return Still("英文_一句话_换回中文");
            yield return Press(GamepadButton.West);
            yield return Until(() => !game.hud.LineShowing, 10f);   // the line fades
            yield return Hold(new GamepadState(), 1f);
            // a story slot's panel, open, and X
            StorySlot slot = null;
            foreach (StorySlot s in game.world.slots)
                if (slot == null || (s.position - ant.Position).sqrMagnitude < (slot.position - ant.Position).sqrMagnitude) slot = s;
            if (slot != null)
            {
                ant.Place(slot.position.x, slot.position.z, ant.heading);
                game.rig.SnapBehind(ant.heading);
                yield return Hold(new GamepadState(), 0.8f);
                game.OpenSlot(slot);
                yield return Hold(new GamepadState(), 0.4f);
                yield return Still("英文_物件位");
                yield return Press(GamepadButton.West);
                yield return Hold(new GamepadState(), 0.4f);
                Check("slot_switches_open", game.hud.SlotOpen && TextOf("SlotPanel/SlotText").Contains("故事物件位"), TextOf("SlotPanel/SlotText"));
                yield return Still("英文_物件位_换回中文");
            }
            else Check("slot_switches_open", false, "no story slot in this world");
            Finish();
        }

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        /// <summary>
        /// The wired Xbox pad as macOS shows it (Pad.WiredXboxOnMac, 11-03; the real one was not plugged in): a device
        /// described as macOS describes "GamePad-1" (HID, Microsoft's vendor id 1118, product 654) must become a gamepad
        /// with the layout made for it, and the pad's own 14-byte report pressing A, RT, the left stick right and
        /// forward, then the right stick forward (forward as the pad sends it: a positive number) must read so, as must
        /// the game's own Pad.Move and Pad.Look.
        /// </summary>
        static IEnumerator MacWiredXbox(System.Action<string, bool, string> check)
        {
            var desc = new UnityEngine.InputSystem.Layouts.InputDeviceDescription
            {
                interfaceName = "HID",
                product = "GamePad-1",
                manufacturer = "Microsoft",
                capabilities = "{\"vendorId\":1118,\"productId\":654,\"usagePage\":1,\"usage\":5}",
            };
            InputDevice dev = null;
            string err = "";
            try { dev = InputSystem.AddDevice(desc); }
            catch (System.Exception ex) { err = ex.Message.Replace("\"", "'"); }
            var gp = dev as Gamepad;
            check("mac_wired_xbox_is_a_gamepad", gp != null && dev.layout == Pad.NativeXbox, dev != null ? $"{dev.layout}, {dev.GetType().Name}" : "not added: " + err);
            if (gp == null)
            {
                if (dev != null) InputSystem.RemoveDevice(dev);
                yield break;
            }
            var press = new Xbox360Report { size = 0x14, buttons = 1 << 12, rt = 255, lx = 30000, ly = 30000 };
            for (int i = 0; i < 3; i++) { InputSystem.QueueStateEvent(gp, press); yield return null; }
            Vector2 ls = gp.leftStick.ReadValue(), move = Pad.Move;
            check("mac_wired_xbox_a", gp.buttonSouth.isPressed && !gp.buttonEast.isPressed, "");
            check("mac_wired_xbox_rt", gp.rightTrigger.ReadValue() > 0.9f && Pad.Run, $"rt {gp.rightTrigger.ReadValue():F2}");
            check("mac_wired_xbox_left_stick_right", ls.x > 0.5f, $"x {ls.x:F2}");
            check("mac_wired_xbox_left_stick_forward", ls.y > 0.5f && gp.leftStick.up.isPressed, $"y {ls.y:F2} for +30000 in the report");
            check("mac_wired_xbox_walks_forward", Gamepad.current == gp && move.x > 0.5f && move.y > 0.5f, $"Pad.Move ({move.x:F2}, {move.y:F2})");
            var look = new Xbox360Report { size = 0x14, ry = 30000 };
            for (int i = 0; i < 3; i++) { InputSystem.QueueStateEvent(gp, look); yield return null; }
            Vector2 rs = gp.rightStick.ReadValue(), lk = Pad.Look;
            check("mac_wired_xbox_right_stick_forward", rs.y > 0.5f && lk.y > 0.5f && Mathf.Abs(gp.leftStick.ReadValue().y) < 0.05f, $"y {rs.y:F2}, Pad.Look.y {lk.y:F2}");
            InputSystem.QueueStateEvent(gp, new Xbox360Report { size = 0x14 });
            yield return null;
            InputSystem.RemoveDevice(gp);
        }

        /// <summary>The Xbox 360 report, as the wired pad sends it (the first 14 of its 20 bytes): XboxGamepadMacOS's state.</summary>
        [System.Runtime.InteropServices.StructLayout(System.Runtime.InteropServices.LayoutKind.Explicit, Size = 14)]
        struct Xbox360Report : IInputStateTypeInfo
        {
            [System.Runtime.InteropServices.FieldOffset(0)] public byte command;
            [System.Runtime.InteropServices.FieldOffset(1)] public byte size;
            [System.Runtime.InteropServices.FieldOffset(2)] public ushort buttons;   // bit 12 A, 13 B, 14 X, 15 Y
            [System.Runtime.InteropServices.FieldOffset(4)] public byte lt;
            [System.Runtime.InteropServices.FieldOffset(5)] public byte rt;
            [System.Runtime.InteropServices.FieldOffset(6)] public short lx;
            [System.Runtime.InteropServices.FieldOffset(8)] public short ly;          // forward is positive
            [System.Runtime.InteropServices.FieldOffset(10)] public short rx;
            [System.Runtime.InteropServices.FieldOffset(12)] public short ry;
            public UnityEngine.InputSystem.Utilities.FourCC format => new UnityEngine.InputSystem.Utilities.FourCC('H', 'I', 'D');
        }
#endif
    }
}

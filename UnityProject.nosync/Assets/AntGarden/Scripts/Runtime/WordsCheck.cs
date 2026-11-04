using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// Words check (-wordscheck &lt;dir&gt;, 11-04), in the nest it is run in (-world lab:…), played with a virtual pad as
    /// PadCheck plays: what the nest's ants say as antennae touch (NpcLines). Some speak and some do not; a speaker's line
    /// is one of its nest's, named as said by 一只蚂蚁, and written again in English by X; touched again at once, its line
    /// stays; later it says another, then no more; no line is heard twice. Stills of each line in 中文 and in English
    /// (台词_*). It touches an ant only where the prompt offers its antennae: never by a light gate (A there takes the
    /// player's ant through the gate; should one do so all the same, the next nest's run ends the check as failed,
    /// words_left_*.txt), a story slot or an exit nearer than the ant. With -controls, first the controls panel top left (Hud): the pad's, put away by ⧉ (the note in its place,
    /// then nothing), back by ⧉, the keys' once a key is touched, put away by its ×, the pad's in English, and the full
    /// help with its ×, put away together with the controls by ⧉ (面板_*). Writes words_report.json, puts the language
    /// preference back as it was, and quits.
    /// </summary>
    public static class WordsCheck
    {
        static string startedIn;   // the nest the check began in (a light gate taking the ant on reloads the world)

        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            if (startedIn != null)
            {
                // a light gate took the ant on (an ant it touched stood in one): the check ends, as failed
                File.WriteAllText(Path.Combine(dir, "words_left_" + startedIn + ".txt"), $"left {startedIn} for {Game.Selection} by a gate\n");
                Debug.LogWarning($"[AntGarden] wordscheck: left {startedIn} for {Game.Selection} by a gate");
                Application.Quit(1);
                yield break;
            }
            startedIn = Game.Selection;
            // read with the window behind too, the machine's own mice and keyboards left out (as PadCheck)
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            foreach (InputDevice d in new List<InputDevice>(InputSystem.devices))
                if (d is Mouse || d is Keyboard) InputSystem.DisableDevice(d);
            Gamepad pad = InputSystem.AddDevice<Gamepad>("VirtualPad");
            string langPref = PlayerPrefs.GetString("antgarden.lang", "zh");
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
                Debug.Log($"[AntGarden] wordscheck still {name}");
            }
            Transform canvas = game.hud.canvas.transform;
            string TextOf(string path) { Transform t = canvas.Find(path); Text x = t != null ? t.GetComponent<Text>() : null; return x != null ? x.text : ""; }
            bool Visible(string path) { Transform t = canvas.Find(path); return t != null && t.gameObject.activeInHierarchy; }
            float Alpha(string path) { Transform t = canvas.Find(path); CanvasGroup g = t != null ? t.GetComponent<CanvasGroup>() : null; return g != null ? g.alpha : 0f; }
            void Click(string path)
            {
                Transform t = canvas.Find(path);
                if (t != null) ExecuteEvents.Execute(t.gameObject, new PointerEventData(EventSystem.current), ExecuteEvents.pointerClickHandler);
            }
            // whether the player's ant would stand by a light gate there (A would take it through instead)
            bool ByGate(Vector3 p)
            {
                foreach (Gate g in game.gates)
                    if ((g.portal || g.world != null) && new Vector2(g.position.x - p.x, g.position.z - p.z).magnitude < Mathf.Max(game.cfg.gates.portalRadius, g.radius) + 2.5f)
                        return true;
                return false;
            }
            Vector3 Before(NpcAnt npc)
            {
                Vector3 f = npc.ant.Position;
                return f + new Vector3(Mathf.Sin(npc.ant.heading), 0, Mathf.Cos(npc.ant.heading)) * 1.3f;
            }
            // the player's ant just in front of an ant, face to face, the camera a little from the side; then A, once the
            // prompt offers the ant's antennae (by a light gate, a story slot or an exit, A goes to that instead: it waits for
            // the ant to walk on, a while)
            bool touched = false;   // whether the last Touch reached its ant
            IEnumerator Touch(NpcAnt npc)
            {
                touched = false;
                for (float t = 0; t < 15f; t += 1f)
                {
                    for (float w = 0; w < 1f && (ByGate(Before(npc)) || npc.ant.altitude > 0.05f); w += Time.deltaTime) yield return null;
                    if (ByGate(Before(npc)) || npc.ant.altitude > 0.05f) continue;
                    AntController me = game.player;
                    Vector3 f = npc.ant.Position;
                    Vector3 ahead = new Vector3(Mathf.Sin(npc.ant.heading), 0, Mathf.Cos(npc.ant.heading));
                    me.Place(f.x + ahead.x * 1.3f, f.z + ahead.z * 1.3f, Mathf.Atan2(-ahead.x, -ahead.z));
                    game.rig.SnapBehind(me.heading + 0.6f);   // (a little from the side: both ants in view, not one behind the other)
                    yield return null;
                    yield return null;   // the prompt finds it (Game.UpdatePrompt)
                    string prompt = TextOf("Prompt/PromptText");
                    if (prompt.Contains("和它碰碰触角") || prompt.Contains("to touch antennae"))
                    {
                        touched = true;
                        yield return Press(GamepadButton.South);
                        yield break;
                    }
                    for (float w = 0; w < 1f; w += Time.deltaTime) yield return null;   // (a slot or an exit nearer: the ant walks on)
                }
                Debug.Log("[AntGarden] wordscheck: an ant by a gate, a story slot or an exit, or up, not touched");
            }

            string nest = Game.Selection != null && Game.Selection.Contains("@") ? Game.Selection.Substring(Game.Selection.LastIndexOf('@') + 1).Split('#')[0] : "none";
            game.hud.SetStatsVisible(false);
            if (Loc.En) game.ToggleLanguage();   // (from 中文)
            yield return new WaitForSeconds(3f);   // the nest's banner goes, the ants spread out
            // on the nest's way, facing along it (as PadCheck's tutorial walk stands), the pad in hand
            List<Vector2> way = game.LabNest?.Track;
            if (way != null && way.Count > 12)
            {
                DemoMode.PlaceFacing(game, way[4], way[10]);
                game.rig.SnapBehind(game.player.heading);
            }
            yield return Hold(new GamepadState { rightStick = new Vector2(0.4f, 0f) }, 0.05f);
            yield return Hold(new GamepadState(), 1.2f);

            // 1 · the controls panel top left (-controls)
            if (Game.Arg("-controls") != null)
            {
                Check("panel_shown_from_the_start", Visible("Controls"), "");
                Check("panel_names_the_pad_and_how_to_hide_it", TextOf("Controls/ControlsText").Contains("左摇杆") && TextOf("Controls/ControlsText").Contains("⧉"), TextOf("Controls/ControlsText"));
                yield return Still("面板_手柄_中文");
                yield return Press(GamepadButton.Select);
                yield return Hold(new GamepadState(), 0.5f);
                Check("view_puts_it_away", !Visible("Controls") && !game.hud.ControlsOpen, "");
                Check("a_note_says_how_to_bring_it_back", Alpha("ControlsNote") > 0.5f && TextOf("ControlsNote/ControlsNoteText").Contains("⧉"), TextOf("ControlsNote/ControlsNoteText"));
                yield return Still("面板_收起_提示");
                yield return Hold(new GamepadState(), 5.5f);
                Check("the_note_goes", Alpha("ControlsNote") < 0.05f, $"alpha {Alpha("ControlsNote"):F2}");
                yield return Still("面板_收起");
                yield return Press(GamepadButton.Select);
                yield return Hold(new GamepadState(), 0.3f);
                Check("view_brings_it_back", Visible("Controls") && Alpha("ControlsNote") < 0.05f, "");
                // a key touched: the keys, and C to put them away
                Keyboard kb = InputSystem.AddDevice<Keyboard>("VirtualKeyboard");
                InputSystem.QueueStateEvent(kb, new KeyboardState(Key.RightCtrl));
                yield return null;
                yield return null;
                InputSystem.QueueStateEvent(kb, new KeyboardState());
                yield return Hold(new GamepadState(), 0.3f);
                Check("keys_once_a_key_is_touched", TextOf("Controls/ControlsText").Contains("W A S D") && TextOf("Controls/ControlsText").Contains("C"), TextOf("Controls/ControlsText"));
                yield return Still("面板_键盘_中文");
                InputSystem.RemoveDevice(kb);
                // the mouse: its ×
                Check("panel_has_its_x", canvas.Find("Controls/Close") != null, "");
                Click("Controls/Close");
                yield return Hold(new GamepadState(), 0.5f);
                Check("its_x_puts_it_away", !Visible("Controls") && TextOf("ControlsNote/ControlsNoteText").Contains("C"), TextOf("ControlsNote/ControlsNoteText"));
                yield return Still("面板_点叉收起_键盘提示");
                // back by ⧉, in English
                yield return Press(GamepadButton.Select);
                yield return Press(GamepadButton.West);
                yield return Hold(new GamepadState(), 0.5f);
                Check("pad_controls_in_english", Loc.En && Visible("Controls") && TextOf("Controls/ControlsText").Contains("Pad controls") && TextOf("Controls/ControlsText").Contains("hide"), TextOf("Controls/ControlsText"));
                yield return Still("面板_手柄_英文");
                // the full help (☰), with its ×; ⧉ puts it away with the controls
                yield return Press(GamepadButton.Start);
                yield return Hold(new GamepadState(), 0.5f);
                Check("help_has_its_x", Visible("Help") && canvas.Find("Help/Close") != null && TextOf("Help/Title").Contains("⧉"), "");
                yield return Still("说明_英文");
                yield return Press(GamepadButton.Select);
                yield return Hold(new GamepadState(), 0.5f);
                Check("view_puts_help_and_controls_away", !Visible("Help") && !Visible("Controls"), "");
                yield return Press(GamepadButton.Start);
                yield return Hold(new GamepadState(), 0.3f);
                Click("Help/Close");
                yield return Hold(new GamepadState(), 0.3f);
                Check("help_x_shuts_it", !Visible("Help"), "");
                yield return Press(GamepadButton.West);   // 中文 again
                yield return Hold(new GamepadState(), 6f);   // (the note goes)
            }

            // 2 · what the ants say
            var mine = game.npcs.FindAll(n => n.region == 0);
            var speakers = mine.FindAll(n => n.voice != null);
            speakers.Sort((a, b) => a.species.canFly.CompareTo(b.species.canFly));   // (walkers first: a winged one may be up)
            var lines = NpcLines.Of(nest);
            Check("some_ants_speak_not_all", speakers.Count >= 1 && speakers.Count < mine.Count, $"{speakers.Count} of {mine.Count} in {nest}");
            Check("the_nest_has_its_lines", lines.Count >= 3, $"{lines.Count} lines");
            var heard = new List<string>();
            int k = 0;
            foreach (NpcAnt s in speakers)
            {
                if (k >= 2) break;
                yield return Touch(s);
                if (!touched) continue;   // (up in the air, or by a gate: another)
                k++;
                yield return Hold(new GamepadState(), 0.6f);
                string zh = TextOf("Line/LineText");
                Check($"speaker{k}_says_a_line_of_its_nest", game.hud.LineShowing && lines.Exists(l => l.zh == zh), zh);
                Check($"speaker{k}_named", TextOf("Line/Speaker") == NpcLines.Speaker.zh, TextOf("Line/Speaker"));
                heard.Add(zh);
                yield return Still($"台词_{nest}_{k}_中文");
                yield return Press(GamepadButton.West);
                yield return Hold(new GamepadState(), 0.4f);
                string en = TextOf("Line/LineText");
                Check($"speaker{k}_in_english", lines.Exists(l => l.zh == zh && l.en == en) && TextOf("Line/Speaker") == NpcLines.Speaker.en, en);
                yield return Still($"台词_{nest}_{k}_英文");
                yield return Press(GamepadButton.West);
                yield return Hold(new GamepadState(), 0.3f);
                if (k == 1)
                {
                    // touched again at once: its line stays; later another; then no more (perAnt 2)
                    yield return Touch(s);
                    Check("touched_again_at_once_its_line_stays", TextOf("Line/LineText") == zh, TextOf("Line/LineText"));
                    yield return Until(() => !game.hud.LineShowing, 12f);
                    yield return Hold(new GamepadState(), 0.5f);
                    yield return Until(() => s.ant.altitude < 0.05f, 20f);
                    yield return Touch(s);
                    yield return Hold(new GamepadState(), 0.3f);
                    string second = TextOf("Line/LineText");
                    Check("later_it_says_another", game.hud.LineShowing && second != zh && lines.Exists(l => l.zh == second), second);
                    heard.Add(second);
                    yield return Until(() => !game.hud.LineShowing, 12f);
                    yield return Hold(new GamepadState(), 0.5f);
                    yield return Until(() => s.ant.altitude < 0.05f, 20f);
                    yield return Touch(s);
                    yield return Hold(new GamepadState(), 0.3f);
                    Check("then_no_more", !game.hud.LineShowing, s.voice != null ? $"said {s.voice.Said}" : "");
                }
                yield return Until(() => !game.hud.LineShowing, 12f);
                yield return Hold(new GamepadState(), 0.5f);
            }
            Check("two_speakers_heard", k >= 2 || speakers.Count < 2, $"{k} of {speakers.Count}");
            NpcAnt quiet = mine.Find(n => n.voice == null && n.ant.altitude < 0.05f && !ByGate(Before(n)));
            if (quiet != null)
            {
                yield return Touch(quiet);
                yield return Hold(new GamepadState(), 0.3f);
                Check("a_quiet_ant_only_touches_back", !game.hud.LineShowing && quiet.StateName == "Greet", quiet.StateName);
            }
            Check("no_line_heard_twice", heard.Count == new HashSet<string>(heard).Count, string.Join(" / ", heard));

            report.Append($"\n  ],\n  \"nest\": \"{nest}\",\n  \"failed\": {failed}\n}}\n");
            File.WriteAllText(Path.Combine(dir, "words_report.json"), report.ToString());
            Debug.Log("[AntGarden] wordscheck " + report.ToString().Replace("\n", " "));
            PlayerPrefs.SetString("antgarden.lang", langPref);
            PlayerPrefs.Save();
            InputSystem.RemoveDevice(pad);
            yield return new WaitForSeconds(0.3f);
            Application.Quit(failed > 0 ? 1 : 0);
        }
    }
}

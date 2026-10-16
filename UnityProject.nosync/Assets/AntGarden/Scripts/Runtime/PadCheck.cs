using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AntGarden
{
    /// <summary>
    /// Pad check (-padcheck &lt;dir&gt;): plugs in a virtual game pad and plays with it the way a person would: walks on
    /// the left stick, runs with RT, turns the camera with the right stick, zooms with LB, opens the map with Y,
    /// moves its cursor and closes it with B, touches antennae with an ant on A, reaches out with B. Each step is
    /// checked against what it should have done; writes pad_report.json and a screenshot, then quits.
    /// </summary>
    public static class PadCheck
    {
        public static IEnumerator Run(Game game, string dir)
        {
            Directory.CreateDirectory(dir);
            yield return new WaitForSeconds(1.5f);
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
            report.Append($"\n  ],\n  \"failed\": {failed}\n}}\n");
            File.WriteAllText(Path.Combine(dir, "pad_report.json"), report.ToString());
            Debug.Log("[AntGarden] padcheck " + report.ToString().Replace("\n", " "));
            InputSystem.RemoveDevice(pad);
            Application.Quit();
        }
    }
}

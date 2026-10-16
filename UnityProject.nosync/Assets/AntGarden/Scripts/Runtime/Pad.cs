using UnityEngine;
using UnityEngine.InputSystem;

namespace AntGarden
{
    /// <summary>
    /// A game controller (an Xbox pad, or any pad the Input System recognises) beside the keyboard and mouse:
    /// left stick moves (a gentle tilt walks slowly), right stick turns the camera, RT runs, A is E, B reaches out
    /// with the antennae (or goes back out of the map or a panel), Y opens the map, LB / RB zoom out / in, ☰ shows
    /// the help. With no pad connected everything reads zero. It also remembers whether the pad or the keyboard
    /// was used last, so prompts name the right button, and gives the pad a short rumble on touches.
    /// </summary>
    public static class Pad
    {
        static float rumbleUntil;
        public static bool rumble = true;

        /// <summary>The pad was the last thing touched (prompts then say A instead of E).</summary>
        public static bool LastUsed { get; private set; }

        static Gamepad G => Gamepad.current;

        /// <summary>Radial dead zone, rescaled so a stick just past it starts from zero.</summary>
        static Vector2 Dead(Vector2 v, float dz)
        {
            float m = v.magnitude;
            if (m < dz) return Vector2.zero;
            return v / m * Mathf.Min(1, (m - dz) / (1 - dz));
        }

        /// <summary>Left stick (or the d-pad): x = right, y = forward, length 0..1.</summary>
        public static Vector2 Move
        {
            get
            {
                Gamepad g = G;
                if (g == null) return Vector2.zero;
                return Vector2.ClampMagnitude(Dead(g.leftStick.ReadValue(), 0.18f) + g.dpad.ReadValue(), 1);
            }
        }

        /// <summary>Right stick: x = turn right, y = look up.</summary>
        public static Vector2 Look => G == null ? Vector2.zero : Dead(G.rightStick.ReadValue(), 0.15f);

        public static bool Run => G != null && (G.rightTrigger.ReadValue() > 0.35f || G.leftStickButton.isPressed);
        public static bool Interact => G != null && G.buttonSouth.wasPressedThisFrame;   // A
        public static bool Back => G != null && G.buttonEast.wasPressedThisFrame;        // B
        public static bool Map => G != null && G.buttonNorth.wasPressedThisFrame;        // Y
        public static bool Help => G != null && G.startButton.wasPressedThisFrame;       // ☰ (menu)

        /// <summary>LB pulls the camera back, RB brings it closer (+1 / -1 while held).</summary>
        public static float Zoom => G == null ? 0 : (G.leftShoulder.isPressed ? 1 : 0) - (G.rightShoulder.isPressed ? 1 : 0);

        /// <summary>Once a frame: which device was used last, and the end of a rumble.</summary>
        public static void Tick()
        {
            Gamepad g = G;
            if (g != null)
            {
                if (g.leftStick.ReadValue().sqrMagnitude > 0.09f || g.rightStick.ReadValue().sqrMagnitude > 0.09f || g.dpad.ReadValue().sqrMagnitude > 0.1f
                    || g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame
                    || g.buttonWest.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.rightTrigger.ReadValue() > 0.3f)
                    LastUsed = true;
                if (rumbleUntil > 0 && (Time.unscaledTime > rumbleUntil || !Application.isFocused))
                {
                    g.SetMotorSpeeds(0, 0);
                    rumbleUntil = 0;
                }
            }
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            if ((k != null && k.anyKey.wasPressedThisFrame) || (m != null && (m.leftButton.wasPressedThisFrame || m.delta.ReadValue().sqrMagnitude > 4f)))
                LastUsed = false;
        }

        /// <summary>A short rumble (low and high motor, 0..1) for a touch, a flare or a gate.</summary>
        public static void Rumble(float low, float high, float seconds)
        {
            Gamepad g = G;
            if (!rumble || g == null) return;
            g.SetMotorSpeeds(low, high);
            rumbleUntil = Time.unscaledTime + seconds;
        }

        /// <summary>Prompts name the button of whatever the player is holding: "E" on the keyboard, "A" on the pad.</summary>
        public static string Buttons(string text) =>
            LastUsed ? text.Replace("按 E", "按 A").Replace("press E", "press A").Replace("Press E", "Press A") : text;
    }
}

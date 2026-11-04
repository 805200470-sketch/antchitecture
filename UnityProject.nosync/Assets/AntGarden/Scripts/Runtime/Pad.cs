using UnityEngine;
using UnityEngine.InputSystem;

namespace AntGarden
{
    /// <summary>
    /// A game controller (an Xbox pad, or any pad the Input System recognises or, on a Mac, Apple's GameController reads)
    /// beside the keyboard and mouse:
    /// left stick moves (a gentle tilt walks slowly), right stick turns the camera, RT runs, A is E, B reaches out
    /// with the antennae (or goes back out of the map or a panel), Y opens the map, X switches 中文 / English (as L, and
    /// as X on the headset), LB / RB zoom out / in, ☰ shows the help (held a second, it skips the tutorial), ⧉ (View) puts
    /// the controls top left away or brings them back (as C; 11-04). With no pad connected everything reads zero. It also remembers whether the pad or the keyboard was used last, so prompts name
    /// the right button, and gives the pad a short rumble on touches.
    /// </summary>
    public static class Pad
    {
        static float rumbleUntil;
        public static bool rumble = true;
        static bool touched;   // anything used yet: until then a plugged-in pad counts as the one in hand

        /// <summary>The pad was the last thing touched (prompts then say A instead of E); a pad plugged in before anything
        /// is touched counts (a demo hands the pad over: the tutorial's first words name its stick, not W A S D).</summary>
        public static bool LastUsed { get; private set; }

        static Gamepad G => Gamepad.current;

#if UNITY_STANDALONE_OSX || UNITY_EDITOR_OSX
        /// <summary>
        /// An Xbox pad on a USB cable on a Mac: macOS's own driver shows it to the game as "GamePad-1" (Microsoft,
        /// product 0x028E, the Xbox 360 report). The Input System knows that report only under its old driver's name
        /// ("Xbox … Wired Controller"), so the pad came in as a plain joystick and did nothing (11-03). Its reports are
        /// the wired Xbox layout's own, byte for byte: 2 header bytes, the buttons, the triggers, four 16-bit stick axes.
        /// (The cable's other device, "Controller", sends only Microsoft's own protocol and stays unused.)
        /// Byte for byte but for one thing (11-03, PadCheck's mac_* checks): the sticks' up. The pad sends up as a positive
        /// number, as every Xbox 360 report does, and Apple's driver (XboxGamepad.dext, Xbox360Gamepad::handlePayload)
        /// hands the report on untouched; that layout reads up from a negative number, because its old driver
        /// (360Controller) turned the axes over first. Read with it, the left stick pushed forward walked the ant back and
        /// the right stick tilted the camera the wrong way. So the pad gets that layout with the sticks' up and down the
        /// right way round (NativeXbox).
        /// (11-04, with the user's pad, an Xbox Series X on the cable: an app's HID lists no "GamePad-1" at all, only
        /// "Controller" in Microsoft's own protocol, so the game saw no pad; GameControllerMac reads it through Apple's
        /// GameController. This layout stays for a Mac whose HID does list GamePad-1.)
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void WiredXboxOnMac()
        {
            InputSystem.RegisterLayout(NativeXboxJson);
            InputSystem.RegisterLayoutMatcher(NativeXbox, new UnityEngine.InputSystem.Layouts.InputDeviceMatcher().WithInterface("HID")
                .WithCapability("vendorId", 0x045E).WithCapability("productId", 0x028E));
        }
#endif
        /// <summary>The wired Xbox pad's layout on a Mac: XboxGamepadMacOS with the sticks' Y read as the pad sends it.</summary>
        public const string NativeXbox = "XboxGamepadMacOSNative";
        const string NativeXboxJson = "{ \"name\": \"" + NativeXbox + "\", \"extend\": \"XboxGamepadMacOS\", \"controls\": [ "
            + "{ \"name\": \"leftStick/y\", \"parameters\": \"invert=false\" }, "
            + "{ \"name\": \"leftStick/up\", \"parameters\": \"clamp=1,clampMin=0,clampMax=1,invert=false\" }, "
            + "{ \"name\": \"leftStick/down\", \"parameters\": \"clamp=1,clampMin=-1,clampMax=0,invert=true\" }, "
            + "{ \"name\": \"rightStick/y\", \"parameters\": \"invert=false\" }, "
            + "{ \"name\": \"rightStick/up\", \"parameters\": \"clamp=1,clampMin=0,clampMax=1,invert=false\" }, "
            + "{ \"name\": \"rightStick/down\", \"parameters\": \"clamp=1,clampMin=-1,clampMax=0,invert=true\" } ] }";
        static bool stickLogged;

        /// <summary>Which pads the game sees, in the log: a pad that does nothing usually came in as a plain joystick.</summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void LogPads()
        {
            foreach (InputDevice d in InputSystem.devices) LogPad(d);
            InputSystem.onDeviceChange += (d, change) => { if (change == InputDeviceChange.Added) LogPad(d); };
        }

        static void LogPad(InputDevice d)
        {
            if (d is Gamepad || d is Joystick)
                Debug.Log($"[AntGarden] pad: {d.displayName} ({d.layout}, {(d is Gamepad ? "used" : "a plain joystick, not used")})");
        }

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
        public static bool HelpHeld => G != null && G.startButton.isPressed;             // ☰ held (the tutorial: a second skips it)
        public static bool Language => G != null && G.buttonWest.wasPressedThisFrame;    // X: 中文 / English
        public static bool Controls => G != null && G.selectButton.wasPressedThisFrame;  // ⧉ (View): the controls top left away / back (11-04)

        /// <summary>LB pulls the camera back, RB brings it closer (+1 / -1 while held).</summary>
        public static float Zoom => G == null ? 0 : (G.leftShoulder.isPressed ? 1 : 0) - (G.rightShoulder.isPressed ? 1 : 0);

        /// <summary>Once a frame: which device was used last, and the end of a rumble.</summary>
        public static void Tick()
        {
#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
            GameControllerMac.Tick();   // a pad only Apple's GameController reads (an Xbox Series pad on a cable, 11-04)
#endif
            Gamepad g = G;
            if (g != null)
            {
                if (!touched && !Tutorial.CheckRun) LastUsed = true;   // (a check run's prompts stay as they were)
                // the first firm push on the left stick, in the log: pushed forward first (as one starts to walk), y should be near +1
                if (!stickLogged && g.leftStick.ReadValue().sqrMagnitude > 0.5f)
                {
                    stickLogged = true;
                    Debug.Log($"[AntGarden] pad: left stick first pushed to ({g.leftStick.ReadValue().x:F2}, {g.leftStick.ReadValue().y:F2}), {g.layout}");
                }
                if (g.leftStick.ReadValue().sqrMagnitude > 0.09f || g.rightStick.ReadValue().sqrMagnitude > 0.09f || g.dpad.ReadValue().sqrMagnitude > 0.1f
                    || g.buttonSouth.wasPressedThisFrame || g.buttonEast.wasPressedThisFrame || g.buttonNorth.wasPressedThisFrame
                    || g.buttonWest.wasPressedThisFrame || g.startButton.wasPressedThisFrame || g.selectButton.wasPressedThisFrame
                    || g.rightTrigger.ReadValue() > 0.3f)
                    LastUsed = touched = true;
                if (rumbleUntil > 0 && (Time.unscaledTime > rumbleUntil || !Application.isFocused))
                {
                    g.SetMotorSpeeds(0, 0);
                    rumbleUntil = 0;
                }
            }
            Keyboard k = Keyboard.current;
            Mouse m = Mouse.current;
            if ((k != null && k.anyKey.wasPressedThisFrame) || (m != null && (m.leftButton.wasPressedThisFrame || m.delta.ReadValue().sqrMagnitude > 4f)))
            {
                LastUsed = false;
                touched = true;
            }
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

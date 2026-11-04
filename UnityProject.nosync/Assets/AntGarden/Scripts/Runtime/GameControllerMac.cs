#if UNITY_STANDALONE_OSX && !UNITY_EDITOR
using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;

namespace AntGarden
{
    /// <summary>
    /// Pads a Mac keeps for its own game controller service (11-04). An Xbox Series X|S pad on a USB cable reaches an
    /// app's HID only as Microsoft's own protocol (vendor page 0xFF00, report 0x20 = the input), which the Input System
    /// cannot read, so the game saw no pad at all; the "GamePad-1" macOS also lists is that service's own copy, and an
    /// app's HID never sees it. Apple's GameController framework reads these pads (and PlayStation, Switch and MFi ones).
    /// This asks it before each input update, through the Objective-C runtime (no plugin to build), and hands what it
    /// reads to an ordinary Input System gamepad, "AppleGameController", so Pad and everything after it use
    /// Gamepad.current as with any other pad. A pad the Input System also reads itself (an Xbox pad on Bluetooth) is
    /// there twice; only the one used last is current, so it still moves the ant once. No rumble on this path.
    /// The pad also plays while another app is in front (11-04: the user typed in another window, picked the pad up and
    /// the ant did not move; the framework and the Input System both stop listening to an app that is not in front).
    /// </summary>
    static class GameControllerMac
    {
        const string ObjC = "/usr/lib/libobjc.A.dylib";
        [DllImport("/usr/lib/libSystem.B.dylib")] static extern IntPtr dlopen(string path, int mode);
        [DllImport(ObjC)] static extern IntPtr objc_getClass(string name);
        [DllImport(ObjC)] static extern IntPtr sel_registerName(string name);
        [DllImport(ObjC)] static extern IntPtr objc_autoreleasePoolPush();
        [DllImport(ObjC)] static extern void objc_autoreleasePoolPop(IntPtr pool);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern IntPtr Send(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern IntPtr SendAt(IntPtr receiver, IntPtr selector, UIntPtr index);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern float SendFloat(IntPtr receiver, IntPtr selector);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] static extern void SendFlag(IntPtr receiver, IntPtr selector, [MarshalAs(UnmanagedType.I1)] bool flag);
        [DllImport(ObjC, EntryPoint = "objc_msgSend")] [return: MarshalAs(UnmanagedType.I1)] static extern bool SendAsk(IntPtr receiver, IntPtr selector, IntPtr argument);

        static IntPtr gcClass, controllers, count, objectAt, extended, vendorName, utf8, value, xAxis, yAxis;
        static IntPtr leftThumbstick, rightThumbstick, dpad, up, down, left, right, leftTrigger, rightTrigger;
        static IntPtr buttonA, buttonB, buttonX, buttonY, leftShoulder, rightShoulder, leftStickButton, rightStickButton, menu, options;
        static Gamepad device;
        static IntPtr deviceFor;        // the GCController the gamepad stands for (compared only, never messaged)
        static GamepadState sent;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Init()
        {
            try
            {
                dlopen("/System/Library/Frameworks/GameController.framework/GameController", 2);   // RTLD_NOW
                gcClass = objc_getClass("GCController");
            }
            catch (Exception e) { Debug.Log($"[AntGarden] pad: Apple's GameController not reachable ({e.Message})"); return; }
            if (gcClass == IntPtr.Zero) { Debug.Log("[AntGarden] pad: Apple's GameController not found"); return; }
            controllers = sel_registerName("controllers"); count = sel_registerName("count");
            objectAt = sel_registerName("objectAtIndex:"); extended = sel_registerName("extendedGamepad");
            vendorName = sel_registerName("vendorName"); utf8 = sel_registerName("UTF8String"); value = sel_registerName("value");
            xAxis = sel_registerName("xAxis"); yAxis = sel_registerName("yAxis");
            leftThumbstick = sel_registerName("leftThumbstick"); rightThumbstick = sel_registerName("rightThumbstick");
            dpad = sel_registerName("dpad"); up = sel_registerName("up"); down = sel_registerName("down");
            left = sel_registerName("left"); right = sel_registerName("right");
            leftTrigger = sel_registerName("leftTrigger"); rightTrigger = sel_registerName("rightTrigger");
            buttonA = sel_registerName("buttonA"); buttonB = sel_registerName("buttonB");
            buttonX = sel_registerName("buttonX"); buttonY = sel_registerName("buttonY");
            leftShoulder = sel_registerName("leftShoulder"); rightShoulder = sel_registerName("rightShoulder");
            leftStickButton = sel_registerName("leftThumbstickButton"); rightStickButton = sel_registerName("rightThumbstickButton");
            menu = sel_registerName("buttonMenu"); options = sel_registerName("buttonOptions");
            IntPtr pool = objc_autoreleasePoolPush();
            Send(gcClass, controllers);   // the framework starts looking for pads now, so one is listed by the first frame
            IntPtr background = sel_registerName("setShouldMonitorBackgroundEvents:");   // (macOS 11.3 on)
            if (SendAsk(gcClass, sel_registerName("respondsToSelector:"), background)) SendFlag(gcClass, background, true);
            objc_autoreleasePoolPop(pool);
            InputSystem.onBeforeUpdate += Feed;
            // without focus the Input System keeps its devices running (the pad); the keyboard and mouse are let go when
            // the game loses focus instead, so a key held while switching away does not stay down
            InputSystem.settings.backgroundBehavior = InputSettings.BackgroundBehavior.IgnoreFocus;
            Application.focusChanged += focused =>
            {
                if (focused) return;
                if (Keyboard.current != null) InputSystem.ResetDevice(Keyboard.current);
                if (Mouse.current != null) InputSystem.ResetDevice(Mouse.current);
            };
        }

        static IntPtr appClass, sharedApplication, windows, occlusionState;

        /// <summary>Whether one of the game's windows is on screen (not covered, not on another Space): with another app
        /// in front, the game keeps its full frame rate while it can still be seen (Game.OnApplicationFocus, 11-04).
        /// Shares the Objective-C calls above; false if they fail.</summary>
        public static bool WindowOnScreen()
        {
            try
            {
                if (appClass == IntPtr.Zero)
                {
                    appClass = objc_getClass("NSApplication");
                    sharedApplication = sel_registerName("sharedApplication"); windows = sel_registerName("windows");
                    occlusionState = sel_registerName("occlusionState");
                    count = sel_registerName("count"); objectAt = sel_registerName("objectAtIndex:");
                }
                IntPtr pool = objc_autoreleasePoolPush();
                try
                {
                    IntPtr list = Send(Send(appClass, sharedApplication), windows);
                    long n = Send(list, count).ToInt64();
                    for (long i = 0; i < n; i++)
                        if ((Send(SendAt(list, objectAt, (UIntPtr)(ulong)i), occlusionState).ToInt64() & 2) != 0) return true;   // NSWindowOcclusionStateVisible
                    return false;
                }
                finally { objc_autoreleasePoolPop(pool); }
            }
            catch (Exception) { return false; }
        }

        /// <summary>Once a frame (from Pad.Tick): a gamepad while the framework lists a pad, none when it is gone.</summary>
        public static void Tick()
        {
            if (gcClass == IntPtr.Zero) return;
            IntPtr pool = objc_autoreleasePoolPush();
            try
            {
                IntPtr pad = Find(out IntPtr controller);
                if (pad == IntPtr.Zero)
                {
                    if (device == null) return;
                    InputSystem.RemoveDevice(device);
                    device = null;
                    deviceFor = IntPtr.Zero;
                    Debug.Log("[AntGarden] pad: Apple's GameController pad gone");
                    return;
                }
                if (controller == deviceFor) return;
                if (device == null) device = InputSystem.AddDevice<Gamepad>("AppleGameController");
                deviceFor = controller;
                sent = default;
                IntPtr name = Send(Send(controller, vendorName), utf8);
                Debug.Log($"[AntGarden] pad: {(name == IntPtr.Zero ? "a pad" : Marshal.PtrToStringAnsi(name))} through Apple's GameController");
            }
            finally { objc_autoreleasePoolPop(pool); }
        }

        /// <summary>Before each input update: the pad's sticks, triggers and buttons as a gamepad state, when they change.</summary>
        static void Feed()
        {
            if (device == null || !device.added) return;
            IntPtr pool = objc_autoreleasePoolPush();
            try
            {
                IntPtr pad = Find(out _);
                if (pad == IntPtr.Zero) return;
                IntPtr d = Send(pad, dpad);
                GamepadState s = new GamepadState
                {
                    leftStick = Stick(Send(pad, leftThumbstick)),
                    rightStick = Stick(Send(pad, rightThumbstick)),
                    leftTrigger = SendFloat(Send(pad, leftTrigger), value),
                    rightTrigger = SendFloat(Send(pad, rightTrigger), value),
                }
                .WithButton(GamepadButton.DpadUp, Pressed(Send(d, up))).WithButton(GamepadButton.DpadDown, Pressed(Send(d, down)))
                .WithButton(GamepadButton.DpadLeft, Pressed(Send(d, left))).WithButton(GamepadButton.DpadRight, Pressed(Send(d, right)))
                .WithButton(GamepadButton.South, Pressed(Send(pad, buttonA))).WithButton(GamepadButton.East, Pressed(Send(pad, buttonB)))
                .WithButton(GamepadButton.West, Pressed(Send(pad, buttonX))).WithButton(GamepadButton.North, Pressed(Send(pad, buttonY)))
                .WithButton(GamepadButton.LeftShoulder, Pressed(Send(pad, leftShoulder)))
                .WithButton(GamepadButton.RightShoulder, Pressed(Send(pad, rightShoulder)))
                .WithButton(GamepadButton.LeftStick, Pressed(Send(pad, leftStickButton)))
                .WithButton(GamepadButton.RightStick, Pressed(Send(pad, rightStickButton)))
                .WithButton(GamepadButton.Start, Pressed(Send(pad, menu))).WithButton(GamepadButton.Select, Pressed(Send(pad, options)));
                if (s.buttons == sent.buttons && s.leftStick == sent.leftStick && s.rightStick == sent.rightStick
                    && s.leftTrigger == sent.leftTrigger && s.rightTrigger == sent.rightTrigger) return;
                InputSystem.QueueStateEvent(device, s);
                sent = s;
            }
            finally { objc_autoreleasePoolPop(pool); }
        }

        /// <summary>The first listed pad with sticks and shoulder buttons (its extendedGamepad), or zero.</summary>
        static IntPtr Find(out IntPtr controller)
        {
            controller = IntPtr.Zero;
            IntPtr list = Send(gcClass, controllers);
            long n = Send(list, count).ToInt64();
            for (long i = 0; i < n; i++)
            {
                IntPtr c = SendAt(list, objectAt, (UIntPtr)(ulong)i);
                IntPtr pad = Send(c, extended);
                if (pad != IntPtr.Zero) { controller = c; return pad; }
            }
            return IntPtr.Zero;
        }

        // (a missing element is nil, and a message to nil reads zero)
        static Vector2 Stick(IntPtr pad) => new Vector2(SendFloat(Send(pad, xAxis), value), SendFloat(Send(pad, yAxis), value));
        static bool Pressed(IntPtr button) => SendFloat(button, value) > 0.5f;
    }
}
#endif

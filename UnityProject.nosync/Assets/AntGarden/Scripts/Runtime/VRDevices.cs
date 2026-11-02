using System;
using UnityEngine;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// In a headset: the player's hands and controllers as the game sees them, one frame at a time (VRPad), read from
    /// the headset's own tracking. This is the input layer: it reads the devices and says what the player did; what that
    /// does in the game is VRPlayer's, VRTutorial's and Game's (the user, 11-01: "把输入层和游戏逻辑分开"). On a Meta Quest
    /// it is Meta's XR Core SDK (MetaDevices, in Scripts/VR: the controllers' own models, the headset's own hands, their
    /// pinches); without it (a computer through Quest Link, a preview) Unity's XR input and VRHands' drawn controllers.
    /// Nothing here recognises a hand from a camera picture or guesses where one is: the headset tracks them.
    /// </summary>
    public interface IVRDevices
    {
        /// <summary>The controllers (and hands) drawn in origin, where the player's are; light: their brightness.</summary>
        void Init(Transform origin, bool draw, float light);
        /// <summary>This frame's sticks, buttons, triggers and pinches.</summary>
        void Read(ref VRPad pad);
    }

    /// <summary>What the player is doing with their hands this frame, as held states (VRPlayer finds the presses).</summary>
    public struct VRPad
    {
        public Vector2 move;        // the left stick (x right, y forward); a hand: the left hand pinched (index), straight ahead
        public Vector2 turn;        // the right stick
        public bool run;            // the left stick pressed in
        public bool a, b, x, y;     // the right hand's two buttons, the left's; a hand: right index and middle pinches, left middle (Y)
        public bool menu;           // the left controller's menu button, or the left hand's menu gesture
        public bool walkToggle;     // the right stick pressed in
        public float trigger;       // the further pulled of the two triggers (0-1)
    }

    /// <summary>Which input layer this headset gets: Meta's (registered by Scripts/VR as the game starts) where Meta's
    /// runtime is up, else Unity's XR input.</summary>
    public static class VRDevices
    {
        public static Func<IVRDevices> Meta;

        public static IVRDevices Make() => Meta?.Invoke() ?? new XRDevices();
    }

    /// <summary>Unity's XR input (InputDevices): the controllers' sticks, buttons and triggers; no hands. The controllers
    /// drawn by VRHands.</summary>
    public sealed class XRDevices : IVRDevices
    {
        public void Init(Transform origin, bool draw, float light)
        {
            if (draw) origin.gameObject.AddComponent<VRHands>().Init(origin, light);
            Debug.Log("[AntGarden] VR input: Unity's XR input (no Meta runtime): controllers, no hands");
        }

        public void Read(ref VRPad pad)
        {
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            pad.move = left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 m) && m.sqrMagnitude > 0.02f ? m : Vector2.zero;
            pad.turn = right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 t) ? t : Vector2.zero;
            pad.run = left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool run) && run;
            pad.a = right.TryGetFeatureValue(CommonUsages.primaryButton, out bool a) && a;
            pad.b = right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool b) && b;
            pad.x = left.TryGetFeatureValue(CommonUsages.primaryButton, out bool x) && x;
            pad.y = left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool y) && y;
            pad.menu = left.TryGetFeatureValue(CommonUsages.menuButton, out bool menu) && menu;
            pad.walkToggle = right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool click) && click;
            pad.trigger = Mathf.Max(left.TryGetFeatureValue(CommonUsages.trigger, out float lt) ? lt : 0f, right.TryGetFeatureValue(CommonUsages.trigger, out float rt) ? rt : 0f);
        }
    }
}

using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.UI;

namespace AntGarden
{
    /// <summary>
    /// In a Meta Quest: the headset's input layer (IVRDevices) on Meta's XR Core SDK, which the user chose (11-01, "用A").
    /// Everything here is the headset's own tracking read through the SDK: nothing recognises a hand in a camera's
    /// picture, nothing guesses where a hand is.
    /// - The SDK's manager (OVRManager), made as the headset view starts: hands and controllers tracked together
    ///   (Multimodal: a hand holding its controller is drawn round it from the controller's own sensors, a free hand from
    ///   the headset's cameras; at the low tracking rate, never Fast Motion Mode, no body tracking: VRSetup), in the same
    ///   eye-level space the camera's pose is read in (VRPlayer), so the camera, the hands and the controllers agree.
    /// - The controllers: Meta's own models of them (OVRControllerPrefab: a Quest 3's Touch Plus, its buttons moving as
    ///   they are pressed), at the pose the SDK gives just before each frame is drawn, in the VR origin as the camera is;
    ///   lit by themselves (VRModel: the nest has no lights for Meta's lit shader); each button's letter over it, the one
    ///   the tutorial waits for picked out (the user, 11-01: "看不清按键"). A controller the headset loses stays where it
    ///   was a moment, then hides.
    /// - The hands: the headset's own hand mesh on its tracked skeleton (OVRHandPrefab), drawn as its home draws them
    ///   (VRGhostHand). Checked every frame: a hand the headset loses, or sees with low confidence, keeps its last pose
    ///   and fades out; it fades back as it is seen again.
    /// - Pinches, from a hand the cameras see (not one drawn round its controller, not in the middle of the system's own
    ///   gesture), each with two thresholds (pressed past 0.8, let go below 0.5), so a pinch held near the edge never
    ///   chatters: right thumb to index A, to middle B; left thumb to index crawls (the way the head looks), to middle Y;
    ///   the left hand's menu gesture opens the instructions.
    /// Used where Meta's runtime is up (a Quest, its Meta XR feature on: VRSetup.ConfigureQuest); elsewhere XRDevices.
    /// </summary>
    public class MetaDevices : MonoBehaviour, IVRDevices
    {
        const float PinchOn = 0.8f, PinchOff = 0.5f;
        const float FadeTime = 0.25f;     // a hand the headset loses fades out over this long (seconds), and back
        const float LostHide = 0.3f;      // a controller it loses stays where it was this long, then hides
        const float LetterSize = 0.012f;  // a button's letter, this tall (real metres)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() =>
            VRDevices.Meta = () => OVRPlugin.initialized ? new GameObject("VR Devices (Meta)").AddComponent<MetaDevices>() : null;

        /// <summary>One hand's side: its controller and its hand.</summary>
        sealed class Side
        {
            public OVRInput.Controller touch;
            public OVRInput.Hand hand;
            public bool left;
            public Transform controller;        // the controller's pose in the origin, Meta's model in it
            public float lost;                  // how long the headset has not seen it
            public OVRHand tracked;             // the hand
            public SkinnedMeshRenderer skin;    // its mesh
            public Material look;               // its own material, so it fades on its own
            public float shown;                 // how much of it is drawn
            public bool index, middle;          // its pinches, held
        }

        /// <summary>A button's letter over it.</summary>
        sealed class Letter
        {
            public string button;
            public Transform at, sign;
            public Text text;
        }

        readonly Side l = new Side { touch = OVRInput.Controller.LTouch, hand = OVRInput.Hand.HandLeft, left = true };
        readonly Side r = new Side { touch = OVRInput.Controller.RTouch, hand = OVRInput.Hand.HandRight };
        readonly List<Letter> letters = new List<Letter>();
        string hint;

        public void Init(Transform origin, bool draw, float light)
        {
            transform.SetParent(origin, false);
            Manager();
            var refs = draw ? Resources.Load<MetaRefs>("AntGarden/VR/MetaRefs") : null;
            if (draw && refs == null) Debug.LogWarning("[AntGarden] VR: Meta's prefabs are not named (Resources/AntGarden/VR/MetaRefs): no controllers or hands drawn");
            foreach (Side s in new[] { l, r })
            {
                if (refs != null && refs.controller != null) Controller(s, refs.controller, light, origin);
                if (refs != null && refs.hand != null) Hand(s, refs.hand, light, origin);
            }
            Application.onBeforeRender += Pose;
            Debug.Log($"[AntGarden] VR input: Meta XR Core SDK (OVRPlugin {OVRPlugin.version}); hands with controllers {(OVRPlugin.IsMultimodalHandsControllersSupported() ? "supported" : "NOT supported")}; drawn: controllers {l.controller != null}, hands {l.tracked != null}, {letters.Count} button letters");
        }

        void OnDestroy() => Application.onBeforeRender -= Pose;

        /// <summary>The SDK's manager, once for the game: set as a scene's would be, before it wakes.</summary>
        static void Manager()
        {
            if (OVRManager.instance != null) return;
            var go = new GameObject("OVRManager");
            go.SetActive(false);
            var m = go.AddComponent<OVRManager>();
            m.trackingOriginType = OVRManager.TrackingOrigin.EyeLevel;   // VRPlayer's Device: one space for all
            m.resetTrackerOnLoad = false;                                 // the view is VRPlayer's to centre
            m.SimultaneousHandsAndControllersEnabled = true;
            m.launchSimultaneousHandsControllersOnStartup = true;
            m.controllerDrivenHandPosesType = OVRManager.ControllerDrivenHandPosesType.ConformingToController;
            DontDestroyOnLoad(go);
            go.SetActive(true);
        }

        /// <summary>A controller: Meta's model of it, lit by itself, its buttons lettered.</summary>
        void Controller(Side s, GameObject prefab, float light, Transform origin)
        {
            s.controller = new GameObject(s.left ? "VR Controller L" : "VR Controller R").transform;
            s.controller.SetParent(origin, false);
            GameObject model = Instantiate(prefab, s.controller, false);
            var helper = model.GetComponent<OVRControllerHelper>();
            if (helper != null) helper.m_controller = s.touch;   // (read as it starts, next frame)
            var shader = Resources.Load<Shader>("AntGarden/VR/VRModel");
            if (shader != null)
                foreach (Renderer rd in model.GetComponentsInChildren<Renderer>(true))
                {
                    Material[] mats = rd.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        if (mats[i] == null) continue;
                        Texture tex = mats[i].HasProperty("_BaseMap") ? mats[i].GetTexture("_BaseMap") : mats[i].mainTexture;
                        var m = new Material(shader) { name = "VRModel " + mats[i].name };
                        if (tex != null) m.SetTexture("_BaseMap", tex);
                        m.SetFloat("_Light", light);
                        mats[i] = m;
                    }
                    rd.sharedMaterials = mats;
                    rd.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                    rd.receiveShadows = false;
                }
            GameObject plus = helper == null ? null : s.left ? helper.m_modelMetaTouchPlusLeftController : helper.m_modelMetaTouchPlusRightController;
            if (plus == null) return;
            if (s.left)
            {
                Letters(s, plus.transform, "button_x", "X");
                Letters(s, plus.transform, "button_y", "Y");
                Letters(s, plus.transform, "button_oculus", "≡", "Menu");
            }
            else
            {
                Letters(s, plus.transform, "button_a", "A");
                Letters(s, plus.transform, "button_b", "B");
            }
        }

        /// <summary>A letter over the button whose node's name ends so (on the Touch Plus model).</summary>
        void Letters(Side s, Transform model, string node, string text, string button = null)
        {
            Transform at = FindEnd(model, node);
            if (at == null) return;
            var canvas = new GameObject("VR Letter " + text, typeof(RectTransform)).AddComponent<Canvas>();
            canvas.renderMode = RenderMode.WorldSpace;
            var rt = (RectTransform)canvas.transform;
            rt.SetParent(s.controller, false);
            rt.sizeDelta = new Vector2(64, 64);
            rt.localScale = Vector3.one * (LetterSize / 40f);   // (a 40-unit letter)
            Text t = UiKit.Label(rt, "Letter", text, 40, UiKit.Ink, TextAnchor.MiddleCenter);
            var edge = t.gameObject.AddComponent<Outline>();
            edge.effectColor = new Color(0f, 0f, 0f, 0.9f);
            edge.effectDistance = new Vector2(2.5f, -2.5f);
            letters.Add(new Letter { button = button ?? text, at = at, sign = rt, text = t });
        }

        static Transform FindEnd(Transform t, string end)
        {
            if (t.name.ToLowerInvariant().EndsWith(end)) return t;
            foreach (Transform c in t)
            {
                Transform f = FindEnd(c, end);
                if (f != null) return f;
            }
            return null;
        }

        /// <summary>A hand: the headset's own mesh on its tracked skeleton, posed by the skeleton itself in the origin
        /// (not under the SDK's camera rig), shown and faded here rather than by the SDK.</summary>
        void Hand(Side s, GameObject prefab, float light, Transform origin)
        {
            var holder = new GameObject(s.left ? "VR Hand L" : "VR Hand R");
            holder.SetActive(false);   // (set before it wakes)
            holder.transform.SetParent(origin, false);
            GameObject hand = Instantiate(prefab, holder.transform, false);
            s.tracked = hand.GetComponent<OVRHand>();
            bool set = Set(s.tracked, "HandType", s.left ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight)
                & Set(hand.GetComponent<OVRSkeleton>(), "_updateRootPose", true);
            var meshRenderer = hand.GetComponent<OVRMeshRenderer>();
            set &= Set(meshRenderer, "_confidenceBehavior", OVRMeshRenderer.ConfidenceBehavior.None)
                & Set(meshRenderer, "_systemGestureBehavior", OVRMeshRenderer.SystemGestureBehavior.None);
            if (!set) Debug.LogWarning("[AntGarden] VR: the hand prefab has changed; the hands may be drawn wrong");
            s.skin = hand.GetComponent<SkinnedMeshRenderer>();
            var shader = Resources.Load<Shader>("AntGarden/VR/VRGhostHand");
            if (s.skin != null && shader != null)
            {
                s.look = new Material(shader);
                s.look.SetFloat("_Light", light);
                s.look.SetFloat("_Alpha", 0f);
                s.skin.sharedMaterial = s.look;
                s.skin.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                s.skin.enabled = false;
            }
            holder.SetActive(true);
        }

        static bool Set(Component c, string field, object value)
        {
            FieldInfo f = c == null ? null : c.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            if (f == null) return false;
            f.SetValue(c, value);
            return true;
        }

        /// <summary>Each controller where the SDK has it, as late as possible before a frame is drawn.</summary>
        void Pose()
        {
            foreach (Side s in new[] { l, r })
            {
                if (s.controller == null) continue;
                if (OVRInput.GetControllerPositionTracked(s.touch) || OVRInput.GetControllerOrientationTracked(s.touch))
                {
                    s.controller.localPosition = OVRInput.GetLocalControllerPosition(s.touch);
                    s.controller.localRotation = OVRInput.GetLocalControllerRotation(s.touch);
                }
            }
        }

        public void Read(ref VRPad pad)
        {
            Pinches(l);
            Pinches(r);
            Vector2 stick = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.LTouch);
            pad.move = stick.sqrMagnitude > 0.02f ? stick : l.index ? Vector2.up : Vector2.zero;
            pad.turn = OVRInput.Get(OVRInput.Axis2D.PrimaryThumbstick, OVRInput.Controller.RTouch);
            pad.run = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.LTouch);
            pad.a = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.RTouch) || r.index;
            pad.b = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.RTouch) || r.middle;
            pad.x = OVRInput.Get(OVRInput.Button.One, OVRInput.Controller.LTouch);
            pad.y = OVRInput.Get(OVRInput.Button.Two, OVRInput.Controller.LTouch) || l.middle;
            pad.menu = OVRInput.Get(OVRInput.Button.Start, OVRInput.Controller.LTouch) || OVRInput.Get(OVRInput.Button.Start, OVRInput.Controller.LHand);
            pad.walkToggle = OVRInput.Get(OVRInput.Button.PrimaryThumbstick, OVRInput.Controller.RTouch);
            pad.trigger = Mathf.Max(OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.LTouch), OVRInput.Get(OVRInput.Axis1D.PrimaryIndexTrigger, OVRInput.Controller.RTouch));
        }

        /// <summary>A hand's pinches, from a hand the cameras see well: each pressed past PinchOn, let go below PinchOff.</summary>
        static void Pinches(Side s)
        {
            OVRHand h = s.tracked;
            bool free = h != null && h.IsTracked && h.IsDataHighConfidence && !h.IsSystemGestureInProgress
                && !OVRInput.AreHandPosesGeneratedByControllerData(OVRPlugin.Step.Render, s.hand);
            s.index = Held(s.index, free ? h.GetFingerPinchStrength(OVRHand.HandFinger.Index) : 0f);
            s.middle = Held(s.middle, free ? h.GetFingerPinchStrength(OVRHand.HandFinger.Middle) : 0f);
        }

        static bool Held(bool was, float strength) => was ? strength > PinchOff : strength > PinchOn;

        public void Hint(string button) => hint = button;

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            foreach (Side s in new[] { l, r })
            {
                // a controller the headset has lost: where it was a moment, then hidden
                if (s.controller != null)
                {
                    s.lost = OVRInput.GetControllerPositionTracked(s.touch) ? 0f : s.lost + dt;
                    bool on = s.lost < LostHide;
                    if (s.controller.gameObject.activeSelf != on) s.controller.gameObject.SetActive(on);
                }
                // a hand: its last pose kept while the headset cannot see it well, fading out; back as it is seen
                if (s.look != null)
                {
                    bool seen = s.tracked != null && s.tracked.IsTracked && s.tracked.IsDataHighConfidence;
                    s.shown = Mathf.MoveTowards(s.shown, seen ? 1f : 0f, dt / FadeTime);
                    s.look.SetFloat("_Alpha", s.shown);
                    bool draw = s.shown > 0.01f;
                    if (s.skin.enabled != draw) s.skin.enabled = draw;
                }
            }
            // the letters over their buttons, a little toward the eyes, facing them; the one waited for larger and warm
            Camera cam = Camera.main;
            if (cam == null) return;
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * 6f);
            foreach (Letter t in letters)
            {
                bool on = t.at != null && t.at.gameObject.activeInHierarchy;
                if (t.sign.gameObject.activeSelf != on) t.sign.gameObject.SetActive(on);
                if (!on) continue;
                Vector3 toEye = cam.transform.position - t.at.position;
                float scale = t.sign.parent.lossyScale.x;   // (the origin's: real metres to the world's)
                t.sign.position = t.at.position + toEye.normalized * 0.006f * scale;
                t.sign.rotation = Quaternion.LookRotation(-toEye, cam.transform.up);
                bool wanted = t.button == hint;
                t.sign.localScale = Vector3.one * (LetterSize / 40f) * (wanted ? 1.5f + 0.25f * pulse : 1f);
                t.text.color = wanted ? Color.Lerp(UiKit.Warm, Color.white, pulse * 0.5f) : UiKit.Ink;
            }
        }
    }
}

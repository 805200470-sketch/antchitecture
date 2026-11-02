using System;
using System.IO;
using System.Reflection;
using System.Text;
using UnityEngine;
using UnityEngine.XR;

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
    ///   they are pressed, their letters printed on them as on the real ones, nothing added over them: the user, 11-02,
    ///   "手柄上不需要再强调按键的yxab等，应该本身就能直接看到的"), lit by themselves (VRModel: the nest has no lights for
    ///   Meta's lit shader). A controller the headset loses stays where it was a moment, then hides.
    /// - The hands: the headset's own hand mesh on its tracked skeleton (OVRHandPrefab), drawn as its home draws them
    ///   (VRGhostHand). Checked every frame: a hand the headset loses, or sees with low confidence, keeps its last pose
    ///   and fades out; it fades back as it is seen again.
    /// - Both posed as late as possible before each frame is drawn, the hands as their controllers are, so a hand round
    ///   its controller does not trail it (the SDK poses a hand in Update, a frame's work earlier).
    /// - Pinches, from a hand the cameras see (not one drawn round its controller, not in the middle of the system's own
    ///   gesture), each with two thresholds (pressed past 0.8, let go below 0.5), so a pinch held near the edge never
    ///   chatters: right thumb to index A, to middle B; left thumb to index crawls (the way the head looks), to middle Y;
    ///   the left hand's menu gesture opens the instructions.
    /// - What the headset tracked, a line a second, in vr_track.txt beside quest.json, for a headset nobody can watch
    ///   (the user, 11-02: "手柄和手势对不上"): the head as the camera has it and as the SDK has it, each controller as the
    ///   SDK and as Unity's XR have it, each hand's root against its controller and its mesh's middle.
    /// Used where Meta's runtime is up (a Quest, its Meta XR feature on: VRSetup.ConfigureQuest); elsewhere XRDevices.
    /// </summary>
    public class MetaDevices : MonoBehaviour, IVRDevices
    {
        const float PinchOn = 0.8f, PinchOff = 0.5f;
        const float FadeTime = 0.25f;     // a hand the headset loses fades out over this long (seconds), and back
        const float LostHide = 0.3f;      // a controller it loses stays where it was this long, then hides
        const float TrackEvery = 1f;      // a line of vr_track.txt this often (seconds)
        const int TrackLines = 3600;      // and at most this many (an hour)

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Register() =>
            VRDevices.Meta = () => OVRPlugin.initialized ? new GameObject("VR Devices (Meta)").AddComponent<MetaDevices>() : null;

        /// <summary>One hand's side: its controller and its hand.</summary>
        sealed class Side
        {
            public OVRInput.Controller touch;
            public OVRInput.Hand hand;
            public XRNode node;                   // its controller in Unity's XR input (the track file sets the two side by side)
            public bool left;
            public Transform controller;          // the controller's pose in the origin, Meta's model in it
            public float lost;                    // how long the headset has not seen it
            public OVRHand tracked;               // the hand
            public OVRSkeleton skeleton;          // its bones
            public OVRMesh mesh;                  // its mesh
            public OVRMeshRenderer binder;        // which binds the mesh to the bones
            public Action<OVRPlugin.Step> read;   // the hand's own reading of the headset (OVRHand.GetHandState)
            public Action pose;                   // and its bones' own posing from it (OVRSkeleton.UpdateSkeleton)
            public SkinnedMeshRenderer skin;      // its mesh, drawn
            public Material look;                 // its own material, so it fades on its own
            public float shown;                   // how much of it is drawn
            public bool index, middle;            // its pinches, held
        }

        readonly Side l = new Side { touch = OVRInput.Controller.LTouch, hand = OVRInput.Hand.HandLeft, node = XRNode.LeftHand, left = true };
        readonly Side r = new Side { touch = OVRInput.Controller.RTouch, hand = OVRInput.Hand.HandRight, node = XRNode.RightHand };
        StreamWriter track;
        float trackAt;
        int trackCount;

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
            try
            {
                track = new StreamWriter(Path.Combine(Application.persistentDataPath, "vr_track.txt"), false) { AutoFlush = true };
                track.WriteLine($"# Ant Garden VR track {DateTime.Now:yyyy-MM-dd HH:mm:ss}, a line a second. Positions in the origin's space (the headset's eye-level space), real metres. " +
                    "head: the camera (Unity's XR) / the SDK's. ctrl: OVRInput / xr: Unity's XR. hand: its root (the wrist), lost while the headset cannot see it; " +
                    "from-ctrl: its pose made from the controller's sensors; apart: root to controller; mesh: the middle of the drawn mesh.");
            }
            catch (Exception e) { Debug.LogWarning("[AntGarden] VR: no track file: " + e.Message); }
            Debug.Log($"[AntGarden] VR input: Meta XR Core SDK (OVRPlugin {OVRPlugin.version}); hands with controllers {(OVRPlugin.IsMultimodalHandsControllersSupported() ? "supported" : "NOT supported")}; drawn: controllers {l.controller != null}, hands {l.tracked != null} (posed before each frame: {l.read != null && l.pose != null}); track file {(track != null ? "on" : "off")}");
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= Pose;
            track?.Dispose();
            track = null;
        }

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

        /// <summary>A controller: Meta's model of it, lit by itself.</summary>
        void Controller(Side s, GameObject prefab, float light, Transform origin)
        {
            s.controller = new GameObject(s.left ? "VR Controller L" : "VR Controller R").transform;
            s.controller.SetParent(origin, false);
            GameObject model = Instantiate(prefab, s.controller, false);
            var helper = model.GetComponent<OVRControllerHelper>();
            if (helper != null) helper.m_controller = s.touch;   // (read as it starts, next frame)
            var shader = Resources.Load<Shader>("AntGarden/VR/VRModel");
            if (shader == null) return;
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
            s.skeleton = hand.GetComponent<OVRSkeleton>();
            s.mesh = hand.GetComponent<OVRMesh>();
            s.binder = hand.GetComponent<OVRMeshRenderer>();
            bool set = Set(s.tracked, "HandType", s.left ? OVRHand.Hand.HandLeft : OVRHand.Hand.HandRight);
            // the prefab's skeleton is saved as the older kind (the editor puts that right as the prefab goes into a scene;
            // made at runtime nothing does), so it found no data and never moved from where it was made: lying across the
            // middle of the view (the user, 11-01). Its kind and its data now come from the hand itself.
            if (s.tracked is OVRSkeleton.IOVRSkeletonDataProvider provider)
                set &= Set(s.skeleton, "_skeletonType", provider.GetSkeletonType()) & Set(s.skeleton, "_dataProvider", provider);
            set &= Set(s.skeleton, "_updateRootPose", true);
            set &= Set(s.binder, "_confidenceBehavior", OVRMeshRenderer.ConfidenceBehavior.None)
                & Set(s.binder, "_systemGestureBehavior", OVRMeshRenderer.SystemGestureBehavior.None);
            // read and posed again just before each frame is drawn, as its controller is: the hand's and its bones' own
            // methods, called later than the SDK calls them
            s.read = Method<Action<OVRPlugin.Step>>(s.tracked, "GetHandState");
            s.pose = Method<Action>(s.skeleton, "UpdateSkeleton");
            if (!set || s.read == null || s.pose == null) Debug.LogWarning("[AntGarden] VR: the hand prefab has changed; the hands may be drawn wrong");
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

        static T Method<T>(Component c, string name) where T : Delegate
        {
            MethodInfo m = c == null ? null : c.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
            return m == null ? null : Delegate.CreateDelegate(typeof(T), c, m, false) as T;
        }

        /// <summary>Each controller where the SDK has it, and each hand read again and posed, as late as possible before
        /// a frame is drawn.</summary>
        void Pose()
        {
            foreach (Side s in new[] { l, r })
            {
                if (s.controller != null && (OVRInput.GetControllerPositionTracked(s.touch) || OVRInput.GetControllerOrientationTracked(s.touch)))
                {
                    s.controller.localPosition = OVRInput.GetLocalControllerPosition(s.touch);
                    s.controller.localRotation = OVRInput.GetLocalControllerRotation(s.touch);
                }
                if (s.read != null && s.pose != null && s.tracked.isActiveAndEnabled)
                {
                    s.read(OVRPlugin.Step.Render);
                    s.pose();
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
                if (s.look == null) continue;
                // the mesh bound to the bones once both are there (on a headset the SDK binds it only as it starts: bones
                // not there yet then, a hand never drawn)
                if (s.binder != null && !s.binder.IsInitialized && s.skeleton != null && s.skeleton.IsInitialized && s.mesh != null && s.mesh.IsInitialized)
                    s.binder.ForceRebind();
                // a hand: its last pose kept while the headset cannot see it well, fading out; back as it is seen
                bool seen = s.tracked != null && s.tracked.IsTracked && s.tracked.IsDataHighConfidence;
                s.shown = Mathf.MoveTowards(s.shown, seen ? 1f : 0f, dt / FadeTime);
                s.look.SetFloat("_Alpha", s.shown);
                bool draw = s.shown > 0.01f;
                if (s.skin.enabled != draw) s.skin.enabled = draw;
            }
            if (track != null && Time.unscaledTime >= trackAt) Track();
        }

        /// <summary>A line of vr_track.txt: the head, each controller and each hand, in the origin (real metres).</summary>
        void Track()
        {
            trackAt = Time.unscaledTime + TrackEvery;
            Transform origin = transform.parent;
            var line = new StringBuilder(480);
            line.Append(Time.unscaledTime.ToString("F1"));
            Camera cam = Camera.main;
            line.Append(" | head ").Append(cam != null ? V(cam.transform.localPosition) : "-")
                .Append(" sdk ").Append(V(OVRPlugin.GetNodePose(OVRPlugin.Node.EyeCenter, OVRPlugin.Step.Render).ToOVRPose().position));
            foreach (Side s in new[] { l, r })
            {
                line.Append(s.left ? " | L" : " | R");
                bool held = OVRInput.GetControllerPositionTracked(s.touch);
                Vector3 c = OVRInput.GetLocalControllerPosition(s.touch);
                line.Append(" ctrl ").Append(held ? V(c) : "lost");
                if (InputDevices.GetDeviceAtXRNode(s.node).TryGetFeatureValue(CommonUsages.devicePosition, out Vector3 u)) line.Append(" xr ").Append(V(u));
                OVRHand h = s.tracked;
                if (h == null) continue;
                Vector3 root = h.transform.localPosition;
                line.Append(" hand ").Append(h.IsTracked ? V(root) : "lost")
                    .Append(" conf ").Append(h.HandConfidence)
                    .Append(" from-ctrl ").Append(OVRInput.AreHandPosesGeneratedByControllerData(OVRPlugin.Step.Render, s.hand) ? 1 : 0)
                    .Append(" in-hand ").Append(OVRInput.GetControllerIsInHandState(s.hand))
                    .Append(" bones ").Append(s.skeleton == null || !s.skeleton.IsInitialized ? "none" : s.skeleton.IsDataValid ? "moving" : "still")
                    .Append(" mesh ").Append(s.binder != null && s.binder.IsInitialized ? "bound" : "unbound")
                    .Append(" shown ").Append(s.shown.ToString("F2"));
                if (h.IsTracked && held) line.Append(" apart ").Append((root - c).magnitude.ToString("F3"));
                if (s.skin != null && s.skin.enabled && origin != null) line.Append(" mesh at ").Append(V(origin.InverseTransformPoint(s.skin.bounds.center)));
            }
            try { track.WriteLine(line.ToString()); }
            catch (Exception) { track = null; return; }
            if (trackCount++ % 5 == 0) Debug.Log("[AntGarden] VR track: " + line);
            if (trackCount >= TrackLines) { track.Dispose(); track = null; }
        }

        static string V(Vector3 v) => $"({v.x:F3},{v.y:F3},{v.z:F3})";
    }
}

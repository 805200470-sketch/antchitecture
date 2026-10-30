using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// Playing in a headset (a standalone Meta Quest, or PC VR through Meta Quest Link; OpenXR is started by XR Plug-in
    /// Management, see Editor/VR/VRSetup.cs). The player looks out through the ant's own eyes, at ant scale: the camera
    /// hangs in an origin at the ant's head, scaled down (scale), so a real metre is a fraction of an ant length and the
    /// plants stand round the player like trees. Y steps out behind the ant (its body seen, the origin a little larger and
    /// following it) and back in. The headset turns and moves the camera within the origin; the left stick walks the ant
    /// the way the head faces (pressed in: run); the right stick turns in steps (a smooth turn makes many people sick);
    /// A reaches out with the antennae (as E), B touches antennae (as Space); X switches the language (as L); the left
    /// controller's menu button shows every control (VRTutorial). The screen's text hangs in front of the eyes, always on
    /// top. Without a headset none of this is made and the game plays on the screen as before.
    /// </summary>
    public class VRPlayer : MonoBehaviour
    {
        /// <summary>Where the player looks from: the ant's own eyes, or behind and above it.</summary>
        public enum View { Eye, Behind }

        /// <summary>A headset is on and drawing.</summary>
        public static bool Active => XRSettings.enabled && XRSettings.isDeviceActive;

        public float scale = 0.25f;         // the origin's scale: a real metre is this many units (the ant is one unit long)
        public float eyeHeight = 0.34f;     // the ant's eyes above its feet (units)
        public float heightEase = 6f;       // how quickly the eyes follow the ant up and down (a bump must not jolt the view)
        public float turnStep = 30f;        // one flick of the right stick (degrees)
        public float textDistance = 1.5f;   // the screen's text, this far in front of the eyes (real metres)
        public float textWidth = 1.5f;      // and this wide (real metres; about 53° at 1.5 m, the text large enough to read)
        public View view = View.Eye;        // where the player starts
        public float behindScale = 0.5f;    // behind the ant: the origin's scale (the player a little larger than the ant)
        public float behindBack = 2f;       // the resting head this far behind the ant (units, the way the player faces)
        public float behindUp = 1.1f;       // and this far above its feet
        public float behindEase = 4f;       // how quickly the view catches up with the ant (it never turns by itself)

        Camera cam;
        Transform origin;
        AntController ant;
        float yaw, eyeY;
        bool centred, turned, aWas, bWas, xWas, yWas, menuWas, placeBehind, blinked;
        Vector3 rest;                       // where the head was in the origin when it was centred (the player's own resting pose)
        Vector3 behindAt;                   // the resting head's place behind the ant, eased

        /// <summary>The left stick (x right, y forward), whether it is pressed in (run), and A and B pressed this frame.</summary>
        public Vector2 Move { get; private set; }
        public bool Run { get; private set; }
        public bool Interact { get; private set; }
        public bool Talk { get; private set; }
        /// <summary>The menu button pressed this frame (every control, VRTutorial), the view switched this frame, and the
        /// turns taken so far (the tutorial's steps watch them).</summary>
        public bool Help { get; private set; }
        public bool Switched { get; private set; }
        public int Turns { get; private set; }

        public void Init(Camera camera, AntController player, Canvas hud)
        {
            cam = camera;
            ant = player;
            if (QuestLook.On) QuestLook.Tune(this);   // a standalone headset: where it starts, how it stands behind the ant
            origin = new GameObject("VR Origin").transform;
            origin.localScale = Vector3.one * scale;
            cam.transform.SetParent(origin, false);
            cam.nearClipPlane = 0.01f;
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances(subsystems);
            foreach (XRInputSubsystem s in subsystems) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
            ant.SetVisible(false);   // the player is the ant: its body would fill the view
            if (hud != null)
            {
                // the text in front of the eyes, drawn over the world
                hud.renderMode = RenderMode.WorldSpace;
                hud.worldCamera = cam;
                var rt = (RectTransform)hud.transform;
                rt.SetParent(cam.transform, false);
                rt.sizeDelta = new Vector2(1920, 1080);
                rt.localPosition = new Vector3(0f, -0.08f, textDistance);
                rt.localRotation = Quaternion.identity;
                rt.localScale = Vector3.one * (textWidth / 1920f);
                Shader.SetGlobalInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
                gameObject.AddComponent<VRTutorial>().Init(this, hud, player);
            }
            eyeY = ant.Position.y + eyeHeight;
            if (view == View.Behind) SetView(View.Behind);
            Application.onBeforeRender += Pose;
            Debug.Log($"[AntGarden] VR: {XRSettings.loadedDeviceName}, {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} per eye, refresh {XRDevice.refreshRate:F0} Hz, world scale 1/{scale:F2}, view {view}");
        }

        void OnDestroy() => Application.onBeforeRender -= Pose;

        /// <summary>Through the ant's eyes (its body hidden), or behind it (its body seen, the world a little smaller).</summary>
        public void SetView(View v)
        {
            view = v;
            origin.localScale = Vector3.one * (v == View.Eye ? scale : behindScale);
            ant.SetVisible(v == View.Behind);
            placeBehind = true;   // straight there: a glide would pass through the walls
            // and in a blink: dark at once, clearing over a third of a second (the cut is gentler on the stomach)
            Game.Instance?.hud?.SetFade(1f, true);
            blinked = true;
            Debug.Log($"[AntGarden] VR view: {v}");
        }

        /// <summary>The ant's way of walking relative to where the head faces (forward, right; on the floor).</summary>
        public (Vector2 fwd, Vector2 right) Basis()
        {
            Vector3 f = cam.transform.forward;
            var fwd = new Vector2(f.x, f.z);
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector2.up;
            return (fwd, new Vector2(fwd.y, -fwd.x));
        }

        void Update()
        {
            Pose();
            if (blinked) { blinked = false; Game.Instance?.hud?.SetFade(0f); }
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Move = left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 m) && m.sqrMagnitude > 0.02f ? Vector2.ClampMagnitude(m, 1f) : Vector2.zero;
            Run = left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool run) && run;
            bool a = right.TryGetFeatureValue(CommonUsages.primaryButton, out bool av) && av;
            bool b = right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bv) && bv;
            bool x = left.TryGetFeatureValue(CommonUsages.primaryButton, out bool xv) && xv;
            bool y = left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool yv) && yv;
            bool menu = left.TryGetFeatureValue(CommonUsages.menuButton, out bool mv) && mv;
            Interact = a && !aWas;
            Talk = b && !bWas;
            Switched = y && !yWas;
            Help = menu && !menuWas;
            bool language = x && !xWas;
            aWas = a;
            bWas = b;
            xWas = x;
            yWas = y;
            menuWas = menu;
            if (Switched) SetView(view == View.Eye ? View.Behind : View.Eye);
            if (language && Game.Instance != null) Game.Instance.ToggleLanguage();
            // the right stick turns in steps: one step per flick, the stick must come back before the next
            float tx = right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 t) ? t.x : 0f;
            if (!turned && Mathf.Abs(tx) > 0.7f) { yaw += Mathf.Sign(tx) * turnStep; turned = true; Turns++; }
            else if (turned && Mathf.Abs(tx) < 0.3f) turned = false;
        }

        void LateUpdate()
        {
            if (ant == null) return;
            if (!centred && cam.transform.localRotation != Quaternion.identity)
            {
                // the first real pose: the player starts looking the way the ant faces
                rest = cam.transform.localPosition;
                Vector3 af = ant.transform.forward;
                yaw = Mathf.Atan2(af.x, af.z) * Mathf.Rad2Deg - cam.transform.localEulerAngles.y;
                centred = true;
            }
            Vector3 p = ant.Position;
            eyeY = Mathf.Lerp(eyeY, p.y + eyeHeight, 1f - Mathf.Exp(-heightEase * Time.deltaTime));
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            origin.rotation = r;
            if (view == View.Eye)
            {
                // the resting head at the ant's eyes; leaning moves it a little (in real metres times scale)
                origin.position = new Vector3(p.x, eyeY, p.z) - r * (rest * scale);
                return;
            }
            // behind the ant, the way the player faces: the view follows the ant but only the stick turns it
            Vector3 want = new Vector3(p.x, eyeY - eyeHeight + behindUp, p.z) - r * Vector3.forward * behindBack;
            behindAt = placeBehind ? want : Vector3.Lerp(behindAt, want, 1f - Mathf.Exp(-behindEase * Time.deltaTime));
            placeBehind = false;
            origin.position = behindAt - r * (rest * behindScale);
        }

        /// <summary>The headset's pose, as late as possible before each frame is drawn (and once in Update, for play).</summary>
        void Pose()
        {
            if (cam == null) return;
            InputDevice head = InputDevices.GetDeviceAtXRNode(XRNode.CenterEye);
            if (!head.isValid) head = InputDevices.GetDeviceAtXRNode(XRNode.Head);
            if (head.TryGetFeatureValue(CommonUsages.centerEyePosition, out Vector3 p) || head.TryGetFeatureValue(CommonUsages.devicePosition, out p))
                cam.transform.localPosition = p;
            if (head.TryGetFeatureValue(CommonUsages.centerEyeRotation, out Quaternion q) || head.TryGetFeatureValue(CommonUsages.deviceRotation, out q))
                cam.transform.localRotation = q;
        }
    }
}

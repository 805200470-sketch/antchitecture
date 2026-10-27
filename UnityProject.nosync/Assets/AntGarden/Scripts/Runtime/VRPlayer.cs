using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// Playing in a headset (PC VR: a Meta Quest through Meta Quest Link; OpenXR is started by XR Plug-in Management,
    /// see Editor/VR/VRSetup.cs). The player looks out through the ant's own eyes, at ant scale: the camera hangs in an
    /// origin at the ant's head, scaled down (scale), so a real metre is a fraction of an ant length and the plants stand
    /// round the player like trees. The headset turns and moves the camera within the origin; the left stick walks the
    /// ant the way the head faces (pressed in: run); the right stick turns in steps (a smooth turn makes many people
    /// sick); A reaches out with the antennae (as E), B touches antennae (as Space). The screen's text hangs in front of
    /// the eyes, always on top. Without a headset none of this is made and the game plays on the screen as before.
    /// </summary>
    public class VRPlayer : MonoBehaviour
    {
        /// <summary>A headset is on and drawing.</summary>
        public static bool Active => XRSettings.enabled && XRSettings.isDeviceActive;

        public float scale = 0.25f;         // the origin's scale: a real metre is this many units (the ant is one unit long)
        public float eyeHeight = 0.34f;     // the ant's eyes above its feet (units)
        public float heightEase = 6f;       // how quickly the eyes follow the ant up and down (a bump must not jolt the view)
        public float turnStep = 30f;        // one flick of the right stick (degrees)
        public float textDistance = 1.5f;   // the screen's text, this far in front of the eyes (real metres)

        Camera cam;
        Transform origin;
        AntController ant;
        float yaw, eyeY;
        bool centred, turned, aWas, bWas;
        Vector3 rest;                       // where the head was in the origin when it was centred (the player's own resting pose)

        /// <summary>The left stick (x right, y forward), whether it is pressed in (run), and A and B pressed this frame.</summary>
        public Vector2 Move { get; private set; }
        public bool Run { get; private set; }
        public bool Interact { get; private set; }
        public bool Talk { get; private set; }

        public void Init(Camera camera, AntController player, Canvas hud)
        {
            cam = camera;
            ant = player;
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
                rt.localScale = Vector3.one * (0.9f * textDistance / 1920f);   // about 50° wide
                Shader.SetGlobalInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            }
            eyeY = ant.Position.y + eyeHeight;
            Application.onBeforeRender += Pose;
            Debug.Log($"[AntGarden] VR: {XRSettings.loadedDeviceName}, {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} per eye, refresh {XRDevice.refreshRate:F0} Hz, world scale 1/{scale:F2}");
        }

        void OnDestroy() => Application.onBeforeRender -= Pose;

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
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Move = left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 m) && m.sqrMagnitude > 0.02f ? Vector2.ClampMagnitude(m, 1f) : Vector2.zero;
            Run = left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool run) && run;
            bool a = right.TryGetFeatureValue(CommonUsages.primaryButton, out bool av) && av;
            bool b = right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bv) && bv;
            Interact = a && !aWas;
            Talk = b && !bWas;
            aWas = a;
            bWas = b;
            // the right stick turns in steps: one step per flick, the stick must come back before the next
            float tx = right.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 t) ? t.x : 0f;
            if (!turned && Mathf.Abs(tx) > 0.7f) { yaw += Mathf.Sign(tx) * turnStep; turned = true; }
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
            origin.rotation = Quaternion.Euler(0f, yaw, 0f);
            // the resting head at the ant's eyes; leaning moves it a little (in real metres times scale)
            origin.position = new Vector3(p.x, eyeY, p.z) - origin.rotation * (rest * scale);
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

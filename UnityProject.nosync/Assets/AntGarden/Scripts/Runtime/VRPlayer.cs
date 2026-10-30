using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// Playing in a headset (a standalone Meta Quest, or PC VR through Meta Quest Link; OpenXR is started by XR Plug-in
    /// Management, see Editor/VR/VRSetup.cs). The player looks out through the ant's own eyes, at ant scale: the camera
    /// hangs in an origin just above the ant's head, scaled down (scale), so a real metre is a fraction of an ant length
    /// and the plants stand round the player like trees; the ant's own body is there too (body): its head below the
    /// view, its antennae reaching out ahead, its legs when the player looks down. Y steps out behind the ant (the origin
    /// a little larger and following it) and back in. The headset turns and moves the camera within the origin, and the
    /// ant turns the way the head looks (gazeTurn; without it the right stick turns in steps); the left stick walks it
    /// that way (pressed in: run); A reaches out with the antennae (as E), B touches antennae (as Space); X switches the
    /// language (as L); the left controller's menu button shows every control (VRTutorial). The controllers are drawn in
    /// the player's hands (VRHands). The player stands still: a lean moves the view, a step further does not (walking
    /// about a room in a headset is how people get hurt), unless walking for real is switched on (the right stick's
    /// click), when the ant crawls after each step. The screen's text hangs in front of the eyes, always on top. Without
    /// a headset none of this is made and the game plays on the screen as before.
    /// </summary>
    public class VRPlayer : MonoBehaviour
    {
        /// <summary>Where the player looks from: the ant's own eyes, or behind and above it.</summary>
        public enum View { Eye, Behind }

        /// <summary>A headset is on and drawing.</summary>
        public static bool Active => XRSettings.enabled && XRSettings.isDeviceActive;

        public float scale = 0.25f;         // the origin's scale: a real metre is this many units (the ant is one unit long)
        public float eyeHeight = 0.34f;     // without the ant's body: the eyes above its feet, over its middle (units)
        public float heightEase = 6f;       // how quickly the eyes follow the ant up and down (a bump must not jolt the view)
        public float turnStep = 30f;        // one flick of the right stick (degrees), when the head does not turn the ant
        public float textDistance = 1.5f;   // the screen's text, this far in front of the eyes (real metres)
        public float textWidth = 1.5f;      // and this wide (real metres; about 53° at 1.5 m, the text large enough to read)
        public View view = View.Eye;        // where the player starts
        public float behindScale = 0.5f;    // behind the ant: the origin's scale (the player a little larger than the ant)
        public float behindBack = 2f;       // the resting head this far behind the ant (units)
        public float behindUp = 1.1f;       // and this far above its feet
        public float behindEase = 4f;       // how quickly the view catches up with the ant
        public float behindTurn = 1.5f;     // and swings round behind it as it crawls (1/s at a crawl; never while it stands)
        public float leanRadius = 0.3f;     // the head may lean this far from where the player stands (real metres); further, the view stays put
        public bool realWalk;               // walking for real: a step past leanRadius sends the ant crawling after it (the right stick's click switches)
        public bool gazeTurn = true;        // the ant turns the way the head looks (off: the right stick turns in steps)
        public float gazeDeadZone = 40f;    // the head may look this far aside (degrees) without turning the ant
        public float gazeDwell = 0.5f;      // further, for this long (seconds), and the ant turns after it
        public bool body = true;            // through the ant's eyes its own body is there: its head below, legs, antennae
        public float eyeAbove = 0.15f;      // the eyes this far above the middle of its head (units, for an ant one long)
        public float eyeAhead = 0.07f;      // and this far ahead of it
        public bool hands = true;           // the controllers drawn in the player's hands (VRHands)
        public bool ghostHands = true;      // and the hands round them, as the headset's home draws them
        public float handLight = 1f;        // and how bright
        public float lookDown;              // the view tilted down this far (degrees): screenshots on a headset nobody wears

        Camera cam;
        Transform origin;
        AntController ant;
        float yaw, floorY, behindYaw, lastHeading, turnedBy, aside;
        bool centred, turned, chasing, aWas, bWas, xWas, yWas, menuWas, clickWas, placeBehind, blinked;
        Vector3 rest;                       // where the head was in the origin when it was centred (the player's own resting pose)
        Vector3 behindAt;                   // the resting head's place behind the ant, eased
        Vector3 pending, antLast;           // walking for real: how far the ant has still to crawl after the player's steps (world)
        Vector3 eyeAt;                      // the eyes on the ant: up from its feet (y) and ahead of its middle (z), units

        /// <summary>The left stick (x right, y forward), whether it is pressed in (run), and A and B pressed this frame.</summary>
        public Vector2 Move { get; private set; }
        public bool Run { get; private set; }
        public bool Interact { get; private set; }
        public bool Talk { get; private set; }
        /// <summary>The menu button pressed this frame (every control, VRTutorial), the view switched this frame, and the
        /// turns the ant has taken so far, one for every 45° (the tutorial's steps watch them).</summary>
        public bool Help { get; private set; }
        public bool Switched { get; private set; }
        public int Turns { get; private set; }

        public void Init(Camera camera, AntController player, Canvas hud)
        {
            cam = camera;
            ant = player;
            if (QuestLook.On) QuestLook.Tune(this);   // a standalone headset: where it starts, how it turns, what it shows
            origin = new GameObject("VR Origin").transform;
            origin.localScale = Vector3.one * scale;
            cam.transform.SetParent(origin, false);
            cam.nearClipPlane = 0.01f;
            var subsystems = new List<XRInputSubsystem>();
            SubsystemManager.GetInstances(subsystems);
            foreach (XRInputSubsystem s in subsystems) s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
            eyeAt = new Vector3(0f, eyeHeight, 0f);
            if (body) body = FindEyes();
            ant.SetVisible(body);   // the player is the ant: its body in view (or, without its head found, none of it)
            if (hands) gameObject.AddComponent<VRHands>().Init(origin, handLight, ghostHands);
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
            floorY = ant.Position.y;
            antLast = ant.Position;
            lastHeading = behindYaw = ant.heading;
            if (view == View.Behind) SetView(View.Behind);
            Application.onBeforeRender += Pose;
            Debug.Log($"[AntGarden] VR: {XRSettings.loadedDeviceName}, {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} per eye, refresh {XRDevice.refreshRate:F0} Hz, world scale 1/{scale:F2}, view {view}, turning {(gazeTurn ? "by gaze" : "by stick")}, body {body}, hands {hands}");
        }

        void OnDestroy() => Application.onBeforeRender -= Pose;

        /// <summary>The ant's own body seen through its eyes: the eyes just above and ahead of the middle of its head (its
        /// head bone, measured as it stands), so the head is below the view and the antennae reach out in front of it.
        /// False if its skeleton has no head where an ant's would be.</summary>
        bool FindEyes()
        {
            Transform head = Find(ant.Model, "head");
            if (head == null) return false;
            Vector3 local = Quaternion.Euler(0f, -ant.heading * Mathf.Rad2Deg, 0f) * (head.position - ant.Position);
            float len = ant.cfg.bodyLength;
            if (local.y < 0.1f * len || local.y > 1.5f * len || Mathf.Abs(local.z) > len) return false;
            eyeAt = new Vector3(0f, local.y + eyeAbove * len, local.z + eyeAhead * len);
            Debug.Log($"[AntGarden] VR eyes: the head at {local}, the eyes at {eyeAt}");
            return true;
        }

        static Transform Find(Transform t, string name)
        {
            if (t == null) return null;
            if (t.name == name) return t;
            foreach (Transform child in t)
            {
                Transform found = Find(child, name);
                if (found != null) return found;
            }
            return null;
        }

        /// <summary>Through the ant's eyes (its body below them), or behind it (the world a little smaller).</summary>
        public void SetView(View v)
        {
            view = v;
            origin.localScale = Vector3.one * (v == View.Eye ? scale : behindScale);
            ant.SetVisible(v == View.Behind || body);
            behindYaw = ant.heading;   // straight behind it
            placeBehind = true;   // straight there: a glide would pass through the walls
            // and in a blink: dark at once, clearing over a third of a second (the cut is gentler on the stomach)
            Game.Instance?.hud?.SetFade(1f, true);
            blinked = true;
            Debug.Log($"[AntGarden] VR view: {v}");
        }

        /// <summary>Walking for real, or standing still and crawling with the stick (the safer way, and the default).</summary>
        public void SetWalk(bool real)
        {
            realWalk = real;
            pending = Vector3.zero;
            Game.Instance?.hud?.ShowLine(Loc.T("走动方式", "Moving"), real
                ? Loc.T("真实走动：开。你迈步，蚂蚁跟着爬。注意周围，别走出边界。", "Walking for real: on. Step, and the ant crawls after you. Mind your surroundings.")
                : Loc.T("真实走动：关。站在原地，用左摇杆爬。", "Walking for real: off. Stand still and crawl with the left stick."));
            Debug.Log($"[AntGarden] VR walking: {(real ? "real" : "stick")}");
        }

        /// <summary>Walking for real: the way to where the player has stepped, as a push of the left stick (covered in
        /// about a third of a second, at most at a crawl).</summary>
        Vector2 Walk()
        {
            if (!realWalk || pending.sqrMagnitude < 1e-6f) return Vector2.zero;
            var (fwd, right) = Basis();
            Vector2 to = new Vector2(pending.x, pending.z) / (Mathf.Max(0.1f, ant.cfg.crawlSpeed) * 0.35f);
            return new Vector2(Vector2.Dot(to, right), Vector2.Dot(to, fwd));
        }

        /// <summary>Where the player stands: a lean within leanRadius moves the view, a step beyond it does not (the view
        /// stays at the edge of the lean), unless walking for real, when the step is passed on to the ant to crawl.</summary>
        void Stand(Vector3 p, Quaternion r)
        {
            Vector3 moved = p - antLast;
            moved.y = 0f;
            antLast = p;
            if (pending.sqrMagnitude > 1e-8f)
            {
                Vector3 dir = pending.normalized;
                pending -= dir * Mathf.Clamp(Vector3.Dot(moved, dir), 0f, pending.magnitude);
                pending *= Mathf.Exp(-Time.deltaTime / 2f);   // what the ant cannot reach (a wall in the way) fades
            }
            if (!centred) return;
            Vector3 off = cam.transform.localPosition - rest;
            off.y = 0f;
            if (off.magnitude <= leanRadius) return;
            Vector3 step = off - off.normalized * leanRadius;
            rest += step;
            if (realWalk) pending = Vector3.ClampMagnitude(pending + r * step * origin.localScale.x, 1.5f);
        }

        /// <summary>The ant's way of walking relative to where the head faces (forward, right; on the floor).</summary>
        public (Vector2 fwd, Vector2 right) Basis()
        {
            Vector3 f = cam.transform.forward;
            var fwd = new Vector2(f.x, f.z);
            fwd = fwd.sqrMagnitude > 1e-6f ? fwd.normalized : Vector2.up;
            return (fwd, new Vector2(fwd.y, -fwd.x));
        }

        static Vector3 Dir(float heading) => new Vector3(Mathf.Sin(heading), 0f, Mathf.Cos(heading));

        /// <summary>The ant turns the way the head looks: once the head has looked further aside than gazeDeadZone for
        /// gazeDwell, the ant turns after it at its own pace until it faces the same way (a glance at a leg or an antenna
        /// turns nothing; while the stick walks it, it already goes the head's way). Looking well up or down turns
        /// nothing either.</summary>
        void FollowGaze()
        {
            Vector3 f = cam.transform.forward;
            if (Mathf.Abs(f.y) > 0.6f || Move.sqrMagnitude > 0.0025f) { chasing = false; aside = 0f; return; }
            float diff = Mathf.DeltaAngle(ant.heading * Mathf.Rad2Deg, Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg);
            aside = Mathf.Abs(diff) > gazeDeadZone ? aside + Time.deltaTime : 0f;
            if (aside > gazeDwell) chasing = true;
            else if (Mathf.Abs(diff) < 3f) chasing = false;
            if (!chasing) return;
            float most = ant.cfg.turnRate * Mathf.Rad2Deg * 0.7f * Time.deltaTime;
            float step = Mathf.Clamp(diff * ant.cfg.turnGain * Time.deltaTime, -most, most);
            ant.heading += step * Mathf.Deg2Rad;
            ant.transform.rotation = Quaternion.AngleAxis(step, Vector3.up) * ant.transform.rotation;   // its body now, not at its next step
        }

        /// <summary>However the ant turned this frame (after the head, or the stick's way as it crawls): through its eyes
        /// it turns about them rather than its middle, so the view stays put and its body sweeps round under the player;
        /// and its turns are counted.</summary>
        void Turned()
        {
            float h = ant.heading, d = Mathf.DeltaAngle(lastHeading * Mathf.Rad2Deg, h * Mathf.Rad2Deg);
            if (view == View.Eye && Mathf.Abs(d) > 0.01f)
            {
                Vector3 shift = (Dir(lastHeading) - Dir(h)) * eyeAt.z;
                ant.transform.position += shift;
                antLast += shift;   // not a step of the player's
            }
            turnedBy += Mathf.Abs(d);
            if (turnedBy >= 45f) { turnedBy = 0f; Turns++; }
            lastHeading = h;
        }

        void Update()
        {
            Pose();
            if (blinked) { blinked = false; Game.Instance?.hud?.SetFade(0f); }
            InputDevice left = InputDevices.GetDeviceAtXRNode(XRNode.LeftHand), right = InputDevices.GetDeviceAtXRNode(XRNode.RightHand);
            Vector2 stick = left.TryGetFeatureValue(CommonUsages.primary2DAxis, out Vector2 m) && m.sqrMagnitude > 0.02f ? m : Vector2.zero;
            Move = Vector2.ClampMagnitude(stick + Walk(), 1f);
            Run = left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool run) && run;
            bool a = right.TryGetFeatureValue(CommonUsages.primaryButton, out bool av) && av;
            bool b = right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bv) && bv;
            bool x = left.TryGetFeatureValue(CommonUsages.primaryButton, out bool xv) && xv;
            bool y = left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool yv) && yv;
            bool menu = left.TryGetFeatureValue(CommonUsages.menuButton, out bool mv) && mv;
            bool click = right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool cv) && cv;
            if (click && !clickWas) SetWalk(!realWalk);
            clickWas = click;
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
            if (gazeTurn) return;
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
            if (centred && gazeTurn) FollowGaze();
            Turned();
            Vector3 p = ant.Position;
            floorY = Mathf.Lerp(floorY, p.y, 1f - Mathf.Exp(-heightEase * Time.deltaTime));
            origin.rotation = Quaternion.Euler(lookDown, yaw, 0f);
            Stand(p, Quaternion.Euler(0f, yaw, 0f));
            if (view == View.Eye)
            {
                // the resting head at the ant's eyes; leaning moves it a little (in real metres times scale)
                Vector3 eye = p + Dir(ant.heading) * eyeAt.z;
                origin.position = new Vector3(eye.x, floorY + eyeAt.y, eye.z) - origin.rotation * (rest * scale);
                return;
            }
            // behind the ant, along the way it crawls: the view follows it, and swings round behind it only while it
            // crawls (looking about turns the ant, never the view)
            float crawl = Mathf.Clamp01(ant.speed / Mathf.Max(0.05f, ant.cfg.crawlSpeed));
            behindYaw = Mathf.LerpAngle(behindYaw * Mathf.Rad2Deg, ant.heading * Mathf.Rad2Deg, 1f - Mathf.Exp(-behindTurn * crawl * Time.deltaTime)) * Mathf.Deg2Rad;
            Vector3 want = new Vector3(p.x, floorY + behindUp, p.z) - Dir(behindYaw) * behindBack;
            behindAt = placeBehind ? want : Vector3.Lerp(behindAt, want, 1f - Mathf.Exp(-behindEase * Time.deltaTime));
            placeBehind = false;
            origin.position = behindAt - origin.rotation * (rest * behindScale);
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

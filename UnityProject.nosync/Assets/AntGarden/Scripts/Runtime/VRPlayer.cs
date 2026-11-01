using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.XR;

namespace AntGarden
{
    /// <summary>
    /// Playing in a headset (a standalone Meta Quest, or PC VR through Meta Quest Link; OpenXR is started by XR Plug-in
    /// Management, see Editor/VR/VRSetup.cs). The player looks out through the ant's own eyes, at ant scale: the camera
    /// hangs in an origin at the ant's eyes, scaled down (scale), so a real metre is a fraction of an ant length and the
    /// plants stand round the player like trees; the ant's own body is there too (body), see-through and carried with the
    /// view (VRSelf): its legs when the player looks down. Y steps out behind the ant (the origin a little larger and
    /// moving with it) and back in. The headset turns and moves the camera within the origin, and the
    /// ant turns the way the head looks (gazeTurn; without it the right stick turns in steps); the left stick walks it
    /// that way (pressed in: run); A reaches out with the antennae (as E), B touches antennae (as Space); X switches the
    /// language (as L); the left controller's menu button shows every control (VRTutorial). The controllers are drawn in
    /// the player's hands (VRHands). The player stands still: a lean moves the view, a step further does not (walking
    /// about a room in a headset is how people get hurt; a player who wanders off is asked back, strayWarn), unless
    /// walking for real is switched on (the right stick's click), when the ant crawls after each step. While the ant
    /// crawls the edges of the view darken (comfort, VRComfort), against motion sickness. The screen's text hangs in front
    /// of the eyes, always on top. Without a headset none of this is made and the game plays on the screen as before
    /// (unless -vrpreview asks for the headset's view on the screen: VRPreview).
    /// </summary>
    public class VRPlayer : MonoBehaviour
    {
        /// <summary>Where the player looks from: the ant's own eyes, or behind and above it.</summary>
        public enum View { Eye, Behind }

        /// <summary>A headset is on and drawing (or a computer shows what it would: VRPreview).</summary>
        public static bool Active => (XRSettings.enabled && XRSettings.isDeviceActive) || VRPreview.On;

        public float scale = 0.25f;         // the origin's scale: a real metre is this many units (the ant is one unit long)
        public float eyeHeight = 0.34f;     // without the ant's body: the eyes above its feet, over its middle (units)
        public float heightEase = 4f;       // how quickly the eyes follow the ant up and down (a bump must not jolt the view; slower sickens less)
        public float turnStep = 30f;        // one flick of the right stick (degrees), when the head does not turn the ant
        public float textDistance = 1.5f;   // the screen's text, this far in front of the eyes (real metres)
        public float textWidth = 1.5f;      // and this wide (real metres; about 53° at 1.5 m, the text large enough to read)
        public View view = View.Eye;        // where the player starts
        public float behindScale = 0.5f;    // behind the ant: the origin's scale (the player a little larger than the ant)
        public float behindBack = 2f;       // the resting head this far behind the ant (units)
        public float behindUp = 1.1f;       // and this far above its feet
        public float behindSwing = 60f;     // the head turned this far from the way the view looks past the ant, for a while: in a blink the view is behind it again
        public float leanRadius = 0.3f;     // the head may lean this far from where the player stands (real metres); further, the view stays put
        public bool realWalk;               // walking for real: a step past leanRadius sends the ant crawling after it (the right stick's click switches)
        public bool gazeTurn = true;        // the ant turns the way the head looks (off: the right stick turns in steps)
        public float gazeDeadZone = 40f;    // the head may look this far aside (degrees) without turning the ant
        public float gazeDwell = 0.5f;      // further, for this long (seconds), and the ant turns after it
        public bool body = true;            // through the ant's eyes its own body is there: its legs, its antennae ahead
        public float selfAlpha = 0.8f;      // see-through: its outline this opaque, the rest of it fainter, nothing of it close to the eyes (VRSelf)
        public float selfPoints = 0.5f;     // drawn with this share of all its points, each smaller than the other ants'
        public float selfSize = 0.5f;       // and each this much of the size that would just cover it (smaller: a finer outline)
        public float eyeAbove = 0f;         // the view this far above the ant's eyes (units, for an ant one long)
        public float eyeAhead = 0.02f;      // and this far ahead of its face
        public float comfort = 0.6f;        // how dark the view's edges grow while the ant crawls (0: never; VRComfort)
        public float strayWarn = 0.6f;      // standing still to play: a head this far from where the player started (real metres) is asked back (0: never)
        public string tutorial = "auto";    // the tutorial: "auto" (the first time on this headset), "always", "never" (VRTutorial)
        public float focusDark = 0.65f;     // at its last step the nest this dark round the way's light (0: not at all; VRPathFocus)
        public float focusTime = 10f;       // for this long (seconds)
        public int tutorialFrom;            // tests: the tutorial from this step on (5: the way on, its light picked out; -1: the instructions instead)
        public float testCrawl;             // tests on a headset nobody wears: the ant crawls the way the headset faces for this long once the nest is ready (seconds)
        public bool hands = true;           // the controllers drawn in the player's hands (VRHands)
        public bool ghostHands = true;      // and the hands round them, as the headset's home draws them
        public float handLight = 1f;        // and how bright
        public float lookDown;              // the view tilted down this far (degrees): screenshots on a headset nobody wears

        Camera cam;
        Transform origin;
        AntController ant;
        float yaw, floorY, behindYaw, lastHeading, turnedBy, aside, away, warned, readyAt;
        bool centred, turned, chasing, aWas, bWas, xWas, yWas, menuWas, clickWas, triggerHeld, blinked;
        readonly List<XRInputSubsystem> subsystems = new List<XRInputSubsystem>();
        Vector3 rest;                       // where the head was in the origin when it was centred (the player's own resting pose)
        Vector3 spot;                       // and where the player stood then (the origin's floor, real metres)
        Vector3 pending, antLast;           // walking for real: how far the ant has still to crawl after the player's steps (world)
        Vector3 eyeAt;                      // the eyes on the ant: up from its feet (y) and ahead of its middle (z), units
        VRSelf self;                        // the ant's own body through its eyes
        Renderer ring;                      // the glowing ring round its feet (from behind; through its eyes it is all round the player)

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
        /// <summary>Either trigger pulled this frame (pulled past 0.7, let go below 0.3 before it counts again).</summary>
        public bool Trigger { get; private set; }

        public void Init(Camera camera, AntController player, Canvas hud)
        {
            cam = camera;
            ant = player;
            if (QuestLook.On) QuestLook.Tune(this);   // a standalone headset: where it starts, how it turns, what it shows
            origin = new GameObject("VR Origin").transform;
            origin.localScale = Vector3.one * scale;
            cam.transform.SetParent(origin, false);
            cam.nearClipPlane = 0.01f;
            SubsystemManager.GetInstances(subsystems);
            foreach (XRInputSubsystem s in subsystems)
            {
                s.TrySetTrackingOriginMode(TrackingOriginModeFlags.Device);
                s.trackingOriginUpdated += Recentred;
            }
            eyeAt = new Vector3(0f, eyeHeight, 0f);
            if (body) body = FindEyes();
            if (body) self = VRSelf.Make(GetComponent<ParticleAnts>(), ant, selfPoints, selfSize, selfAlpha);
            if (self == null) body = false;   // (a solid body round the eyes would hide the view)
            ant.SetVisible(body);   // the player is the ant: its body in view (or, without its head found, none of it)
            ring = GameObject.Find("PlayerRing")?.GetComponent<Renderer>();
            if (hands) gameObject.AddComponent<VRHands>().Init(origin, handLight, ghostHands);
            if (comfort > 0f) cam.gameObject.AddComponent<VRComfort>().Init(ant, comfort);
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
                if (focusDark > 0f) gameObject.AddComponent<VRPathFocus>().Init(cam, focusDark, focusTime);
                gameObject.AddComponent<VRTutorial>().Init(this, hud, player);
            }
            floorY = ant.Position.y;
            antLast = ant.Position;
            readyAt = Time.time;
            lastHeading = behindYaw = ant.heading;
            if (view == View.Behind) SetView(View.Behind);
            Application.onBeforeRender += Pose;
            if (VRPreview.On) gameObject.AddComponent<VRPreview>().Init(cam);
            Debug.Log($"[AntGarden] VR: {XRSettings.loadedDeviceName}, {XRSettings.eyeTextureWidth}x{XRSettings.eyeTextureHeight} per eye, refresh {XRDevice.refreshRate:F0} Hz, world scale 1/{scale:F2}, view {view}, turning {(gazeTurn ? "by gaze" : "by stick")}, body {body} ({selfAlpha:F2}), hands {hands}, comfort {comfort:F2}");
        }

        void OnDestroy()
        {
            Application.onBeforeRender -= Pose;
            foreach (XRInputSubsystem s in subsystems) s.trackingOriginUpdated -= Recentred;
        }

        /// <summary>The headset recentred (the player held its Meta button): the player starts again looking the way the
        /// ant faces, standing where they are.</summary>
        void Recentred(XRInputSubsystem s)
        {
            centred = false;
            Debug.Log("[AntGarden] VR: recentred");
        }

        /// <summary>The ant's own body seen through its eyes: the view between its eyes, at their height, just in front of
        /// its face (the user, 11-01: from above its head "感觉像是骑着蚂蚁", "摄像头位置可以在蚂蚁眼部"), so its antennae
        /// reach out ahead and its legs are below. Measured as it stands; without its eyes in its points file, just above
        /// and ahead of the middle of its head bone. False if its skeleton has no head where an ant's would be.</summary>
        bool FindEyes()
        {
            Transform head = Find(ant.Model, "head");
            if (head == null) return false;
            Quaternion toAnt = Quaternion.Euler(0f, -ant.heading * Mathf.Rad2Deg, 0f);
            Vector3 local = toAnt * (head.position - ant.Position);
            float len = ant.cfg.bodyLength;
            if (local.y < 0.1f * len || local.y > 1.5f * len || Mathf.Abs(local.z) > len) return false;
            if (EyesFromPoints(toAnt, out Vector3 eyes, out float face) && Mathf.Abs(eyes.z - local.z) < 0.5f * len && eyes.y > 0f)
                eyeAt = new Vector3(0f, eyes.y + eyeAbove * len, Mathf.Max(eyes.z, face) + eyeAhead * len);
            else
                eyeAt = new Vector3(0f, local.y + 0.15f * len, local.z + 0.07f * len);
            Debug.Log($"[AntGarden] VR eyes: the head at {local}, its eyes at {eyes} (face at {face:F3}), the view at {eyeAt}");
            return true;
        }

        /// <summary>The middle of the ant's eyes in its own frame (y up from its feet, z ahead of its middle) and how far
        /// ahead its face is there (the furthest of its head's own points ahead, near its middle line, at the eyes'
        /// height), from its points file (as ParticleAnts reads it: the eyes are the points marked 1), skinned as its
        /// skeleton stands now.</summary>
        bool EyesFromPoints(Quaternion toAnt, out Vector3 eyes, out float face)
        {
            eyes = Vector3.zero;
            face = float.MinValue;
            var asset = ant.Ref != null ? Resources.Load<TextAsset>("AntGarden/Points/ant_" + ant.Ref.id) : null;
            if (asset == null) return false;
            var faces = new List<Vector3>();
            int seen = 0;
            using (var r = new BinaryReader(new MemoryStream(asset.bytes)))
            {
                int magic = r.ReadInt32();
                if (magic != 0x32414741 && magic != 0x33414741) return false;   // "AGA2", "AGA3" (with its area)
                int ns = r.ReadInt32();
                var skin = new Matrix4x4[ns];
                var onHead = new bool[ns];
                for (int i = 0; i < ns; i++)
                {
                    string path = r.ReadString();
                    var pose = new Matrix4x4();
                    for (int j = 0; j < 16; j++) pose[j] = r.ReadSingle();
                    Transform bone = string.IsNullOrEmpty(path) ? ant.Model : ant.Model.Find(path) ?? ant.Model;
                    skin[i] = bone.localToWorldMatrix * pose;
                    onHead[i] = bone.name == "head";
                }
                int n = r.ReadInt32();
                if (magic == 0x33414741) r.ReadSingle();
                for (int i = 0; i < n; i++)
                {
                    var pos = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    r.ReadSingle(); r.ReadSingle(); r.ReadSingle();   // its normal
                    r.ReadByte(); r.ReadByte(); r.ReadByte();         // its colour
                    int kind = r.ReadByte();
                    int s0 = r.ReadUInt16(), s1 = r.ReadUInt16();
                    float w = r.ReadSingle();
                    if (s0 >= ns || s1 >= ns || (kind != 1 && !onHead[w >= 0.5f ? s0 : s1])) continue;
                    Vector3 p = toAnt * (Vector3.Lerp(skin[s1].MultiplyPoint3x4(pos), skin[s0].MultiplyPoint3x4(pos), w) - ant.Position);
                    if (kind == 1) { eyes += p; seen++; }
                    else faces.Add(p);
                }
            }
            if (seen == 0) return false;
            eyes /= seen;
            float len = ant.cfg.bodyLength;
            foreach (Vector3 p in faces)
                if (Mathf.Abs(p.x) < 0.06f * len && Mathf.Abs(p.y - eyes.y) < 0.06f * len) face = Mathf.Max(face, p.z);
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
            away = 0f;
            Blink();
            Debug.Log($"[AntGarden] VR view: {v}");
        }

        /// <summary>A cut in a blink: dark at once, clearing over a third of a second (gentler on the stomach than a
        /// glide, and a glide would pass through the walls).</summary>
        void Blink()
        {
            Game.Instance?.hud?.SetFade(1f, true);
            blinked = true;
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

        /// <summary>Standing still to play, a player who has wandered further than strayWarn from where they started is
        /// asked back, every few seconds while they are out there, before the headset's own boundary turns the game off
        /// (the user, 11-01: "在现实中走两步场景就会消失": the headset shows the room once the player steps out of it).</summary>
        void WarnStray()
        {
            warned -= Time.deltaTime;
            if (realWalk || strayWarn <= 0f || !centred || warned > 0f) return;
            Vector3 off = cam.transform.localPosition - spot;
            off.y = 0f;
            if (off.magnitude < strayWarn) return;
            warned = 6f;
            Game.Instance?.hud?.ShowLine(Loc.T("请回到原地", "Please step back"),
                Loc.T("站在原地就能玩，不用走动。再往外走，头显的安全边界会让画面消失、显示现实的房间。", "You can play standing still. Step further and the headset's boundary hides the game and shows the room."));
            Debug.Log($"[AntGarden] VR: the player has wandered {off.magnitude:F2} m from where they started");
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
            if (testCrawl > 0f && Time.time - readyAt < testCrawl) stick = Vector2.up;
            Move = Vector2.ClampMagnitude(stick + Walk(), 1f);
            Run = left.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool run) && run;
            bool a = right.TryGetFeatureValue(CommonUsages.primaryButton, out bool av) && av;
            bool b = right.TryGetFeatureValue(CommonUsages.secondaryButton, out bool bv) && bv;
            bool x = left.TryGetFeatureValue(CommonUsages.primaryButton, out bool xv) && xv;
            bool y = left.TryGetFeatureValue(CommonUsages.secondaryButton, out bool yv) && yv;
            bool menu = left.TryGetFeatureValue(CommonUsages.menuButton, out bool mv) && mv;
            bool click = right.TryGetFeatureValue(CommonUsages.primary2DAxisClick, out bool cv) && cv;
            float pull = Mathf.Max(left.TryGetFeatureValue(CommonUsages.trigger, out float lt) ? lt : 0f, right.TryGetFeatureValue(CommonUsages.trigger, out float rt) ? rt : 0f);
            bool held = triggerHeld ? pull > 0.3f : pull > 0.7f;
            Trigger = held && !triggerHeld;
            triggerHeld = held;
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
            if (!centred && (cam.transform.localRotation != Quaternion.identity || VRPreview.On))
            {
                // the first real pose (a preview's head, held still, is one): the player starts looking the way the ant faces
                rest = cam.transform.localPosition;
                spot = new Vector3(rest.x, 0f, rest.z);
                Vector3 af = ant.transform.forward;
                yaw = Mathf.Atan2(af.x, af.z) * Mathf.Rad2Deg - cam.transform.localEulerAngles.y;
                centred = true;
            }
            if (centred && gazeTurn) FollowGaze();
            Turned();
            WarnStray();
            // through its eyes the ant is drawn see-through and carried with the view (VRSelf); behind it, as every ant is
            // (ParticleAnts' own dissolves away meanwhile, point by point)
            ant.FadePoints(view == View.Eye && self != null ? 0f : 1f, Time.deltaTime);
            if (self != null) self.Shown = view == View.Eye;
            if (ring != null) ring.enabled = view == View.Behind;
            Vector3 p = ant.Position;
            floorY = Mathf.Lerp(floorY, p.y, 1f - Mathf.Exp(-heightEase * Time.deltaTime));
            origin.rotation = Quaternion.Euler(lookDown, yaw, 0f);
            Stand(p, Quaternion.Euler(0f, yaw, 0f));
            if (view == View.Eye)
            {
                // the resting head at the ant's eyes; leaning moves it a little (in real metres times scale)
                Vector3 eye = p + Dir(ant.heading) * eyeAt.z;
                origin.position = new Vector3(eye.x, floorY + eyeAt.y, eye.z) - origin.rotation * (rest * scale);
                self?.Carry(new Vector3(p.x, floorY, p.z), ant.heading);
                return;
            }
            // behind the ant the view moves with it, step for step, as through its eyes: the ant's crawl is the player's own
            // push of the stick, and a view that trails it and then catches up, or slides round it as it turns, moves by
            // itself (the user, 11-01, behind the ant with the stick: "镜头偏移不自然，摄像机也有点莫名奇妙的运动"). Once the
            // head has looked well aside of the way the view looks past the ant, for a while (looking about turns the ant
            // after the head, gazeTurn), the view is put behind the ant that way again, in a blink.
            Vector3 f = cam.transform.forward;
            float look = Mathf.Atan2(f.x, f.z);
            away = Mathf.Abs(f.y) < 0.6f && Mathf.Abs(Mathf.DeltaAngle(behindYaw * Mathf.Rad2Deg, look * Mathf.Rad2Deg)) > behindSwing ? away + Time.deltaTime : 0f;
            if (away > gazeDwell + 0.5f)
            {
                behindYaw = look;
                away = 0f;
                Blink();
            }
            origin.position = new Vector3(p.x, floorY + behindUp, p.z) - Dir(behindYaw) * behindBack - origin.rotation * (rest * behindScale);
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

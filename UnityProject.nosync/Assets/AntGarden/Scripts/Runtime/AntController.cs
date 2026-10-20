using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace AntGarden
{
    /// <summary>
    /// One ant (player or NPC): steering, collision against the canyon walls, props and other ants,
    /// terrain-aligned orientation, body lean, and clip blending (Idle / Crawl / FastCrawl / AntennaTalk,
    /// plus TakeOff / Fly / Land for flyers) through a Playables mixer whose leg cadence follows ground speed.
    /// Ported from the web prototype's ant.js; heading 0 = +Z, increasing towards +X.
    /// </summary>
    public class AntController : MonoBehaviour
    {
        public enum Clip { Idle, Crawl, Fast, Talk, TakeOff, Fly, Land }

        public World world;
        public AntSettings cfg;             // tuning (player uses GameConfig.ant; NPCs a per-species copy)
        public float crawlCycleSpeed = 0.15f; // world units / s the Crawl clip was authored for
        public float fastCycleSpeed = 0.45f;
        public List<AntController> others = new List<AntController>();

        public float heading;               // radians
        public float speed;
        public float altitude;              // flight height above the floor
        public bool sprinting;
        public bool dormant;                // far NPCs skip animation
        public Clip? clipOverride;          // flight clips while flying
        public float talkTimer;
        public NpcAnt npc;                  // the behaviour driving this ant (null for the player)
        public AntGrid grid;                // neighbours for crowding (falls back to `others`)
        public int animInterval = 1;        // crowd LOD: evaluate the animation every n frames
        public float Radius => cfg.radius;
        public Vector3 Position => transform.position;
        public AntRef Ref { get; private set; }
        public Transform Model => model;
        public float Hue { get; private set; }
        public float Saturation { get; private set; } = 1;
        /// <summary>Shown (not far off, not in the camera's way): drawn as its meshes, or as points.</summary>
        public bool Visible { get; private set; } = true;
        bool pointsOnly, darkBody, bodyShown;
        /// <summary>Drawn as points: how much of the ant is there (it dissolves out of the camera's way and back).</summary>
        public float PointFade { get; private set; } = 1f;

        /// <summary>Drawn as points: dissolve toward target (0 gone, 1 whole) over a third of a second; the dark body
        /// under the points shows only while the ant is whole.</summary>
        public void FadePoints(float target, float dt)
        {
            PointFade = Mathf.MoveTowards(PointFade, target, dt / 0.35f);
            ShowBody();
        }

        void ShowBody()
        {
            bool body = Visible && darkBody && PointFade > 0.98f;
            if (renderers == null || body == bodyShown) return;
            bodyShown = body;
            foreach (var r in renderers) r.enabled = body;
        }
        /// <summary>The particle nest: ParticleAnts draws the ant as points. The meshes are not drawn, or, given a
        /// dark body material, drawn with it (wings with nothing), so nothing behind the ant shows through its
        /// points; the skeleton keeps moving either way.</summary>
        public void DrawAsPoints(Material body)
        {
            pointsOnly = true;
            darkBody = body != null;
            var animator = model != null ? model.GetComponentInChildren<Animator>() : null;
            if (animator != null) animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            if (renderers == null) return;
            Material none = null;
            if (darkBody)
            {
                none = new Material(body) { name = "AntOccluder_none" };
                none.SetFloat("_ZWrite", 0);
                none.SetFloat("_ColorMask", 0);
            }
            foreach (var r in renderers)
            {
                if (darkBody)
                {
                    var mats = r.sharedMaterials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        string n = mats[i] != null ? mats[i].name.ToLowerInvariant() : "";
                        mats[i] = n.Contains("wing") || n.Contains("vein") ? none : BodyFor(body, mats[i]);
                    }
                    r.sharedMaterials = mats;
                    r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }
                r.enabled = false;
            }
            bodyShown = false;
            ShowBody();
        }
        public bool PointsOnly => pointsOnly;

        static readonly Dictionary<Material, Material> bodies = new Dictionary<Material, Material>();
        /// <summary>The dim body for one of the ant's own materials: the body material with that material's texture
        /// (shared by every ant of a kind).</summary>
        static Material BodyFor(Material body, Material own)
        {
            if (own == null) return body;
            if (bodies.TryGetValue(own, out Material m) && m != null) return m;
            Texture tex = null;
            foreach (string prop in new[] { "_MainTex", "_BaseMap", "_BaseColorMap" })
                if (tex == null && own.HasProperty(prop)) tex = own.GetTexture(prop);
            m = new Material(body) { name = "AntOccluder_" + own.name };
            if (tex != null) m.SetTexture("_MainTex", tex);
            else m.SetFloat("_TexLight", 0);
            bodies[own] = m;
            return m;
        }

        Transform lean;
        Transform model;
        Vector3 up = Vector3.up, footNormal = Vector3.up;
        float accel, turnVel, leanRoll, leanPitch, footY, animDt;
        bool hasFoot, shadows = true;
        int animPhase;
        readonly List<AntController> near = new List<AntController>(16);
        Clip gait = Clip.Idle;
        readonly float[] weights = new float[7];
        PlayableGraph graph;
        AnimationMixerPlayable mixer;
        // leg layer: the walk clips again, at ground-matched speed, masked to the leg bones
        AnimationLayerMixerPlayable layers;
        AnimationMixerPlayable legMixer;
        AnimationClipPlayable legCrawl, legFast;
        bool hasLegLayer, hasLegFast;
        static readonly Dictionary<GameObject, AvatarMask> legMasks = new Dictionary<GameObject, AvatarMask>();
        readonly AnimationClipPlayable[] clips = new AnimationClipPlayable[7];
        readonly bool[] hasClip = new bool[7];
        Renderer[] renderers;
        MaterialPropertyBlock block;
        LegRig legs;                        // procedural tripod legs for near ants (null: clips only)
        public LegRig Legs => legs;
        /// <summary>Animation check only: play the clips exactly as authored (rate = ground speed / authored
        /// speed, no leg layer, no procedural legs), to compare the game against the original animation.</summary>
        public bool authoredPlayback;
        readonly Transform[] antenna2 = new Transform[2], antennaEnd = new Transform[2]; // last two antenna bones, L / R
        public bool Talking => talkTimer > 0;

        /// <summary>Build the visual: model scaled to the body length, Playables graph over the clips.</summary>
        public void Init(World w, AntSettings settings, AntRef ant, Color rim, Color glow, float hue = 0, float saturation = 1)
        {
            world = w;
            cfg = settings;
            Ref = ant;
            Hue = hue;
            Saturation = saturation;
            animPhase = Random.Range(0, 64);
            lean = new GameObject("Lean").transform;
            lean.SetParent(transform, false);
            GameObject go = Instantiate(ant.prefab, lean);
            go.name = ant.id;
            model = go.transform;
            float s = cfg.bodyLength / Mathf.Max(1e-4f, ant.modelBodyLength);
            model.localScale = Vector3.one * s;
            model.localPosition = new Vector3(0, 0, -ant.modelCenterZ * s);
            crawlCycleSpeed = ant.crawlCycleSpeed * cfg.bodyLength;
            fastCycleSpeed = ant.fastCycleSpeed * cfg.bodyLength;

            renderers = go.GetComponentsInChildren<Renderer>();
            block = new MaterialPropertyBlock();
            foreach (var r in renderers)
            {
                r.GetPropertyBlock(block);
                block.SetColor("_RimColor", rim);
                block.SetFloat("_RimStrength", cfg.rimStrength);
                block.SetColor("_GlowColor", glow);
                block.SetFloat("_GlowStrength", cfg.glowStrength);
                block.SetFloat("_HueShift", hue);
                block.SetFloat("_Saturation", saturation);
                r.SetPropertyBlock(block);
                if (r is SkinnedMeshRenderer smr) smr.updateWhenOffscreen = false;
            }

            var animator = go.GetComponentInChildren<Animator>();
            if (animator == null) animator = go.AddComponent<Animator>();
            animator.applyRootMotion = false;
            animator.runtimeAnimatorController = null;
            graph = PlayableGraph.Create(name + "_anim");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var output = AnimationPlayableOutput.Create(graph, "anim", animator);
            mixer = AnimationMixerPlayable.Create(graph, 7);
            AddClip(Clip.Idle, ant.idle);
            AddClip(Clip.Crawl, ant.crawl);
            AddClip(Clip.Fast, ant.fast);
            AddClip(Clip.Talk, ant.talk);
            AddClip(Clip.TakeOff, ant.takeoff);
            AddClip(Clip.Fly, ant.fly);
            AddClip(Clip.Land, ant.land);
            AvatarMask legMask = ant.crawl != null ? LegMask(ant.prefab, animator.transform) : null;
            if (legMask != null)
            {
                layers = AnimationLayerMixerPlayable.Create(graph, 2);
                graph.Connect(mixer, 0, layers, 0);
                layers.SetInputWeight(0, 1);
                legMixer = AnimationMixerPlayable.Create(graph, 2);
                legCrawl = AnimationClipPlayable.Create(graph, ant.crawl);
                graph.Connect(legCrawl, 0, legMixer, 0);
                hasLegFast = ant.fast != null;
                if (hasLegFast)
                {
                    legFast = AnimationClipPlayable.Create(graph, ant.fast);
                    graph.Connect(legFast, 0, legMixer, 1);
                }
                graph.Connect(legMixer, 0, layers, 1);
                layers.SetLayerMaskFromAvatarMask(1, legMask);
                layers.SetInputWeight(1, 0);
                output.SetSourcePlayable(layers);
                hasLegLayer = true;
            }
            else output.SetSourcePlayable(mixer);
            weights[(int)Clip.Idle] = 1;
            ApplyWeights();
            graph.Evaluate(0);
            // the rest pose for the procedural legs is read now: idle pose, body at the origin
            if (cfg.proceduralLegs) legs = LegRig.Create(this, model, lean);
            foreach (Transform t in model.GetComponentsInChildren<Transform>())
            {
                int side = t.name.EndsWith(".L") ? 0 : t.name.EndsWith(".R") ? 1 : -1;
                if (side < 0) continue;
                if (t.name.StartsWith("antenna_2b")) antennaEnd[side] = t;
                else if (t.name.StartsWith("antenna_2")) antenna2[side] = t;
            }
        }

        /// <summary>Stand-in body (a glowing capsule) when the species' model has not been converted yet.</summary>
        public void InitPlaceholder(World w, AntSettings settings, Material mat)
        {
            world = w;
            cfg = settings;
            lean = new GameObject("Lean").transform;
            lean.SetParent(transform, false);
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            Destroy(body.GetComponent<Collider>());
            body.transform.SetParent(lean, false);
            body.transform.localRotation = Quaternion.Euler(90, 0, 0);
            body.transform.localScale = new Vector3(0.35f, 0.5f, 0.3f) * cfg.bodyLength;
            body.transform.localPosition = new Vector3(0, 0.18f * cfg.bodyLength, 0);
            if (mat != null) body.GetComponent<Renderer>().sharedMaterial = mat;
            model = body.transform;
            renderers = body.GetComponentsInChildren<Renderer>();
        }

        /// <summary>
        /// Mask that lets the leg layer drive only the six legs (bones named leg_*), shared by every ant
        /// built from the same model. Null when the skeleton has no such bones.
        /// </summary>
        static AvatarMask LegMask(GameObject prefab, Transform animatorRoot)
        {
            if (legMasks.TryGetValue(prefab, out var cached)) return cached;
            var paths = new List<(string path, bool leg)>();
            void Walk(Transform t, string prefix)
            {
                foreach (Transform c in t)
                {
                    string path = prefix.Length == 0 ? c.name : prefix + "/" + c.name;
                    paths.Add((path, c.name.StartsWith("leg_")));
                    Walk(c, path);
                }
            }
            Walk(animatorRoot, "");
            AvatarMask mask = null;
            if (paths.Exists(p => p.leg))
            {
                mask = new AvatarMask { transformCount = paths.Count };
                for (int i = 0; i < paths.Count; i++)
                {
                    mask.SetTransformPath(i, paths[i].path);
                    mask.SetTransformActive(i, paths[i].leg);
                }
            }
            legMasks[prefab] = mask;
            return mask;
        }

        void AddClip(Clip key, AnimationClip clip)
        {
            if (clip == null) return;
            var p = AnimationClipPlayable.Create(graph, clip);
            graph.Connect(p, 0, mixer, (int)key);
            clips[(int)key] = p;
            hasClip[(int)key] = true;
        }

        public bool HasClip(Clip c) => hasClip[(int)c];

        void OnDestroy()
        {
            if (graph.IsValid()) graph.Destroy();
        }

        /// <summary>Put the ant on the floor at (x, z) facing heading (radians).</summary>
        public void Place(float x, float z, float h)
        {
            heading = h;
            speed = 0;
            altitude = 0;
            footY = Footprint(x, z, out footNormal);
            hasFoot = true;
            transform.position = new Vector3(x, footY, z);
            up = footNormal;
            Orient(1, false);
            legs?.Reset();
        }

        public void PlayTalk()
        {
            if (hasClip[(int)Clip.Talk]) talkTimer = 2.4f;
        }

        /// <summary>Advance one frame. move = desired world direction on XZ (length 0..1).</summary>
        public void Tick(float dt, Vector2 move, bool fast)
        {
            float mag = Mathf.Min(1, move.magnitude);
            bool wantsSprint = fast && mag > 0.05f;
            float targetSpeed = 0, turn = 0;
            if (mag > 0.05f)
            {
                float target = Mathf.Atan2(move.x, move.y);
                float diff = Mathf.Atan2(Mathf.Sin(target - heading), Mathf.Cos(target - heading));
                // pivot quickly from rest, steer in smooth arcs at speed
                float rate = Mathf.Lerp(cfg.turnRate, cfg.sprintTurnRate, Mathf.Clamp01(speed / cfg.fastSpeed));
                turn = Mathf.Clamp(diff * cfg.turnGain * dt, -rate * dt, rate * dt);
                if (Mathf.Abs(turn) > Mathf.Abs(diff)) turn = diff;
                heading += turn;
                // only sharp direction changes cost speed; gentle steering keeps momentum
                float align = 0.3f + 0.7f * Smooth(-0.35f, 0.75f, Mathf.Cos(diff));
                targetSpeed = (wantsSprint ? cfg.fastSpeed : cfg.crawlSpeed) * mag * align;
                talkTimer = 0;
            }
            float prev = speed;
            float k = targetSpeed > speed ? (wantsSprint ? cfg.sprintAccel : cfg.accel) : cfg.decel;
            speed += (targetSpeed - speed) * (1 - Mathf.Exp(-k * dt));
            if (targetSpeed == 0 && speed < 0.004f) speed = 0;
            float inv = 1 / Mathf.Max(dt, 1e-4f);
            accel += ((speed - prev) * inv - accel) * (1 - Mathf.Exp(-10 * dt));
            turnVel += (turn * inv - turnVel) * (1 - Mathf.Exp(-12 * dt));
            sprinting = wantsSprint && speed > cfg.crawlSpeed * 1.15f;

            bool due = Due();
            Vector3 p = transform.position;
            p.x += Mathf.Sin(heading) * speed * dt;
            p.z += Mathf.Cos(heading) * speed * dt;
            p = Collide(p, dt);
            // the body rides on its legs: height and tilt come from the four foot positions, eased, so
            // small bumps in the ground do not rock it (an ant's body stays remarkably steady)
            float ground;
            if (!dormant && due) ground = Footprint(p.x, p.z, out footNormal);
            else ground = world.WalkHeight(p.x, p.z);
            footY = hasFoot ? footY + (ground - footY) * (1 - Mathf.Exp(-14 * dt)) : ground;
            hasFoot = true;
            p.y = Mathf.Max(footY, ground - 0.05f * cfg.bodyLength) + altitude;
            transform.position = p;
            if (!dormant) Orient(1 - Mathf.Exp(-cfg.bodySettle * dt), false);
            Lean(dt);
            Animate(dt, due);
        }

        bool Due() => animInterval <= 1 || (Time.frameCount + animPhase) % animInterval == 0;

        /// <summary>Ground height under the body and the normal of the plane through its four feet.</summary>
        float Footprint(float x, float z, out Vector3 normal)
        {
            float l = cfg.bodyLength * 0.45f, w = cfg.bodyLength * 0.28f;
            float fx = Mathf.Sin(heading), fz = Mathf.Cos(heading);
            float rx = fz, rz = -fx;
            float hf = world.WalkHeight(x + fx * l, z + fz * l), hb = world.WalkHeight(x - fx * l, z - fz * l);
            float hr = world.WalkHeight(x + rx * w, z + rz * w), hl = world.WalkHeight(x - rx * w, z - rz * w);
            normal = Vector3.Cross(new Vector3(fx * 2 * l, hf - hb, fz * 2 * l), new Vector3(rx * 2 * w, hr - hl, rz * 2 * w)).normalized;
            if (normal.y < 0) normal = -normal;
            return Mathf.Max(world.WalkHeight(x, z), (hf + hb + hr + hl) * 0.25f);
        }

        /// <summary>Flight: move freely towards a target heading at a given altitude (no wall collision).</summary>
        public void FlyTick(float dt, float targetHeading, float flySpeed, float targetAltitude)
        {
            float diff = Mathf.Atan2(Mathf.Sin(targetHeading - heading), Mathf.Cos(targetHeading - heading));
            heading += Mathf.Clamp(diff * 2.5f * dt, -1.6f * dt, 1.6f * dt);
            speed += (flySpeed - speed) * (1 - Mathf.Exp(-2 * dt));
            altitude += (targetAltitude - altitude) * (1 - Mathf.Exp(-1.2f * dt));
            Vector3 p = transform.position;
            p.x += Mathf.Sin(heading) * speed * dt;
            p.z += Mathf.Cos(heading) * speed * dt;
            // keep inside the hexagon frame
            if (world.HexDist(p.x, p.z) > -3)
            {
                Vector2 g = world.GradDist(p.x, p.z);
                heading = Mathf.Atan2(-g.x, -g.y);
            }
            p.y = world.Height(p.x, p.z) + altitude;
            transform.position = p;
            Orient(1 - Mathf.Exp(-4 * dt), true);
            Animate(dt, Due());
        }

        Vector3 Collide(Vector3 p, float dt)
        {
            float r = cfg.radius;
            // other ants: ease apart over a few frames instead of snapping (snapping made crowds tremble)
            if (grid != null) grid.Near(p.x, p.z, near);
            List<AntController> crowd = grid != null ? near : others;
            float maxPush = cfg.separation * Mathf.Max(dt, 1e-3f);
            foreach (var o in crowd)
            {
                if (o == this || o.altitude > 1f) continue;
                float dx = p.x - o.transform.position.x, dz = p.z - o.transform.position.z;
                float min = o.Radius + r, d2 = dx * dx + dz * dz;
                if (d2 < min * min && d2 > 1e-8f)
                {
                    float dist = Mathf.Sqrt(d2);
                    float push = Mathf.Min((min - dist) * 0.5f, maxPush);
                    p.x += dx / dist * push;
                    p.z += dz / dist * push;
                }
            }
            for (int iter = 0; iter < 3; iter++)
            {
                // canyon walls (distance field); an ant allowed to climb (the player) may go up the banks to
                // an invisible wall just below their crest
                bool climbs = cfg.climb > 0;
                float d = climbs ? world.ClimbDist(p.x, p.z, cfg.climb) : world.Dist(p.x, p.z);
                if (d > -r)
                {
                    Vector2 g = climbs ? world.GradClimb(p.x, p.z, cfg.climb) : world.GradDist(p.x, p.z);
                    float len = g.magnitude;
                    if (len > 1e-6f)
                    {
                        p.x -= g.x / len * (d + r);
                        p.z -= g.y / len * (d + r);
                    }
                }
                // props: the body is long and narrow, so against stems and rocks it counts as a smaller disc than
                // against walls and other ants, and can slip between the stalks of a clump
                float rp = r * 0.75f;
                foreach (var c in world.colliders.Near(p.x, p.z))
                {
                    float dx = p.x - c.x, dz = p.z - c.z;
                    float min = c.r + rp, d2 = dx * dx + dz * dz;
                    if (d2 < min * min && d2 > 1e-8f)
                    {
                        float dist = Mathf.Sqrt(d2);
                        p.x += dx / dist * (min - dist);
                        p.z += dz / dist * (min - dist);
                    }
                }
            }
            return p;
        }

        void Orient(float t, bool keepUp)
        {
            if (!keepUp) up = Vector3.Slerp(up, footNormal, t).normalized;
            else up = Vector3.Slerp(up, Vector3.up, t).normalized;
            Vector3 fwd = new Vector3(Mathf.Sin(heading), 0, Mathf.Cos(heading));
            fwd = Vector3.ProjectOnPlane(fwd, up).normalized;
            transform.rotation = Quaternion.LookRotation(fwd, up);
        }

        /// <summary>Bank into turns and dip the head when accelerating; purely visual.</summary>
        void Lean(float dt)
        {
            float roll = Mathf.Clamp(-turnVel * speed * cfg.leanRoll, -0.2f, 0.2f);
            float pitch = Mathf.Clamp(accel * cfg.leanPitch, -0.12f, 0.14f);
            float k = 1 - Mathf.Exp(-6 * dt);
            leanRoll += (roll - leanRoll) * k;
            leanPitch += (pitch - leanPitch) * k;
            lean.localRotation = Quaternion.Euler(leanPitch * Mathf.Rad2Deg, 0, leanRoll * Mathf.Rad2Deg);
        }

        void Animate(float dt, bool due)
        {
            talkTimer = Mathf.Max(0, talkTimer - dt);
            if (dormant || !graph.IsValid()) return;
            animDt += dt;
            if (!due) return; // crowd LOD: farther ants are posed every few frames
            dt = animDt;
            animDt = 0;
            Clip want;
            bool walkingNow = gait == Clip.Crawl || gait == Clip.Fast;
            // hysteresis: an ant easing to a stop does not flicker between standing and walking
            float idleBelow = walkingNow ? cfg.idleBelow * 0.6f : cfg.idleBelow * 1.6f;
            if (clipOverride.HasValue && hasClip[(int)clipOverride.Value]) want = clipOverride.Value;
            else if (speed < idleBelow) want = talkTimer > 0 && hasClip[(int)Clip.Talk] ? Clip.Talk : Clip.Idle;
            else if (speed > cfg.fastAbove || (gait == Clip.Fast && speed > cfg.fastAbove * 0.8f)) want = Clip.Fast;
            else want = Clip.Crawl;
            if (!hasClip[(int)want]) want = want == Clip.Fast ? Clip.Crawl : Clip.Idle;
            if (want != gait)
            {
                // keep the tripod gait's phase when switching between the two walking clips
                bool walking(Clip c) => c == Clip.Crawl || c == Clip.Fast;
                if (walking(want) && walking(gait) && hasClip[(int)want] && hasClip[(int)gait])
                {
                    var from = clips[(int)gait];
                    var to = clips[(int)want];
                    double phase = from.GetTime() / from.GetAnimationClip().length;
                    to.SetTime(phase * to.GetAnimationClip().length);
                    if (hasLegLayer && hasLegFast)
                    {
                        var lf = gait == Clip.Crawl ? legCrawl : legFast;
                        var lt = want == Clip.Crawl ? legCrawl : legFast;
                        double legPhase = lf.GetTime() / lf.GetAnimationClip().length;
                        lt.SetTime(legPhase * lt.GetAnimationClip().length);
                    }
                }
                if (want == Clip.TakeOff || want == Clip.Land) clips[(int)want].SetTime(0);
                gait = want;
            }
            float kf = Mathf.Min(1, dt / cfg.gaitFade);
            for (int i = 0; i < weights.Length; i++)
                weights[i] += ((i == (int)gait ? 1 : 0) - weights[i]) * kf;
            ApplyWeights();
            if (authoredPlayback)
            {
                if (hasClip[(int)Clip.Crawl]) clips[(int)Clip.Crawl].SetSpeed(speed / Mathf.Max(1e-3f, crawlCycleSpeed));
                if (hasClip[(int)Clip.Fast]) clips[(int)Clip.Fast].SetSpeed(speed / Mathf.Max(1e-3f, fastCycleSpeed));
                if (hasLegLayer) layers.SetInputWeight(1, 0);
                graph.Evaluate(dt);
                legs?.Update(dt, false);
                return;
            }
            bool legsActive = legs != null && animInterval <= 1 && !clipOverride.HasValue && altitude < 0.05f;
            float legW = legs != null ? legs.Weight : 0;
            // body layer: a calm walking rhythm over a longer effective stride (see AntSettings.strideScale)
            float stride = Mathf.Max(1f, cfg.strideScale);
            if (hasClip[(int)Clip.Crawl]) clips[(int)Clip.Crawl].SetSpeed(Mathf.Clamp(speed / Mathf.Max(1e-3f, crawlCycleSpeed * stride), cfg.minCadence, cfg.maxCadence));
            if (hasClip[(int)Clip.Fast]) clips[(int)Clip.Fast].SetSpeed(Mathf.Clamp(speed / Mathf.Max(1e-3f, fastCycleSpeed * stride), cfg.minCadence, cfg.maxCadence * 1.4f));
            if (hasLegLayer)
            {
                // leg layer: steps matched to ground speed, so the feet grip instead of skating
                float wc = weights[(int)Clip.Crawl], wf = hasLegFast ? weights[(int)Clip.Fast] : 0;
                float walk = Mathf.Clamp01(wc + wf);
                layers.SetInputWeight(1, walk);
                legMixer.SetInputWeight(0, walk > 1e-3f ? wc / (wc + wf) : 1);
                if (hasLegFast) legMixer.SetInputWeight(1, walk > 1e-3f ? wf / (wc + wf) : 0);
                legCrawl.SetSpeed(Mathf.Clamp(speed / Mathf.Max(1e-3f, crawlCycleSpeed), cfg.minCadence, cfg.legCadenceMax));
                if (hasLegFast) legFast.SetSpeed(Mathf.Clamp(speed / Mathf.Max(1e-3f, fastCycleSpeed), cfg.minCadence, cfg.legCadenceMax * 1.3f));
                layers.SetInputWeight(1, walk * (1 - legW)); // the procedural legs take over near the camera
            }
            if (legW > 0)
            {
                // with procedural legs the body's walking sway follows their step clock
                foreach (Clip c in new[] { Clip.Crawl, Clip.Fast })
                {
                    if (!hasClip[(int)c]) continue;
                    clips[(int)c].SetSpeed(0);
                    clips[(int)c].SetTime(legs.Phase * clips[(int)c].GetAnimationClip().length);
                }
            }
            if (hasClip[(int)Clip.Fly]) clips[(int)Clip.Fly].SetSpeed(1.6f);
            graph.Evaluate(dt);
            legs?.Update(dt, legsActive);
        }

        /// <summary>
        /// World position of an antenna tip (side 0 = left, 1 = right): the last bone's head plus one more
        /// segment along it. Without antenna bones, a point just ahead of the head.
        /// </summary>
        public Vector3 AntennaTip(int side)
        {
            Transform a = antenna2[side], b = antennaEnd[side];
            if (a != null && b != null) return b.position + (b.position - a.position);
            return transform.position + transform.forward * cfg.bodyLength * (0.75f + (Talking ? 0.2f : 0)) + transform.up * cfg.bodyLength * 0.2f;
        }

        /// <summary>Animation check: the current gait and playback rates, for the log.</summary>
        public string AnimInfo()
        {
            string Rate(Clip c) => hasClip[(int)c] ? clips[(int)c].GetSpeed().ToString("F2") : "-";
            string legLayer = hasLegLayer ? $" legLayer w{layers.GetInputWeight(1):F2} x{legCrawl.GetSpeed():F2}" : "";
            string proc = legs != null ? $" procLegs w{legs.Weight:F2}" : "";
            return $"{gait} speed {speed:F2} u/s, crawl x{Rate(Clip.Crawl)} fast x{Rate(Clip.Fast)}{legLayer}{proc}";
        }

        /// <summary>Seconds left in a one-shot clip (TakeOff / Land), 0 when finished or missing.</summary>
        public float Remaining(Clip c)
        {
            if (!hasClip[(int)c]) return 0;
            var p = clips[(int)c];
            return Mathf.Max(0, p.GetAnimationClip().length - (float)p.GetTime());
        }

        void ApplyWeights()
        {
            for (int i = 0; i < weights.Length; i++)
                if (hasClip[i]) mixer.SetInputWeight(i, weights[i]);
        }

        /// <summary>Shadows only for near ants (crowd LOD).</summary>
        public void SetShadows(bool on)
        {
            if (on == shadows || renderers == null || pointsOnly) return;
            shadows = on;
            foreach (var r in renderers) r.shadowCastingMode = on ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        /// <summary>Show or hide the model (far NPCs) without stopping the logic.</summary>
        public void SetVisible(bool v)
        {
            Visible = v;
            if (pointsOnly) { ShowBody(); return; }
            if (renderers == null) return;
            foreach (var r in renderers) r.enabled = v;
        }

        static float Smooth(float a, float b, float v)
        {
            float t = Mathf.Clamp01((v - a) / (b - a));
            return t * t * (3 - 2 * t);
        }
    }
}

using UnityEngine;
using UnityEngine.EventSystems;

namespace AntGarden
{
    /// <summary>
    /// Third-person follow camera, the way most action games do it. The camera is dragged along after the ant
    /// like a weight on a string: walking sideways circles it round, walking towards it pushes it back, and only
    /// while the ant runs away from it does it ease round behind, gently. In a canyon its end slides along the walls and round tall props at the same
    /// distance, so it follows the bends instead of cutting through the banks; only when the ant would really be
    /// hidden does the boom shorten (quickly), and it lengthens again (slowly) after a short hold, so it does not
    /// pump in and out. Left-drag orbits, the wheel zooms; sprinting widens the view a little.
    /// </summary>
    public class CameraRig : MonoBehaviour
    {
        public Camera cam;
        public World world;
        public CameraSettings cfg;
        public float yaw = Mathf.PI;
        public float pitch;
        public float distance;
        float lastDrag = -1e9f, time, sprint, baseFov;
        bool dragging, padTurning;
        Vector3 lastMouse;
        bool placed, reanchor;
        float boom, smoothY, hold, swing, tickYaw, climbAllow;
        Vector2 anchor;     // where the boom's end is held, seen from above (the weight on the string)
        Vector3 offset;     // the camera's eased position relative to the look-at point
        /// <summary>Demo wide shots: the boom may rise out of the canyon (ground and tall props still stop it).</summary>
        public bool free;
        public bool manual;                 // checks only: the camera is placed by hand, the rig leaves it alone
        public bool classic;                // checks only: the previous camera, to compare against
        public string blockedBy = "";       // checks only: what shortened the boom this frame
        /// <summary>
        /// The particle nests (Game.Lab, 11-03): the steadier camera of TickSteady, because there this one lurched in and
        /// out past the plants and flicked from side to side. -camsteady 0 gives them this one again, for checks.
        /// </summary>
        public bool steady;
        public Vector2? checkLook;          // checks only: the right stick as CamCheck holds it
        public float lifted, slideBy, clamped; // checks only: how far the ground lifted the camera, the walls moved the boom's end, the floor stopped it
        /// <summary>The steady camera inside a plant or a thing (0..1): the splats round its lens clear further (ParticleSolid _LensClear).</summary>
        public float lensClear;
        float smoothYVel, lift, liftVel, boomVel, slideOn = 1, lensVel, lastYaw, yawVel;
        Vector2 lookEased, lookVel;
        readonly System.Collections.Generic.List<Circle> around = new System.Collections.Generic.List<Circle>(128);
        Vector3 offVel;
        bool lensSet;

        public void Init(Camera c, World w, CameraSettings settings)
        {
            cam = c;
            world = w;
            cfg = settings;
            pitch = cfg.pitch;
            distance = cfg.distance;
            baseFov = cfg.fov;
            cam.fieldOfView = baseFov;
            steady = Game.Lab && Game.Arg("-camsteady") != "0";
            Shader.SetGlobalVector("_LensClear", Vector4.zero);   // (a global: none left over from the nest before)
        }

        /// <summary>Camera-relative movement basis on the XZ plane: (forward, right).</summary>
        public (Vector2 fwd, Vector2 right) Basis()
        {
            return (new Vector2(-Mathf.Sin(yaw), -Mathf.Cos(yaw)), new Vector2(-Mathf.Cos(yaw), Mathf.Sin(yaw)));
        }

        public void SnapBehind(float heading)
        {
            yaw = heading + Mathf.PI;
            placed = false;
        }

        /// <summary>Checks only: turn the camera as if the player had just dragged it round and let go.</summary>
        public void Nudge(float dyaw)
        {
            yaw += dyaw;
            lastDrag = time;
            reanchor = true;
            slideOn = 0;   // (steady: as after a drag, the walls are taken up again only as the ant walks on)
        }

        void HandleMouse(bool inputEnabled, float dt)
        {
            // the pad: the right stick turns the camera like a drag, LB / RB zoom
            Vector2 look = checkLook ?? (inputEnabled ? Pad.Look : Vector2.zero);
            if (steady)
            {
                // (the particle nests: the stick's turn starts and stops over a tenth of a second, not in a frame)
                lookEased = Vector2.SmoothDamp(lookEased, look, ref lookVel, 0.09f, Mathf.Infinity, dt);
                if (look.sqrMagnitude == 0 && lookEased.sqrMagnitude < 1e-4f) lookEased = lookVel = Vector2.zero;
                look = lookEased;
            }
            padTurning = look.sqrMagnitude > 0;
            if (padTurning)
            {
                yaw += look.x * cfg.padTurnSpeed * dt;
                pitch = Mathf.Clamp(pitch - look.y * cfg.padPitchSpeed * dt, cfg.minPitch, cfg.maxPitch);
                lastDrag = time;
            }
            float zoom = inputEnabled ? Pad.Zoom : 0;
            if (zoom != 0) distance = Mathf.Clamp(distance * Mathf.Exp(zoom * 0.8f * dt), cfg.minDistance, cfg.maxDistance);
            bool overUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            if (inputEnabled && Input.GetMouseButtonDown(0) && !overUi)
            {
                dragging = true;
                lastMouse = Input.mousePosition;
            }
            if (Input.GetMouseButtonUp(0)) dragging = false;
            if (dragging)
            {
                Vector3 d = Input.mousePosition - lastMouse;
                lastMouse = Input.mousePosition;
                float k = cfg.mouseSensitivity * 1080f / Mathf.Max(400, Screen.height);
                yaw += d.x * 0.0055f * k;
                pitch = Mathf.Clamp(pitch - d.y * 0.004f * k, cfg.minPitch, cfg.maxPitch);
                lastDrag = time;
            }
            float wheel = Input.mouseScrollDelta.y;
            if (inputEnabled && Mathf.Abs(wheel) > 0.01f && !overUi)
                distance = Mathf.Clamp(distance * Mathf.Exp(-wheel * 0.12f), cfg.minDistance, cfg.maxDistance);
        }

        /// <summary>Follow the ant; call after the ant has moved this frame.</summary>
        public void Tick(float dt, AntController ant, bool inputEnabled)
        {
            if (manual)   // a check run places the camera itself
            {
                if (lensSet) { Shader.SetGlobalVector("_LensClear", Vector4.zero); lensSet = false; }
                return;
            }
            if (classic) { TickClassic(dt, ant, inputEnabled); return; }
            if (steady) { TickSteady(dt, ant, inputEnabled); return; }
            time += dt;
            HandleMouse(inputEnabled, dt);
            sprint += ((ant.sprinting ? 1 : 0) - sprint) * (1 - Mathf.Exp(-(ant.sprinting ? 3 : 2) * dt));
            cam.fieldOfView = baseFov + cfg.sprintFovKick * sprint;
            // the look-at point follows the ant, with its height eased so rolling ground does not jolt the view
            Vector3 antPos = ant.Position;
            if (!placed) smoothY = antPos.y;
            smoothY += (antPos.y - smoothY) * (1 - Mathf.Exp(-8f * dt));
            Vector3 target = new Vector3(antPos.x, smoothY + cfg.height, antPos.z);
            var flatTarget = new Vector2(target.x, target.z);
            // negative pitch: keep the camera low and raise the look-at point instead (look up at the canopy)
            float camPitch = Mathf.Max(pitch, -0.08f);
            float lookUp = Mathf.Max(0, -pitch);
            float dist = distance * (1 + (cfg.sprintDistance - 1) * sprint);
            float reach = dist * Mathf.Cos(camPitch);   // the boom's length seen from above

            // the weight on the string: the camera turns only as far as the ant's own sideways steps pull it
            // (a drag turns it directly, and so does a script that sets yaw, e.g. a slow orbit in the demo)
            if (Mathf.Abs(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, tickYaw * Mathf.Rad2Deg)) > 0.01f) reanchor = true;
            bool turning = dragging || padTurning;
            if (placed && !turning && !reanchor)
            {
                Vector2 v = anchor - flatTarget;
                if (v.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(v.x, v.y);
            }
            reanchor = false;
            // while the ant runs mostly away from the camera, ease round behind it, gently and with a soft start:
            // W with A or D steers like a car, but D alone or S never spins the view, and a drag is left alone
            float swingWanted = 0;
            if (ant.speed > 0.2f && !turning && time - lastDrag > cfg.followDelay)
            {
                float diff = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, (ant.heading + Mathf.PI) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                swingWanted = diff * cfg.recenterRate * Mathf.Clamp01((Mathf.Cos(diff) - 0.35f) / 0.5f);
            }
            swing += (swingWanted - swing) * (1 - Mathf.Exp(-4f * dt));
            yaw += swing * dt;
            Vector2 end = flatTarget + new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * reach;
            // in the canyon the end slides along the walls and round tall props, keeping its distance, instead of
            // cutting through them; eased, so a sharp corner turns the view smoothly rather than flicking it
            slideBy = 0;
            if (!free && ant.altitude < 0.5f && !turning)
            {
                // the camera keeps to the canyon floor, and follows the ant up a bank only as far as it has climbed
                climbAllow = Mathf.Clamp(world.Dist(antPos.x, antPos.z) + 1f, 0, ant.cfg.climb);
                Vector2 slid = Slide(flatTarget, end, reach);
                slideBy = (slid - end).magnitude;   // (checks)
                end = placed ? Vector2.Lerp(end, slid, 1 - Mathf.Exp(-cfg.slideRate * dt)) : slid;
            }
            anchor = end;
            Vector2 flat = end - flatTarget;
            float along = flat.magnitude;
            if (along < 1e-3f) { flat = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * 1e-3f; along = 1e-3f; }
            if (!turning) yaw = Mathf.Atan2(flat.x, flat.y);
            // the same elevation angle when the slide brings the end closer
            Vector3 dir = new Vector3(flat.x, dist * Mathf.Sin(camPitch) * along / Mathf.Max(reach, 1e-3f), flat.y);
            float full = Mathf.Max(dir.magnitude, Mathf.Min(dist, cfg.minDistance));
            dir.Normalize();

            // only something really between the camera and the ant shortens the boom
            float allowed = full;
            blockedBy = "";
            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                Vector3 p = target + dir * (full * i / steps);
                string why = Blocked(p);
                if (why != null) { allowed = full * (i - 1) / steps; blockedBy = why; break; }
            }
            allowed = Mathf.Max(allowed, Mathf.Min(full, cfg.minDistance));
            // in quickly (the ant must not be hidden), then after a short hold out again slowly
            if (!placed) { boom = allowed; hold = 0; }
            else if (allowed < boom - 1e-3f)
            {
                boom += (allowed - boom) * (1 - Mathf.Exp(-cfg.zoomInRate * dt));
                hold = cfg.zoomHold;
            }
            else if ((hold -= dt) <= 0) boom += (allowed - boom) * (1 - Mathf.Exp(-cfg.zoomOutRate * dt));

            // eased relative to the ant, so the ant itself stays steady in the frame while the view turns
            Vector3 want = dir * boom;
            float k = turning ? 1 - Mathf.Exp(-30f * dt) : 1 - Mathf.Exp(-cfg.followSmooth * dt);
            offset = placed ? Vector3.Lerp(offset, want, k) : want;
            Vector3 pos = target + offset;
            float minY = world.Height(pos.x, pos.z) + 0.2f;
            clamped = Mathf.Max(0, minY - pos.y);   // (checks)
            if (pos.y < minY) pos.y = minY;
            float maxY = world.CameraCeiling(pos.x, pos.z) - 0.45f;   // in a tunnel, stay under the roof
            if (pos.y > maxY) pos.y = Mathf.Max(minY, maxY);
            transform.position = pos;
            placed = true;
            tickYaw = yaw;
            Vector3 look = target + Vector3.up * (Mathf.Tan(Mathf.Min(1.25f, lookUp * 2.2f)) * Vector3.Distance(pos, target));
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);
        }

        /// <summary>
        /// Keep the boom's end on the canyon floor and out of tall props: push it back in, then out again to the
        /// boom's length along the wall, a few times; in a corner too tight for that it simply comes closer.
        /// </summary>
        Vector2 Slide(Vector2 from, Vector2 p, float reach, bool props = true)
        {
            for (int it = 0; it < 4; it++)
            {
                Vector2 q = PushOut(p, props);
                if ((q - p).sqrMagnitude < 1e-6f) return p;
                Vector2 v = q - from;
                p = v.sqrMagnitude > 1e-6f ? from + v.normalized * reach : q;
            }
            return PushOut(p, props);
        }

        Vector2 PushOut(Vector2 p, bool props = true)
        {
            float d = climbAllow > 0 ? world.ClimbDist(p.x, p.y, climbAllow) : world.Dist(p.x, p.y);
            if (d > -cfg.wallMargin)
            {
                Vector2 g = climbAllow > 0 ? world.GradClimb(p.x, p.y, climbAllow) : world.GradDist(p.x, p.y);
                float len = g.magnitude;
                if (len > 1e-6f) p -= g / len * (d + cfg.wallMargin);
            }
            if (!props) return p;   // (the particle nests: TickSteady)
            foreach (Circle c in world.colliders.Near(p.x, p.y))
            {
                if (c.h < cfg.blockingPropHeight) continue;   // the camera passes over low things
                Vector2 dc = p - new Vector2(c.x, c.z);
                float r = c.r + 0.35f;
                if (dc.sqrMagnitude < r * r && dc.sqrMagnitude > 1e-8f) p = new Vector2(c.x, c.z) + dc.normalized * r;
            }
            return p;
        }

        string Blocked(Vector3 p)
        {
            if (p.y < world.Height(p.x, p.z) + 0.3f) return "ground";
            if (p.y > world.CameraCeiling(p.x, p.z) - 0.5f) return "roof";
            if (HitsProp(p)) return "prop";
            return null;
        }

        /// <summary>
        /// The particle nests' camera (steady): the same weight on a string as Tick, three things changed, because there
        /// that one lurched in and out past the plants and flicked from side to side (-camcheck, 11-03: 22 jolts in 18 s):
        /// - props never move it. A nest's plants and things are splats the shader already clears from the line to the ant
        ///   and fades at the lens (ParticleSolid: sight, the near fade); and a plant's footprint is some thirty small
        ///   circles, so the boom came in whenever its line grazed one and went out again, and its end, pushed out of them
        ///   one by one, jumped from side to side. The end still slides along the canyon's walls (the ground's distance field).
        /// - the ground lifts the camera a little, smoothly, instead of shortening the boom (a rise behind the ant, a bump
        ///   under the camera); only what lifting cannot clear (a high bank, a roof) brings it closer.
        /// - every change is eased with continuous speed, on springs instead of steps: the look-at height, the lift, the
        ///   boom and the camera's place round the ant.
        /// </summary>
        void TickSteady(float dt, AntController ant, bool inputEnabled)
        {
            time += dt;
            HandleMouse(inputEnabled, dt);
            sprint += ((ant.sprinting ? 1 : 0) - sprint) * (1 - Mathf.Exp(-(ant.sprinting ? 3 : 2) * dt));
            cam.fieldOfView = baseFov + cfg.sprintFovKick * sprint;
            Vector3 antPos = ant.Position;
            // the look-at point: on the ant, its height following on a spring (no steps in speed)
            if (!placed) { smoothY = antPos.y; smoothYVel = 0; }
            smoothY = Mathf.SmoothDamp(smoothY, antPos.y, ref smoothYVel, 0.12f, Mathf.Infinity, dt);
            Vector3 target = new Vector3(antPos.x, smoothY + cfg.height, antPos.z);
            var flatTarget = new Vector2(target.x, target.z);
            float camPitch = Mathf.Max(pitch, -0.08f);
            float lookUp = Mathf.Max(0, -pitch);
            float dist = distance * (1 + (cfg.sprintDistance - 1) * sprint);
            float reach = dist * Mathf.Cos(camPitch);

            // the weight on the string, and easing round behind an ant running away from it: as in Tick
            if (Mathf.Abs(Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, tickYaw * Mathf.Rad2Deg)) > 0.01f) reanchor = true;
            bool turning = dragging || padTurning;
            if (placed && !turning && !reanchor)
            {
                Vector2 v = anchor - flatTarget;
                if (v.sqrMagnitude > 1e-4f) yaw = Mathf.Atan2(v.x, v.y);
            }
            reanchor = false;
            float swingWanted = 0;
            if (ant.speed > 0.2f && !turning && time - lastDrag > cfg.followDelay)
            {
                float diff = Mathf.DeltaAngle(yaw * Mathf.Rad2Deg, (ant.heading + Mathf.PI) * Mathf.Rad2Deg) * Mathf.Deg2Rad;
                swingWanted = diff * cfg.recenterRate * Mathf.Clamp01((Mathf.Cos(diff) - 0.35f) / 0.5f);
            }
            swing += (swingWanted - swing) * (1 - Mathf.Exp(-4f * dt));
            yaw += swing * dt;
            Vector2 end = flatTarget + new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * reach;
            // the end slides along the canyon's walls, never round props. Where the player has turned the camera it stays:
            // the walls are taken up again only as the ant walks on, gently, the end moving at most 2.5 units a second
            slideOn += ((turning ? 0f : ant.speed > 0.1f ? 1f : slideOn) - slideOn) * (1 - Mathf.Exp(-(turning ? 5f : 2.5f) * dt));
            slideBy = 0;
            if (!free && ant.altitude < 0.5f)
            {
                climbAllow = Mathf.Clamp(world.Dist(antPos.x, antPos.z) + 1f, 0, ant.cfg.climb);
                Vector2 s = Slide(flatTarget, end, reach, false);
                slideBy = (s - end).magnitude;
                if (!placed) end = s;
                else end += Vector2.ClampMagnitude((s - end) * (1 - Mathf.Exp(-cfg.slideRate * dt)) * slideOn, 2.5f * dt);
            }
            anchor = end;
            Vector2 flat = end - flatTarget;
            float along = flat.magnitude;
            if (along < 1e-3f) { flat = new Vector2(Mathf.Sin(yaw), Mathf.Cos(yaw)) * 1e-3f; along = 1e-3f; }
            if (!turning) yaw = Mathf.Atan2(flat.x, flat.y);
            Vector3 dir = new Vector3(flat.x, dist * Mathf.Sin(camPitch) * along / Mathf.Max(reach, 1e-3f), flat.y);
            Vector3 want = dir.normalized * Mathf.Max(dir.magnitude, Mathf.Min(dist, cfg.minDistance));
            if (placed) yawVel += (Mathf.DeltaAngle(lastYaw * Mathf.Rad2Deg, yaw * Mathf.Rad2Deg) * Mathf.Deg2Rad / Mathf.Max(dt, 1e-4f) - yawVel) * (1 - Mathf.Exp(-10f * dt));
            lastYaw = yaw;

            // the ground lifts the camera: above the floor under it (and under where a turn is taking it), and the line to
            // the ant above the floor between them
            float need = LiftNeed(target, want);
            float ahead = Mathf.Clamp(yawVel * 0.15f, -0.6f, 0.6f);
            if (Mathf.Abs(ahead) > 0.02f) need = Mathf.Max(need, LiftNeed(target, Quaternion.Euler(0, ahead * Mathf.Rad2Deg, 0) * want));
            need = Mathf.Clamp(need, 0, MaxLift);
            if (!placed) { lift = need; liftVel = 0; }
            else lift = Mathf.SmoothDamp(lift, need, ref liftVel, need > lift ? 0.08f : 0.5f, Mathf.Infinity, dt);
            lifted = lift;
            Vector3 raised = want + Vector3.up * lift;
            float length = raised.magnitude;
            Vector3 ldir = raised / length;

            // what lifting cannot clear (a high bank, a roof) shortens the boom, found to the hundredth rather than in steps
            float allowed = length;
            blockedBy = "";
            const int steps = 24;
            for (int i = 1; i <= steps; i++)
            {
                string why = SteadyBlocked(target + ldir * (length * i / steps));
                if (why == null) continue;
                float lo = length * (i - 1) / steps, hi = length * i / steps;
                for (int b = 0; b < 5; b++)
                {
                    float m = (lo + hi) * 0.5f;
                    if (SteadyBlocked(target + ldir * m) == null) lo = m; else hi = m;
                }
                allowed = lo;
                blockedBy = why;
                break;
            }
            allowed = Mathf.Max(allowed, Mathf.Min(length, cfg.minDistance));
            // in quickly, then after a short hold out again slowly; on springs, so its speed never jumps
            if (!placed) { boom = allowed; boomVel = 0; hold = 0; }
            else
            {
                if (allowed < boom - 0.02f) hold = cfg.zoomHold;
                else hold -= dt;
                float to = allowed < boom || hold <= 0 ? allowed : boom;
                boom = Mathf.SmoothDamp(boom, to, ref boomVel, to < boom ? 0.08f : 0.7f, Mathf.Infinity, dt);
            }

            // the camera's place round the ant on a spring too (quick while the player turns it)
            Vector3 wantOff = ldir * boom;
            if (!placed) { offset = wantOff; offVel = Vector3.zero; }
            else offset = Vector3.SmoothDamp(offset, wantOff, ref offVel, turning ? 0.05f : 0.14f, Mathf.Infinity, dt);
            Vector3 pos = target + offset;
            // never under the floor: eased onto it from a quarter above (soft), never through it
            float minY = world.Height(pos.x, pos.z) + 0.15f;
            float y0 = pos.y;
            const float soft = 0.15f;
            float over = (pos.y - minY) / soft;
            if (over < 6f) pos.y = minY + soft * Mathf.Log(1 + Mathf.Exp(over));
            clamped = Mathf.Max(0, pos.y - y0);
            float maxY = world.CameraCeiling(pos.x, pos.z) - 0.45f;   // in a tunnel, stay under the roof
            if (pos.y > maxY) pos.y = Mathf.Max(minY, maxY);
            transform.position = pos;
            placed = true;
            tickYaw = yaw;
            Vector3 look = target + Vector3.up * (Mathf.Tan(Mathf.Min(1.25f, lookUp * 2.2f)) * Vector3.Distance(pos, target));
            transform.rotation = Quaternion.LookRotation(look - pos, Vector3.up);

            // inside a plant or a thing taller than the camera (or one right before the lens): its splats round the lens
            // clear further, eased in and out (the camera itself never moves for props)
            float inside = Occupancy(pos);
            Vector3 toward = (target - pos).normalized;
            inside = Mathf.Max(inside, 0.8f * Occupancy(pos + toward * 0.7f));
            lensClear = Mathf.SmoothDamp(lensClear, inside, ref lensVel, inside > lensClear ? 0.12f : 0.4f, Mathf.Infinity, dt);
            if (lensClear < 1e-3f && inside == 0) lensClear = 0;
            if (lensClear > 0 || lensSet) { Shader.SetGlobalVector("_LensClear", new Vector4(lensClear, 0, 0, 0)); lensSet = lensClear > 0; }
        }

        /// <summary>How far the steady camera must rise above the boom's end: the floor under it, the line to the ant.</summary>
        float LiftNeed(Vector3 target, Vector3 want)
        {
            float need = world.Height(target.x + want.x, target.z + want.z) + 0.4f - (target.y + want.y);
            for (int i = 3; i < 12; i++)
            {
                float f = i / 12f;
                Vector3 p = target + want * f;
                need = Mathf.Max(need, (world.Height(p.x, p.z) + 0.25f - p.y) / f);
            }
            return need;
        }

        /// <summary>How much p stands inside a prop that rises above it (0..1): 1 within its footprint, nothing 0.8 out.</summary>
        float Occupancy(Vector3 p)
        {
            float occ = 0, floor = float.NaN;
            foreach (Circle c in world.colliders.Around(p.x, p.z, 0.9f, around))
            {
                float dx = p.x - c.x, dz = p.z - c.z, reach = c.r + 0.9f;
                if (dx * dx + dz * dz > reach * reach) continue;
                if (float.IsNaN(floor)) floor = world.Height(p.x, p.z);
                if (floor + c.h < p.y - 0.1f) continue;   // below the camera: it looks over it
                float d = Mathf.Sqrt(dx * dx + dz * dz) - c.r;
                occ = Mathf.Max(occ, 1 - Mathf.SmoothStep(0, 1, (d - 0.1f) / 0.8f));
                if (occ >= 1) break;
            }
            return occ;
        }

        const float MaxLift = 2.5f;   // the most the ground lifts the steady camera (units; turned toward a bank it rises over it); beyond, the boom comes in

        string SteadyBlocked(Vector3 p)
        {
            if (p.y < world.Height(p.x, p.z) + 0.12f) return "ground";
            if (p.y > world.CameraCeiling(p.x, p.z) - 0.5f) return "roof";
            return null;
        }

        /// <summary>The previous camera (swings back behind the ant, the boom stays inside the canyon), for checks.</summary>
        void TickClassic(float dt, AntController ant, bool inputEnabled)
        {
            time += dt;
            HandleMouse(inputEnabled, dt);
            bool moving = ant.speed > 0.05f;
            sprint += ((ant.sprinting ? 1 : 0) - sprint) * (1 - Mathf.Exp(-(ant.sprinting ? 3 : 2) * dt));
            cam.fieldOfView = baseFov + 9f * sprint;
            float delay = cfg.followDelay * (1 - 0.6f * sprint);
            if (moving && !dragging && time - lastDrag > delay)
            {
                float diff = ant.heading + Mathf.PI - yaw;
                diff = Mathf.Atan2(Mathf.Sin(diff), Mathf.Cos(diff));
                yaw += diff * (1 - Mathf.Exp(-(1.6f + 2.2f * sprint) * dt));
            }
            Vector3 antPos = ant.Position;
            if (!placed) smoothY = antPos.y;
            smoothY += (antPos.y - smoothY) * (1 - Mathf.Exp(-8f * dt));
            Vector3 target = new Vector3(antPos.x, smoothY + cfg.height, antPos.z);
            float camPitch = Mathf.Max(pitch, -0.08f);
            float lookUp = Mathf.Max(0, -pitch);
            float cp = Mathf.Cos(camPitch);
            float dist = distance * (1 + (1.15f - 1) * sprint);
            Vector3 dir = new Vector3(Mathf.Sin(yaw) * cp, Mathf.Sin(camPitch), Mathf.Cos(yaw) * cp);
            float allowed = dist;
            blockedBy = "";
            const int steps = 32;
            for (int i = 1; i <= steps; i++)
            {
                float d = dist * i / steps;
                Vector3 p = target + dir * d;
                string why = p.y < world.Height(p.x, p.z) + 0.3f ? "ground" : p.y > world.Ceiling(p.x, p.z) - 0.5f ? "roof"
                    : !free && ant.altitude < 0.5f && world.Dist(p.x, p.z) > -cfg.wallMargin ? "wall" : HitsProp(p) ? "prop" : null;
                if (why != null) { allowed = dist * (i - 1) / steps; blockedBy = why; break; }
            }
            allowed = Mathf.Max(allowed, Mathf.Min(dist, cfg.minDistance));
            if (!placed) boom = allowed;
            else if (allowed < boom)
            {
                float rate = boom - allowed > 2f ? 20f : 10f;
                boom += (allowed - boom) * (1 - Mathf.Exp(-rate * dt));
            }
            else boom += (allowed - boom) * (1 - Mathf.Exp(-1.6f * dt));
            Vector3 desired = target + dir * boom;
            float minY = world.Height(desired.x, desired.z) + 0.2f;
            if (desired.y < minY) desired.y = minY;
            float maxY = world.Ceiling(desired.x, desired.z) - 0.45f;
            if (desired.y > maxY) desired.y = Mathf.Max(minY, maxY);
            float k = 1 - Mathf.Exp(-(18 + 8 * sprint) * dt);
            transform.position = placed ? Vector3.Lerp(transform.position, desired, k) : desired;
            placed = true;
            Vector3 look = target + Vector3.up * (Mathf.Tan(Mathf.Min(1.25f, lookUp * 2.2f)) * Vector3.Distance(transform.position, target));
            transform.rotation = Quaternion.LookRotation(look - transform.position, Vector3.up);
        }

        bool HitsProp(Vector3 p)
        {
            foreach (var c in world.colliders.Near(p.x, p.z))
            {
                if (c.h < cfg.blockingPropHeight) continue; // low props never push the camera in
                float dx = p.x - c.x, dz = p.z - c.z, r = c.r * 0.85f;
                if (dx * dx + dz * dz < r * r && p.y < world.Height(c.x, c.z) + c.h + 0.3f) return true;
            }
            return false;
        }
    }
}

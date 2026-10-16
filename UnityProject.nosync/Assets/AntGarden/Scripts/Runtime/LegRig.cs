using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// Procedural legs for ants near the camera, modelled on how ants walk:
    /// - an alternating tripod gait: front and hind leg of one side step together with the middle leg of
    ///   the other side, while the other three hold the body;
    /// - feet stay planted while on the ground (no sliding) and swing in a quick arc to a point ahead;
    /// - stride length grows with speed, so cadence stays calm at a walk and quickens only when running;
    /// - a low, sprawled posture: feet out to the sides, knees bent up and outward above the body.
    /// The foot (the tip of the tarsus) is the contact point. The tarsus keeps the slope it has in the idle
    /// pose, pointing away from the body, which fixes the ankle; two-bone IK (femur, tibia) reaches it.
    /// The clip still drives the body, head, antennae and gaster; farther ants keep the clip legs.
    /// </summary>
    public class LegRig
    {
        class Leg
        {
            public Transform coxa, femur, tibia, tarsus;
            public Quaternion coxaRest, femurRest, tibiaRest, tarsusRest; // idle local rotations
            public Vector3 femurAxis, tibiaAxis;                          // bone directions in local space
            public float upper, lower, tarsusLen, slopeSin, slopeCos;
            public Vector3 home;                                          // foot rest, body space, on the ground plane
            public Vector2 hip2;                                          // hip, body space, on the ground plane
            public float maxFlat;                                         // how far from the hip the foot can reach
            public int group;
            public Vector3 foot, lift;                                    // world space foot (tarsus tip) targets
            public bool swinging;
        }

        static readonly string[] Names = { "front", "mid", "hind" };
        static readonly System.Collections.Generic.HashSet<string> logged = new System.Collections.Generic.HashSet<string>();
        readonly AntController ant;
        readonly Transform body, lean;
        readonly Leg[] legs = new Leg[6];
        float weight, phase, rate, lastHeading, strideLimit = float.MaxValue;
        Vector3 lastPos, aheadNow;
        bool planted, settling;

        public float Weight => weight;
        public Vector3 Foot(int i) => legs[i].foot;
        public bool Swinging(int i) => legs[i].swinging;
        public float Phase => phase;
        // checks: the posed bones and the visible tip of each foot
        public Transform Femur(int i) => legs[i].femur;
        public Transform Tibia(int i) => legs[i].tibia;
        public Vector3 Tip(int i) => legs[i].tarsus.position + legs[i].tarsus.TransformDirection(legs[i].tibiaAxis) * legs[i].tarsusLen;

        LegRig(AntController ant, Transform lean)
        {
            this.ant = ant;
            body = ant.transform;
            this.lean = lean;
        }

        /// <summary>Build from the model's skeleton (bones leg_[front|mid|hind]_[coxa|femur|tibia|tarsus].[L|R]),
        /// with the model in its idle pose at the body's origin. Null when the bones are missing.</summary>
        public static LegRig Create(AntController ant, Transform model, Transform lean)
        {
            var rig = new LegRig(ant, lean);
            var bones = model.GetComponentsInChildren<Transform>(true);
            Transform Find(string n)
            {
                foreach (var t in bones) if (t.name == n) return t;
                return null;
            }
            AntSettings cfg = ant.cfg;
            float bl = cfg.bodyLength;
            for (int i = 0; i < 6; i++)
            {
                string pair = Names[i / 2], side = i % 2 == 0 ? "L" : "R";
                var leg = new Leg
                {
                    coxa = Find($"leg_{pair}_coxa.{side}"),
                    femur = Find($"leg_{pair}_femur.{side}"),
                    tibia = Find($"leg_{pair}_tibia.{side}"),
                    tarsus = Find($"leg_{pair}_tarsus.{side}"),
                };
                if (leg.coxa == null || leg.femur == null || leg.tibia == null || leg.tarsus == null) return null;
                // tripod groups: front L, mid R, hind L step together; then front R, mid L, hind R
                leg.group = ((i / 2) % 2 == 0) == (side == "L") ? 0 : 1;
                leg.coxaRest = leg.coxa.localRotation;
                leg.femurRest = leg.femur.localRotation;
                leg.tibiaRest = leg.tibia.localRotation;
                leg.tarsusRest = leg.tarsus.localRotation;
                Vector3 knee = leg.tibia.position - leg.femur.position, shin = leg.tarsus.position - leg.tibia.position;
                leg.upper = knee.magnitude;
                leg.lower = shin.magnitude;
                leg.femurAxis = leg.femur.InverseTransformDirection(knee.normalized);
                leg.tibiaAxis = leg.tibia.InverseTransformDirection(shin.normalized);
                // rest pose in body space (the body sits at the origin when this runs). The tarsus has no end
                // joint: its length follows from the idle foot touching the ground at the tarsus' idle slope.
                Vector3 ankle = ant.transform.InverseTransformPoint(leg.tarsus.position);
                Vector3 hip = ant.transform.InverseTransformPoint(leg.femur.position);
                Vector3 tdir = ant.transform.InverseTransformDirection(leg.tarsus.TransformDirection(leg.tibiaAxis));
                leg.slopeSin = Mathf.Clamp(-tdir.y, 0.2f, 0.8f);
                leg.slopeCos = Mathf.Sqrt(1 - leg.slopeSin * leg.slopeSin);
                leg.tarsusLen = Mathf.Clamp(Mathf.Max(0.01f * bl, ankle.y) / leg.slopeSin, 0.05f * bl, 0.35f * bl);
                Vector2 h = new Vector2(tdir.x, tdir.z);
                h = h.sqrMagnitude > 1e-6f ? h.normalized : new Vector2(Mathf.Sign(ankle.x), 0);
                Vector2 tip = new Vector2(ankle.x, ankle.z) + h * leg.tarsusLen * leg.slopeCos;
                // spread the foot away from its hip (wider stance), front feet further forward, hind further back
                Vector2 hip2 = new Vector2(hip.x, hip.z);
                Vector2 off = tip - hip2;
                off.x *= cfg.legSpread;
                if (i / 2 != 1) off.y *= cfg.legReach;
                // keep the ankle within reach once the body is lowered
                float reach = 0.92f * (leg.upper + leg.lower);
                float hipY = hip.y - cfg.bodyDrop * bl;
                float rise = hipY - leg.tarsusLen * leg.slopeSin;
                float maxFlat = Mathf.Sqrt(Mathf.Max(0.0001f, reach * reach - rise * rise)) + leg.tarsusLen * leg.slopeCos;
                // leave room to step: the foot must reach half a stride in front of and behind its home.
                // Too far forward (or back) first moves it towards the hip line, then towards the body.
                float half = 0.5f * cfg.strideRoom * bl;
                float Need(Vector2 o) => Mathf.Sqrt(o.x * o.x + (Mathf.Abs(o.y) + half) * (Mathf.Abs(o.y) + half));
                if (Need(off) > maxFlat)
                {
                    float room = maxFlat * maxFlat - off.x * off.x;
                    float oy = room > 0 ? Mathf.Max(0, Mathf.Sqrt(room) - half) : 0;
                    off.y = Mathf.Sign(off.y) * Mathf.Min(Mathf.Abs(off.y), oy);
                    if (Need(off) > maxFlat)
                    {
                        float ox = Mathf.Sqrt(Mathf.Max(0, maxFlat * maxFlat - half * half));
                        off.x = Mathf.Sign(off.x) * Mathf.Max(0.06f * bl, Mathf.Min(Mathf.Abs(off.x), ox));
                    }
                }
                Vector3 home = new Vector3(hip2.x + off.x, 0, hip2.y + off.y);
                leg.hip2 = hip2;
                leg.maxFlat = maxFlat;
                float fore = Mathf.Sqrt(Mathf.Max(0.0001f, maxFlat * maxFlat - off.x * off.x)) - Mathf.Abs(off.y);
                rig.strideLimit = Mathf.Min(rig.strideLimit, Mathf.Max(0.12f * bl, 2f * 0.95f * fore));
                leg.home = home;
                rig.legs[i] = leg;
                if (logged.Add(model.name + i))
                    Debug.Log($"[AntGarden] legs {model.name} {pair}.{side}: hip ({hip.x / bl:F2},{hip.y / bl:F2},{hip.z / bl:F2}) " +
                              $"ankle ({ankle.x / bl:F2},{ankle.y / bl:F2},{ankle.z / bl:F2}) femur {leg.upper / bl:F2} tibia {leg.lower / bl:F2} " +
                              $"tarsus {leg.tarsusLen / bl:F2} slope {leg.slopeSin:F2} foot ({home.x / bl:F2},{home.z / bl:F2}) stride<= {rig.strideLimit / bl:F2} BL");
            }
            return rig;
        }

        /// <summary>Plant every foot at its home again (after a teleport).</summary>
        public void Reset() => planted = false;

        Vector3 HomeWorld(Leg leg, Vector3 ahead)
        {
            Vector3 p = body.TransformPoint(leg.home) + ahead;
            // never further from the hip than the leg reaches
            Vector3 hip = body.TransformPoint(new Vector3(leg.hip2.x, 0, leg.hip2.y));
            Vector2 off = new Vector2(p.x - hip.x, p.z - hip.z);
            if (off.magnitude > leg.maxFlat)
            {
                off = off.normalized * leg.maxFlat;
                p.x = hip.x + off.x;
                p.z = hip.z + off.y;
            }
            p.y = ant.world.WalkHeight(p.x, p.z) + 0.004f * ant.cfg.bodyLength;
            return p;
        }

        /// <summary>Advance the gait and pose the legs; call after the clips were evaluated this frame.</summary>
        public void Update(float dt, bool active)
        {
            AntSettings cfg = ant.cfg;
            weight = Mathf.MoveTowards(weight, active ? 1 : 0, dt / Mathf.Max(0.01f, cfg.ikBlend));
            Vector3 pos = body.position;
            float moved = new Vector2(pos.x - lastPos.x, pos.z - lastPos.z).magnitude;
            float turned = Mathf.Abs(Mathf.DeltaAngle(lastHeading * Mathf.Rad2Deg, ant.heading * Mathf.Rad2Deg)) * Mathf.Deg2Rad;
            lastPos = pos;
            lastHeading = ant.heading;
            float bl = cfg.bodyLength;
            lean.localPosition = new Vector3(0, -cfg.bodyDrop * bl * weight, 0);
            if (weight <= 0)
            {
                planted = false;
                return;
            }
            if (!planted || moved > 2f * bl)
            {
                moved = turned = 0;   // a teleport is not a step
                foreach (var leg in legs)
                {
                    leg.foot = HomeWorld(leg, Vector3.zero);
                    leg.swinging = false;
                }
                planted = true;
                phase = 0.42f; // between the two swings: all six feet down
                rate = 0;
                aheadNow = Vector3.zero;
            }

            // the gait clock advances with ground covered. Turning adds steps too, but only up to a calm
            // cadence: beyond it the planted feet pivot a little instead of the legs flickering through steps
            // (the short-legged ants once reached 7-8 steps a second while steering, with one-frame swings)
            float speedBl = ant.speed / bl;
            float stride = Mathf.Min(strideLimit, bl * Mathf.Min(cfg.strideMax, cfg.strideBase + cfg.strideGain * speedBl));
            float walk = moved / stride, turn = turned * cfg.turnStep * bl / stride;
            float dphi = Mathf.Min(walk + turn, Mathf.Max(walk, cfg.legCadenceCap * dt));
            bool anySwing = false;
            foreach (var leg in legs) anySwing |= leg.swinging;
            // finish a step that is in the air, or take a few steps to settle after stopping
            if (dphi < 0.6f * dt && (anySwing || settling)) dphi = Mathf.Max(dphi, 2.2f * dt);
            bool moving = ant.speed > 0.05f * bl;
            // eased, so starting, stopping or a sudden turn changes the rhythm smoothly, never in one frame;
            // but once the ant stands with all six feet down, the clock stops dead (no stepping in place)
            if (!moving && !anySwing && !settling && turned < 1e-4f) rate = 0;
            else rate += (dphi / Mathf.Max(dt, 1e-4f) - rate) * (1 - Mathf.Exp(-10f * dt));
            phase = Mathf.Repeat(phase + Mathf.Min(rate * dt, 0.25f), 1f);

            Vector3 fwd = body.forward;
            // where a stepping foot lands: half a stride ahead while walking, home when stopping; eased, so a
            // foot already in the air when the ant stops does not jump to its new landing point
            Vector3 aheadWant = moving ? new Vector3(fwd.x, 0, fwd.z).normalized * stride * 0.5f : Vector3.zero;
            aheadNow = Vector3.Lerp(aheadNow, aheadWant, 1 - Mathf.Exp(-8f * dt));
            Vector3 ahead = aheadNow;
            float lift = cfg.stepHeight * bl;
            settling = false;
            foreach (var leg in legs)
            {
                float lp = Mathf.Repeat(phase + leg.group * 0.5f, 1f);
                bool swing = lp < cfg.swingFraction;
                if (swing && !leg.swinging)
                {
                    leg.swinging = true;
                    leg.lift = leg.foot;
                }
                if (leg.swinging)
                {
                    Vector3 land = HomeWorld(leg, ahead);
                    if (!swing)
                    {
                        leg.swinging = false;
                        leg.foot = land;
                    }
                    else
                    {
                        float s = lp / cfg.swingFraction;
                        float e = s * s * (3 - 2 * s);
                        leg.foot = Vector3.Lerp(leg.lift, land, e) + body.up * (lift * Mathf.Sin(Mathf.PI * s));
                    }
                }
                else
                {
                    // a planted foot the body has turned or run away from slides in, rather than the leg
                    // overstretching until the next step
                    Vector3 hipW = body.TransformPoint(new Vector3(leg.hip2.x, 0, leg.hip2.y));
                    Vector2 off = new Vector2(leg.foot.x - hipW.x, leg.foot.z - hipW.z);
                    if (off.magnitude > leg.maxFlat * 1.02f)
                    {
                        off = off.normalized * leg.maxFlat * 1.02f;
                        leg.foot.x = hipW.x + off.x;
                        leg.foot.z = hipW.z + off.y;
                        leg.foot.y = ant.world.WalkHeight(leg.foot.x, leg.foot.z) + 0.004f * bl;
                    }
                }
                if (!moving)
                {
                    Vector3 h = body.TransformPoint(leg.home);
                    if (new Vector2(leg.foot.x - h.x, leg.foot.z - h.z).magnitude > 0.12f * bl) settling = true;
                }
                Solve(leg, cfg);
            }
        }

        void Solve(Leg leg, AntSettings cfg)
        {
            float w = weight;
            // start from the idle pose so every frame solves from the same twist
            leg.coxa.localRotation = Quaternion.Slerp(leg.coxa.localRotation, leg.coxaRest, w);
            leg.femur.localRotation = Quaternion.Slerp(leg.femur.localRotation, leg.femurRest, w);
            leg.tibia.localRotation = Quaternion.Slerp(leg.tibia.localRotation, leg.tibiaRest, w);
            leg.tarsus.localRotation = Quaternion.Slerp(leg.tarsus.localRotation, leg.tarsusRest, w);

            Vector3 up = body.up;
            Vector3 hip = leg.femur.position;
            // the tarsus runs from the ankle down and away from the body to the foot: that fixes the ankle
            Vector3 flat = Vector3.ProjectOnPlane(leg.foot - hip, up);
            Vector3 outward = Vector3.ProjectOnPlane(hip - body.position, up);
            outward = outward.sqrMagnitude > 1e-8f ? outward.normalized : body.right;
            Vector3 away = flat.sqrMagnitude > 1e-8f ? flat.normalized : outward;
            Vector3 ankleTarget = leg.foot - away * (leg.tarsusLen * leg.slopeCos) + up * (leg.tarsusLen * leg.slopeSin);
            // never above the hip: a foot planted high on a step or a steep bank would otherwise swing the knee
            // round to the other side in a single frame (it then sinks a hair into the step instead)
            float rise = Vector3.Dot(ankleTarget - hip, up), lowest = -0.04f * cfg.bodyLength;
            if (rise > lowest) ankleTarget -= up * (rise - lowest);
            Vector3 toT = ankleTarget - hip;
            float a = leg.upper, b = leg.lower;
            float d = Mathf.Clamp(toT.magnitude, Mathf.Abs(a - b) + 1e-3f, a + b - 1e-3f);
            Vector3 dir = toT.sqrMagnitude > 1e-8f ? toT.normalized : -up;
            Vector3 bend = Vector3.ProjectOnPlane(up + outward * cfg.kneeOut, dir);
            bend = bend.sqrMagnitude > 1e-8f ? bend.normalized : up;
            float cosA = Mathf.Clamp((a * a + d * d - b * b) / (2 * a * d), -1f, 1f);
            float sinA = Mathf.Sqrt(1 - cosA * cosA);
            Vector3 knee = hip + (dir * cosA + bend * sinA) * a;
            Vector3 ankle = hip + dir * d;

            Vector3 cur = leg.femur.TransformDirection(leg.femurAxis);
            Quaternion target = Quaternion.FromToRotation(cur, knee - hip) * leg.femur.rotation;
            leg.femur.rotation = Quaternion.Slerp(leg.femur.rotation, target, w);
            cur = leg.tibia.TransformDirection(leg.tibiaAxis);
            target = Quaternion.FromToRotation(cur, ankle - leg.tibia.position) * leg.tibia.rotation;
            leg.tibia.rotation = Quaternion.Slerp(leg.tibia.rotation, target, w);
            // tarsus: from the ankle to the foot
            cur = leg.tarsus.TransformDirection(leg.tibiaAxis);
            Vector3 toFoot = leg.foot - leg.tarsus.position;
            target = Quaternion.FromToRotation(cur, toFoot.sqrMagnitude > 1e-8f ? toFoot : -up) * leg.tarsus.rotation;
            leg.tarsus.rotation = Quaternion.Slerp(leg.tarsus.rotation, target, w);
        }
    }
}

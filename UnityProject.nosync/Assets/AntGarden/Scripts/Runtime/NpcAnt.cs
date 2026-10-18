using System.Collections.Generic;
using UnityEngine;

namespace AntGarden
{
    /// <summary>
    /// NPC ant, modelled on how real ants move:
    /// - Trail ants stream along the recognized paths in both directions, close to the centreline, and at
    ///   junctions mostly carry straight on, so the paths read as foraging trails.
    /// - Wanderers roam the whole canyon width.
    /// - Chamber ants mill about a junction chamber: short walks, pauses, antenna contact, sometimes leaving.
    /// Everyone walks in bouts (speed drifts, short stops), follows a gently weaving line instead of a
    /// polyline, and when two ants meet head-on they stop, touch antennae and pass each other.
    /// They also greet the player, and winged species take off, fly to another path and land.
    /// With the colony's pheromone trails (ColonyTrails), trail ants forage like a real colony: they search
    /// along the "to food" pheromone, pick up food at the dead ends, carry it home along "to home", and mark
    /// the way as they go; meeting a carrier head-on recruits a searcher to the food trail. Steering is gentle
    /// (no spinning on the spot): a rejoined path is walked the way the ant already faces, chamber ants
    /// choose their next spot ahead of them.
    /// </summary>
    public class NpcAnt
    {
        public enum Role { Trail, Wander, Chamber }
        enum State { Walk, Pause, Greet, Meet, TakeOff, Fly, Land }

        public readonly AntController ant;
        public readonly SpeciesSettings species;
        public readonly int region;          // NPCs live in one region (regions are not connected on foot)
        public Role role { get; private set; }
        readonly World world;
        readonly NpcSettings cfg;
        readonly System.Random rng;
        readonly float takeoffForward, takeoffRise, landForward;
        readonly List<WEdge> regionEdges = new List<WEdge>();
        readonly List<AntController> nearby = new List<AntController>(16);

        State state = State.Walk;
        public bool carrying;               // a forager on its way home with food
        public string StateName => state.ToString();   // for the movement check's breakdown
        public int lineIndex = -1;          // a messenger: the next line it shares when antennae touch (-1: none)

        /// <summary>A messenger's next line (it has a few, in turn), or null for an ant that only touches.</summary>
        public string NextLine()
        {
            if (lineIndex < 0) return null;
            string line = Voices.T(Voices.AntLines[lineIndex % Voices.AntLines.Length]);
            lineIndex += 1 + Voices.AntLines.Length / 3;   // its next line is a different one
            return line;
        }
        float recruit;                      // seconds of eager trail following after meeting a carrier
        float timer, greetCooldown, meetCooldown, meetCheck, flyCooldown, flightAlt, flightTime;
        float age, baseLane, phaseA, phaseB, phaseC;
        float stall, bestGap = float.MaxValue;   // no progress towards the current path point: give it up
        bool fast;
        WEdge edge;
        List<Vector2> pts;
        int index;
        string endNode;
        Vector2 flyTarget, millTarget;
        WNode chamber;
        AntController face;

        public NpcAnt(AntController ant, SpeciesSettings species, World world, NpcSettings cfg, int seed, float takeoffForward, float takeoffRise, float landForward, int region)
        {
            this.region = region;
            foreach (WEdge e in world.edges) if (e.region == region && e.pts.Count >= 2) regionEdges.Add(e);
            this.ant = ant;
            ant.npc = this;
            this.species = species;
            this.world = world;
            this.cfg = cfg;
            rng = new System.Random(seed);
            this.takeoffForward = takeoffForward;
            this.takeoffRise = takeoffRise;
            this.landForward = landForward;
            greetCooldown = Range(0, 6);
            meetCooldown = Range(0, 5);
            flyCooldown = Range(cfg.flight.cooldown.x, cfg.flight.cooldown.y);
            phaseA = Range(0, 100);
            phaseB = Range(0, 100);
            phaseC = Range(0, 100);
        }

        float Range(float a, float b) => a + (float)rng.NextDouble() * (b - a);
        bool Chance(float p) => rng.NextDouble() < p;
        T Pick<T>(IList<T> list) => list[rng.Next(list.Count)];
        Vector2 Pos => new Vector2(ant.Position.x, ant.Position.z);
        float BodyLength => ant.cfg.bodyLength;

        /// <summary>Start somewhere suited to the role, away from <paramref name="avoid"/>.</summary>
        public void Spawn(Vector3 avoid, Role r, WNode home)
        {
            role = r;
            chamber = home;
            if (role == Role.Chamber && chamber != null)
            {
                Vector2 p = MillPoint();
                ant.Place(p.x, p.y, Range(-Mathf.PI, Mathf.PI));
                millTarget = MillPoint();
                return;
            }
            role = role == Role.Chamber ? Role.Trail : role;
            baseLane = role == Role.Trail ? Range(-0.28f, 0.28f) : Range(-0.5f, 0.5f);
            carrying = role == Role.Trail && Chance(0.5f);
            for (int tries = 0; tries < 60 && regionEdges.Count > 0; tries++)
            {
                WEdge e = PickByLength();
                bool forward = Chance(0.5f);
                var p = forward ? e.pts : Reversed(e.pts);
                if (p.Count < 4) continue;
                int i = rng.Next(2, Mathf.Max(3, p.Count - 2));
                float x = p[i].x, z = p[i].y;
                if (world.Dist(x, z) > -1.2f) continue;
                if (Vector2.Distance(new Vector2(x, z), new Vector2(avoid.x, avoid.z)) < 6) continue;
                SetRoute(e, p, Mathf.Min(i + 1, p.Count - 1), forward ? e.target : e.source);
                float nx = p[index].x, nz = p[index].y;
                ant.Place(x, z, Mathf.Atan2(nx - x, nz - z));
                return;
            }
            var sp = world.SpawnPoint(region);
            ant.Place(sp.x, sp.z, sp.heading);
            RejoinPath();
        }

        /// <summary>Longer paths get proportionally more ants, so trails look evenly busy.</summary>
        WEdge PickByLength()
        {
            int total = 0;
            foreach (WEdge e in regionEdges) if (e.pts.Count >= 4) total += e.pts.Count;
            int k = rng.Next(Mathf.Max(1, total));
            foreach (WEdge e in regionEdges)
            {
                if (e.pts.Count < 4) continue;
                k -= e.pts.Count;
                if (k < 0) return e;
            }
            return regionEdges[regionEdges.Count - 1];
        }

        static List<Vector2> Reversed(List<Vector2> p)
        {
            var r = new List<Vector2>(p);
            r.Reverse();
            return r;
        }

        void SetRoute(WEdge e, List<Vector2> p, int i, string end)
        {
            edge = e;
            pts = p;
            index = i;
            endNode = end;
            stall = 0;
            bestGap = float.MaxValue;
        }

        /// <summary>Inside a plant's collision circle (with a margin for the ant's own body)?</summary>
        bool Blocked(Vector2 q)
        {
            float r = ant.cfg.radius + 0.15f;
            foreach (var c in world.colliders.Near(q.x, q.y))
                if ((new Vector2(q.x - c.x, q.y - c.z)).sqrMagnitude < (c.r + r) * (c.r + r)) return true;
            return false;
        }

        ColonySettings Colony => cfg.colony;
        bool Foraging => world.trails != null && Colony.enabled && role == Role.Trail;

        /// <summary>
        /// At a junction. Foragers: mark the path just walked, pick up food at a dead end or drop it at home,
        /// then choose by the colony's pheromone (ColonyTrails.Choose). Others: mostly carry straight on.
        /// </summary>
        void NextEdge()
        {
            if (Foraging)
            {
                bool walkedForward = endNode == edge.target;
                world.trails.Lay(edge, walkedForward, carrying, Colony.deposit);
                bool goal = false;
                if (!carrying && world.trails.IsFood(endNode)) { carrying = true; goal = true; }
                else if (carrying && world.trails.IsHome(endNode, region)) { carrying = false; goal = true; }
                if (goal)
                {
                    // food is picked up (or put down) with the antennae and mandibles: a short stop
                    state = State.Pause;
                    timer = Range(Colony.pickupPause.x, Colony.pickupPause.y);
                    ant.PlayTalk();
                }
                Vector2 dir = (pts[pts.Count - 1] - pts[Mathf.Max(0, pts.Count - 3)]).normalized;
                float alpha = recruit > 0 ? Colony.recruitedAlpha : Colony.alpha;
                // after a pick-up or a drop-off the way back is allowed: that is where the trail leads
                if (world.trails.Choose(endNode, goal ? null : edge, dir, carrying, alpha, Colony.straightness, rng, out WEdge next, out bool fwd))
                {
                    var path = fwd ? next.pts : Reversed(next.pts);
                    SetRoute(next, path, 1, fwd ? next.target : next.source);
                    return;
                }
            }
            var options = new List<(WEdge e, List<Vector2> p, string end)>();
            foreach (WEdge e in regionEdges)
            {
                if (e == edge && regionEdges.Count > 1) continue;
                if (e.source == endNode) options.Add((e, e.pts, e.target));
                else if (e.target == endNode) options.Add((e, Reversed(e.pts), e.source));
            }
            if (options.Count == 0)
            {
                // dead end: walk back the way we came
                string end = edge.source == endNode ? edge.target : edge.source;
                SetRoute(edge, Reversed(pts), 1, end);
                return;
            }
            Vector2 inDir = (pts[pts.Count - 1] - pts[Mathf.Max(0, pts.Count - 3)]).normalized;
            float straightness = role == Role.Trail ? 2.6f : 0.8f;
            float total = 0;
            var w = new float[options.Count];
            for (int i = 0; i < options.Count; i++)
            {
                var p = options[i].p;
                Vector2 outDir = (p[Mathf.Min(2, p.Count - 1)] - p[0]).normalized;
                w[i] = Mathf.Exp(straightness * Vector2.Dot(inDir, outDir));
                total += w[i];
            }
            float k = Range(0, total);
            int pick = 0;
            for (; pick < options.Count - 1; pick++)
            {
                k -= w[pick];
                if (k <= 0) break;
            }
            SetRoute(options[pick].e, options[pick].p, 1, options[pick].end);
        }

        void TurnBack()
        {
            var p = Reversed(pts);
            int i = Mathf.Max(1, p.Count - index);
            string end = edge.source == endNode ? edge.target : edge.source;
            SetRoute(edge, p, i, end);
        }

        /// <summary>Continue along the nearest path (after landing, or when leaving a chamber).</summary>
        void RejoinPath()
        {
            WEdge best = null;
            int bestI = 0;
            float bestD = float.MaxValue;
            Vector2 pos = Pos;
            foreach (WEdge e in regionEdges)
                for (int i = 0; i < e.pts.Count; i++)
                {
                    float d = (e.pts[i] - pos).sqrMagnitude;
                    if (d < bestD) { bestD = d; best = e; bestI = i; }
                }
            if (best == null) return;
            bool forward = Chance(0.5f);
            if (Colony.enabled)
            {
                // walk on the way the ant already faces, instead of turning round on the spot
                Vector2 along = best.pts[Mathf.Min(best.pts.Count - 1, bestI + 1)] - best.pts[Mathf.Max(0, bestI - 1)];
                Vector2 facing = new Vector2(Mathf.Sin(ant.heading), Mathf.Cos(ant.heading));
                forward = Vector2.Dot(along, facing) >= 0;
            }
            var p = forward ? best.pts : Reversed(best.pts);
            int idx = forward ? bestI : best.pts.Count - 1 - bestI;
            SetRoute(best, p, Mathf.Min(idx + 1, p.Count - 1), forward ? best.target : best.source);
        }

        /// <summary>A walkable point inside the home chamber; with the colony on, one ahead of the ant.</summary>
        Vector2 MillPoint()
        {
            Vector2 c = new Vector2(chamber.x, chamber.z);
            float radius = Mathf.Max(1.2f, chamber.clear * cfg.chamberSpread);
            if (Colony.enabled && age > 0)
            {
                // the next spot lies within a cone ahead, a few body lengths on: chamber ants wander in arcs
                Vector2 me = Pos;
                for (int tries = 0; tries < 16; tries++)
                {
                    float a = ant.heading + Range(-Colony.millCone, Colony.millCone) * Mathf.Deg2Rad;
                    float r = Range(1.5f, 4f) * BodyLength;
                    Vector2 p = me + new Vector2(Mathf.Sin(a), Mathf.Cos(a)) * r;
                    if ((p - c).magnitude < radius && world.Dist(p.x, p.y) < -0.7f) return p;
                }
            }
            for (int tries = 0; tries < 12; tries++)
            {
                float a = Range(0, Mathf.PI * 2), r = radius * Mathf.Sqrt(Range(0, 1));
                Vector2 p = c + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
                if (world.Dist(p.x, p.y) < -0.7f) return p;
            }
            return world.SnapToWalkable(c.x, c.y, 0.7f, out Vector2 s) ? s : c;
        }

        Vector2 PickFlightTarget()
        {
            for (int tries = 0; tries < 60; tries++)
            {
                WEdge e = Pick(regionEdges);
                Vector2 q = Pick(e.pts);
                if (world.Dist(q.x, q.y) > -1.5f || world.HexDist(q.x, q.y) > -8) continue;
                if (Vector2.Distance(q, Pos) < 15) continue;
                return q;
            }
            return Pos;
        }

        public bool Near(AntController player, float radius)
        {
            Vector3 a = player.Position, b = ant.Position;
            return (new Vector2(a.x - b.x, a.z - b.z)).sqrMagnitude < radius * radius;
        }

        public void Greet(AntController player)
        {
            if (state == State.TakeOff || state == State.Fly || state == State.Land) return;
            state = State.Greet;
            timer = 3.2f;
            greetCooldown = 14;
            face = player;
            ant.PlayTalk();
        }

        /// <summary>Two ants meeting head-on: both stop, face each other and touch antennae.</summary>
        void Meet(NpcAnt other)
        {
            float t = Range(0.8f, 1.6f);
            Sfx.Touch(ant.Position, true);   // heard only near the camera
            foreach (var (a, b) in new[] { (this, other), (other, this) })
            {
                a.state = State.Meet;
                a.timer = t;
                a.face = b.ant;
                a.meetCooldown = a.Range(7, 15);
                a.ant.PlayTalk();
            }
            // a searcher that meets a carrier smells the food on it and follows the trail more eagerly
            if (carrying != other.carrying)
            {
                NpcAnt searcher = carrying ? other : this;
                searcher.recruit = cfg.colony.recruitTime;
            }
            // afterwards they pass on opposite sides
            float side = baseLane >= other.baseLane ? 1 : -1;
            baseLane = Mathf.Clamp(baseLane + 0.25f * side, -0.55f, 0.55f);
            other.baseLane = Mathf.Clamp(other.baseLane - 0.25f * side, -0.55f, 0.55f);
        }

        void LookForMeeting(float dt)
        {
            meetCheck -= dt;
            if (meetCheck > 0 || meetCooldown > 0 || state != State.Walk || ant.grid == null) return;
            meetCheck = 0.25f;
            Vector2 me = Pos;
            Vector2 fwd = new Vector2(Mathf.Sin(ant.heading), Mathf.Cos(ant.heading));
            float reach = cfg.meetRadius * BodyLength;
            ant.grid.Near(me.x, me.y, nearby);
            foreach (AntController o in nearby)
            {
                NpcAnt other = o.npc;
                if (other == null || other == this || other.state != State.Walk || other.meetCooldown > 0 || o.altitude > 0.3f) continue;
                Vector2 to = new Vector2(o.Position.x, o.Position.z) - me;
                float d = to.magnitude;
                if (d > reach || d < 1e-3f) continue;
                to /= d;
                Vector2 ofwd = new Vector2(Mathf.Sin(o.heading), Mathf.Cos(o.heading));
                if (Vector2.Dot(fwd, to) > 0.6f && Vector2.Dot(ofwd, -to) > 0.4f)
                {
                    Meet(other);
                    return;
                }
            }
        }

        /// <summary>Where the camera is (Game sets it every frame): an ant in the way between it and the player's
        /// ant, or right up against the lens, is not drawn, so none ever blocks the view.</summary>
        public static Vector3? CameraAt;

        static bool InTheWay(Vector3 p, Vector3 player)
        {
            if (CameraAt == null) return false;
            Vector3 cam = CameraAt.Value, toPlayer = player - cam, toAnt = p - cam;
            float dp = toPlayer.magnitude, da = toAnt.magnitude;
            if (da < 1.3f) return true;                    // right up against the lens
            if (da > dp - 0.9f || dp < 0.5f) return false; // beside the player's ant or beyond it
            // nearer than the player's ant and inside the view toward it (about 37° round it), or close to the
            // lens anywhere in view
            float cos = Vector3.Dot(toAnt, toPlayer) / (da * dp);
            return cos > 0.8f || (da < 2.6f && cos > 0.45f);
        }

        /// <summary>Advance one frame. Far NPCs keep walking but are not drawn or animated.</summary>
        public void Tick(float dt, AntController player)
        {
            age += dt;
            float far = Vector3.Distance(player.Position, ant.Position);
            bool hidden = far > cfg.drawDistance, inWay = InTheWay(ant.Position, player.Position);
            if (ant.PointsOnly)
            {
                // drawn as points: it dissolves out of the camera's way and back, and in and out at the edge of
                // sight, instead of vanishing or appearing at once
                bool gone = inWay || far > cfg.drawDistance * 0.9f;
                ant.FadePoints(gone ? 0 : 1, dt);
                hidden = hidden && ant.PointFade <= 0f;
                ant.SetVisible(!hidden);
            }
            else ant.SetVisible(!hidden && !inWay);
            ant.dormant = hidden;
            // crowd LOD: nearer ants are animated every frame and cast shadows (as points, every ant every frame:
            // a skeleton posed every few frames would make its points jump)
            ant.animInterval = ant.PointsOnly || far < cfg.lodNear ? 1 : far < cfg.lodMid ? 2 : 4;
            ant.SetShadows(far < cfg.shadowDistance);
            greetCooldown -= dt;
            meetCooldown -= dt;
            flyCooldown -= dt;
            recruit -= dt;
            if (species.canFly && TickFlight(dt)) return;

            if (state == State.Walk && greetCooldown <= 0 && Near(player, cfg.greetRadius) && player.speed < 1.2f)
            {
                if (Chance(0.5f)) Greet(player);
                else greetCooldown = 6;
            }
            if (!hidden) LookForMeeting(dt);

            Vector2 move = Vector2.zero;
            if (state == State.Greet || state == State.Meet || state == State.Pause)
            {
                timer -= dt;
                if ((state == State.Greet || state == State.Meet) && face != null)
                {
                    // turn on the spot to face the other ant
                    float a = Mathf.Atan2(face.Position.x - ant.Position.x, face.Position.z - ant.Position.z);
                    float diff = Mathf.Atan2(Mathf.Sin(a - ant.heading), Mathf.Cos(a - ant.heading));
                    ant.heading += diff * Mathf.Min(1, dt * (Colony.enabled ? 2.5f : 4f));
                    if (timer > 0.4f && ant.talkTimer < 0.3f) ant.PlayTalk();
                }
                if (timer <= 0) state = State.Walk;
            }
            else if (role == Role.Chamber && chamber != null) move = Mill(dt);
            else if (pts != null) move = Follow(dt);
            if (state == State.TakeOff) return; // started this frame; TickFlight drives it from now on
            ant.Tick(dt, move, fast);
        }

        /// <summary>Walking bouts: the pace drifts, and now and then the ant stops briefly.</summary>
        float Pace()
        {
            float p = 0.82f + 0.2f * Mathf.Sin(age * 0.63f + phaseB) + 0.1f * Mathf.Sin(age * 1.7f + phaseC);
            return Mathf.Clamp(p, 0.5f, 1.12f);
        }

        void MaybeStop(float dt, float rate)
        {
            if (!Chance(rate * dt)) return;
            state = State.Pause;
            timer = Range(cfg.shortStop.x, cfg.shortStop.y);
            if (Chance(0.35f)) ant.PlayTalk(); // antennae sweep the ground while it waits
        }

        Vector2 Follow(float dt)
        {
            // pure pursuit: aim at a point a body length or so ahead, offset into a gently weaving lane
            float look = 1.4f * BodyLength;
            Vector2 me = Pos;
            if (Colony.enabled && index < pts.Count - 1)
            {
                // points the ant has already passed (beyond the end of their segment), or has made no headway
                // towards for a while (blocked by a stone or the crowd), are given up instead of circled
                Vector2 a0 = pts[Mathf.Max(0, index - 1)], b0 = pts[index];
                float gap = (b0 - me).magnitude;
                if (gap < bestGap - 0.05f) { bestGap = gap; stall = 0; }
                else stall += dt;
                if (Vector2.Dot(me - b0, b0 - a0) > 0 || stall > 1.2f)
                {
                    index++;
                    stall = 0;
                    bestGap = float.MaxValue;
                }
            }
            while (index < pts.Count - 1 && (pts[index] - me).magnitude < look)
            {
                index++;
                if (world.HexDist(pts[index].x, pts[index].y) > -3.5f)
                {
                    // the hexagon's outer rim: turn back
                    TurnBack();
                    state = State.Pause;
                    timer = Range(cfg.pause.x, cfg.pause.y);
                    return Vector2.zero;
                }
            }
            if (index >= pts.Count - 1 && (pts[pts.Count - 1] - me).magnitude < 0.9f)
            {
                if (species.canFly && world.regions[region].scene == null && flyCooldown <= 0 && Chance(cfg.flight.chance) && ant.HasClip(AntController.Clip.TakeOff))
                {
                    StartTakeoff();
                    return Vector2.zero;
                }
                NextEdge();
                if (Chance(cfg.pauseChance))
                {
                    state = State.Pause;
                    timer = Range(cfg.pause.x, cfg.pause.y);
                }
                fast = Chance(cfg.fastChance);
                return Vector2.zero;
            }
            Vector2 a = pts[Mathf.Max(0, index - 1)], b = pts[index];
            Vector2 seg = b - a;
            float len = Mathf.Max(1e-4f, seg.magnitude);
            Vector2 side = new Vector2(-seg.y, seg.x) / len;
            float lane = baseLane + 0.16f * Mathf.Sin(age * 0.45f + phaseA);
            Vector2 target = b + side * lane * world.HalfWidth(b.x, b.y);
            // a lane point inside a wall or a stone is no target: aim at the trail itself
            if (Colony.enabled && (world.Dist(target.x, target.y) > -(ant.cfg.radius + 0.3f) || Blocked(target))) target = b;
            Vector2 d = target - me;
            MaybeStop(dt, cfg.stopRate);
            return d.sqrMagnitude < 1e-4f ? Vector2.zero : d.normalized * Pace();
        }

        Vector2 Mill(float dt)
        {
            Vector2 d = millTarget - Pos;
            if (d.magnitude < 0.45f * BodyLength)
            {
                if (Chance(cfg.leaveChamber))
                {
                    // off to forage along the trails
                    role = Role.Trail;
                    baseLane = Range(-0.28f, 0.28f);
                    RejoinPath();
                    return Vector2.zero;
                }
                state = State.Pause;
                timer = Range(0.5f, 2.2f);
                if (Chance(0.45f)) ant.PlayTalk();
                millTarget = MillPoint();
                return Vector2.zero;
            }
            fast = false;
            return d.normalized * (0.6f * Pace());
        }

        void StartTakeoff()
        {
            state = State.TakeOff;
            ant.clipOverride = AntController.Clip.TakeOff;
            ant.speed = 0;
            timer = 0.05f; // let the clip start before reading its remaining time
        }

        /// <summary>Flight states (winged species only). Returns true when it handled this frame.</summary>
        bool TickFlight(float dt)
        {
            var fl = cfg.flight;
            switch (state)
            {
                case State.TakeOff:
                    timer -= dt;
                    ant.Tick(dt, Vector2.zero, false);
                    if (timer <= 0 && ant.Remaining(AntController.Clip.TakeOff) <= 0.02f)
                    {
                        Nudge(takeoffForward);
                        // TakeOff ends with the root already lifted, and Fly keeps it there: no extra altitude jump
                        ant.altitude = 0;
                        ant.clipOverride = AntController.Clip.Fly;
                        flyTarget = PickFlightTarget();
                        flightAlt = Range(fl.altitude.x, fl.altitude.y);
                        flightTime = Range(fl.duration.x, fl.duration.y);
                        state = State.Fly;
                    }
                    return true;
                case State.Fly:
                {
                    flightTime -= dt;
                    float dx = flyTarget.x - ant.Position.x, dz = flyTarget.y - ant.Position.z;
                    float d = Mathf.Sqrt(dx * dx + dz * dz);
                    bool descending = d < 7;
                    float alt = descending ? 0 : flightAlt;
                    float speed = descending ? Mathf.Max(0.5f, Mathf.Min(fl.speed, d * 0.45f)) : fl.speed;
                    ant.FlyTick(dt, Mathf.Atan2(dx, dz), speed, alt);
                    if (descending && d < 1.2f && ant.altitude < 0.15f)
                    {
                        state = State.Land;
                        ant.clipOverride = AntController.Clip.Land;
                        ant.speed = 0;
                        ant.altitude = 0;
                        timer = 0.05f;
                    }
                    return true;
                }
                case State.Land:
                    timer -= dt;
                    ant.Tick(dt, Vector2.zero, false);
                    if (timer <= 0 && ant.Remaining(AntController.Clip.Land) <= 0.02f)
                    {
                        Nudge(landForward);
                        ant.clipOverride = null;
                        role = role == Role.Chamber ? Role.Trail : role;
                        RejoinPath();
                        state = State.Walk;
                        flyCooldown = Range(30, 70);
                    }
                    return true;
                default:
                    return false;
            }
        }

        void Nudge(float bodyLengths)
        {
            Vector3 p = ant.Position;
            float d = bodyLengths * BodyLength;
            ant.Place(p.x + Mathf.Sin(ant.heading) * d, p.z + Mathf.Cos(ant.heading) * d, ant.heading);
        }
    }
}

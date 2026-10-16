// NPC ants: other species wandering the recognized path network. They follow
// the paths edge by edge, pause now and then, turn back at the hexagon frame,
// and stop to touch antennae when the player comes close.
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { Ant } from './ant.js';
import { Rng } from './noise.js';

export class Npc {
  constructor({ field, colliders, cfg, species, rng, shared }) {
    this.field = field;
    this.cfg = cfg;
    this.rng = rng;
    this.species = species;
    const antCfg = { ...cfg.ant, ...species, crawlSpeed: rng.float(...(species.walkSpeed ?? cfg.npcs.walkSpeed)) };
    this.ant = new Ant({ field, colliders, cfg, antCfg });
    this.shared = shared;
    this.state = 'walk';
    this.timer = 0;
    this.greetCooldown = rng.float(0, 4);
    this.lane = 0;
    this.fast = false;
    this.flyCooldown = rng.float(6, 18);
  }

  get position() {
    return this.ant.position;
  }

  get radius() {
    return this.ant.radius;
  }

  async load() {
    await this.ant.load(this.species.url, this.shared);
  }

  /** Start on a random edge, at a random point along it. */
  spawn(avoid) {
    const f = this.field;
    for (let tries = 0; tries < 40; tries++) {
      const e = this.rng.pick(f.edges);
      const forward = this.rng.chance(0.5);
      const pts = forward ? e.pts : [...e.pts].reverse();
      const i = this.rng.int(2, Math.max(2, pts.length - 3));
      const [x, z] = pts[i] ?? pts[0];
      if (f.dist(x, z) > -1.2) continue;
      if (avoid && Math.hypot(x - avoid.x, z - avoid.z) < 8) continue;
      this.route = { edge: e, pts, i: Math.min(i + 1, pts.length - 1), endNode: forward ? e.target : e.source };
      this.lane = this.rng.float(-0.45, 0.45);
      const [nx, nz] = pts[this.route.i];
      this.ant.place(x, z, Math.atan2(nx - x, nz - z));
      return;
    }
  }

  _nextEdge() {
    const f = this.field;
    const node = this.route.endNode;
    const options = [];
    for (const e of f.edges) {
      if (e === this.route.edge && f.edges.length > 1) continue;
      if (e.source === node) options.push({ e, pts: e.pts, end: e.target });
      else if (e.target === node) options.push({ e, pts: [...e.pts].reverse(), end: e.source });
    }
    if (!options.length) {
      // dead end: walk back the way we came
      const pts = [...this.route.pts].reverse();
      const end = this.route.edge.source === node ? this.route.edge.target : this.route.edge.source;
      this.route = { edge: this.route.edge, pts, i: 1, endNode: end };
    } else {
      const o = this.rng.pick(options);
      this.route = { edge: o.e, pts: o.pts, i: 1, endNode: o.end };
    }
    this.lane = this.rng.float(-0.45, 0.45);
  }

  _turnBack() {
    const pts = [...this.route.pts].reverse();
    const i = Math.max(1, pts.length - 1 - this.route.i + 1);
    const end = this.route.edge.source === this.route.endNode ? this.route.edge.target : this.route.edge.source;
    this.route = { edge: this.route.edge, pts, i, endNode: end };
  }

  /** A landing spot on another stretch of path, well inside the frame. */
  _pickFlightTarget() {
    const f = this.field;
    for (let tries = 0; tries < 60; tries++) {
      const e = this.rng.pick(f.edges);
      const [x, z] = this.rng.pick(e.pts);
      if (f.dist(x, z) > -1.5 || f.hexDist(x, z) > -8) continue;
      if (Math.hypot(x - this.position.x, z - this.position.z) < 15) continue;
      return { x, z };
    }
    return { x: this.position.x, z: this.position.z };
  }

  /** After landing: continue along the nearest path, away from where we came from. */
  _rejoinPath() {
    let best = null;
    let bestD = Infinity;
    for (const e of this.field.edges) {
      e.pts.forEach(([x, z], i) => {
        const d = Math.hypot(x - this.position.x, z - this.position.z);
        if (d < bestD) {
          bestD = d;
          best = { e, i };
        }
      });
    }
    if (!best) return;
    const forward = this.rng.chance(0.5);
    const pts = forward ? best.e.pts : [...best.e.pts].reverse();
    const i = forward ? best.i : best.e.pts.length - 1 - best.i;
    this.route = { edge: best.e, pts, i: Math.min(i + 1, pts.length - 1), endNode: forward ? best.e.target : best.e.source };
  }

  _startTakeoff() {
    this.state = 'takeoff';
    this.timer = this.ant.actions.takeoff?.getClip().duration ?? 2.5;
    this.ant.setFlightClip('takeoff');
  }

  /** Flight states (winged species only). Returns true when it handled this frame. */
  _updateFlight(dt) {
    const fl = this.species.flight;
    if (this.state === 'takeoff') {
      this.timer -= dt;
      this.ant.update(dt, { x: 0, z: 0 }, false);
      if (this.timer <= 0) {
        this.ant.nudgeForward(fl.takeoffForward);
        this.ant.setFlightClip('fly');
        this.ant.altitude = this.position.y;
        this.target = this._pickFlightTarget();
        this.flightAlt = this.rng.float(...fl.altitude);
        this.flightTime = this.rng.float(...fl.duration);
        this.state = 'fly';
      }
      return true;
    }
    if (this.state === 'fly') {
      this.flightTime -= dt;
      const dx = this.target.x - this.position.x;
      const dz = this.target.z - this.position.z;
      const d = Math.hypot(dx, dz);
      if (this.flightTime <= 0 && d > 12) this.target = { x: this.target.x, z: this.target.z }; // keep heading home
      const descending = d < 7;
      const ground = this.field.height(this.target.x, this.target.z);
      const alt = descending ? ground : ground + this.flightAlt;
      const speed = descending ? Math.max(0.5, Math.min(fl.speed, d * 0.45)) : fl.speed;
      this.ant.flyUpdate(dt, Math.atan2(dx, dz), speed, alt, descending ? 0 : 1.2);
      if (descending && d < 1.2 && this.position.y - this.field.height(this.position.x, this.position.z) < 0.15) {
        this.state = 'land';
        this.timer = this.ant.actions.land?.getClip().duration ?? 2.5;
        this.ant.setFlightClip('land');
        this.ant.speed = 0;
        this.ant.altitude = 0;
      }
      return true;
    }
    if (this.state === 'land') {
      this.timer -= dt;
      this.ant.update(dt, { x: 0, z: 0 }, false);
      if (this.timer <= 0) {
        this.ant.nudgeForward(fl.landForward);
        this.ant.setFlightClip(null);
        this._rejoinPath();
        this.state = 'walk';
        this.flyCooldown = this.rng.float(18, 40);
      }
      return true;
    }
    return false;
  }

  /** True when this NPC is close enough to the player to be greeted. */
  near(player, radius) {
    return Math.hypot(player.position.x - this.position.x, player.position.z - this.position.z) < radius;
  }

  greet(player) {
    this.state = 'greet';
    this.timer = 3.2;
    this.greetCooldown = 12;
    this.face = player;
    this.ant.playTalk();
  }

  update(dt, player) {
    const n = this.cfg.npcs;
    const f = this.field;
    // Far away: keep walking the route, but skip drawing and the animation mixer.
    const far = Math.hypot(this.position.x - player.position.x, this.position.z - player.position.z) > n.drawDistance;
    this.ant.dormant = far;
    this.ant.root.visible = !far;
    this.ant.shadow.visible = !far;
    this.greetCooldown -= dt;
    this.flyCooldown -= dt;
    if (this.species.flight && this._updateFlight(dt)) return;
    let move = { x: 0, z: 0 };
    if (this.state === 'walk' && this.greetCooldown <= 0 && this.near(player, n.greetRadius) && player.speed < 1.2) {
      this.greet(player);
    }
    if (this.state === 'greet' || this.state === 'pause') {
      this.timer -= dt;
      if (this.state === 'greet' && this.face) {
        // turn on the spot to face the player
        const a = Math.atan2(this.face.position.x - this.position.x, this.face.position.z - this.position.z);
        const diff = Math.atan2(Math.sin(a - this.ant.heading), Math.cos(a - this.ant.heading));
        this.ant.heading += diff * Math.min(1, dt * 4);
        if (this.timer > 0.4 && this.ant.talkTimer < 0.3) this.ant.playTalk();
      }
      if (this.timer <= 0) this.state = 'walk';
    } else if (this.route) {
      const pts = this.route.pts;
      let [tx, tz] = pts[this.route.i];
      // walk a little off the centerline so ants don't queue single file
      const [px, pz] = pts[Math.max(0, this.route.i - 1)];
      const len = Math.hypot(tx - px, tz - pz) || 1;
      const hw = f.halfWidth(tx, tz);
      const ox = (-(tz - pz) / len) * this.lane * hw;
      const oz = ((tx - px) / len) * this.lane * hw;
      tx += ox;
      tz += oz;
      const dx = tx - this.position.x;
      const dz = tz - this.position.z;
      const d = Math.hypot(dx, dz);
      if (d < 0.8) {
        this.route.i++;
        if (this.route.i < pts.length && f.hexDist(pts[this.route.i][0], pts[this.route.i][1]) > -3.5) {
          this._turnBack();
          this.state = 'pause';
          this.timer = this.rng.float(...n.pause);
        } else if (this.route.i >= pts.length) {
          if (this.species.flight && this.flyCooldown <= 0 && this.rng.chance(this.species.flight.chance)) {
            this._startTakeoff();
            return;
          }
          this._nextEdge();
          if (this.rng.chance(n.pauseChance)) {
            this.state = 'pause';
            this.timer = this.rng.float(...n.pause);
          }
          this.fast = this.rng.chance(n.fastChance);
        }
      } else {
        move = { x: dx / d, z: dz / d };
      }
    }
    this.ant.update(dt, move, this.fast);
  }
}

/**
 * Load each species once, then spawn the configured number of NPCs.
 * @returns {Promise<Npc[]>}
 */
export async function spawnNpcs({ field, colliders, cfg, scene, player }) {
  const rng = new Rng(cfg.seed + 4242);
  const loader = new GLTFLoader();
  const shared = {};
  for (const sp of cfg.npcs.species) {
    try {
      shared[sp.id] = await loader.loadAsync(sp.url);
    } catch (err) {
      console.warn(`NPC model ${sp.url} failed to load`, err);
    }
  }
  const npcs = [];
  const pool = cfg.npcs.species.filter((sp) => shared[sp.id]);
  if (!pool.length) return npcs;
  const nests = Math.max(1, field.hexCenters?.length ?? 1);
  const count = Math.min(cfg.npcs.maxCount, cfg.npcs.count * nests);
  for (let i = 0; i < count; i++) {
    const species = pool[i % pool.length];
    const npc = new Npc({ field, colliders, cfg, species, rng, shared: shared[species.id] });
    await npc.load();
    npc.spawn(player.position);
    scene.add(npc.ant.root);
    scene.add(npc.ant.shadow);
    npcs.push(npc);
  }
  // everyone keeps clear of everyone (player included)
  const bodies = [player, ...npcs.map((n) => n.ant)];
  for (const b of bodies) b.others = bodies;
  return npcs;
}

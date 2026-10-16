// Player ant: loads the rigged GLB, blends Idle / Crawl / FastCrawl by speed,
// and moves it along the canyon floor. Collision uses the path distance field
// (the canyon walls) plus circle colliders for props inside the corridor.
import * as THREE from 'three';
import { GLTFLoader } from 'three/addons/loaders/GLTFLoader.js';
import { clone as cloneSkinned } from 'three/addons/utils/SkeletonUtils.js';
import { clamp, lerp, smoothstep } from './noise.js';

const _n = new THREE.Vector3();
const _g = { x: 0, z: 0 };
const _fwd = new THREE.Vector3();
const _right = new THREE.Vector3();
const _m = new THREE.Matrix4();
const _q = new THREE.Quaternion();

function findClip(clips, names) {
  for (const name of names) {
    const exact = clips.find((c) => c.name === name);
    if (exact) return exact;
  }
  for (const name of names) {
    const fuzzy = clips.find((c) => c.name.toLowerCase().includes(name.toLowerCase()));
    if (fuzzy) return fuzzy;
  }
  return null;
}

/**
 * Soft bioluminescence: a faint emissive tint following the shell texture plus a
 * fresnel rim that brightens the silhouette (and catches the bloom pass).
 */
function applyGlow(m, c) {
  if (m.userData.glow) return; // NPC clones share materials
  m.userData.glow = true;
  m.emissive = new THREE.Color(c.glowColor ?? 0x2f9aa0);
  m.emissiveIntensity = c.glowIntensity ?? 0.12;
  if (m.map) m.emissiveMap = m.map;
  const rimColor = new THREE.Color(c.rimColor ?? 0x9ff6ff);
  const rimStrength = c.rimStrength ?? 0.85;
  m.onBeforeCompile = (shader) => {
    shader.uniforms.uRimColor = { value: rimColor };
    shader.uniforms.uRimStrength = { value: rimStrength };
    shader.fragmentShader = shader.fragmentShader
      .replace('#include <common>', '#include <common>\nuniform vec3 uRimColor;\nuniform float uRimStrength;')
      .replace(
        '#include <emissivemap_fragment>',
        `#include <emissivemap_fragment>
        float rimF = pow(1.0 - clamp(abs(dot(normalize(normal), normalize(vViewPosition))), 0.0, 1.0), 2.4);
        totalEmissiveRadiance += uRimColor * rimF * uRimStrength;`,
      );
  };
  m.needsUpdate = true;
}

function makeShadowTexture() {
  const c = document.createElement('canvas');
  c.width = c.height = 128;
  const g = c.getContext('2d');
  const grad = g.createRadialGradient(64, 64, 4, 64, 64, 62);
  grad.addColorStop(0, 'rgba(0,0,0,0.85)');
  grad.addColorStop(0.55, 'rgba(0,0,0,0.45)');
  grad.addColorStop(1, 'rgba(0,0,0,0)');
  g.fillStyle = grad;
  g.fillRect(0, 0, 128, 128);
  return new THREE.CanvasTexture(c);
}

export class Ant {
  /**
   * @param {object} deps { field, colliders, cfg, antCfg } — field is a PathField; antCfg
   *   overrides cfg.ant (used for NPC species with their own model and gait numbers)
   */
  constructor({ field, colliders, cfg, antCfg }) {
    this.field = field;
    this.colliders = colliders;
    this.cfg = antCfg ?? cfg.ant;
    this.others = []; // moving bodies to keep clear of: { position, radius }
    this.root = new THREE.Group(); // world transform (position + orientation)
    this.root.name = 'PlayerAnt';
    this.position = this.root.position;
    this.heading = 0; // radians, 0 = +Z
    this.speed = 0;
    this.up = new THREE.Vector3(0, 1, 0);
    this.mixer = null;
    this.actions = {};
    this.talkTimer = 0;
    this.loaded = false;
    this.placeholder = false;
    this.accel = 0;
    this.turnVel = 0;
    this.sprinting = false;
    this.gait = 'idle';
    this.weights = { idle: 1, crawl: 0, fast: 0, talk: 0 };
    this.leanRoll = 0;
    this.leanPitch = 0;
    this.altitude = 0; // flight height above the canyon floor
    this.clipOverride = null; // 'takeoff' | 'fly' | 'land' while flying
    // pivot between the floor-aligned root and the model: body lean happens here
    this.lean = new THREE.Group();
    this.root.add(this.lean);

    const shadow = new THREE.Mesh(
      new THREE.PlaneGeometry(1, 1),
      new THREE.MeshBasicMaterial({ map: makeShadowTexture(), transparent: true, depthWrite: false, opacity: 0.6 }),
    );
    shadow.rotation.x = -Math.PI / 2;
    shadow.renderOrder = 20;
    this.shadow = shadow;
  }

  /** Collision radius (used when other ants keep clear of this one). */
  get radius() {
    return this.cfg.radius;
  }

  /** Load the GLB (or clone a shared, already-loaded one); placeholder body if missing. */
  async load(url, shared) {
    try {
      const gltf = shared ?? (await new GLTFLoader().loadAsync(url));
      this._setupModel(shared ? cloneSkinned(gltf.scene) : gltf.scene, gltf.animations);
    } catch (err) {
      console.warn('Ant GLB failed to load, using placeholder:', err);
      this._setupPlaceholder();
    }
    this.loaded = true;
  }

  _setupModel(model, clips) {
    const c = this.cfg;
    // Rotate so the head points along +Z, then scale to the configured body length.
    const forwardFix = { '+Z': 0, '-Z': Math.PI, '+X': -Math.PI / 2, '-X': Math.PI / 2 }[c.forwardAxis ?? '+Z'] ?? 0;
    model.rotation.y = forwardFix;
    model.updateMatrixWorld(true);
    const box = new THREE.Box3().setFromObject(model);
    const size = box.getSize(new THREE.Vector3());
    // Scale by the measured mandible-to-gaster length when known (antennae and
    // wings would otherwise shrink some species), else by the bounding box.
    const length = c.glbBodyLength ?? (c.forwardAxis === '+X' || c.forwardAxis === '-X' ? size.x : size.z);
    const s = c.bodyLength / Math.max(1e-6, length || Math.max(size.x, size.z));
    const holder = new THREE.Group();
    holder.add(model);
    holder.scale.setScalar(s);
    holder.updateMatrixWorld(true);
    const box2 = new THREE.Box3().setFromObject(holder);
    const center = box2.getCenter(new THREE.Vector3());
    const cz = c.glbBodyCenterZ !== undefined ? c.glbBodyCenterZ * s : center.z;
    holder.position.set(-center.x, -box2.min.y + c.groundOffset, -cz);
    this.lean.add(holder);
    this.model = holder;

    model.traverse((o) => {
      if (o.isMesh) {
        o.castShadow = false;
        o.receiveShadow = false;
        o.frustumCulled = false; // skinned bounds are unreliable
        const mats = Array.isArray(o.material) ? o.material : [o.material];
        for (const m of mats) {
          if (m && 'envMapIntensity' in m) m.envMapIntensity = 1.2;
          if (m?.isMeshStandardMaterial && !m.transparent) applyGlow(m, c);
          // translucent wings: veins draw after the membrane
          if (m?.transparent) o.renderOrder = /vein/i.test(m.name) ? 3 : 2;
        }
      }
    });

    this.mixer = new THREE.AnimationMixer(model);
    const idle = findClip(clips, ['Idle']);
    const crawl = findClip(clips, ['Crawl_InPlace', 'Crawl']);
    const fast = findClip(clips, ['FastCrawl_InPlace', 'FastCrawl', 'Run']);
    const talk = findClip(clips, ['AntennaTalk', 'Talk']);
    const takeoff = clips.find((cl) => cl.name === 'TakeOff') ?? null;
    const fly = clips.find((cl) => cl.name === 'Fly') ?? null;
    const land = clips.find((cl) => cl.name === 'Land') ?? null;
    for (const [key, clip] of Object.entries({ idle, crawl, fast, talk, takeoff, fly, land })) {
      if (!clip) continue;
      const action = this.mixer.clipAction(clip);
      if (key === 'takeoff' || key === 'land') {
        action.setLoop(THREE.LoopOnce, 1);
        action.clampWhenFinished = true;
      }
      action.play();
      action.setEffectiveWeight(key === 'idle' ? 1 : 0);
      this.actions[key] = action;
    }
    this.clipNames = clips.map((cl) => cl.name);
  }

  _setupPlaceholder() {
    this.placeholder = true;
    const mat = new THREE.MeshStandardMaterial({ color: 0x5fc6c0, roughness: 0.35, metalness: 0.0 });
    const body = new THREE.Group();
    const parts = [
      [0.16, 0.0, 0.32],
      [0.12, 0.0, 0.05],
      [0.2, 0.0, -0.25],
    ];
    for (const [r, , z] of parts) {
      const m = new THREE.Mesh(new THREE.SphereGeometry(r, 20, 14), mat);
      m.position.set(0, 0.22, z);
      body.add(m);
    }
    this.lean.add(body);
    this.model = body;
  }

  /** Face the ant along a heading (radians, 0 = +Z) at a floor position. */
  place(x, z, heading) {
    this.position.set(x, this.field.height(x, z), z);
    this.heading = heading;
    this.speed = 0;
    this._orient(1);
  }

  playTalk() {
    if (this.actions.talk) this.talkTimer = 2.4;
  }

  /**
   * @param {number} dt seconds
   * @param {{x:number,z:number}} move desired world-space direction (length 0..1)
   * @param {boolean} fast FastCrawl modifier
   */
  update(dt, move, fast) {
    const c = this.cfg;
    const mag = Math.min(1, Math.hypot(move.x, move.z));
    const wantsSprint = fast && mag > 0.05;
    let targetSpeed = 0;
    let turn = 0;
    if (mag > 0.05) {
      const target = Math.atan2(move.x, move.z);
      const diff = Math.atan2(Math.sin(target - this.heading), Math.cos(target - this.heading));
      // Pivot quickly from rest, steer in smooth arcs at speed.
      const rate = lerp(c.turnRate, c.sprintTurnRate, clamp(this.speed / c.fastSpeed, 0, 1));
      turn = clamp(diff * c.turnGain * dt, -rate * dt, rate * dt);
      if (Math.abs(turn) > Math.abs(diff)) turn = diff;
      this.heading += turn;
      // Only sharp direction changes cost speed; gentle steering keeps momentum.
      const align = 0.3 + 0.7 * smoothstep(-0.35, 0.75, Math.cos(diff));
      targetSpeed = (wantsSprint ? c.fastSpeed : c.crawlSpeed) * mag * align;
      this.talkTimer = 0;
    }
    const prev = this.speed;
    const k = targetSpeed > this.speed ? (wantsSprint ? c.sprintAccel : c.accel) : c.decel;
    this.speed += (targetSpeed - this.speed) * (1 - Math.exp(-k * dt));
    if (targetSpeed === 0 && this.speed < 0.004) this.speed = 0;
    const inv = 1 / Math.max(dt, 1e-4);
    this.accel += ((this.speed - prev) * inv - this.accel) * (1 - Math.exp(-10 * dt));
    this.turnVel += (turn * inv - this.turnVel) * (1 - Math.exp(-12 * dt));
    this.sprinting = wantsSprint && this.speed > c.crawlSpeed * 1.15;

    const p = this.position;
    p.x += Math.sin(this.heading) * this.speed * dt;
    p.z += Math.cos(this.heading) * this.speed * dt;
    this._collide();
    p.y = this.field.height(p.x, p.z) + this.altitude;
    this.shadow.material.opacity = 0.6;
    this._orient(1 - Math.exp(-10 * dt));
    this._lean(dt);
    this._animate(dt);
  }

  /** Bank into turns and dip the head when accelerating; purely visual. */
  _lean(dt) {
    const c = this.cfg;
    const roll = clamp(-this.turnVel * this.speed * c.leanRoll, -0.2, 0.2);
    const pitch = clamp(this.accel * c.leanPitch, -0.12, 0.14);
    const k = 1 - Math.exp(-9 * dt);
    this.leanRoll += (roll - this.leanRoll) * k;
    this.leanPitch += (pitch - this.leanPitch) * k;
    this.lean.rotation.set(this.leanPitch, 0, this.leanRoll);
  }

  _collide() {
    const p = this.position;
    const r = this.cfg.radius;
    for (let iter = 0; iter < 3; iter++) {
      // canyon walls (distance field)
      const d = this.field.dist(p.x, p.z);
      if (d > -r) {
        this.field.gradDist(p.x, p.z, _g);
        const len = Math.hypot(_g.x, _g.z) || 1;
        p.x -= (_g.x / len) * (d + r);
        p.z -= (_g.z / len) * (d + r);
      }
      // other ants
      for (const o of this.others) {
        if (o === this) continue;
        const dx = p.x - o.position.x;
        const dz = p.z - o.position.z;
        const min = (o.radius ?? r) + r;
        const d2 = dx * dx + dz * dz;
        if (d2 < min * min && d2 > 1e-8) {
          const dist = Math.sqrt(d2);
          p.x += (dx / dist) * (min - dist) * 0.5;
          p.z += (dz / dist) * (min - dist) * 0.5;
        }
      }
      // props on the canyon floor
      for (const col of this.colliders) {
        const dx = p.x - col.x;
        const dz = p.z - col.z;
        const min = col.r + r;
        const d2 = dx * dx + dz * dz;
        if (d2 < min * min && d2 > 1e-8) {
          const dist = Math.sqrt(d2);
          p.x += (dx / dist) * (min - dist);
          p.z += (dz / dist) * (min - dist);
        }
      }
    }
  }

  _orient(t, keepUp = false) {
    const p = this.position;
    if (!keepUp) {
      this.field.normal(p.x, p.z, _n);
      this.up.lerp(_n, t).normalize();
    }
    _fwd.set(Math.sin(this.heading), 0, Math.cos(this.heading));
    _fwd.addScaledVector(this.up, -_fwd.dot(this.up)).normalize();
    _right.crossVectors(this.up, _fwd).normalize();
    _m.makeBasis(_right, this.up, _fwd);
    _q.setFromRotationMatrix(_m);
    this.root.quaternion.copy(_q);
    // blob shadow hugs the floor
    this.shadow.position.set(p.x, p.y + 0.03, p.z);
    this.shadow.quaternion.copy(_q);
    this.shadow.rotateX(-Math.PI / 2);
    this.shadow.scale.set(this.cfg.bodyLength * 0.75, this.cfg.bodyLength * 1.15, 1);
  }

  _animate(dt) {
    if (!this.mixer || this.dormant) return;
    const c = this.cfg;
    const s = this.speed;
    this.talkTimer = Math.max(0, this.talkTimer - dt);
    // Gait with hysteresis so it never flickers between two clips.
    let want;
    if (this.clipOverride && this.actions[this.clipOverride]) want = this.clipOverride;
    else if (s < c.idleBelow) want = this.talkTimer > 0 && this.actions.talk ? 'talk' : 'idle';
    else if (s > c.fastAbove || (this.gait === 'fast' && s > c.fastAbove * 0.8)) want = 'fast';
    else want = 'crawl';
    if (!this.actions[want]) want = want === 'fast' ? 'crawl' : 'idle';
    if (want !== this.gait) {
      const from = this.actions[this.gait];
      const to = this.actions[want];
      // keep the tripod gait's phase when switching between the two walking clips
      if (from && to && (want === 'fast' || want === 'crawl') && (this.gait === 'fast' || this.gait === 'crawl')) {
        to.time = (from.time / from.getClip().duration) * to.getClip().duration;
      }
      this.gait = want;
    }
    const k = Math.min(1, dt / c.gaitFade);
    for (const [key, action] of Object.entries(this.actions)) {
      const target = key === this.gait ? 1 : 0;
      this.weights[key] = (this.weights[key] ?? 0) + (target - (this.weights[key] ?? 0)) * k;
      action.setEffectiveWeight(this.weights[key]);
    }
    // Leg cadence follows ground speed exactly, so the feet do not skate.
    if (this.actions.crawl) this.actions.crawl.timeScale = clamp(s / c.crawlCycleSpeed, 0.8, 9);
    if (this.actions.fast) this.actions.fast.timeScale = clamp(s / c.fastCycleSpeed, 0.8, 8);
    if (this.actions.fly) this.actions.fly.timeScale = 1.6; // quicker beats read better than the authored 24 fps
    this.mixer.update(dt);
  }

  /** Play a one-shot or looping flight clip from its start ('takeoff' | 'fly' | 'land'), or clear it. */
  setFlightClip(key) {
    this.clipOverride = key;
    const a = key ? this.actions[key] : null;
    if (a) {
      a.reset();
      a.play();
    }
  }

  /** Slide the body forward along the heading (compensates root motion baked into flight clips). */
  nudgeForward(dist) {
    this.position.x += Math.sin(this.heading) * dist;
    this.position.z += Math.cos(this.heading) * dist;
  }

  /**
   * Airborne step: steer toward a heading, fly level at `altitude` above the canyon
   * floor (never into the banks), stay inside the frame; walls do not apply.
   */
  flyUpdate(dt, targetHeading, speed, altitude, minClear = 1.2) {
    const diff = Math.atan2(Math.sin(targetHeading - this.heading), Math.cos(targetHeading - this.heading));
    const turn = clamp(diff, -2.2 * dt, 2.2 * dt);
    this.heading += turn;
    this.turnVel += (turn / Math.max(dt, 1e-4) - this.turnVel) * (1 - Math.exp(-6 * dt));
    this.speed += (speed - this.speed) * (1 - Math.exp(-2 * dt));
    const p = this.position;
    p.x += Math.sin(this.heading) * this.speed * dt;
    p.z += Math.cos(this.heading) * this.speed * dt;
    const hd = this.field.hexDist(p.x, p.z);
    if (hd > -4) {
      const len = Math.hypot(p.x, p.z) || 1;
      p.x -= (p.x / len) * (hd + 4);
      p.z -= (p.z / len) * (hd + 4);
    }
    this.altitude += (altitude - this.altitude) * (1 - Math.exp(-1.2 * dt));
    const ground = this.field.height(p.x, p.z);
    p.y = Math.max(ground + minClear, this.altitude);
    // level flight with a bank into turns
    this.up.set(0, 1, 0);
    this._orient(1, true);
    this._lean(dt);
    this.lean.rotation.z = clamp(-this.turnVel * 0.25, -0.45, 0.45);
    this.shadow.position.y = ground + 0.03;
    this.shadow.material.opacity = 0.6 * Math.max(0.1, 1 - (p.y - ground) / 12);
    this._animate(dt);
  }
}

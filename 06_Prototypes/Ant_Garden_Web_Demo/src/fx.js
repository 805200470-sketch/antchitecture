// Small motion effects: teleport bursts, spore puffs when sprinting, and a gentle
// sway / glow "breathing" on nearby plants. All splat-based so they sort with
// the scene; only instances near the camera are animated.
import * as THREE from 'three';
import { SplatMesh } from '@sparkjsdev/spark';
import { SplatWriter } from './splat-assets.js';
import { Rng } from './noise.js';
import { PALETTE as P } from './config.js';

const TAU = Math.PI * 2;

function makeBurstSplats(seed) {
  const rng = new Rng(seed);
  const W = new SplatWriter(1400);
  for (let i = 0; i < 600; i++) {
    // shell of glowing motes
    const u = rng.float(-1, 1);
    const a = rng.float(0, TAU);
    const r = Math.sqrt(1 - u * u);
    const rad = rng.float(0.6, 1.0);
    const c = rng.next() < 0.7 ? P.glowCool : P.pearl;
    W.blob(Math.cos(a) * r * rad, 0.5 + u * rad * 0.8, Math.sin(a) * r * rad, rng.float(0.012, 0.03), 0.95, c[0], c[1], c[2]);
  }
  for (let i = 0; i < 260; i++) {
    // ground ring
    const a = (i / 260) * TAU;
    W.blob(Math.cos(a) * 1.1, 0.04, Math.sin(a) * 1.1, rng.float(0.02, 0.04), 0.95, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  }
  W.blob(0, 0.5, 0, 0.55, 0.18, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  return W.splats;
}

function makePuffSplats(seed) {
  const rng = new Rng(seed);
  const W = new SplatWriter(200);
  for (let i = 0; i < 70; i++) {
    const a = rng.float(0, TAU);
    const r = Math.sqrt(rng.next()) * 0.35;
    W.blob(Math.cos(a) * r, rng.float(0.02, 0.25), Math.sin(a) * r, rng.float(0.01, 0.025), 0.7,
      P.frost[0] * 0.8, P.frost[1] * 0.85, P.frost[2] * 0.9);
  }
  return W.splats;
}

export class Fx {
  constructor(scene, world, cfg) {
    this.scene = scene;
    this.world = world;
    this.cfg = cfg;
    this.live = [];
    this.burstSplats = makeBurstSplats(cfg.seed + 501);
    this.puffSplats = makePuffSplats(cfg.seed + 502);
    this.puffTimer = 0;
    this.time = 0;
    // remember each plant's rest pose for sway
    for (const inst of world.instances) {
      if (inst.kind === 'curl' || inst.kind === 'spores' || inst.kind === 'fan') {
        inst.rest = { x: inst.mesh.rotation.x, z: inst.mesh.rotation.z };
        inst.phase = (inst.x * 0.37 + inst.z * 0.61) % TAU;
      }
    }
  }

  _spawn(splats, x, y, z, life, scale0, scale1, opacity0) {
    const mesh = new SplatMesh({ packedSplats: splats });
    mesh.position.set(x, y, z);
    mesh.scale.setScalar(scale0);
    mesh.opacity = opacity0;
    this.scene.add(mesh);
    this.live.push({ mesh, age: 0, life, scale0, scale1, opacity0 });
  }

  /** Bright ring + rising motes where the ant leaves and arrives. */
  burst(x, y, z) {
    this._spawn(this.burstSplats, x, y, z, 0.9, 0.35, 1.9, 1);
  }

  update(dt, camera, ant) {
    this.time += dt;
    // transient effects
    for (let i = this.live.length - 1; i >= 0; i--) {
      const fx = this.live[i];
      fx.age += dt;
      const t = Math.min(1, fx.age / fx.life);
      const e = 1 - (1 - t) * (1 - t);
      fx.mesh.scale.setScalar(fx.scale0 + (fx.scale1 - fx.scale0) * e);
      fx.mesh.opacity = fx.opacity0 * (1 - t);
      if (t >= 1) {
        this.scene.remove(fx.mesh);
        fx.mesh.dispose?.();
        this.live.splice(i, 1);
      }
    }
    // spore puffs kicked up while sprinting
    if (ant.sprinting && ant.speed > 0.5) {
      this.puffTimer -= dt;
      if (this.puffTimer <= 0 && this.live.length < 24) {
        this.puffTimer = 0.11;
        const back = 0.35;
        const px = ant.position.x - Math.sin(ant.heading) * back + (Math.random() - 0.5) * 0.3;
        const pz = ant.position.z - Math.cos(ant.heading) * back + (Math.random() - 0.5) * 0.3;
        this._spawn(this.puffSplats, px, ant.position.y, pz, 0.7, 0.6, 1.6, 0.85);
      }
    }
    // gentle sway + glow breathing on plants near the camera
    const cx = camera.position.x;
    const cz = camera.position.z;
    const near = this.cfg.fx.swayRadius;
    const t = this.time;
    for (const inst of this.world.instances) {
      if (!inst.rest || !inst.mesh.visible) continue;
      if (Math.abs(inst.x - cx) > near || Math.abs(inst.z - cz) > near) continue;
      const amp = inst.kind === 'fan' ? 0.012 : 0.035;
      inst.mesh.rotation.x = inst.rest.x + Math.sin(t * 0.9 + inst.phase) * amp;
      inst.mesh.rotation.z = inst.rest.z + Math.sin(t * 0.7 + inst.phase * 1.7) * amp;
      if (inst.kind === 'spores') {
        const b = 1 + 0.12 * Math.sin(t * 1.8 + inst.phase * 3);
        inst.mesh.recolor.setRGB(inst.tint[0] * b, inst.tint[1] * b, inst.tint[2] * b);
      }
    }
  }
}

export { THREE };

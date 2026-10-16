// Builds one hexagonal module of the canyon from the path field. Generic asset
// variants are generated once, then copied, rotated, scaled and tinted along
// the corridor. Ground, banks, mist and the hexagon's soil cross-section are
// generated per chunk / per side so float16 splat centres stay precise.
import * as THREE from 'three';
import { SplatMesh, PackedSplats, SplatEdit, SplatEditSdf, SplatEditRgbaBlendMode, SplatEditSdfType, generators } from '@sparkjsdev/spark';
import { GENERATORS, SplatWriter, mixC, towardC, scaleC, lit, C } from './splat-assets.js';
import { Rng, clamp, smoothstep } from './noise.js';
import { PALETTE as P } from './config.js';
import { nextFrame } from './yield.js';

const TAU = Math.PI * 2;
const KIND_LABEL = { trunk: '孔洞巨树', mound: '苔藓丘', fan: '扇菌', spores: '发光孢子', curl: '霜蕨', tuft: '地面苔藓' };

function kindSeed(kind) {
  let h = 0;
  for (const ch of kind) h = (h * 31 + ch.charCodeAt(0)) | 0;
  return h & 0xffff;
}

/** Uniform-grid spatial hash for placement spacing checks. */
class SpatialHash {
  constructor(cell) {
    this.cell = cell;
    this.map = new Map();
  }
  key(ix, iz) {
    return (ix * 73856093) ^ (iz * 19349663);
  }
  add(item) {
    const k = this.key(Math.floor(item.x / this.cell), Math.floor(item.z / this.cell));
    let list = this.map.get(k);
    if (!list) this.map.set(k, (list = []));
    list.push(item);
  }
  *near(x, z, radius) {
    const r = Math.ceil(radius / this.cell);
    const ix = Math.floor(x / this.cell);
    const iz = Math.floor(z / this.cell);
    for (let dz = -r; dz <= r; dz++) {
      for (let dx = -r; dx <= r; dx++) {
        const list = this.map.get(this.key(ix + dx, iz + dz));
        if (list) yield* list;
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Ground: canyon floor → mossy bank → plateau, clipped to the hexagon
// ---------------------------------------------------------------------------
/** Mirrored-repeat sample of a painted texture (ImageData) → sets C to its colour. */
function sampleTex(tex, u, v) {
  const fu = u - Math.floor(u);
  const fv = v - Math.floor(v);
  const mu = Math.floor(u) & 1 ? 1 - fu : fu; // mirror every other tile: no visible seams
  const mv = Math.floor(v) & 1 ? 1 - fv : fv;
  const x = Math.min(tex.width - 1, (mu * tex.width) | 0);
  const y = Math.min(tex.height - 1, (mv * tex.height) | 0);
  const i = (y * tex.width + x) * 4;
  C[0] = tex.data[i] / 255;
  C[1] = tex.data[i + 1] / 255;
  C[2] = tex.data[i + 2] / 255;
}

function groundChunk(field, cfg, ox, oz, size, density, seed, textures) {
  const g = cfg.ground;
  const rng = new Rng(seed);
  const noise = field.noise;
  const W = new SplatWriter(40000);
  const sq = Math.sqrt(density);
  const half = size / 2;
  const n = new THREE.Vector3();
  const base = Math.min(g.spacingFloor, g.spacingBank, g.spacingPlateau) / sq;

  for (let lz = -half; lz < half; lz += base) {
    for (let lx = -half; lx < half; lx += base) {
      const x = ox + lx + (rng.next() - 0.5) * base * 0.9;
      const z = oz + lz + (rng.next() - 0.5) * base * 0.9;
      if (field.hexDist(x, z) > 0) continue;
      const d = field.pathDist(x, z);
      const zone = d < -0.4 ? 0 : d < g.bankWidth * 1.25 ? 1 : 2; // floor, bank, plateau
      const sp = (zone === 0 ? g.spacingFloor : zone === 1 ? g.spacingBank : g.spacingPlateau) / sq;
      if (rng.next() > (base / sp) ** 2) continue;
      field.normal(x, z, n);

      const k = 0.5 + 0.5 * noise.fbm2(x * 0.35, z * 0.35, 3);
      let frostP = 0.03;
      if (zone === 0) {
        const hw = field.halfWidth(x, z);
        const trail = hw > 0 ? 1 - smoothstep(0.12 * hw, 0.5 * hw, field.centerDist(x, z)) : 0;
        mixC(P.soil, P.moss, smoothstep(0.25, 0.85, k));
        towardC(P.trail, trail * 0.5);
        towardC(P.mossLight, smoothstep(-2, -0.4, d) * 0.35 * k);
        frostP = 0.01 + 0.05 * smoothstep(0.3, 0.85, noise.noise2(x * 4.3, z * 4.3)) * (1 - trail);
      } else if (zone === 1) {
        // bank: fibrous moss with dark root lines and frosted ledges
        const fib = noise.noise2(x * 2.2 + z * 0.4, field.height(x, z) * 1.7);
        mixC(P.moss, P.mossLight, smoothstep(0.3, 0.9, k));
        towardC(P.barkDeep, smoothstep(0.35, 0.75, fib) * 0.45);
        const root = Math.abs(noise.noise2(x * 0.9 + 40, z * 0.9));
        if (root < 0.045) mixC(P.bark, P.barkLight, 0.4);
        frostP = 0.05 + 0.18 * smoothstep(0.55, 0.95, n.y);
      } else {
        mixC(P.mossDark, P.moss, k);
        towardC(P.haze, 0.08);
        frostP = 0.06 * smoothstep(0.3, 0.8, noise.noise2(x * 1.3, z * 1.3));
      }
      scaleC(lit(n.x, n.y, n.z, zone === 2 ? 0.95 : 1.1) * (0.8 + 0.4 * rng.next()));
      // Painted terrain textures: top-down on the floor/plateau, side-projected on the banks.
      if (textures) {
        const pr = C[0];
        const pg = C[1];
        const pb = C[2];
        const T = cfg.ground.textureTile;
        const hgt = field.height(x, z);
        if (zone === 1 && textures.bank) {
          if (Math.abs(n.x) > Math.abs(n.z)) sampleTex(textures.bank, z / T, -hgt / T);
          else sampleTex(textures.bank, x / T, -hgt / T);
        } else if (textures.floor) {
          sampleTex(textures.floor, x / T, z / T);
        }
        const k2 = cfg.ground.textureMix * (zone === 2 ? 0.85 : 1);
        const sh = 0.62 + 0.38 * lit(n.x, n.y, n.z, 1);
        C[0] = pr + (C[0] * sh * cfg.ground.textureGain - pr) * k2;
        C[1] = pg + (C[1] * sh * cfg.ground.textureGain - pg) * k2;
        C[2] = pb + (C[2] * sh * cfg.ground.textureGain - pb) * k2;
      }

      // Steep bank: add splats down the slope so the wall stays covered.
      const extra = n.y < 0.8 ? Math.min(3, Math.ceil(1 / Math.max(n.y, 0.25)) - 1) : 0;
      const gl = Math.hypot(n.x, n.z) || 1;
      for (let s = 0; s <= extra; s++) {
        const off = (s / (extra + 1)) * sp * 1.1;
        const xx = x + (n.x / gl) * off;
        const zz = z + (n.z / gl) * off;
        const hh = field.height(xx, zz) + 0.04 * noise.noise2(xx * 3.1, zz * 3.1);
        W.disk(xx - ox, hh, zz - oz, n.x, n.y, n.z, sp * 0.62, sp * 0.32, 1, C[0], C[1], C[2]);
      }
      const h = field.height(x, z);
      if (rng.next() < frostP) {
        const fl = 0.3 + 0.22 * rng.next();
        W.blob(x - ox, h + rng.float(0.02, 0.07), z - oz, rng.float(0.012, 0.024), 0.9,
          P.frost[0] * fl, P.frost[1] * fl, P.frost[2] * fl);
      }
      // moss tufts: short upright strands that catch the light
      if (zone < 2 && rng.next() < 0.02) {
        const tl = rng.float(0.025, 0.06);
        mixC(P.mossDark, P.moss, rng.next());
        W.stroke(x - ox + n.x * tl * 0.5, h + n.y * tl * 0.5, z - oz + n.z * tl * 0.5, n.x, n.y, n.z, tl * 0.5, 0.012, 0.9,
          C[0], C[1], C[2]);
      }
    }
  }

  // Tiny glowing sprouts at the foot of the banks.
  const tries = Math.round(size * size * g.sproutDensity * density);
  for (let i = 0; i < tries; i++) {
    const x = ox + rng.float(-half, half);
    const z = oz + rng.float(-half, half);
    if (field.hexDist(x, z) > -0.5) continue;
    const d = field.pathDist(x, z);
    if (d < -3 || d > 2.5 || rng.next() > smoothstep(-3, -0.6, d)) continue;
    const h = field.height(x, z);
    const H = rng.float(0.08, 0.45);
    const lean = rng.float(-0.06, 0.06);
    for (let t = 0; t < H; t += 0.022) {
      W.stroke(x - ox + lean * t, h + t, z - oz, lean, 1, 0, 0.018, 0.007, 0.7, P.stalk[0] * 0.8, P.stalk[1] * 0.8, P.stalk[2] * 0.8);
    }
    const br = rng.float(0.022, 0.055);
    W.blob(x - ox + lean * H, h + H + br, z - oz, br, 1, P.pearl[0], P.pearl[1], P.pearl[2]);
    W.blob(x - ox + lean * H, h + H + br, z - oz, br * 3, 0.1, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  }

  // Mist veils above the canyon and light shafts from the canopy.
  for (let i = 0; i < g.mistPerChunk; i++) {
    const x = ox + rng.float(-half, half);
    const z = oz + rng.float(-half, half);
    if (field.hexDist(x, z) > -3 || field.pathDist(x, z) > 1) continue;
    const h = field.height(x, z);
    const s = rng.float(2.6, 5.2);
    W.ellipsoid(x - ox, h + rng.float(3.5, 9), z - oz, s, rng.float(0.8, 1.8), s * rng.float(0.7, 1.3),
      rng.float(0, TAU), 0, rng.float(0.016, 0.028), P.mist[0], P.mist[1], P.mist[2]);
  }
  if (rng.next() < g.shaftChance) {
    for (let t2 = 0; t2 < 8; t2++) {
      const x = ox + rng.float(-half, half);
      const z = oz + rng.float(-half, half);
      if (field.pathDist(x, z) > -1 || field.hexDist(x, z) > -4) continue;
      const tilt = rng.float(0.12, 0.25);
      const yaw = rng.float(0, TAU);
      const w = rng.float(0.7, 1.4);
      for (let k = 0; k < 7; k++) {
        const y = 4 + k * 6;
        W.ellipsoid(x - ox + Math.sin(tilt) * y * Math.cos(yaw), y, z - oz + Math.sin(tilt) * y * Math.sin(yaw),
          w, 3.6, w, yaw, tilt, 0.026 + 0.012 * (k / 7), P.shaft[0], P.shaft[1], P.shaft[2]);
      }
      break;
    }
  }
  return { splats: W.splats, count: W.count };
}

// ---------------------------------------------------------------------------
// Hexagon frame: soil cross-section on each side and a thin glowing rim
// ---------------------------------------------------------------------------
function hexSide(field, cfg, edge, density, seed) {
  const rng = new Rng(seed);
  const noise = field.noise;
  const v0 = [edge.x0, edge.z0];
  const v1 = [edge.x1, edge.z1];
  const mx = (v0[0] + v1[0]) / 2;
  const mz = (v0[1] + v1[1]) / 2;
  const len = Math.hypot(v1[0] - v0[0], v1[1] - v0[1]);
  const tx = (v1[0] - v0[0]) / len;
  const tz = (v1[1] - v0[1]) / len;
  const nrm = Math.hypot(mx - edge.cx, mz - edge.cz);
  const nx = (mx - edge.cx) / nrm; // outward, away from this module's centre
  const nz = (mz - edge.cz) / nrm;
  const W = new SplatWriter(60000);
  const sq = Math.sqrt(density);
  const depth = cfg.frame.sideDepth;
  const inset = 0.06;
  for (let s = 0; s <= len; s += 0.11 / sq) {
    const px = v0[0] + tx * s - nx * inset;
    const pz = v0[1] + tz * s - nz * inset;
    const top = field.height(px, pz);
    // rim: a thin line of light marking the frame
    W.blob(px - mx, top + 0.03, pz - mz, rng.float(0.02, 0.035), 0.95, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
    for (let y = top; y > -depth; ) {
      const dy = top - y;
      const sp = (0.11 + 0.03 * dy) / sq;
      const band = noise.noise2(s * 0.05, y * 0.55);
      mixC(P.soil, P.barkDeep, 0.5 + 0.5 * band);
      if (dy < 0.6) mixC(P.moss, P.mossLight, rng.next() * 0.6); // living moss skin on top
      const root = Math.abs(noise.noise2(s * 0.35 + 7, y * 0.35));
      if (root < 0.04) mixC(P.bark, P.barkLight, 0.5);
      scaleC((0.55 + 0.45 * smoothstep(-depth, top, y)) * (0.85 + 0.3 * rng.next()));
      const jx = (rng.next() - 0.5) * sp * 0.6;
      W.disk(px + tx * jx - mx, y, pz + tz * jx - mz, nx, 0, nz, sp * 0.62, sp * 0.25, 1, C[0], C[1], C[2]);
      // glowing mycelium threads in the soil
      if (root > 0.04 && root < 0.055 && dy > 0.8) {
        W.blob(px - mx + nx * 0.03, y, pz - mz + nz * 0.03, 0.025, 0.9, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
      }
      y -= sp * 0.85;
    }
  }
  return { splats: W.splats, count: W.count, x: mx, z: mz };
}

function storySlot(seed) {
  const rng = new Rng(seed);
  const W = new SplatWriter(800);
  for (let i = 0; i < 90; i++) {
    const a = (i / 90) * TAU;
    const r = 1.0 + rng.float(-0.05, 0.05);
    W.blob(Math.cos(a) * r, 0.05, Math.sin(a) * r, rng.float(0.03, 0.05), 0.95, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  }
  W.blob(0, 0.9, 0, 0.12, 1, P.pearl[0], P.pearl[1], P.pearl[2]);
  W.blob(0, 0.9, 0, 0.32, 0.4, P.glowWarm[0], P.glowWarm[1], P.glowWarm[2]);
  W.blob(0, 0.9, 0, 0.7, 0.05, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  W.ellipsoid(0, 0.45, 0, 0.12, 0.45, 0.12, 0, 0, 0.12, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  return W.splats;
}

/** A still, dark pond with soft ripples, glints and glowing sprouts on its rim. */
function pondSplats(seed, radius) {
  const rng = new Rng(seed);
  const W = new SplatWriter(12000);
  const sp = 0.085;
  const wob = rng.float(0, TAU);
  for (let z = -radius; z <= radius; z += sp) {
    for (let x = -radius; x <= radius; x += sp) {
      const r = Math.hypot(x, z);
      const edge = radius * (1 + 0.12 * Math.sin(Math.atan2(z, x) * 3 + wob));
      if (r > edge) continue;
      const t = r / edge;
      const ripple = 0.5 + 0.5 * Math.sin(r * 8 - wob);
      W.disk(x + rng.float(-0.03, 0.03), 0.015, z + rng.float(-0.03, 0.03), 0, 1, 0, sp * 0.72, sp * 0.1, 0.98,
        0.025 + 0.035 * ripple * t, 0.07 + 0.06 * ripple * t, 0.11 + 0.08 * ripple * t);
      if (rng.next() < 0.012) W.blob(x, 0.03, z, rng.float(0.01, 0.022), 0.9, 0.72, 0.9, 1);
    }
  }
  for (let i = 0; i < 70; i++) {
    const a = rng.float(0, TAU);
    const rr = radius * (1.02 + rng.float(0, 0.12)) * (1 + 0.12 * Math.sin(a * 3 + wob));
    const H = rng.float(0.08, 0.4);
    for (let y = 0; y < H; y += 0.025) W.stroke(Math.cos(a) * rr, y, Math.sin(a) * rr, 0, 1, 0, 0.018, 0.007, 0.7, 0.45, 0.65, 0.7);
    W.blob(Math.cos(a) * rr, H + 0.03, Math.sin(a) * rr, rng.float(0.02, 0.045), 1, P.pearl[0], P.pearl[1], P.pearl[2]);
    W.blob(Math.cos(a) * rr, H + 0.03, Math.sin(a) * rr, 0.11, 0.1, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  }
  return W.splats;
}

/** A soft curtain of light threads across the canyon where it leaves the frame. */
function exitGate(seed, width) {
  const rng = new Rng(seed);
  const W = new SplatWriter(3000);
  for (let i = 0; i < 70; i++) {
    const x = rng.float(-width / 2, width / 2);
    const h = rng.float(1.5, 5.5);
    for (let y = 0; y < h; y += 0.08) {
      W.stroke(x, y, rng.float(-0.05, 0.05), 0, 1, 0, 0.05, 0.006, 0.35 * (1 - y / h), P.glowCool[0], P.glowCool[1], P.glowCool[2]);
    }
    W.blob(x, h, 0, rng.float(0.025, 0.05), 0.9, P.pearl[0], P.pearl[1], P.pearl[2]);
  }
  W.ellipsoid(0, 1.2, 0, width * 0.5, 1.2, 0.3, 0, 0, 0.06, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  return W.splats;
}

/**
 * Generate the asset library and place everything.
 * @returns world object with instances, colliders, slots, exits and an update(camera) method
 */
export async function buildWorld({ field, cfg, quality, scene, onProgress, textures }) {
  const rng = new Rng(cfg.seed);
  const density = quality.density;
  const lod = cfg.render.lod;
  const world = {
    instances: [],
    colliders: [],
    slots: [],
    exits: [],
    uniqueSplats: 0,
    instancedSplats: 0,
    library: {},
    procedural: {},
  };

  const kinds = Object.keys(cfg.assets);
  const chunkSize = cfg.ground.chunk;
  const b = field.bounds;
  const chunkList = [];
  for (let cz = b.minZ; cz < b.maxZ; cz += chunkSize) {
    for (let cx = b.minX; cx < b.maxX; cx += chunkSize) {
      let inside = false;
      for (let sz = 0; sz <= 4 && !inside; sz++) {
        for (let sx = 0; sx <= 4 && !inside; sx++) {
          if (field.hexDist(cx + (sx / 4) * chunkSize, cz + (sz / 4) * chunkSize) < 0) inside = true;
        }
      }
      if (inside) chunkList.push([cx + chunkSize / 2, cz + chunkSize / 2]);
    }
  }
  const totalSteps = kinds.reduce((a, k) => a + cfg.assets[k].variants, 0) + chunkList.length + field.frameEdges.length + 9;
  let step = 0;
  const progress = async (label) => {
    step++;
    onProgress?.(label, Math.min(1, step / totalSteps));
    if (step % 2 === 0) await nextFrame();
  };

  // 1 · generic asset library
  for (const kind of kinds) {
    world.library[kind] = [];
    for (let v = 0; v < cfg.assets[kind].variants; v++) {
      const asset = GENERATORS[kind](cfg.seed * 7 + v * 1013 + kindSeed(kind), density);
      if (lod) await asset.splats.createLodSplats();
      world.library[kind].push(asset);
      world.uniqueSplats += asset.count;
      await progress(`生成通用素材：${KIND_LABEL[kind]} ${v + 1}/${cfg.assets[kind].variants}`);
    }
  }

  // 1b · high-detail image assets (optional; replace or join the procedural variants)
  let imageIndex = [];
  try {
    const res = await fetch(cfg.imageAssets.index);
    if (res.ok) imageIndex = (await res.json()).assets ?? [];
  } catch {
    /* no converted assets yet */
  }
  for (const entry of imageIndex) {
    if (entry.enabled === false) continue;
    if (!world.library[entry.kind]) world.library[entry.kind] = []; // image-only kinds (e.g. floor tufts)
    onProgress?.(`载入高精度素材：${entry.id}`, Math.min(1, step / totalSteps));
    const splats = new PackedSplats({ url: entry.file });
    await splats.initialized;
    if (lod) await splats.createLodSplats();
    const asset = {
      splats,
      count: splats.numSplats,
      radius: Math.max(0.3, entry.footRadius ?? 1),
      height: entry.height,
      kind: entry.kind,
      image: true,
      stack: !!entry.stack,
      footprint: entry.footprint ?? null,
      solid: entry.shape === 'carved' || entry.shape === 'mesh', // true 3D: any yaw works
      cluster: entry.cluster ?? null,
    };
    if (entry.replace && !world.library[entry.kind].replaced) {
      // keep the light procedural variants for distant placements
      world.procedural[entry.kind] = world.library[entry.kind];
      world.library[entry.kind] = [];
      world.library[entry.kind].replaced = true;
    }
    world.library[entry.kind].push(asset);
    world.uniqueSplats += asset.count;
    world.imageAssets = (world.imageAssets ?? 0) + 1;
  }

  const addMesh = (packedSplats, kind, x, y, z, opts = {}) => {
    const mesh = new SplatMesh({ packedSplats, enableLod: lod ? true : undefined });
    mesh.position.set(x, y, z);
    if (opts.yaw !== undefined) mesh.rotation.set(opts.tiltX ?? 0, opts.yaw, opts.tiltZ ?? 0, 'YXZ');
    if (opts.scale) mesh.scale.setScalar(opts.scale);
    const tint = opts.tint ?? [1, 1, 1];
    mesh.recolor.setRGB(tint[0], tint[1], tint[2]);
    mesh.lodScale = cfg.render.lodScale[kind] ?? 1;
    scene.add(mesh);
    const inst = {
      mesh,
      kind,
      x,
      z,
      tint,
      cull: (cfg.render.cull[kind] ?? 100) * quality.cullScale,
      count: opts.count ?? 0,
      // bounding sphere for camera-frustum culling (centre lifted to mid-height)
      boundR: opts.boundR ?? 6,
      boundY: y + (opts.boundUp ?? 0),
    };
    world.instances.push(inst);
    world.instancedSplats += inst.count;
    return inst;
  };

  /** Collision for one placed asset: its measured footprint circles, or one circle. */
  const addFootprint = (asset, x, z, yaw, scale, mul = 1) => {
    if (!asset.footprint?.length) {
      world.colliders.push({ x, z, r: asset.radius * scale * mul, h: asset.height * scale });
      return;
    }
    const c = Math.cos(yaw);
    const s = Math.sin(yaw);
    for (const f of asset.footprint) {
      const [fx, fz, fr] = f.length === 3 ? f : [f[0], 0, f[1]];
      // local (x, z) rotated by yaw about Y: x' = x cos + z sin, z' = -x sin + z cos
      world.colliders.push({
        x: x + (fx * c + fz * s) * scale,
        z: z + (-fx * s + fz * c) * scale,
        r: fr * scale * mul,
        h: asset.height * scale,
      });
    }
  };

  // 2 · ground chunks (unique per chunk) and the hexagon's soil sides
  for (let i = 0; i < chunkList.length; i++) {
    const [mx, mz] = chunkList[i];
    const chunk = groundChunk(field, cfg, mx, mz, chunkSize, density, cfg.seed + 9000 + i, textures);
    if (chunk.count > 0) {
      if (lod) await chunk.splats.createLodSplats();
      world.uniqueSplats += chunk.count;
      addMesh(chunk.splats, 'ground', mx, 0, mz, { count: chunk.count, boundR: chunkSize * 0.8 + 6, boundUp: 2 });
    }
    await progress(`铺设峡谷地面 ${i + 1}/${chunkList.length}`);
  }
  const outer = field.frameEdges.filter((e) => !e.shared);
  for (let k = 0; k < outer.length; k++) {
    const side = hexSide(field, cfg, outer[k], density, cfg.seed + 7000 + k);
    if (lod) await side.splats.createLodSplats();
    world.uniqueSplats += side.count;
    addMesh(side.splats, 'side', side.x, 0, side.z, { count: side.count, boundR: field.hex.R * 0.6 + 8 });
    await progress(`六边形边框 ${k + 1}/${outer.length}`);
  }

  // 3 · placement along the path
  const hash = new SpatialHash(6);
  const hexArea = 2.598 * field.hex.R * field.hex.R * field.hexCenters.length;
  const fits = (x, z, r, spacing, kind) => {
    for (const o of hash.near(x, z, Math.max(spacing, r + 6))) {
      const dist = Math.hypot(o.x - x, o.z - z);
      if (o.kind === kind && dist < spacing) return false;
      if (dist < (r + o.r) * 0.62) return false;
    }
    return true;
  };
  const scatter = async (kind, rule, opts = {}) => {
    const libKind = opts.libKind ?? kind;
    // far placements use the light procedural variants: in the haze they read the same
    const lib = opts.light && world.procedural[libKind]?.length ? world.procedural[libKind] : world.library[libKind];
    const attempts = Math.ceil((hexArea / (rule.spacing * rule.spacing)) * 4);
    let placed = 0;
    for (let a = 0; a < attempts; a++) {
      const x = rng.float(b.minX, b.maxX);
      const z = rng.float(b.minZ, b.maxZ);
      const d = field.pathDist(x, z);
      if (d < rule.dMin || d > rule.dMax) continue;
      const asset = rng.pick(lib);
      const scale = rng.float(rule.scale[0], rule.scale[1]);
      const r = asset.radius * scale;
      if (field.hexDist(x, z) > -r * (opts.frameKeep ?? 1) - 0.4) continue;
      if (opts.keepOut && d < r * opts.keepOut) continue;
      if (!fits(x, z, r, rule.spacing, kind)) continue;
      hash.add({ x, z, r, kind });
      const haze = rule.haze ?? 0;
      const v = rng.float(-0.07, 0.07);
      const ft = cfg.render.fog.tint;
      const it = asset.image ? cfg.imageAssets.kindTint[opts.libKind ?? kind] ?? cfg.imageAssets.tint : [1, 1, 1];
      const tint = [
        it[0] * ((1 + v + rng.float(-0.04, 0.04)) * (1 - haze) + ft[0] * haze),
        it[1] * ((1 + v) * (1 - haze) + ft[1] * haze),
        it[2] * ((1 + v + rng.float(-0.04, 0.04)) * (1 - haze) + ft[2] * haze),
      ];
      const tiltMax = asset.image ? 0.02 : opts.tilt ?? 0.06;
      const y = field.height(x, z) - (asset.image ? 0.05 : opts.sink ?? 0.12) * scale;
      // Reliefs turn their front (+Z) toward the canyon; true 3D assets can face anywhere.
      let yaw = rng.float(0, TAU);
      if (asset.image && !asset.solid) {
        const e = 0.5;
        const gx = field.pathDist(x + e, z) - field.pathDist(x - e, z);
        const gz = field.pathDist(x, z + e) - field.pathDist(x, z - e);
        yaw = Math.atan2(-gx, -gz) + rng.float(-cfg.imageAssets.faceJitter, cfg.imageAssets.faceJitter);
      }
      const hgt = asset.height * scale * (asset.stack ? 2 : 1);
      const inst = addMesh(asset.splats, opts.cullKind ?? kind, x, y, z, {
        yaw,
        tiltX: rng.float(-tiltMax, tiltMax),
        tiltZ: rng.float(-tiltMax, tiltMax),
        scale,
        tint,
        count: asset.count,
        boundR: Math.max(asset.radius * scale * 2.2, hgt * 0.6),
        boundUp: hgt * 0.5,
      });
      if (opts.lodScale) inst.mesh.lodScale = opts.lodScale;
      // Single elements (one spore stalk, one curl) are grouped into small clusters.
      if (asset.cluster) {
        const n = rng.int(asset.cluster[0], asset.cluster[1]) - 1;
        for (let k = 0; k < n; k++) {
          const a2 = rng.float(0, TAU);
          const rr = rng.float(0.45, 1.1) * Math.max(0.6, asset.radius * 1.6) * scale;
          const sx = x + Math.cos(a2) * rr;
          const sz = z + Math.sin(a2) * rr;
          const ss = scale * rng.float(0.55, 1.1);
          addMesh(asset.splats, opts.cullKind ?? kind, sx, field.height(sx, sz) - 0.05 * ss, sz, {
            yaw: rng.float(0, TAU),
            tiltX: rng.float(-0.12, 0.12),
            tiltZ: rng.float(-0.12, 0.12),
            scale: ss,
            tint,
            count: asset.count,
            boundR: asset.height * ss * 0.6,
            boundUp: asset.height * ss * 0.5,
          });
          if (opts.collide !== false && d - r < 1.5) addFootprint(asset, sx, sz, 0, ss);
        }
      }
      // Small reliefs (floor tufts) get a crossed twin so they never read as a flat card edge-on.
      if (asset.image && !asset.solid && opts.crossed) {
        addMesh(asset.splats, opts.cullKind ?? kind, x, y, z, { yaw: yaw + Math.PI / 2, scale: scale * 0.92, tint, count: asset.count });
        if (opts.collide !== false && d - r < 1.5) addFootprint(asset, x, z, yaw + Math.PI / 2, scale * 0.92);
      }
      // Image trunk sections are cropped top/bottom: stack a second copy so the trunk rises into the haze.
      if (asset.stack) {
        const top = addMesh(asset.splats, opts.cullKind ?? kind, x, y + asset.height * scale * 0.97, z, {
          yaw: yaw + Math.PI + rng.float(-0.3, 0.3),
          scale,
          tint: tint.map((c) => c * 0.92),
          count: asset.count,
        });
        if (opts.lodScale) top.mesh.lodScale = opts.lodScale;
      }
      if (opts.collide !== false && d - r < 1.5) addFootprint(asset, x, z, yaw, scale, asset.image ? 1 : opts.colliderMul ?? 1);
      placed++;
    }
    await progress(`沿路径摆放：${KIND_LABEL[opts.libKind ?? kind]}（${placed}）`);
    return placed;
  };

  // Landmark fan trees in junction clearings, set off the main lines.
  const pl = cfg.placement;
  for (const node of field.nodes) {
    if (node.degree < 3 || !node.inside || node.clear < pl.fanLandmark.minClearing) continue;
    const dirs = [];
    for (const e of field.edges) {
      for (const [pts, ok] of [[e.pts, e.source === node.id], [[...e.pts].reverse(), e.target === node.id]]) {
        if (!ok) continue;
        const p = pts[Math.min(3, pts.length - 1)];
        dirs.push(Math.atan2(p[1] - node.z, p[0] - node.x));
      }
    }
    dirs.sort((a, c) => a - c);
    let bestGap = -1;
    let bestDir = 0;
    for (let i = 0; i < dirs.length; i++) {
      const a0 = dirs[i];
      const a1 = i + 1 < dirs.length ? dirs[i + 1] : dirs[0] + TAU;
      if (a1 - a0 > bestGap) {
        bestGap = a1 - a0;
        bestDir = (a0 + a1) / 2;
      }
    }
    const off = node.clear * 0.62;
    const x = node.x + Math.cos(bestDir) * off;
    const z = node.z + Math.sin(bestDir) * off;
    // A still pond on the other side of the chamber.
    if (rng.chance(cfg.path.pondChance)) {
      const pr = node.clear * 0.3;
      const px = node.x - Math.cos(bestDir) * node.clear * 0.42;
      const pz = node.z - Math.sin(bestDir) * node.clear * 0.42;
      const ps = pondSplats(cfg.seed + 300 + world.colliders.length, pr);
      if (lod) await ps.createLodSplats();
      addMesh(ps, 'ground', px, field.height(px, pz) + 0.02, pz, { count: ps.numSplats });
      world.colliders.push({ x: px, z: pz, r: pr * 0.85, h: 0.3 });
      hash.add({ x: px, z: pz, r: pr, kind: 'pond' });
    }
    const asset = rng.pick(world.library.fan);
    const scale = rng.float(pl.fanLandmark.scale[0], pl.fanLandmark.scale[1]);
    const yaw = asset.image ? Math.atan2(node.x - x, node.z - z) : rng.float(0, TAU);
    const tint = asset.image ? cfg.imageAssets.kindTint.fan ?? cfg.imageAssets.tint : undefined;
    addMesh(asset.splats, 'fan', x, field.height(x, z) - 0.1, z, {
      yaw,
      scale,
      tint,
      count: asset.count,
      boundR: Math.max(asset.radius * scale * 3, asset.height * scale * 0.7),
      boundUp: asset.height * scale * 0.5,
    });
    hash.add({ x, z, r: asset.radius * scale, kind: 'fan' });
    addFootprint(asset, x, z, yaw, scale);
    for (let k = 0; k < 3; k++) {
      const a = bestDir + rng.float(-1.2, 1.2);
      const rr = rng.float(1.2, 2.2) * scale;
      const sx = x + Math.cos(a) * rr;
      const sz = z + Math.sin(a) * rr;
      const sp = rng.pick(world.library.spores);
      const ss = rng.float(0.9, 1.3);
      const syaw = sp.image ? Math.atan2(node.x - sx, node.z - sz) : rng.float(0, TAU);
      addMesh(sp.splats, 'spores', sx, field.height(sx, sz) - 0.05, sz, {
        yaw: syaw,
        scale: ss,
        tint: sp.image ? cfg.imageAssets.kindTint.spores ?? cfg.imageAssets.tint : undefined,
        count: sp.count,
      });
      hash.add({ x: sx, z: sz, r: sp.radius * ss, kind: 'spores' });
      addFootprint(sp, sx, sz, syaw, ss);
    }
  }
  await progress('地标扇菌');

  await scatter('trunk', pl.trunkNear, { keepOut: 0.6, sink: 0.4, frameKeep: 0.8 });
  await scatter('trunk', pl.trunkFar, { sink: 0.4, collide: false, frameKeep: 0.8, light: true });
  await scatter('trunk', pl.backdrop, { sink: 0.4, collide: false, tilt: 0.03, frameKeep: 0.8, lodScale: 0.7, light: true });
  await scatter('fan', pl.fanWall, { libKind: 'fan', sink: 0.1 });
  await scatter('curl', pl.curl, { sink: 0.05 });
  await scatter('mound', pl.mound, { sink: 0.35, colliderMul: 0.8, tilt: 0.12 });
  await scatter('spores', pl.spores, { sink: 0.05, colliderMul: 1.0, tilt: 0.1 });
  // Floor tufts need a light asset: use the low-res 'tuft' conversion, never the full mound.
  if (world.library.tuft?.length) {
    await scatter('tuft', pl.floorTufts, { libKind: 'tuft', cullKind: 'spores', sink: 0.02, tilt: 0.05, crossed: true });
  }

  // Surrounding forest: dark trunk silhouettes outside the hexagon, layered into the haze.
  const sr = cfg.surroundings;
  const silhouettes = world.procedural.trunk?.length ? world.procedural.trunk : world.library.trunk ?? [];
  for (let i = 0; i < sr.count && silhouettes.length; i++) {
    const a = (i / sr.count) * TAU + rng.float(-0.08, 0.08);
    const reach = Math.max(...field.hexCenters.map((c) => Math.hypot(c.x, c.z)));
    const dist = reach + field.hex.R + rng.float(sr.minDist, sr.maxDist);
    const x = Math.cos(a) * dist;
    const z = Math.sin(a) * dist;
    const asset = rng.pick(silhouettes);
    const inst = addMesh(asset.splats, 'trunk', x, -sr.sink, z, {
      yaw: rng.float(0, TAU),
      scale: rng.float(sr.scale[0], sr.scale[1]),
      tint: sr.tint,
      count: asset.count,
    });
    inst.cull = 1e6;
    inst.boundR = 60;
    inst.boundY = 10;
    inst.mesh.lodScale = 0.45;
  }

  // 4 · story slots along the paths (placeholders for user-made objects, next phase)
  const slotSplats = storySlot(cfg.seed + 77);
  const addSlot = (id, x, z) => {
    const inst = addMesh(slotSplats, 'slot', x, field.height(x, z) + 0.02, z, { count: slotSplats.numSplats });
    world.slots.push({ id, x, z, mesh: inst.mesh, baseY: inst.mesh.position.y });
  };
  let slotCount = 0;
  for (const e of field.edges) {
    let acc = 0;
    let next = cfg.ground.slotSpacing * 0.55;
    for (let i = 1; i < e.pts.length; i++) {
      const [x0, z0] = e.pts[i - 1];
      const [x1, z1] = e.pts[i];
      acc += Math.hypot(x1 - x0, z1 - z0);
      if (acc < next) continue;
      next += cfg.ground.slotSpacing;
      if (field.hexDist(x1, z1) > -7) continue;
      if (field.nodes.some((n) => n.degree >= 3 && Math.hypot(n.x - x1, n.z - z1) < n.clear + 4)) continue;
      const len = Math.hypot(x1 - x0, z1 - z0) || 1;
      const side = slotCount % 2 ? 1 : -1;
      const off = Math.max(0, e.hws[i] - 1.6) * side;
      const sx = x1 + (-(z1 - z0) / len) * off;
      const sz = z1 + ((x1 - x0) / len) * off;
      addSlot(`${e.id}·${++slotCount}`, sx, sz);
    }
  }
  for (const node of field.nodes) {
    if (node.degree === 1 && node.inside) addSlot(`${node.id}·尽头`, node.x, node.z);
  }

  // 5 · exits where the canyon leaves the hexagon (to neighbouring modules)
  const gateSplats = exitGate(cfg.seed + 99, cfg.path.fixedWidth);
  for (const ex of field.exits) {
    const inst = addMesh(gateSplats, 'slot', ex.x, field.height(ex.x, ex.z), ex.z, {
      yaw: Math.atan2(ex.dirX, ex.dirZ),
      count: gateSplats.numSplats,
    });
    world.exits.push({ ...ex, mesh: inst.mesh });
  }
  await progress('故事物件位与出口');

  // 6 · floating spores following the camera (Spark's snowBox generator)
  const spores = generators.snowBox({
    box: new THREE.Box3(new THREE.Vector3(-14, 0, -14), new THREE.Vector3(14, 9, 14)),
    density: 2.2 * density,
    minScale: 0.008,
    maxScale: 0.022,
    fallDirection: new THREE.Vector3(0.25, 0.35, 0.1),
    fallVelocity: 0.12,
    wanderScale: 0.3,
    wanderVariance: 1.5,
    color1: new THREE.Color(P.glowCool[0], P.glowCool[1], P.glowCool[2]),
    color2: new THREE.Color(P.glowWarm[0], P.glowWarm[1], P.glowWarm[2]),
    opacity: 0.9,
  });
  scene.add(spores.snow);
  world.spores = spores;
  world.fog = makeDistanceFog(scene, cfg.render.fog);
  onProgress?.('完成', 1);

  /** Per-frame: frustum + distance culling, haze centre, slot bobbing, spore box follow. */
  const frustum = new THREE.Frustum();
  const projView = new THREE.Matrix4();
  const sphere = new THREE.Sphere();
  world.update = (camera, time) => {
    const cx = camera.position.x;
    const cz = camera.position.z;
    camera.updateMatrixWorld();
    projView.multiplyMatrices(camera.projectionMatrix, camera.matrixWorldInverse);
    frustum.setFromProjectionMatrix(projView);
    let visibleSplats = 0;
    for (const inst of world.instances) {
      const dist = Math.hypot(inst.x - cx, inst.z - cz);
      // Only what the camera can see goes to the renderer (sphere inflated so edges don't pop).
      sphere.center.set(inst.x, inst.boundY ?? 0, inst.z);
      sphere.radius = (inst.boundR ?? 6) * 1.3 + 3;
      const visible = dist < inst.cull && (dist < 6 || frustum.intersectsSphere(sphere));
      if (inst.mesh.visible !== visible) inst.mesh.visible = visible;
      if (visible) visibleSplats += inst.count;
    }
    world.fog.center(camera.position);
    for (const s of world.slots) s.mesh.position.y = s.baseY + Math.sin(time * 1.6 + s.x) * 0.06;
    const box = world.spores;
    box.min.value.set(cx - 14, camera.position.y - 3, cz - 14);
    box.max.value.set(cx + 14, camera.position.y + 7, cz + 14);
    world.visibleSplats = visibleSplats;
  };

  return world;
}

/**
 * Distance haze for splats: two global SplatEdits around the camera. Beyond
 * the radius the splat colour is multiplied down and the haze colour added,
 * which together mix far splats toward the haze (Spark edits only multiply or add).
 */
function makeDistanceFog(scene, fog) {
  const keep = 1 - fog.amount;
  const darken = new SplatEdit({ rgbaBlendMode: SplatEditRgbaBlendMode.MULTIPLY, softEdge: fog.softEdge, invert: true });
  darken.add(new SplatEditSdf({ type: SplatEditSdfType.SPHERE, radius: fog.radius, color: new THREE.Color(keep, keep, keep), opacity: 1 }));
  const tint = new SplatEdit({ rgbaBlendMode: SplatEditRgbaBlendMode.ADD_RGBA, softEdge: fog.softEdge, invert: true });
  const c = fog.color;
  tint.add(
    new SplatEditSdf({
      type: SplatEditSdfType.SPHERE,
      radius: fog.radius,
      color: new THREE.Color(c[0] * fog.amount, c[1] * fog.amount, c[2] * fog.amount),
      opacity: 0,
    }),
  );
  scene.add(darken);
  scene.add(tint);
  return {
    center(p) {
      darken.position.copy(p);
      tint.position.copy(p);
    },
  };
}

/** Vertical gradient dome behind everything. */
export function makeSky() {
  const toLinear = (c) => new THREE.Color().setRGB(c[0], c[1], c[2], THREE.SRGBColorSpace);
  const mat = new THREE.ShaderMaterial({
    side: THREE.BackSide,
    depthWrite: false,
    uniforms: {
      top: { value: toLinear(P.skyTop) },
      mid: { value: toLinear(P.skyMid) },
      low: { value: toLinear(P.skyLow) },
    },
    vertexShader: /* glsl */ `
      varying vec3 vDir;
      void main() {
        vDir = normalize(position);
        gl_Position = projectionMatrix * modelViewMatrix * vec4(position, 1.0);
      }`,
    fragmentShader: /* glsl */ `
      uniform vec3 top; uniform vec3 mid; uniform vec3 low;
      varying vec3 vDir;
      void main() {
        float h = vDir.y;
        vec3 c = h > 0.0 ? mix(mid, top, pow(clamp(h, 0.0, 1.0), 0.7)) : mix(mid, low, pow(clamp(-h, 0.0, 1.0), 0.45));
        gl_FragColor = vec4(c, 1.0);
      }`,
  });
  const sky = new THREE.Mesh(new THREE.SphereGeometry(600, 32, 16), mat);
  sky.renderOrder = -10;
  sky.frustumCulled = false;
  return sky;
}

/** Invisible hexagonal floor that writes depth so splats below the ground are hidden. */
export function makeDepthFloor(field) {
  const b = field.bounds;
  const w = b.maxX - b.minX;
  const h = b.maxZ - b.minZ;
  const segX = Math.min(400, Math.ceil(w / 0.6));
  const segZ = Math.min(400, Math.ceil(h / 0.6));
  const geo = new THREE.PlaneGeometry(w, h, segX, segZ);
  geo.rotateX(-Math.PI / 2);
  const pos = geo.attributes.position;
  const inside = new Uint8Array(pos.count);
  for (let i = 0; i < pos.count; i++) {
    const x = pos.getX(i) + (b.minX + b.maxX) / 2;
    const z = pos.getZ(i) + (b.minZ + b.maxZ) / 2;
    inside[i] = field.hexDist(x, z) < -0.1 ? 1 : 0;
    pos.setXYZ(i, x, field.height(x, z) - 0.16, z);
  }
  // keep only triangles fully inside the hexagon, so the soil sides stay visible
  const src = geo.index.array;
  const keep = [];
  for (let i = 0; i < src.length; i += 3) {
    if (inside[src[i]] && inside[src[i + 1]] && inside[src[i + 2]]) keep.push(src[i], src[i + 1], src[i + 2]);
  }
  geo.setIndex(keep);
  geo.computeBoundingSphere();
  const mesh = new THREE.Mesh(geo, new THREE.MeshBasicMaterial({ colorWrite: false }));
  mesh.renderOrder = -5;
  return mesh;
}

export { KIND_LABEL, clamp };

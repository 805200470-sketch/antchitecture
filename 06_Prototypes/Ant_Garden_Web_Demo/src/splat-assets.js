// Procedural Gaussian-splat asset library, modelled on the concept sheet:
// 01 curled frost fern · 02 fan tree · 03 glass spore stalks · 04 porous lace trunk,
// plus moss mounds. Each generator builds one variant in local coordinates
// (base at the origin, +Y up); the world then copies, rotates, scales and
// tints these variants along the recognized path.
import * as THREE from 'three';
import { PackedSplats } from '@sparkjsdev/spark';
import { Rng, Noise, cellular2, clamp, lerp, smoothstep } from './noise.js';
import { PALETTE as P } from './config.js';

const TAU = Math.PI * 2;
const _c = new THREE.Vector3();
const _s = new THREE.Vector3();
const _q = new THREE.Quaternion();
const _v = new THREE.Vector3();
const _col = new THREE.Color();
const AX_X = new THREE.Vector3(1, 0, 0);
const AX_Z = new THREE.Vector3(0, 0, 1);
const LIGHT = new THREE.Vector3(0.3, 1, 0.35).normalize();

/** Thin wrapper over PackedSplats.pushSplat that reuses temporaries. */
export class SplatWriter {
  constructor(maxSplats = 4096) {
    this.splats = new PackedSplats({ maxSplats });
    this.count = 0;
  }

  _push(x, y, z, a, r, g, b) {
    _c.set(x, y, z);
    _col.r = clamp(r, 0, 1);
    _col.g = clamp(g, 0, 1);
    _col.b = clamp(b, 0, 1);
    this.splats.pushSplat(_c, _s, _q, clamp(a, 0, 1), _col);
    this.count++;
  }

  /** Flattened splat lying on a surface with unit normal (nx, ny, nz). */
  disk(x, y, z, nx, ny, nz, radius, thickness, a, r, g, b) {
    _v.set(nx, ny, nz);
    _q.setFromUnitVectors(AX_Z, _v);
    _s.set(radius, radius, thickness);
    this._push(x, y, z, a, r, g, b);
  }

  /** Ellipsoid stretched along unit direction (tx, ty, tz). */
  stroke(x, y, z, tx, ty, tz, half, thick, a, r, g, b) {
    _v.set(tx, ty, tz);
    _q.setFromUnitVectors(AX_X, _v);
    _s.set(half, thick, thick);
    this._push(x, y, z, a, r, g, b);
  }

  /** Round splat. */
  blob(x, y, z, radius, a, r, g, b) {
    _q.identity();
    _s.set(radius, radius, radius);
    this._push(x, y, z, a, r, g, b);
  }

  /** Axis-aligned ellipsoid (sx, sy, sz), then rotated by yaw (Y) and tilt (Z). */
  ellipsoid(x, y, z, sx, sy, sz, yaw, tilt, a, r, g, b) {
    _e.set(0, yaw, tilt, 'YXZ');
    _q.setFromEuler(_e);
    _s.set(sx, sy, sz);
    this._push(x, y, z, a, r, g, b);
  }
}
const _e = new THREE.Euler();

// ---- colour helpers (operate on a shared scratch triple to avoid allocations)
const C = [0, 0, 0];
function setC(c) {
  C[0] = c[0];
  C[1] = c[1];
  C[2] = c[2];
}
function mixC(a, b, t) {
  C[0] = a[0] + (b[0] - a[0]) * t;
  C[1] = a[1] + (b[1] - a[1]) * t;
  C[2] = a[2] + (b[2] - a[2]) * t;
}
function towardC(b, t) {
  C[0] += (b[0] - C[0]) * t;
  C[1] += (b[1] - C[1]) * t;
  C[2] += (b[2] - C[2]) * t;
}
function scaleC(k) {
  C[0] *= k;
  C[1] *= k;
  C[2] *= k;
}
/** Baked lighting: soft sky dome + a key light from the canopy above. */
function lit(nx, ny, nz, ao) {
  const ndl = Math.max(0, nx * LIGHT.x + ny * LIGHT.y + nz * LIGHT.z);
  return (0.34 + 0.3 * (0.5 + 0.5 * ny) + 0.5 * ndl) * ao;
}

// ---------------------------------------------------------------------------
// 04 · Porous trunk with lace membrane (canyon wall)
// ---------------------------------------------------------------------------
export function makeTrunk(seed, density = 1) {
  const rng = new Rng(seed);
  const nz = new Noise(seed + 101);
  const W = new SplatWriter(40000);
  const H = rng.float(36, 58);
  const R0 = rng.float(2.3, 3.4);
  const R1 = R0 * rng.float(0.42, 0.6);
  const roots = rng.int(4, 6);
  const rootPhase = rng.float(0, TAU);
  const wobA = rng.float(0.4, 1.1);
  const wobF = rng.float(0.035, 0.07);
  const wobP = rng.float(0, TAU);
  const leanX = rng.float(-0.04, 0.04);
  const leanZ = rng.float(-0.04, 0.04);
  const holeCell = rng.float(1.5, 2.1);
  const holeSeed = seed * 13 + 7;
  const holeProb = rng.float(0.45, 0.7);
  const cell = {};
  const sq = Math.sqrt(density);

  const base = (y) => R0 + (R1 - R0) * Math.pow(clamp(y / H, 0, 1), 0.75);
  const cxAt = (y) => Math.sin(y * wobF + wobP) * wobA + leanX * y;
  const czAt = (y) => Math.cos(y * wobF * 0.83 + wobP * 1.7) * wobA * 0.8 + leanZ * y;

  let y = -0.8;
  while (y < H) {
    const sp = (0.1 + 0.011 * Math.max(0, y)) / sq;
    const meanR = base(y) * (1 + 0.8 * Math.exp(-Math.max(0, y) / 2.3));
    const n = Math.max(10, Math.ceil((TAU * meanR) / sp));
    for (let i = 0; i < n; i++) {
      const th = ((i + rng.next() * 0.9) / n) * TAU;
      const yy = y + (rng.next() - 0.5) * sp * 0.9;
      const cth = Math.cos(th);
      const sth = Math.sin(th);
      const flare = 1.35 * Math.exp(-Math.max(0, yy) / 2.3);
      const butt = Math.pow(Math.max(0, Math.cos(roots * (th - rootPhase) + nz.noise2(yy * 0.25, th * 2) * 0.5)), 3);
      const fiber = nz.noise2(th * R0 * 2.2, yy * 0.18);
      let r = base(yy) * (1 + flare * (0.2 + 0.8 * butt));
      r += R0 * 0.13 * nz.fbm3(cth * 1.4, yy * 0.11, sth * 1.4, 3) + 0.06 * fiber;

      let nx = cth;
      let ny = 0.55 * flare * (0.3 + butt);
      let nzz = sth;
      const inv = 1 / Math.hypot(nx, ny, nzz);
      nx *= inv;
      ny *= inv;
      nzz *= inv;

      let hole = 0;
      let rim = 0;
      if (yy > 1.5 && yy < H * 0.86) {
        cellular2((th * R0 * 1.05) / holeCell, (yy * 0.55) / holeCell, holeSeed, cell);
        if (cell.r2 < holeProb) {
          const hr = 0.2 + 0.24 * cell.r1;
          if (cell.f1 < hr) hole = 1 - cell.f1 / hr;
          else if (cell.f1 < hr + 0.07) rim = 1 - (cell.f1 - hr) / 0.07;
        }
      }
      const rr = r - hole * R0 * 0.32 + rim * 0.07;

      const k = 0.5 + 0.5 * nz.fbm3(cth * 2.1 + 10, yy * 0.23, sth * 2.1, 3);
      mixC(P.barkDeep, P.bark, k);
      towardC(P.barkLight, smoothstep(0.55, 0.95, 0.5 + 0.5 * fiber) * 0.55);
      const mossy = clamp(
        smoothstep(4.5, 0.5, yy + nz.noise2(th * 3, yy * 0.3) * 1.5) * 0.85 +
          smoothstep(0.55, 0.8, nz.noise2(th * 1.7 + 4, yy * 0.07)) * 0.6,
        0,
        0.9,
      );
      towardC(P.moss, mossy);
      if (hole > 0) mixC(P.holeMid, P.holeDeep, smoothstep(0.05, 0.6, hole));
      if (rim > 0) towardC(P.rim, rim * 0.8);
      const ao = (0.45 + 0.55 * smoothstep(-0.5, 5, yy)) * (1 - 0.5 * hole);
      const L = lit(nx, ny, nzz, ao) * (0.85 + 0.45 * smoothstep(8, H, yy));
      scaleC(L);
      towardC(P.haze, smoothstep(H * 0.25, H * 1.05, yy) * 0.72);

      const x = cxAt(yy) + cth * rr;
      const z = czAt(yy) + sth * rr;
      W.disk(x, yy, z, nx, ny, nzz, sp * 0.62, sp * 0.16, 0.96, C[0], C[1], C[2]);

      // Frost fuzz on mossy bark.
      if (hole === 0 && rng.next() < 0.04 + 0.1 * mossy) {
        const off = 0.04 + rng.next() * 0.1;
        const fl = L * 1.15;
        W.blob(x + cth * off, yy + rng.float(-0.05, 0.05), z + sth * off, sp * 0.22, 0.75,
          P.frost[0] * fl, P.frost[1] * fl, P.frost[2] * fl);
      }
    }
    y += sp * 0.82;
  }

  // Lace membrane draped over one side, with oval openings, veins and pearls.
  const patches = rng.chance(0.85) ? rng.int(1, 2) : 0;
  for (let p = 0; p < patches; p++) {
    addLace(W, rng, nz, {
      th0: rng.float(0, TAU),
      dth: rng.float(0.9, 1.6),
      y0: rng.float(2.5, 7),
      h: rng.float(9, 18),
      radiusAt: base,
      cxAt,
      czAt,
      seed: seed * 31 + p,
      density,
    });
  }

  return { splats: W.splats, count: W.count, radius: R0 * 1.5, height: H, kind: 'trunk' };
}

function addLace(W, rng, nz, o) {
  const cell = {};
  const sq = Math.sqrt(o.density);
  const step = 0.075 / sq;
  const cellSize = rng.float(0.8, 1.25);
  for (let yy = o.y0; yy < o.y0 + o.h; yy += step) {
    const rBase = o.radiusAt(yy) + 0.35;
    const dthStep = step / rBase;
    for (let th = o.th0 - o.dth / 2; th < o.th0 + o.dth / 2; th += dthStep) {
      const u = (th - o.th0) / (o.dth / 2); // -1..1 across the patch
      const v = (yy - o.y0) / o.h; // 0..1 up the patch
      const edge = Math.min(1 - Math.abs(u), v * 4, (1 - v) * 3);
      const outline = edge + nz.noise2(th * 3 + 7, yy * 0.4) * 0.25;
      if (outline < 0) continue;
      const warp = nz.noise2(th * 2.3 + 3, yy * 0.21) * 0.6;
      cellular2((th * rBase) / cellSize + warp, (yy * 0.75) / cellSize - warp, o.seed, cell);
      const vein = cell.f2 - cell.f1;
      const cth = Math.cos(th);
      const sth = Math.sin(th);
      const bulge = 0.12 * Math.sin(v * Math.PI);
      const rr = rBase + bulge;
      const x = o.cxAt(yy) + cth * rr + (rng.next() - 0.5) * step * 0.6;
      const z = o.czAt(yy) + sth * rr + (rng.next() - 0.5) * step * 0.6;
      const yj = yy + (rng.next() - 0.5) * step * 0.6;
      const glow = 0.5 + 0.25 * smoothstep(0, 1, v);
      if (vein < 0.07) {
        mixC(P.lace, P.laceGold, 0.4 + 0.6 * cell.r1);
        scaleC(glow);
        W.disk(x, yj, z, cth, 0, sth, step * 0.5, step * 0.25, 0.6, C[0], C[1], C[2]);
        if (rng.next() < 0.012) {
          const pr = rng.float(0.05, 0.11);
          W.blob(x + cth * 0.05, yj, z + sth * 0.05, pr, 1, P.pearl[0], P.pearl[1], P.pearl[2]);
          W.blob(x + cth * 0.06, yj, z + sth * 0.06, pr * 2.6, 0.13, P.glowWarm[0], P.glowWarm[1], P.glowWarm[2]);
        }
      } else if (cell.f1 > 0.3 + 0.12 * cell.r2) {
        // translucent membrane between veins; the cell centres stay open
        mixC(P.holeMid, P.lace, 0.45);
        scaleC(0.75 * glow);
        W.disk(x, yj, z, cth, 0, sth, step * 0.7, step * 0.2, 0.42, C[0], C[1], C[2]);
      }
      // Fringe hairs along the patch's side edge.
      if (outline < 0.08 && Math.abs(u) > 0.6 && rng.next() < 0.25) {
        const side = Math.sign(u);
        const tx = -sth * side;
        const tz = cth * side;
        const len = rng.float(0.2, 0.5);
        W.stroke(x + tx * len * 0.5, yj, z + tz * len * 0.5, tx, 0.1, tz, len * 0.5, 0.006, 0.45,
          P.frost[0], P.frost[1], P.frost[2]);
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Moss mound (fills the gaps at the foot of the canyon walls)
// ---------------------------------------------------------------------------
export function makeMound(seed, density = 1) {
  const rng = new Rng(seed);
  const nz = new Noise(seed + 202);
  const W = new SplatWriter(12000);
  const rx = rng.float(1.3, 3.0);
  const rz = rng.float(1.3, 3.0);
  const ry = rng.float(0.8, 2.0);
  const sp = 0.075 / Math.sqrt(density);
  const area = TAU * Math.pow((Math.pow(rx * rz, 1.6) + Math.pow(rx * ry, 1.6) + Math.pow(rz * ry, 1.6)) / 3, 1 / 1.6) * 0.62;
  const n = Math.ceil(area / (sp * sp));
  const golden = Math.PI * (3 - Math.sqrt(5));
  for (let i = 0; i < n; i++) {
    // Fibonacci points over the upper part of a sphere, slightly below the equator.
    const t = (i + 0.5) / n;
    const yN = 1 - t * 1.15;
    const rad = Math.sqrt(Math.max(0, 1 - yN * yN));
    const ph = i * golden;
    const dx = Math.cos(ph) * rad;
    const dz = Math.sin(ph) * rad;
    const disp = 1 + 0.22 * nz.fbm3(dx * 1.6, yN * 1.6, dz * 1.6, 3) + 0.08 * nz.noise3(dx * 6, yN * 6, dz * 6);
    const x = dx * rx * disp + (rng.next() - 0.5) * sp;
    const y = yN * ry * disp;
    const z = dz * rz * disp + (rng.next() - 0.5) * sp;
    let nx = dx / rx;
    let ny = yN / ry;
    let nzz = dz / rz;
    const inv = 1 / Math.hypot(nx, ny, nzz);
    nx *= inv;
    ny *= inv;
    nzz *= inv;
    const k = 0.5 + 0.5 * nz.fbm3(x * 0.7, y * 0.7, z * 0.7, 3);
    mixC(P.mossDark, P.moss, k);
    towardC(P.mossLight, smoothstep(0.6, 0.9, k) * 0.6);
    const frost = smoothstep(0.35, 0.9, ny) * smoothstep(-0.1, 0.5, nz.noise3(x * 1.3, y * 1.3, z * 1.3));
    towardC(P.frost, frost * 0.55);
    scaleC(lit(nx, ny, nzz, 0.55 + 0.45 * smoothstep(-0.2, ry * 0.7, y)));
    W.disk(x, y, z, nx, ny, nzz, sp * 0.65, sp * 0.3, 0.95, C[0], C[1], C[2]);
    // fuzzy tips
    if (rng.next() < 0.45) {
      const off = 0.03 + rng.next() * 0.09;
      W.blob(x + nx * off, y + ny * off, z + nzz * off, sp * 0.28, 0.7, C[0] * 1.35, C[1] * 1.35, C[2] * 1.35);
    }
    if (rng.next() < 0.0035) {
      W.blob(x + nx * 0.05, y + ny * 0.05, z + nzz * 0.05, 0.035, 1, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
    }
  }
  return { splats: W.splats, count: W.count, radius: Math.max(rx, rz) * 0.85, height: ry, kind: 'mound' };
}

// ---------------------------------------------------------------------------
// 03 · Glass spore stalks
// ---------------------------------------------------------------------------
export function makeSpores(seed, density = 1) {
  const rng = new Rng(seed);
  const W = new SplatWriter(4000);
  const sq = Math.sqrt(density);
  const stalks = rng.int(3, 7);
  // tuft at the base
  for (let i = 0; i < 260 * density; i++) {
    const a = rng.float(0, TAU);
    const rr = Math.sqrt(rng.next()) * 0.45;
    const h = (0.45 - rr) * 0.5 * rng.next();
    mixC(P.mossDark, P.moss, rng.next());
    scaleC(0.9);
    W.blob(Math.cos(a) * rr, h, Math.sin(a) * rr, 0.035, 0.9, C[0], C[1], C[2]);
  }
  for (let s = 0; s < stalks; s++) {
    const H = rng.float(1.2, 4.2);
    const a0 = rng.float(0, TAU);
    const r0 = rng.float(0, 0.18);
    const bend = rng.float(0.05, 0.35);
    const bendDir = rng.float(0, TAU);
    const stemR = rng.float(0.022, 0.045);
    const bulbR = rng.float(0.13, 0.3) * (0.7 + 0.3 * H / 4.2);
    const bx = Math.cos(a0) * r0;
    const bz = Math.sin(a0) * r0;
    const step = 0.03 / sq;
    let px = bx;
    let py = 0;
    let pz = bz;
    for (let y = 0; y < H; y += step) {
      const t = y / H;
      const off = bend * t * t;
      const x = bx + Math.cos(bendDir) * off;
      const z = bz + Math.sin(bendDir) * off;
      const tx = x - px;
      const ty = y - py + 1e-6;
      const tz = z - pz;
      const inv = 1 / Math.hypot(tx, ty, tz);
      const glowT = smoothstep(0.55, 1, t);
      mixC(P.stem, P.stalk, 0.35 + 0.65 * t);
      scaleC(0.85 + 0.5 * glowT);
      W.stroke(x, y, z, tx * inv, ty * inv, tz * inv, step * 0.9, stemR * (1 - 0.35 * t), 0.75, C[0], C[1], C[2]);
      px = x;
      py = y;
      pz = z;
    }
    // glass bulb: translucent shell, bright core, soft halo
    const cx = px;
    const cy = H + bulbR * 0.8;
    const cz = pz;
    const shellN = Math.ceil(90 * density);
    for (let i = 0; i < shellN; i++) {
      const u = rng.float(-1, 1);
      const ph = rng.float(0, TAU);
      const rr = Math.sqrt(1 - u * u);
      const elong = 1.15;
      const nx = rr * Math.cos(ph);
      const ny = u;
      const nzz = rr * Math.sin(ph);
      mixC(P.stalk, P.glowCool, 0.5 + 0.5 * ny);
      W.disk(cx + nx * bulbR, cy + ny * bulbR * elong, cz + nzz * bulbR, nx, ny, nzz, bulbR * 0.32, bulbR * 0.06, 0.26,
        C[0], C[1], C[2]);
    }
    W.blob(cx, cy - bulbR * 0.15, cz, bulbR * 0.38, 1, P.pearl[0], P.pearl[1], P.pearl[2]);
    W.blob(cx, cy - bulbR * 0.2, cz, bulbR * 0.75, 0.55, P.glowWarm[0], P.glowWarm[1], P.glowWarm[2]);
    W.blob(cx - bulbR * 0.35, cy + bulbR * 0.45, cz - bulbR * 0.2, bulbR * 0.12, 0.9, 1, 1, 1);
    W.blob(cx, cy, cz, bulbR * 2.6, 0.1, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  }
  return { splats: W.splats, count: W.count, radius: 0.45, height: 4.5, kind: 'spores' };
}

// ---------------------------------------------------------------------------
// 02 · Fan tree (landmark at junctions)
// ---------------------------------------------------------------------------
export function makeFanTree(seed, density = 1) {
  const rng = new Rng(seed);
  const nz = new Noise(seed + 404);
  const W = new SplatWriter(42000);
  const sq = Math.sqrt(density);
  const Hs = rng.float(6, 10);
  const rBase = rng.float(0.38, 0.55);
  const rTop = rBase * 0.55;
  const lean = rng.float(0, 0.6);
  const leanDir = rng.float(0, TAU);
  const stemX = (y) => Math.cos(leanDir) * lean * (y / Hs) ** 2;
  const stemZ = (y) => Math.sin(leanDir) * lean * (y / Hs) ** 2;

  // fibrous stem with flared roots
  for (let y = -0.3; y < Hs; ) {
    const sp = 0.06 / sq;
    const flare = 1.6 * Math.exp(-Math.max(0, y) / 0.7);
    const r0 = lerp(rBase, rTop, clamp(y / Hs, 0, 1)) * (1 + flare);
    const n = Math.max(8, Math.ceil((TAU * r0) / sp));
    for (let i = 0; i < n; i++) {
      const th = ((i + rng.next()) / n) * TAU;
      const fib = nz.noise2(th * 5, y * 0.6);
      const r = r0 * (1 + 0.12 * fib + flare * 0.5 * Math.max(0, Math.cos(5 * th + 1.3)) ** 3);
      const cth = Math.cos(th);
      const sth = Math.sin(th);
      mixC(P.barkDeep, P.bark, 0.4 + 0.4 * fib);
      towardC(P.barkLight, smoothstep(0.4, 0.9, fib) * 0.4);
      scaleC(lit(cth, 0.2 * flare, sth, 0.55 + 0.45 * smoothstep(0, 3, y)));
      W.disk(stemX(y) + cth * r, y, stemZ(y) + sth * r, cth, 0.2 * flare, sth, sp * 0.6, sp * 0.15, 0.95, C[0], C[1], C[2]);
    }
    y += sp * 0.8;
  }

  const tx0 = stemX(Hs);
  const tz0 = stemZ(Hs);
  const top = Hs;
  const Rc = rng.float(3.6, 5.8);
  const flat = rng.float(0.45, 0.65);

  // ribs carrying pearl drops (the branches inside the fan)
  const ribs = rng.int(12, 20);
  for (let i = 0; i < ribs; i++) {
    const ph = (i / ribs) * TAU + rng.float(-0.15, 0.15);
    const len = Rc * rng.float(0.45, 0.75);
    let px = tx0;
    let py = top;
    let pz = tz0;
    for (let t = 0; t <= 1; t += 0.03) {
      const al = lerp(0.15, 1.05, t);
      const x = tx0 + Math.cos(ph) * Math.sin(al) * len;
      const y = top + Math.cos(al) * len * flat;
      const z = tz0 + Math.sin(ph) * Math.sin(al) * len;
      const dx = x - px;
      const dy = y - py + 1e-6;
      const dz = z - pz;
      const inv = 1 / Math.hypot(dx, dy, dz);
      mixC(P.bark, P.stalk, t);
      W.stroke(x, y, z, dx * inv, dy * inv, dz * inv, 0.07, lerp(0.07, 0.02, t), 0.85, C[0], C[1], C[2]);
      if (t > 0.2 && rng.next() < 0.22) {
        W.stroke(x, y + 0.04, z, dx * inv, dy * inv, dz * inv, rng.float(0.07, 0.13), rng.float(0.035, 0.055), 1,
          P.pearl[0], P.pearl[1], P.pearl[2]);
        W.blob(x, y + 0.04, z, 0.26, 0.12, P.glowWarm[0], P.glowWarm[1], P.glowWarm[2]);
      }
      px = x;
      py = y;
      pz = z;
    }
  }

  // fine translucent filaments forming the umbrella
  const filaments = Math.ceil(150 * Math.min(1.4, density));
  const fstep = 0.055 / sq;
  for (let i = 0; i < filaments; i++) {
    const ph = (i / filaments) * TAU + rng.float(-0.02, 0.02);
    const a1 = rng.float(1.45, 1.85);
    const len = Rc * rng.float(0.92, 1.05);
    const arc = len * a1;
    let px = tx0;
    let py = top;
    let pz = tz0;
    for (let s = fstep; s < arc; s += fstep) {
      const al = (s / arc) * a1;
      const x = tx0 + Math.cos(ph) * Math.sin(al) * len;
      const y = top + (Math.cos(al) * flat - 0.08 * (s / arc) ** 2) * len;
      const z = tz0 + Math.sin(ph) * Math.sin(al) * len;
      const dx = x - px;
      const dy = y - py;
      const dz = z - pz;
      const inv = 1 / Math.max(1e-6, Math.hypot(dx, dy, dz));
      const t = s / arc;
      W.stroke(x, y, z, dx * inv, dy * inv, dz * inv, fstep * 0.65, 0.007, 0.32 + 0.25 * (1 - t),
        P.filament[0], P.filament[1], P.filament[2]);
      if (rng.next() < 0.012) {
        W.blob(x, y, z, rng.float(0.025, 0.045), 1, P.pearl[0], P.pearl[1], P.pearl[2]);
      }
      px = x;
      py = y;
      pz = z;
    }
  }
  W.blob(tx0, top + 0.4, tz0, Rc * 0.55, 0.06, P.glowCool[0], P.glowCool[1], P.glowCool[2]);
  return { splats: W.splats, count: W.count, radius: rBase * 1.9, height: Hs + Rc * flat, kind: 'fan' };
}

// ---------------------------------------------------------------------------
// 01 · Curled frost fern
// ---------------------------------------------------------------------------
export function makeCurl(seed, density = 1) {
  const rng = new Rng(seed);
  const nz = new Noise(seed + 505);
  const W = new SplatWriter(30000);
  const sq = Math.sqrt(density);
  const cell = {};
  const curls = rng.int(1, 3);
  let maxH = 0;
  for (let k = 0; k < curls; k++) {
    const L = rng.float(4, 8) * (k === 0 ? 1 : rng.float(0.5, 0.75));
    const r0 = rng.float(0.32, 0.5) * (L / 7);
    const az = rng.float(0, TAU);
    const ox = k === 0 ? 0 : rng.float(-0.8, 0.8);
    const oz = k === 0 ? 0 : rng.float(-0.8, 0.8);
    const ca = Math.cos(az);
    const sa = Math.sin(az);
    // planar curve (u = sideways, v = up) built by integrating curvature
    let u = 0;
    let v = 0;
    let ang = Math.PI / 2 + rng.float(-0.12, 0.12);
    const ds = 0.04 / sq;
    for (let s = 0; s < L; s += ds) {
      const t = s / L;
      const kappa = 0.05 + 2.6 * Math.pow(t, 2.4) / Math.max(0.8, L * 0.25);
      ang -= kappa * ds;
      u += Math.cos(ang) * ds;
      v += Math.sin(ang) * ds;
      maxH = Math.max(maxH, v);
      const r = r0 * (1 - 0.78 * Math.pow(t, 0.85)) + 0.02;
      // frame: tangent T in the (u,v) plane, N = in-plane normal on the outside of
      // the clockwise curl (left of T), B = plane normal
      const Tu = Math.cos(ang);
      const Tv = Math.sin(ang);
      const Nu = -Tv;
      const Nv = Tu;
      const ring = Math.max(6, Math.ceil((TAU * r) / (0.045 / sq)));
      for (let j = 0; j < ring; j++) {
        const be = ((j + rng.next() * 0.8) / ring) * TAU;
        const cb = Math.cos(be);
        const sb = Math.sin(be);
        // local offset in (N, B) plane
        const lu = Nu * cb * r;
        const lv = Nv * cb * r;
        const lb = sb * r;
        const nnU = Nu * cb;
        const nnV = Nv * cb;
        const nnB = sb;
        // to world: u axis = (ca, 0, sa), B axis = (-sa, 0, ca)
        const x = ox + (u + lu) * ca - lb * sa;
        const y = v + lv;
        const z = oz + (u + lu) * sa + lb * ca;
        const wnx = nnU * ca - nnB * sa;
        const wny = nnV;
        const wnz = nnU * sa + nnB * ca;
        cellular2(s * 2.2, be * r * 3.2, seed + k, cell);
        const vein = cell.f2 - cell.f1 < 0.09;
        mixC(P.bark, P.lace, smoothstep(0.1, 0.8, t));
        towardC(P.frost, clamp(0.25 + 0.6 * cb, 0, 1) * smoothstep(0.2, 0.9, t) * 0.7);
        if (vein) towardC(P.laceGold, 0.55);
        scaleC(lit(wnx, wny, wnz, 0.55 + 0.45 * smoothstep(0, 2, y)) * (vein ? 1.15 : 1));
        W.disk(x, y, z, wnx, wny, wnz, 0.04 / sq, 0.012, 0.95, C[0], C[1], C[2]);
        if (vein && rng.next() < 0.03) {
          W.blob(x + wnx * 0.03, y + wny * 0.03, z + wnz * 0.03, rng.float(0.025, 0.05), 1, P.pearl[0], P.pearl[1], P.pearl[2]);
        }
      }
      // fringe hairs along the outer edge of the curl
      if (t > 0.3 && rng.next() < 0.7) {
        const hl = rng.float(0.12, 0.38) * (0.6 + t);
        const hx = Nu * ca;
        const hy = Nv;
        const hz = Nu * sa;
        const bx = ox + (u + Nu * r) * ca;
        const by = v + Nv * r;
        const bz = oz + (u + Nu * r) * sa;
        W.stroke(bx + hx * hl * 0.5, by + hy * hl * 0.5, bz + hz * hl * 0.5, hx, hy, hz, hl * 0.5, 0.005, 0.5,
          P.frost[0], P.frost[1], P.frost[2]);
      }
    }
  }
  // mossy base
  for (let i = 0; i < 500 * density; i++) {
    const a = rng.float(0, TAU);
    const rr = Math.sqrt(rng.next()) * 0.9;
    mixC(P.mossDark, P.moss, rng.next());
    W.blob(Math.cos(a) * rr, (0.9 - rr) * 0.25 * rng.next(), Math.sin(a) * rr, 0.05, 0.9, C[0], C[1], C[2]);
  }
  return { splats: W.splats, count: W.count, radius: 0.7, height: maxH, kind: 'curl' };
}

// Shared colour scratch + helpers for other procedural builders (ground chunks).
export { C, mixC, towardC, scaleC, setC, lit };

export const GENERATORS = {
  trunk: makeTrunk,
  mound: makeMound,
  spores: makeSpores,
  fan: makeFanTree,
  curl: makeCurl,
};

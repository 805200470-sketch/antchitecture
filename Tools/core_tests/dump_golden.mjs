// Writes golden.json from the JavaScript prototype (the reference implementation) so the C# port
// in UnityProject.nosync/Assets/AntGarden/Scripts/Core can be checked against it.
//
//   node dump_golden.mjs [out.json]
//
// Env overrides: ANT_GARDEN_SRC (default ~/Desktop/AntGarden/06_Prototypes/Ant_Garden_Web_Demo/src),
//                ANT_GARDEN_NETWORKS (default ~/Desktop/AntGarden/04_Paths/Networks).
// Read-only with respect to the JS project.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { makeFdlibm, plainMulAdd, exactFma } from './fdlibm_ref.mjs';

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SRC = process.env.ANT_GARDEN_SRC ?? `${process.env.HOME}/Desktop/AntGarden/06_Prototypes/Ant_Garden_Web_Demo/src`;
const NETS = process.env.ANT_GARDEN_NETWORKS ?? '/Users/xujiafeng/Desktop/AntGarden/04_Paths/Networks';
const OUT = process.argv[2] ?? path.join(HERE, 'golden.json');

const imp = (f) => import(pathToFileURL(path.join(SRC, f)).href);
const { CONFIG } = await imp('config.js');
const { PathField, validateNetwork } = await imp('pathfield.js');
const { stitchModules } = await imp('stitch.js');
const { Rng, Noise, ihash, cellular2, clamp, lerp, smoothstep } = await imp('noise.js');

/** three.js r180 Vector3, only what PathField.normal() touches (set → normalize). */
class Vector3 {
  constructor(x = 0, y = 0, z = 0) { this.x = x; this.y = y; this.z = z; }
  set(x, y, z) { if (z === undefined) z = this.z; this.x = x; this.y = y; this.z = z; return this; }
  length() { return Math.sqrt(this.x * this.x + this.y * this.y + this.z * this.z); }
  multiplyScalar(s) { this.x *= s; this.y *= s; this.z *= s; return this; }
  divideScalar(s) { return this.multiplyScalar(1 / s); }
  normalize() { return this.divideScalar(this.length() || 1); }
}

const loadNet = (id) => JSON.parse(fs.readFileSync(path.join(NETS, `${id}.json`), 'utf8'));

// FNV-1a over raw bytes: bit-exactness fingerprints for float32 grids and float64 geometry.
function fnv(bytes) {
  let h = 0x811c9dc5;
  for (let i = 0; i < bytes.length; i++) {
    h ^= bytes[i];
    h = Math.imul(h, 0x01000193);
  }
  return h >>> 0;
}
const hashF32 = (a) => fnv(new Uint8Array(a.buffer, a.byteOffset, a.byteLength));
const hashF64 = (list) => fnv(new Uint8Array(Float64Array.from(list).buffer));
const sum = (a) => { let s = 0; for (let i = 0; i < a.length; i++) s += a[i]; return s; };

/** ~3000 points spread over the bounds (R2 low-discrepancy sequence, 4% outside), plus special points. */
function samplePoints(f, n = 3000) {
  const { minX, maxX, minZ, maxZ } = f.bounds;
  const padX = (maxX - minX) * 0.04;
  const padZ = (maxZ - minZ) * 0.04;
  const g = 1.324717957244746; // plastic number
  const a1 = 1 / g;
  const a2 = 1 / (g * g);
  const xs = [];
  const zs = [];
  for (let k = 0; k < n; k++) {
    const u = (0.5 + a1 * k) % 1;
    const v = (0.5 + a2 * k) % 1;
    xs.push(minX - padX + u * (maxX - minX + 2 * padX));
    zs.push(minZ - padZ + v * (maxZ - minZ + 2 * padZ));
  }
  // exact grid nodes, far outside, the spawn point, node positions and exits
  const gr = f.grid;
  for (let k = 0; k < 40; k++) {
    xs.push(gr.minX + ((k * 37) % gr.nx) * gr.cell);
    zs.push(gr.minZ + ((k * 53) % gr.nz) * gr.cell);
  }
  xs.push(minX - 50, maxX + 50, 0, minX, maxX);
  zs.push(minZ - 50, maxZ + 50, 0, minZ, maxZ);
  const sp = f.spawnPoint();
  xs.push(sp.x); zs.push(sp.z);
  for (const nd of f.nodes) { xs.push(nd.x); zs.push(nd.z); }
  for (const ex of f.exits) { xs.push(ex.x); zs.push(ex.z); }
  return { xs, zs };
}

function describe(f) {
  const { xs, zs } = samplePoints(f);
  const v3 = new Vector3();
  const out = { pathDist: [], dist: [], centerDist: [], halfWidth: [], height: [], hexDist: [], normal: [], grad: [] };
  const grad = { x: 0, z: 0 };
  for (let k = 0; k < xs.length; k++) {
    const x = xs[k];
    const z = zs[k];
    out.pathDist.push(f.pathDist(x, z));
    out.dist.push(f.dist(x, z));
    out.centerDist.push(f.centerDist(x, z));
    out.halfWidth.push(f.halfWidth(x, z));
    out.height.push(f.height(x, z));
    out.hexDist.push(f.hexDist(x, z));
    f.normal(x, z, v3);
    out.normal.push(v3.x, v3.y, v3.z);
    f.gradDist(x, z, grad);
    out.grad.push(grad.x, grad.z);
  }
  const g = f.grid;
  return {
    name: f.image.name,
    widthMode: f.widthMode,
    R: f.hex.R,
    apothem: f.hex.apothem,
    scale: f.scale,
    bounds: f.bounds,
    hexCenters: f.hexCenters.map((c) => [c.x, c.z]),
    totalLength: f.totalLength,
    walkableArea: f.walkableArea,
    grid: {
      nx: g.nx, nz: g.nz, minX: g.minX, minZ: g.minZ, cell: g.cell,
      hash: { d: hashF32(g.d), c: hashF32(g.c), h: hashF32(g.h), hw: hashF32(g.hw) },
      sum: { d: sum(g.d), c: sum(g.c), h: sum(g.h), hw: sum(g.hw) },
    },
    nodes: f.nodes.map((n) => ({
      id: n.id, type: n.type ?? null, boundary: n.boundary, x: n.x, z: n.z, hw: n.hw, degree: n.degree, clear: n.clear, inside: n.inside,
    })),
    edges: f.edges.map((e) => ({
      id: e.id, source: e.source ?? null, target: e.target ?? null, connector: !!e.connector, inferred: !!e.inferred,
      hw: e.hw, length: e.length, n: e.pts.length,
      pts: e.pts.flat(), hws: e.hws, ptsHash: hashF64(e.pts.flat()), hwsHash: hashF64(e.hws),
    })),
    exits: f.exits,
    spawn: f.spawnPoint(),
    frameEdges: f.frameEdges,
    modules: f.modules ?? [],
    samples: { x: xs, z: zs, ...out },
  };
}

const cases = [
  { name: 'single path-013', kind: 'single', ids: ['path-013'] },
  { name: 'single path-010', kind: 'single', ids: ['path-010'] },
  { name: 'single path-001', kind: 'single', ids: ['path-001'] },
  { name: 'row path-010+path-013', kind: 'stitch', layout: 'row', ids: ['path-010', 'path-013'] },
  { name: 'row path-006+path-010+path-013', kind: 'stitch', layout: 'row', ids: ['path-006', 'path-010', 'path-013'] },
  { name: 'cluster path-001+path-002+path-003', kind: 'stitch', layout: 'cluster', ids: ['path-001', 'path-002', 'path-003'] },
];

const results = [];
for (const c of cases) {
  const t0 = performance.now();
  let field;
  let report = null;
  if (c.kind === 'single') {
    field = new PathField(loadNet(c.ids[0]), CONFIG);
  } else {
    const r = stitchModules(c.ids.map(loadNet), c.ids, CONFIG, c.layout);
    field = r.field;
    report = r.report;
  }
  const ms = performance.now() - t0;
  results.push({ ...c, layout: c.layout ?? null, jsMs: ms, report, field: describe(field) });
  console.log(`${c.name}: ${ms.toFixed(0)} ms, ${field.nodes.length} nodes, ${field.edges.length} edges, ${field.exits.length} exits, grid ${field.grid.nx}x${field.grid.nz}${report ? `, ${JSON.stringify(report)}` : ''}`);
}

// ---- RNG / noise / helper values -----------------------------------------------------------
const rng = [];
for (const seed of [CONFIG.seed, CONFIG.seed + 11, CONFIG.seed + 77, 0, 1, -1, 123456789.75, 4294967296 + 5, -2147483649, 2 ** 31]) {
  const r = new Rng(seed);
  const next = [];
  for (let k = 0; k < 8; k++) next.push(r.next());
  rng.push({
    seed, next,
    float: r.float(-3, 7), floatDefault: r.float(), int: r.int(2, 9), pick: r.pick([10, 20, 30, 40, 50]),
    chance: r.chance(0.5), gauss: r.gauss(), after: r.next(),
  });
}
const nz = new Noise(CONFIG.seed + 11);
const nz2 = new Noise(7);
const pts2 = [[0, 0], [0.5, 0.25], [-1.75, 3.5], [12.3, -45.6], [99.9, 0.03], [-300.25, 127.5], [1e5 + 0.5, -2e4 + 0.25], [3.3, 3.3], [-0.0001, 0.0001], [255.5, 256.5]];
const noise = {
  noise2: pts2.map(([x, y]) => [x, y, nz.noise2(x, y), nz2.noise2(x, y)]),
  noise3: pts2.map(([x, y], k) => [x, y, k * 0.7 - 2, nz.noise3(x, y, k * 0.7 - 2), nz2.noise3(x, y, k * 0.7 - 2)]),
  fbm2: pts2.map(([x, y]) => [x, y, nz.fbm2(x, y), nz.fbm2(x * 0.06, y * 0.06, 3), nz.fbm2(x, y, 5, 2.1, 0.45)]),
  fbm3: pts2.map(([x, y], k) => [x, y, k * 0.3, nz.fbm3(x, y, k * 0.3), nz.fbm3(x, y, k * 0.3, 2, 1.9, 0.6)]),
};
const ihashes = [[0, 0, 0, 0], [-5, 7, 0, 20261002], [123456, -98765, 3, -1], [2.7, -3.2, 1, 99.9], [2147483647, -2147483648, 5, 4294967296 + 9], [-1, -1, -1, -1]]
  .map(([x, y, z, s]) => [x, y, z, s, ihash(x, y, z, s)]);
const cells = [[0.3, 0.7, 1], [-2.5, 4.25, 20261002], [10.01, -3.99, -7], [123.456, 789.012, 42]].map(([x, y, s]) => {
  const o = cellular2(x, y, s, {});
  return [x, y, s, o.f1, o.f2, o.r1, o.r2];
});
const helpers = {
  hypot: [[3, 4], [0, 0], [-0, 5], [1e-300, 1e-300], [1e300, 1e300], [0.1, 0.2], [-7.25, 1e-9], [33.3333, -0.0001], [2.220446049250313e-16, 1]].map(([a, b]) => [a, b, Math.hypot(a, b)]),
  round: [0.5, 1.5, 2.5, -0.5, -1.5, -2.5, 0.49999999999999994, 4503599627370495.5, -4503599627370495.5, 1.4999999999999998, 7.2, -7.8].map((x) => [x, Math.round(x)]),
  clamp: [[5, 0, 1], [-1, 0, 1], [0.3, 0, 1], [2, 3, 1]].map(([v, a, b]) => [v, a, b, clamp(v, a, b)]),
  lerp: [[0, 10, 0.25], [-3, 7, 1.5], [0.8, 1.4, 0.123456789]].map(([a, b, t]) => [a, b, t, lerp(a, b, t)]),
  smoothstep: [[0.45, 0.85, 0.6], [-0.9, 4.5, -2], [-0.9, 4.5, 1.234], [3.15, 8.1, 9]].map(([a, b, v]) => [a, b, v, smoothstep(a, b, v)]),
};

// ---- transcendental functions ---------------------------------------------------------------
// The port carries its own fdlibm (Fdlibm.cs), the library V8 uses. Rows are [args..., V8, fdlibm]:
// the C# port must equal the fdlibm column bit for bit; the V8 column is reported alongside. On
// Apple Silicon V8 is built with fused multiply-adds, which the FMA variant below reproduces exactly.
const plain = makeFdlibm(plainMulAdd);
const fused = makeFdlibm(exactFma);
const det = new Rng(12345);
const libm = { sin: [], cos: [], atan2: [], pow: [], log: [] };
const libmStats = {};
const tally = (name, v8, p, f) => {
  const s = (libmStats[name] ??= { n: 0, v8EqualsFdlibm: 0, v8EqualsFdlibmFma: 0 });
  s.n++;
  if (Object.is(v8, p)) s.v8EqualsFdlibm++;
  if (Object.is(v8, f)) s.v8EqualsFdlibmFma++;
};
const angles = [];
for (let k = 0; k < 6; k++) {
  angles.push(k * (Math.PI / 3)); // stitch rotations
  const a0 = Math.PI / 2 + (k * Math.PI) / 3; // hexagon vertices and edge normals
  angles.push(a0, a0 + Math.PI / 3, a0 + Math.PI / 6);
}
angles.push(0, -0, Math.PI, -Math.PI, Math.PI / 4, 1e-9, -1e-12, 3e-8, 2.356194490192345, 4 * Math.PI, 100, -1234.5, 823549.5);
for (let k = 0; k < 1500; k++) angles.push(det.float(-Math.PI, Math.PI), 2 * Math.PI * det.next(), det.float(-40, 40));
for (const a of angles) {
  libm.sin.push([a, Math.sin(a), plain.sin(a)]);
  libm.cos.push([a, Math.cos(a), plain.cos(a)]);
  tally('sin', Math.sin(a), plain.sin(a), fused.sin(a));
  tally('cos', Math.cos(a), plain.cos(a), fused.cos(a));
}
const atanArgs = [[0, 0], [-0, 0], [0, -0], [-0, -0], [1, 0], [-1, 0], [0, -1], [1, 1], [-1, -1], [3, 1], [1, 3], [1e-300, 1], [1, 1e-300], [-1e-300, -1],
  [Infinity, 1], [1, Infinity], [Infinity, -Infinity], [-Infinity, -Infinity], [5, -Infinity], [-5, Infinity], [2, 1], [0.4, 1], [1e20, -1]];
for (let k = 0; k < 3000; k++) atanArgs.push([det.float(-60, 60), det.float(-60, 60)], [det.float(-1, 1), det.float(-1, 1) * 1e-3]);
for (const [y, x] of atanArgs) {
  libm.atan2.push([y, x, Math.atan2(y, x), plain.atan2(y, x)]);
  tally('atan2', Math.atan2(y, x), plain.atan2(y, x), fused.atan2(y, x));
}
for (let k = 0; k < 400; k++) {
  const x = det.float(0.5, 2);
  libm.pow.push([x, 1.5, x ** 1.5]);
}
for (const x of [1, 2, 0.5, 1e-9, 1 - 1e-12, 1 + 1e-12, 5e-324, 1e300]) libm.log.push([x, Math.log(x), plain.log(x)]);
for (let k = 0; k < 1000; k++) {
  const x = det.float(1e-9, 1);
  libm.log.push([x, Math.log(x), plain.log(x)]);
  tally('log', Math.log(x), plain.log(x), fused.log(x));
}
for (const [name, s] of Object.entries(libmStats)) {
  console.log(`V8 Math.${name}: ${s.v8EqualsFdlibm}/${s.n} equal plain fdlibm, ${s.v8EqualsFdlibmFma}/${s.n} equal fdlibm with fused multiply-add`);
}

// ---- validateNetwork messages for malformed input -------------------------------------------
const validation = [
  ['a number', '42'],
  ['null', 'null'],
  ['an array', '[]'],
  ['no schemaVersion', '{}'],
  ['schemaVersion 1.0', '{"schemaVersion":"1.0"}'],
  ['numeric schemaVersion 2.5, no image', '{"schemaVersion":2.5}'],
  ['image without height', '{"schemaVersion":"2.5","image":{"width":10}}'],
  ['no nodes array', '{"schemaVersion":"2.5","image":{"width":10,"height":10},"edges":[]}'],
  ['empty edges', '{"schemaVersion":"2.5","image":{"width":10,"height":10},"nodes":[],"edges":[]}'],
  ['minimal valid', '{"schemaVersion":"2.5","image":{"width":10,"height":10},"nodes":[],"edges":[{"id":"e0"}]}'],
].map(([name, json]) => {
  try {
    validateNetwork(JSON.parse(json));
    return { name, json, error: null };
  } catch (err) {
    return { name, json, error: err.message };
  }
});

const pick = (o, keys) => Object.fromEntries(keys.map((k) => [k, o[k] ?? null]));
const golden = {
  generatedAt: new Date().toISOString(),
  node: process.version,
  src: SRC,
  networks: NETS,
  config: {
    seed: CONFIG.seed,
    path: pick(CONFIG.path, ['worldSize', 'pxToWorld', 'widthMode', 'lineDrawingMaxWidth', 'measuredInfluence', 'fixedWidth', 'edgeWidthRange',
      'widthVariation', 'widthFrequency', 'pinch', 'minHalfWidth', 'maxHalfWidth', 'chamberNoise', 'chamberMin', 'chamberMax', 'pondChance',
      'widthScale', 'minWidth', 'maxWidth', 'junctionClearing', 'endpointClearing', 'resampleStep', 'defaultSample']),
    field: pick(CONFIG.field, ['cell']),
    frame: pick(CONFIG.frame, ['fill', 'borderMargin', 'innerPad', 'wallMargin', 'sideDepth', 'exitRadius', 'forceR']),
    ground: pick(CONFIG.ground, ['undulation', 'bankHeight', 'bankWidth', 'bankLumps', 'plateauNoise']),
    stitch: pick(CONFIG.stitch, ['portInset', 'linkBulge']),
  },
  cases: results,
  rng,
  noise,
  ihash: ihashes,
  cellular2: cells,
  helpers,
  libm,
  libmStats,
  validation,
};
// JSON cannot carry NaN, ±Infinity or -0: write them as strings.
const replacer = (k, v) => (typeof v !== 'number' ? v : !Number.isFinite(v) ? String(v) : Object.is(v, -0) ? '-0' : v);
fs.writeFileSync(OUT, JSON.stringify(golden, replacer));
console.log(`wrote ${OUT} (${(fs.statSync(OUT).size / 1e6).toFixed(1)} MB)`);

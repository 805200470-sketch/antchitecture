// Turns a Path Recognize network (schemaVersion 2.x) into a walkable canyon
// framed by a regular hexagon:
//   pathDist(x, z)  signed distance to the canyon edge (negative = canyon floor)
//   dist(x, z)      walkable distance: canyon AND inside the hexagon frame
//   height(x, z)    floor → moss bank → plateau profile
// Everything the player can touch is derived from the recognized path, never
// from the splat visuals.
import { Noise, Rng, clamp, lerp, smoothstep } from './noise.js';

const SQRT3 = Math.sqrt(3);

/** Throws a readable error when the JSON is not a usable Path Recognize network. */
export function validateNetwork(net) {
  if (!net || typeof net !== 'object') throw new Error('不是 JSON 对象');
  const v = String(net.schemaVersion ?? '');
  if (!v.startsWith('2.')) throw new Error(`不支持的 schemaVersion「${v || '缺失'}」，需要 Path Recognize 导出的 2.x network`);
  if (!(net.image?.width > 0 && net.image?.height > 0)) throw new Error('缺少 image.width / image.height');
  if (!Array.isArray(net.nodes) || !Array.isArray(net.edges)) throw new Error('缺少 nodes 或 edges 数组');
  if (net.edges.length === 0) throw new Error('这张图没有识别出路径（edges 为空）');
}

function polyLength(pts) {
  let len = 0;
  for (let i = 1; i < pts.length; i++) len += Math.hypot(pts[i][0] - pts[i - 1][0], pts[i][1] - pts[i - 1][1]);
  return len;
}

/** Resample a polyline to roughly uniform spacing, keeping both endpoints. */
function resample(pts, step) {
  const total = polyLength(pts);
  if (total < step * 1.5) return [pts[0], pts[pts.length - 1]];
  const n = Math.max(1, Math.round(total / step));
  const out = [pts[0]];
  let seg = 1;
  let segStart = 0;
  let segLen = Math.hypot(pts[1][0] - pts[0][0], pts[1][1] - pts[0][1]);
  for (let k = 1; k < n; k++) {
    const target = (k / n) * total;
    while (segStart + segLen < target && seg < pts.length - 1) {
      segStart += segLen;
      seg++;
      segLen = Math.hypot(pts[seg][0] - pts[seg - 1][0], pts[seg][1] - pts[seg - 1][1]);
    }
    const t = segLen > 0 ? (target - segStart) / segLen : 0;
    const a = pts[seg - 1];
    const b = pts[seg];
    out.push([a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t]);
  }
  out.push(pts[pts.length - 1]);
  return out;
}

function segmentDistance(px, pz, ax, az, bx, bz, out) {
  const vx = bx - ax;
  const vz = bz - az;
  const wx = px - ax;
  const wz = pz - az;
  const len2 = vx * vx + vz * vz;
  const t = len2 > 0 ? clamp((wx * vx + wz * vz) / len2, 0, 1) : 0;
  const dx = wx - vx * t;
  const dz = wz - vz * t;
  out.t = t;
  return Math.sqrt(dx * dx + dz * dz);
}

/**
 * Outline of one or more pointy-top hexagons that share edges (a stitched world).
 * `dist` is the signed distance to the outer rim — negative inside — so the seams
 * between neighbouring hexagons are interior floor, not walls.
 */
export class HexFrame {
  /**
   * @param {{x: number, z: number}[]} centers hexagon centres
   * @param {number} R circumradius (centre to vertex)
   */
  constructor(centers, R) {
    this.centers = centers;
    this.R = R;
    this.apothem = (R * SQRT3) / 2;
    this.edges = [];
    for (const c of centers) {
      for (let k = 0; k < 6; k++) {
        const a0 = Math.PI / 2 + (k * Math.PI) / 3;
        const a1 = a0 + Math.PI / 3;
        const am = a0 + Math.PI / 6; // edge midpoint direction
        const nx = c.x + Math.cos(am) * 2 * this.apothem;
        const nz = c.z + Math.sin(am) * 2 * this.apothem;
        this.edges.push({
          x0: c.x + R * Math.cos(a0),
          z0: c.z + R * Math.sin(a0),
          x1: c.x + R * Math.cos(a1),
          z1: c.z + R * Math.sin(a1),
          cx: c.x,
          cz: c.z,
          shared: centers.some((o) => o !== c && Math.hypot(o.x - nx, o.z - nz) < 1),
        });
      }
    }
    this.rim = this.edges.filter((e) => !e.shared);
  }

  /** Signed distance to the outer rim (negative inside the union of hexagons). */
  dist(x, z) {
    let best = Infinity;
    for (const c of this.centers) {
      const ax = Math.abs(x - c.x);
      const az = Math.abs(z - c.z);
      const d = Math.max(ax, ax * 0.5 + az * 0.8660254) - this.apothem;
      if (d < best) best = d;
    }
    if (this.centers.length === 1) return best;
    let m = Infinity;
    for (const e of this.rim) {
      const ex = e.x1 - e.x0;
      const ez = e.z1 - e.z0;
      const t = clamp(((x - e.x0) * ex + (z - e.z0) * ez) / (ex * ex + ez * ez), 0, 1);
      const d = Math.hypot(x - e.x0 - ex * t, z - e.z0 - ez * t);
      if (d < m) m = d;
    }
    return best < 1e-6 ? -m : m;
  }
}

export class PathField {
  /**
   * @param {object} network Path Recognize network JSON (schemaVersion 2.x)
   * @param {object} cfg CONFIG (path, frame, field and ground sections are used)
   */
  constructor(network, cfg, composite = null) {
    this.cfg = cfg;
    this.noise = new Noise(cfg.seed + 11);
    this.hexCenters = [{ x: 0, z: 0 }];
    const rawNodes = composite ? this._initComposite(composite) : this._initNetwork(network);
    const pc = cfg.path;

    const byId = new Map();
    for (const n of rawNodes) {
      byId.set(n.id, { id: n.id, x: n.x, z: n.z, type: n.type, boundary: !!n.boundary, hw: 0, degree: 0, clear: 0 });
    }
    for (const e of this.edges) {
      for (const id of [e.source, e.target]) {
        const n = byId.get(id);
        if (n) {
          n.hw = Math.max(n.hw, e.hw);
          n.degree += e.source === e.target ? 2 : 1;
        }
      }
    }
    this.nodes = [...byId.values()].filter((n) => n.degree > 0);
    for (const n of this.nodes) {
      n.clear = n.hw * (n.degree >= 3 ? pc.junctionClearing : n.degree === 1 ? pc.endpointClearing : 1);
      if (n.degree !== 2) n.clear = clamp(n.clear, pc.chamberMin, pc.chamberMax);
      n.inside = this.hexDist(n.x, n.z) < -2;
    }
    this.totalLength = this.edges.reduce((a, e) => a + e.length, 0);

    // bounds: union of all module hexagons
    const m = 1.5;
    const R = this.hex.R;
    const a = this.hex.apothem;
    const xs = this.hexCenters.map((c) => c.x);
    const zs = this.hexCenters.map((c) => c.z);
    this.bounds = {
      minX: Math.min(...xs) - a - m,
      maxX: Math.max(...xs) + a + m,
      minZ: Math.min(...zs) - R - m,
      maxZ: Math.max(...zs) + R + m,
    };
    this.pathBounds = this.bounds;
    this.frameEdges = this._frameEdges();
    this._buildGrids();
    this.exits = this._findExits();
  }

  /** Stitched worlds: edges and nodes already in world units, several hexagons. */
  _initComposite(c) {
    this.network = null;
    this.image = { width: 0, height: 0, name: c.name ?? '' };
    this.scale = 1;
    this.toWorld = (x, z) => [x, z];
    this.hex = { R: c.R, apothem: (c.R * SQRT3) / 2 };
    this.hexCenters = c.centers;
    this.widthMode = 'designed';
    this.edges = c.edges.map((e) => ({ inferred: false, ...e, hw: Math.max(...e.hws), length: polyLength(e.pts) }));
    this.modules = c.modules ?? [];
    return c.nodes;
  }

  /** Hexagon edges of the (possibly stitched) frame; `shared` marks seams between modules. */
  _frameEdges() {
    return new HexFrame(this.hexCenters, this.hex.R).edges;
  }

  _initNetwork(network) {
    validateNetwork(network);
    this.network = network;
    const cfg = this.cfg;
    const pc = cfg.path;
    const W = network.image.width;
    const H = network.image.height;
    const s = pc.worldSize ? pc.worldSize / Math.max(W, H) : pc.pxToWorld;
    this.scale = s;
    this.image = { width: W, height: H, name: network.image.name ?? '' };
    this.toWorld = (px, py) => [(px - W / 2) * s, (py - H / 2) * s];

    // Regular pointy-top hexagon. Start from the one inscribed in the image, then grow it
    // until every inner feature (junctions, loops, spirals) fits; only the tails that run
    // off the image edge may cross the frame, and they are extended to reach it.
    const halfW = (W * s) / 2;
    const halfH = (H * s) / 2;
    const borderW = cfg.frame.borderMargin * Math.max(W, H) * s;
    const nearBorder = (x, z) => Math.abs(x) > halfW - borderW || Math.abs(z) > halfH - borderW;
    const support = (x, z) => Math.max(Math.abs(x), Math.abs(x) * 0.5 + Math.abs(z) * 0.8660254) / 0.8660254;
    let R = Math.min((W * s) / SQRT3, (H * s) / 2) * cfg.frame.fill;
    for (const e of network.edges) {
      for (const [px, py] of e.points ?? []) {
        const [x, z] = this.toWorld(px, py);
        if (!nearBorder(x, z)) R = Math.max(R, support(x, z) + cfg.frame.innerPad);
      }
    }
    if (cfg.frame.forceR) R = Math.max(R, cfg.frame.forceR); // stitched modules share one size
    this.hex = { R, apothem: (R * SQRT3) / 2 };

    const widths = network.edges
      .map((e) => e.meanWidthPx)
      .filter((w) => Number.isFinite(w) && w > 0)
      .sort((a, b) => a - b);
    const medianPx = widths.length ? widths[widths.length >> 1] : 20;
    // Canyon width is a design decision. 'designed' (default): every path gets its own
    // character around fixedWidth, and the photo's measured widths only say which paths
    // are broader or narrower than the rest. 'measured': the photo's widths as they are.
    // 'auto': measured, unless the drawing is hairline.
    const mode = pc.widthMode ?? 'designed';
    this.widthMode = mode === 'auto' ? (medianPx * s < pc.lineDrawingMaxWidth ? 'designed' : 'measured') : mode;

    const edgeRng = new Rng(cfg.seed + 77);
    this.edges = network.edges
      .filter((e) => Array.isArray(e.points) && e.points.length >= 2)
      .map((e, ei) => {
        const pts = resample(e.points.map(([x, y]) => this.toWorld(x, y)), pc.resampleStep);
        let hws;
        if (this.widthMode !== 'measured') {
          // Every path gets its own character: some are broad avenues, some narrow
          // tunnels, and each swells and pinches along its length.
          const rel = e.meanWidthPx > 0 ? clamp((e.meanWidthPx / medianPx) ** pc.measuredInfluence, 0.7, 1.45) : 1;
          const mult = lerp(pc.edgeWidthRange[0], pc.edgeWidthRange[1], edgeRng.next()) * rel;
          let acc = 0;
          hws = pts.map((p, i) => {
            if (i > 0) acc += Math.hypot(p[0] - pts[i - 1][0], p[1] - pts[i - 1][1]);
            const v = this.noise.noise2(acc * pc.widthFrequency + ei * 17.3, ei * 3.1);
            const pinch = smoothstep(0.45, 0.85, this.noise.noise2(acc * 0.03 + 99 + ei, ei * 1.7)) * pc.pinch;
            const hw = (pc.fixedWidth / 2) * mult * (1 + pc.widthVariation * v) * (1 - pinch);
            return clamp(hw, pc.minHalfWidth, pc.maxHalfWidth);
          });
        } else {
          const w = clamp((e.meanWidthPx > 0 ? e.meanWidthPx : medianPx) * s * pc.widthScale, pc.minWidth, pc.maxWidth);
          hws = pts.map(() => w / 2);
        }
        // Tails that ran off the image edge are continued straight on until they cross the frame.
        for (const atEnd of [true, false]) {
          const list = atEnd ? pts : pts.slice().reverse();
          const hl = atEnd ? hws : hws.slice().reverse();
          const [lx, lz] = list[list.length - 1];
          if (!nearBorder(lx, lz) || list.length < 2) continue;
          const [qx, qz] = list[list.length - 2];
          const len = Math.hypot(lx - qx, lz - qz) || 1;
          const dx = (lx - qx) / len;
          const dz = (lz - qz) / len;
          let k = 1;
          while (this.hexDist(lx + dx * k * pc.resampleStep, lz + dz * k * pc.resampleStep) < 3 && k < 400) {
            list.push([lx + dx * k * pc.resampleStep, lz + dz * k * pc.resampleStep]);
            hl.push(hl[hl.length - 1]);
            k++;
          }
          if (!atEnd) {
            pts.splice(0, pts.length, ...list.reverse());
            hws.splice(0, hws.length, ...hl.reverse());
          }
        }
        return {
          id: e.id,
          source: e.source,
          target: e.target,
          pts,
          hws,
          hw: Math.max(...hws),
          inferred: (e.inferredFraction ?? 0) > 0,
          length: polyLength(pts),
        };
      });

    return network.nodes.map((n) => {
      const [x, z] = this.toWorld(n.x, n.y);
      return { id: n.id, x, z, type: n.type, boundary: n.boundary };
    });
  }

  /**
   * Signed distance to the module frame (negative inside). Stitched worlds measure
   * to the outer rim only, so the seams between modules are open floor.
   */
  hexDist(x, z) {
    if (this.hexCenters.length > 1) return (this._frame ??= new HexFrame(this.hexCenters, this.hex.R)).dist(x, z);
    const ax = Math.abs(x - this.hexCenters[0].x);
    const az = Math.abs(z - this.hexCenters[0].z);
    return Math.max(ax, ax * 0.5 + az * 0.8660254) - this.hex.apothem;
  }

  _buildGrids() {
    const { cell } = this.cfg.field;
    const { minX, minZ, maxX, maxZ } = this.bounds;
    const nx = Math.ceil((maxX - minX) / cell) + 1;
    const nz = Math.ceil((maxZ - minZ) / cell) + 1;
    const band = Math.max(maxX - minX, maxZ - minZ);
    const far = band + 1;
    const d = new Float32Array(nx * nz).fill(far);
    const c = new Float32Array(nx * nz).fill(1e3);
    const hwAt = new Float32Array(nx * nz).fill(0);
    const seg = { t: 0 };

    const stamp = (ax, az, bx, bz, ha, hb, reach) => {
      const R = Math.max(ha, hb) + reach;
      const x0 = Math.max(0, Math.floor((Math.min(ax, bx) - R - minX) / cell));
      const x1 = Math.min(nx - 1, Math.ceil((Math.max(ax, bx) + R - minX) / cell));
      const z0 = Math.max(0, Math.floor((Math.min(az, bz) - R - minZ) / cell));
      const z1 = Math.min(nz - 1, Math.ceil((Math.max(az, bz) + R - minZ) / cell));
      for (let iz = z0; iz <= z1; iz++) {
        const pz = minZ + iz * cell;
        let idx = iz * nx + x0;
        for (let ix = x0; ix <= x1; ix++, idx++) {
          const px = minX + ix * cell;
          const dist = segmentDistance(px, pz, ax, az, bx, bz, seg);
          const hw = ha + (hb - ha) * seg.t;
          if (dist < c[idx]) {
            c[idx] = dist;
            hwAt[idx] = hw;
          }
          const v = dist - hw;
          if (v < d[idx]) d[idx] = v;
        }
      }
    };

    for (const e of this.edges) {
      for (let k = 0; k < e.pts.length - 1; k++) {
        stamp(e.pts[k][0], e.pts[k][1], e.pts[k + 1][0], e.pts[k + 1][1], e.hws[k], e.hws[k + 1], band);
      }
    }
    // Chambers at junctions and inner dead ends: irregular, organic outlines.
    const chamberNoise = this.cfg.path.chamberNoise;
    for (const n of this.nodes) {
      if (!(n.clear > 0) || n.degree === 2) continue;
      const R = n.clear * (1 + chamberNoise) + band;
      const x0 = Math.max(0, Math.floor((n.x - R - minX) / cell));
      const x1 = Math.min(nx - 1, Math.ceil((n.x + R - minX) / cell));
      const z0 = Math.max(0, Math.floor((n.z - R - minZ) / cell));
      const z1 = Math.min(nz - 1, Math.ceil((n.z + R - minZ) / cell));
      for (let iz = z0; iz <= z1; iz++) {
        const pz = minZ + iz * cell;
        for (let ix = x0; ix <= x1; ix++) {
          const px = minX + ix * cell;
          const dx = px - n.x;
          const dz = pz - n.z;
          const dist = Math.hypot(dx, dz);
          const a = Math.atan2(dz, dx);
          // low angular frequency → rounded, cave-like lobes rather than spikes
          const rr = n.clear * (1 + chamberNoise * this.noise.noise2(Math.cos(a) * 0.55 + n.x * 0.13, Math.sin(a) * 0.55 + n.z * 0.13));
          const v = dist - rr;
          const idx = iz * nx + ix;
          if (v < d[idx]) d[idx] = v;
          if (dist < c[idx]) c[idx] = dist;
        }
      }
    }

    const h = new Float32Array(nx * nz);
    for (let iz = 0; iz < nz; iz++) {
      for (let ix = 0; ix < nx; ix++) {
        const idx = iz * nx + ix;
        h[idx] = this._heightFormula(minX + ix * cell, minZ + iz * cell, d[idx]);
      }
    }
    this.grid = { minX, minZ, nx, nz, cell, d, c, h, hw: hwAt };

    let walk = 0;
    for (let iz = 0; iz < nz; iz++) {
      for (let ix = 0; ix < nx; ix++) {
        if (this.dist(minX + ix * cell, minZ + iz * cell) < 0) walk++;
      }
    }
    this.walkableArea = walk * cell * cell;
  }

  /** Canyon floor, then a mossy bank rising to a plateau where the forest stands. */
  _heightFormula(x, z, d) {
    const g = this.cfg.ground;
    const n = this.noise.fbm2(x * 0.06, z * 0.06, 3) * g.undulation;
    const bank = g.bankHeight * smoothstep(-0.9, g.bankWidth, d);
    const lumps = this.noise.fbm2(x * 0.11 + 5, z * 0.11, 3) * g.bankLumps * smoothstep(-0.2, g.bankWidth * 0.7, d);
    const plateau = this.noise.fbm2(x * 0.035 + 9, z * 0.035, 3) * g.plateauNoise * smoothstep(g.bankWidth * 0.7, g.bankWidth * 1.8, d);
    return n + bank + lumps + plateau;
  }

  /** Points where the canyon runs out through the hexagon frame. */
  _findExits() {
    const exits = [];
    const inset = this.cfg.frame.wallMargin + 1.2;
    for (const e of this.edges) {
      for (const pts of [e.pts, [...e.pts].reverse()]) {
        // walk from the inside outward; record the last point before the frame
        for (let i = 1; i < pts.length; i++) {
          const inA = this.hexDist(pts[i - 1][0], pts[i - 1][1]) < -inset;
          const inB = this.hexDist(pts[i][0], pts[i][1]) < -inset;
          if (inA && !inB) {
            const [x, z] = pts[i - 1];
            const dx = pts[i][0] - x;
            const dz = pts[i][1] - z;
            const len = Math.hypot(dx, dz) || 1;
            if (!exits.some((o) => Math.hypot(o.x - x, o.z - z) < 4)) {
              exits.push({ x, z, dirX: dx / len, dirZ: dz / len, edgeId: e.id });
            }
            break;
          }
        }
      }
    }
    return exits;
  }

  _sample(arr, x, z) {
    const g = this.grid;
    const fx = clamp((x - g.minX) / g.cell, 0, g.nx - 1.001);
    const fz = clamp((z - g.minZ) / g.cell, 0, g.nz - 1.001);
    const ix = fx | 0;
    const iz = fz | 0;
    const tx = fx - ix;
    const tz = fz - iz;
    const i = iz * g.nx + ix;
    const a = arr[i];
    const b = arr[i + 1];
    const c = arr[i + g.nx];
    const d = arr[i + g.nx + 1];
    return (a * (1 - tx) + b * tx) * (1 - tz) + (c * (1 - tx) + d * tx) * tz;
  }

  /** Signed distance to the canyon edge (negative = canyon floor). Shapes the terrain. */
  pathDist(x, z) {
    return this._sample(this.grid.d, x, z);
  }

  /** Walkable signed distance: inside the canyon and inside the hexagon frame. */
  dist(x, z) {
    return Math.max(this._sample(this.grid.d, x, z), this.hexDist(x, z) + this.cfg.frame.wallMargin);
  }

  /** Distance to the nearest recognized centerline. */
  centerDist(x, z) {
    return this._sample(this.grid.c, x, z);
  }

  /** Half-width of the nearest corridor. */
  halfWidth(x, z) {
    return this._sample(this.grid.hw, x, z);
  }

  height(x, z) {
    return this._sample(this.grid.h, x, z);
  }

  /** Gradient of the walkable distance (points away from the walkable area). */
  gradDist(x, z, out) {
    const e = this.grid.cell;
    out.x = (this.dist(x + e, z) - this.dist(x - e, z)) / (2 * e);
    out.z = (this.dist(x, z + e) - this.dist(x, z - e)) / (2 * e);
    return out;
  }

  /** Floor normal from the height field. */
  normal(x, z, out) {
    const e = this.grid.cell;
    const hx = (this.height(x + e, z) - this.height(x - e, z)) / (2 * e);
    const hz = (this.height(x, z + e) - this.height(x, z - e)) / (2 * e);
    out.set(-hx, 1, -hz).normalize();
    return out;
  }

  /** A junction near the middle of the module, with a heading along one of its edges. */
  spawnPoint() {
    const candidates = this.nodes.filter((n) => n.degree >= 3 && n.inside);
    const pool = candidates.length ? candidates : this.nodes.filter((n) => n.inside);
    const list = pool.length ? pool : this.nodes;
    let best = list[0];
    let bestScore = Infinity;
    for (const n of list) {
      const score = Math.hypot(n.x, n.z) - n.degree * 3;
      if (score < bestScore) {
        bestScore = score;
        best = n;
      }
    }
    const edge = this.edges.find((e) => e.source === best.id || e.target === best.id);
    let heading = 0;
    if (edge) {
      const pts = edge.source === best.id ? edge.pts : [...edge.pts].reverse();
      const p = pts[Math.min(4, pts.length - 1)];
      heading = Math.atan2(p[0] - best.x, p[1] - best.z);
    }
    return { x: best.x, z: best.z, heading };
  }
}

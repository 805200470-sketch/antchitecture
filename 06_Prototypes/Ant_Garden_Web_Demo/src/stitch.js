// Stitch 2–3 hexagonal nest modules into one world.
//
// 1. Every module is rebuilt at one shared hexagon size and laid out so that
//    neighbours share an edge (a row, or a triangle cluster of three).
// 2. Each module may be turned in 60° steps. All rotation combinations are
//    scored: exits that meet on a shared edge should line up (small offset
//    along the edge) and as few exits as possible should be left unmatched.
// 3. Matched exits are bridged by a smooth S-shaped connector canyon across
//    the shared edge; unmatched ones on inner edges end in a dead-end chamber;
//    exits on the outer rim stay exits.
import { HexFrame, PathField } from './pathfield.js';

const SQRT3 = Math.sqrt(3);
const STEP = Math.PI / 3;

/** Rotate (x, z) by r × 60° (from +x toward +z). */
function rotate(x, z, r) {
  const a = r * STEP;
  const c = Math.cos(a);
  const s = Math.sin(a);
  return [x * c - z * s, x * s + z * c];
}

/** Which of the six hexagon edges (normals at k × 60°) a direction points through. */
function edgeIndexOf(x, z) {
  const k = Math.round(Math.atan2(z, x) / STEP);
  return ((k % 6) + 6) % 6;
}

/**
 * Tail ends of a module's paths that run out through its frame. A tail is cut back
 * `portInset` inside the frame (or as far as it is long) so the connector canyon
 * has room to curve.
 */
function collectPorts(field, cfg) {
  const ports = [];
  const insets = [cfg.stitch?.portInset ?? 9, cfg.frame.wallMargin + 2.5];
  for (const e of field.edges) {
    for (const atEnd of [false, true]) {
      const pts = atEnd ? [...e.pts].reverse() : e.pts; // walk from the outer end inward
      if (field.hexDist(pts[0][0], pts[0][1]) < -2) continue; // this end is inside: not a tail
      for (const inset of insets) {
        let cross = pts[0];
        let i = 0;
        while (i < pts.length - 1 && field.hexDist(pts[i][0], pts[i][1]) > -inset) {
          if (field.hexDist(pts[i][0], pts[i][1]) > 0) cross = pts[i];
          i++;
        }
        if (i <= 0 || i >= pts.length - 1) continue;
        const [px, pz] = pts[i];
        const [ox, oz] = pts[Math.max(0, i - 3)];
        const len = Math.hypot(ox - px, oz - pz) || 1;
        const k = edgeIndexOf(cross[0], cross[1]);
        const tx = -Math.sin(k * STEP);
        const tz = Math.cos(k * STEP);
        ports.push({
          edge: e,
          atEnd,
          keep: atEnd ? e.pts.length - i : i + 1, // points kept after trimming (from the far end)
          k,
          s: cross[0] * tx + cross[1] * tz, // position along the edge
          point: [px, pz],
          dir: [(ox - px) / len, (oz - pz) / len],
          hw: atEnd ? e.hws[e.hws.length - 1 - i] : e.hws[i],
        });
        break;
      }
    }
  }
  return ports;
}

/** Greedy pairing of exits meeting on one shared edge; returns pairs and the cost. */
function pairUp(a, b, penalty, maxGap) {
  const pairs = [];
  const usedB = new Set();
  const cand = [];
  for (const pa of a) for (const pb of b) cand.push({ pa, pb, gap: Math.abs(pa.s - -pb.s) });
  cand.sort((x, y) => x.gap - y.gap);
  const usedA = new Set();
  let cost = 0;
  for (const c of cand) {
    if (usedA.has(c.pa) || usedB.has(c.pb) || c.gap > maxGap) continue;
    usedA.add(c.pa);
    usedB.add(c.pb);
    pairs.push(c);
    cost += c.gap;
  }
  cost += (a.length - usedA.size + b.length - usedB.size) * penalty;
  return { pairs, cost };
}

function bezier(p0, p1, p2, p3, step) {
  const approx = Math.hypot(p3[0] - p0[0], p3[1] - p0[1]) * 1.3;
  const n = Math.max(4, Math.ceil(approx / step));
  const out = [];
  for (let i = 0; i <= n; i++) {
    const t = i / n;
    const u = 1 - t;
    out.push([
      u * u * u * p0[0] + 3 * u * u * t * p1[0] + 3 * u * t * t * p2[0] + t * t * t * p3[0],
      u * u * u * p0[1] + 3 * u * u * t * p1[1] + 3 * u * t * t * p2[1] + t * t * t * p3[1],
    ]);
  }
  return out;
}

/**
 * @param {object[]} nets Path Recognize networks, one per module
 * @param {string[]} ids module ids (labels)
 * @param {object} cfg CONFIG
 * @param {'row'|'cluster'} layout
 * @returns {{ field: PathField, report: object }}
 */
export function stitchModules(nets, ids, cfg, layout = 'row') {
  const R = Math.max(...nets.map((n) => new PathField(n, cfg).hex.R));
  const cfgR = { ...cfg, frame: { ...cfg.frame, forceR: R } };
  const mods = nets.map((n) => new PathField(n, cfgR));
  const a = (R * SQRT3) / 2;

  let centers = layout === 'cluster' && mods.length === 3 ? [[0, 0], [2 * a, 0], [a, 1.5 * R]] : mods.map((_, i) => [i * 2 * a, 0]);
  const mx = centers.reduce((s, c) => s + c[0], 0) / centers.length;
  const mz = centers.reduce((s, c) => s + c[1], 0) / centers.length;
  centers = centers.map(([x, z]) => [x - mx, z - mz]);
  const frame = new HexFrame(centers.map(([x, z]) => ({ x, z })), R);
  /** A straight passage from p to q that keeps clear of the outer rim (not across a notch). */
  const clearPassage = (p, q, hw) => {
    const n = Math.max(2, Math.ceil(Math.hypot(q[0] - p[0], q[1] - p[1]) / 2));
    for (let k = 0; k <= n; k++) {
      const t = k / n;
      if (frame.dist(p[0] + (q[0] - p[0]) * t, p[1] + (q[1] - p[1]) * t) > -(hw + 3)) return false;
    }
    return true;
  };

  const adj = [];
  for (let i = 0; i < centers.length; i++) {
    for (let j = i + 1; j < centers.length; j++) {
      const dx = centers[j][0] - centers[i][0];
      const dz = centers[j][1] - centers[i][1];
      if (Math.abs(Math.hypot(dx, dz) - 2 * a) < 1) {
        const ki = edgeIndexOf(dx, dz);
        adj.push({ i, j, ki, kj: (ki + 3) % 6 });
      }
    }
  }

  const ports = mods.map((m) => collectPorts(m, cfg));
  const penalty = a * 0.9;
  const maxGap = a * 0.95;

  // Try every combination of 60° turns (the first module stays put).
  let best = null;
  const combos = 6 ** (mods.length - 1);
  for (let c = 0; c < combos; c++) {
    const rot = [0];
    let v = c;
    for (let i = 1; i < mods.length; i++) {
      rot.push(v % 6);
      v = Math.floor(v / 6);
    }
    let cost = 0;
    const matches = [];
    for (const e of adj) {
      const pa = ports[e.i].filter((p) => (p.k + rot[e.i]) % 6 === e.ki);
      const pb = ports[e.j].filter((p) => (p.k + rot[e.j]) % 6 === e.kj);
      const r = pairUp(pa, pb, penalty, maxGap);
      cost += r.cost;
      if (!r.pairs.length) cost += a * 4; // a shared edge nobody can walk across
      for (const pr of r.pairs) matches.push({ i: e.i, j: e.j, a: pr.pa, b: pr.pb, gap: pr.gap });
    }
    if (!best || cost < best.cost || (cost === best.cost && matches.length > best.matches.length)) best = { rot, cost, matches };
  }

  // Which (module, rotated edge) pairs are inner, shared edges.
  const sharedEdge = new Set();
  for (const e of adj) {
    sharedEdge.add(`${e.i}:${e.ki}`);
    sharedEdge.add(`${e.j}:${e.kj}`);
  }

  const toWorldFor = (i) => (x, z) => {
    const [rx, rz] = rotate(x, z, best.rot[i]);
    return [rx + centers[i][0], rz + centers[i][1]];
  };

  const edges = [];
  const nodes = [];
  const portNode = new Map();
  let portCount = 0;
  mods.forEach((m, i) => {
    const tw = toWorldFor(i);
    for (const n of m.nodes) {
      const [x, z] = tw(n.x, n.z);
      nodes.push({ id: `m${i}:${n.id}`, x, z, type: n.type, boundary: n.boundary });
    }
    for (const e of m.edges) {
      let pts = e.pts.map(([x, z]) => tw(x, z));
      let hws = e.hws.slice();
      let source = `m${i}:${e.source}`;
      let target = `m${i}:${e.target}`;
      for (const p of ports[i]) {
        if (p.edge !== e || !sharedEdge.has(`${i}:${(p.k + best.rot[i]) % 6}`)) continue;
        // a tail running into a neighbour: cut it back inside this module
        const id = `m${i}:port${portCount++}`;
        const [wx, wz] = tw(p.point[0], p.point[1]);
        nodes.push({ id, x: wx, z: wz, type: 'endpoint', boundary: false });
        portNode.set(p, id);
        if (p.atEnd) {
          pts = pts.slice(0, p.keep);
          hws = hws.slice(0, p.keep);
          target = id;
        } else {
          pts = pts.slice(pts.length - p.keep);
          hws = hws.slice(hws.length - p.keep);
          source = id;
        }
      }
      if (pts.length >= 2) edges.push({ id: `m${i}:${e.id}`, source, target, pts, hws });
    }
  });

  // Every shared edge must be walkable: force the closest pair when nothing matched,
  // or branch off the neighbour's nearest path when only one side has an exit.
  const extraLinks = [];
  const bridges = [];
  for (const e of adj) {
    if (best.matches.some((m) => (m.i === e.i && m.j === e.j) || (m.i === e.j && m.j === e.i))) continue;
    const pa = ports[e.i].filter((p) => (p.k + best.rot[e.i]) % 6 === e.ki);
    const pb = ports[e.j].filter((p) => (p.k + best.rot[e.j]) % 6 === e.kj);
    if (pa.length && pb.length) {
      const r = pairUp(pa, pb, penalty, Infinity);
      if (r.pairs[0]) best.matches.push({ i: e.i, j: e.j, a: r.pairs[0].pa, b: r.pairs[0].pb, gap: r.pairs[0].gap });
    } else if (pa.length || pb.length) {
      extraLinks.push(pa.length ? { from: e.i, port: pa[0], to: e.j } : { from: e.j, port: pb[0], to: e.i });
    } else {
      bridges.push(e);
    }
  }
  // the port side was trimmed above only for matched ports; trim these too
  for (const xl of extraLinks) {
    if (xl.bridge) continue;
    const i = xl.from;
    const tw = toWorldFor(i);
    const [wx, wz] = tw(xl.port.point[0], xl.port.point[1]);
    let id = portNode.get(xl.port); // ports on shared edges were already trimmed above
    const edge = id ? null : edges.find((ed) => ed.id === `m${i}:${xl.port.edge.id}`);
    if (!id) {
      id = `m${i}:port${portCount++}`;
      nodes.push({ id, x: wx, z: wz, type: 'endpoint', boundary: false });
      portNode.set(xl.port, id);
    }
    if (edge) {
      if (xl.port.atEnd) {
        edge.pts = edge.pts.slice(0, xl.port.keep);
        edge.hws = edge.hws.slice(0, xl.port.keep);
        edge.target = id;
      } else {
        edge.pts = edge.pts.slice(edge.pts.length - xl.port.keep);
        edge.hws = edge.hws.slice(edge.hws.length - xl.port.keep);
        edge.source = id;
      }
    }
    // nearest point on the neighbour's paths: split that edge there and branch to the port
    let bestHit = null;
    for (const ed of edges) {
      if (!ed.id.startsWith(`m${xl.to}:`)) continue;
      ed.pts.forEach(([x, z], k) => {
        if (k < 2 || k > ed.pts.length - 3) return;
        const d = Math.hypot(x - wx, z - wz);
        if ((!bestHit || d < bestHit.d) && clearPassage([wx, wz], [x, z], xl.port.hw)) bestHit = { ed, k, d };
      });
    }
    if (!bestHit) continue;
    const { ed, k } = bestHit;
    const splitId = `m${xl.to}:split${portCount++}`;
    const [sx, sz] = ed.pts[k];
    nodes.push({ id: splitId, x: sx, z: sz, type: 'junction', boundary: false });
    edges.push({ id: `${ed.id}b`, source: splitId, target: ed.target, pts: ed.pts.slice(k), hws: ed.hws.slice(k) });
    ed.pts = ed.pts.slice(0, k + 1);
    ed.hws = ed.hws.slice(0, k + 1);
    ed.target = splitId;
    const d0 = rotate(xl.port.dir[0], xl.port.dir[1], best.rot[i]);
    const L = bestHit.d * 0.4;
    const pts = bezier([wx, wz], [wx + d0[0] * L, wz + d0[1] * L], [sx, sz], [sx, sz], cfg.path.resampleStep);
    const hw = xl.port.hw;
    edges.push({ id: `branch${portCount}`, source: id, target: splitId, pts, hws: pts.map(() => hw), connector: true });
  }

  /** Split an edge at point index k; returns the new junction node id. */
  const splitAt = (ed, k, tag) => {
    const id = `${ed.id}:${tag}${portCount++}`;
    const [x, z] = ed.pts[k];
    nodes.push({ id, x, z, type: 'junction', boundary: false });
    edges.push({ id: `${ed.id}~${portCount}`, source: id, target: ed.target, pts: ed.pts.slice(k), hws: ed.hws.slice(k) });
    ed.pts = ed.pts.slice(0, k + 1);
    ed.hws = ed.hws.slice(0, k + 1);
    ed.target = id;
    return id;
  };
  // No exits on either side: open a passage between the closest stretches of path.
  for (const e of bridges) {
    let bestPair = null;
    const ea = edges.filter((ed) => ed.id.startsWith(`m${e.i}:`));
    const eb = edges.filter((ed) => ed.id.startsWith(`m${e.j}:`));
    for (const A of ea) {
      for (let ka = 2; ka < A.pts.length - 2; ka += 2) {
        for (const B of eb) {
          for (let kb = 2; kb < B.pts.length - 2; kb += 2) {
            const d = Math.hypot(A.pts[ka][0] - B.pts[kb][0], A.pts[ka][1] - B.pts[kb][1]);
            if (bestPair && d >= bestPair.d) continue;
            const hw = Math.min(A.hws[ka], B.hws[kb]);
            if (clearPassage(A.pts[ka], B.pts[kb], hw)) bestPair = { A, ka, B, kb, d };
          }
        }
      }
    }
    if (!bestPair) continue;
    const pa = bestPair.A.pts[bestPair.ka];
    const pb = bestPair.B.pts[bestPair.kb];
    const hw = Math.min(bestPair.A.hws[bestPair.ka], bestPair.B.hws[bestPair.kb]);
    const ida = splitAt(bestPair.A, bestPair.ka, 'bridge');
    const idb = splitAt(bestPair.B, bestPair.kb, 'bridge');
    const mid = [(pa[0] + pb[0]) / 2, (pa[1] + pb[1]) / 2];
    const pts = bezier(pa, mid, mid, pb, cfg.path.resampleStep);
    edges.push({ id: `bridge${portCount}`, source: ida, target: idb, pts, hws: pts.map(() => hw), connector: true });
    extraLinks.push({ bridge: true });
  }

  // Bridge matched exits with a smooth connector canyon across the shared edge. The
  // control directions lean toward the other port so a tail that meets the edge at a
  // slant does not hook back; the canyon swells a little at the seam (a gate hall).
  const bulge = cfg.stitch?.linkBulge ?? 0.3;
  best.matches.forEach((mt, n) => {
    const ta = toWorldFor(mt.i);
    const tb = toWorldFor(mt.j);
    const pa = ta(mt.a.point[0], mt.a.point[1]);
    const pb = tb(mt.b.point[0], mt.b.point[1]);
    const da = rotate(mt.a.dir[0], mt.a.dir[1], best.rot[mt.i]);
    const db = rotate(mt.b.dir[0], mt.b.dir[1], best.rot[mt.j]);
    const D = Math.hypot(pb[0] - pa[0], pb[1] - pa[1]) || 1;
    const ux = (pb[0] - pa[0]) / D;
    const uz = (pb[1] - pa[1]) / D;
    const unit = (x, z) => {
      const l = Math.hypot(x, z) || 1;
      return [x / l, z / l];
    };
    const ca = unit(da[0] + ux * 0.7, da[1] + uz * 0.7);
    const cb = unit(db[0] - ux * 0.7, db[1] - uz * 0.7);
    const L = D * 0.42;
    const pts = bezier(pa, [pa[0] + ca[0] * L, pa[1] + ca[1] * L], [pb[0] + cb[0] * L, pb[1] + cb[1] * L], pb, cfg.path.resampleStep);
    const hws = pts.map((_, k) => {
      const t = k / (pts.length - 1);
      return (mt.a.hw + (mt.b.hw - mt.a.hw) * t) * (1 + bulge * Math.sin(Math.PI * t));
    });
    edges.push({ id: `link${n}`, source: portNode.get(mt.a), target: portNode.get(mt.b), pts, hws, connector: true });
  });

  const field = new PathField(null, cfg, {
    R,
    centers: centers.map(([x, z]) => ({ x, z })),
    edges,
    nodes,
    name: ids.join(' + '),
    modules: ids.map((id, i) => ({ id, rot: best.rot[i] * 60, x: centers[i][0], z: centers[i][1] })),
  });
  const report = {
    modules: ids.map((id, i) => `${id}（转 ${best.rot[i] * 60}°）`),
    connectors: best.matches.length + extraLinks.length,
    meanOffset: best.matches.length ? best.matches.reduce((s, m) => s + m.gap, 0) / best.matches.length : 0,
    combos,
  };
  return { field, report };
}

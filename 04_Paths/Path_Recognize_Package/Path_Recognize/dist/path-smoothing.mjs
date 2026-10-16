// Geometry-only smoothing. The segmentation, skeleton and graph topology stay intact.
export const SMOOTHING_LABELS = ['原始', '轻柔', '适中', '较柔'];
const PRESETS = [null, {sigma: 2, radius: 5, maxOffset: 1.25},
  {sigma: 3, radius: 7, maxOffset: 2}, {sigma: 4, radius: 9, maxOffset: 2.75}];

function inside(mask, width, height, x, y) {
  return x >= 0 && y >= 0 && x < width && y < height && !!mask[Math.floor(y) * width + Math.floor(x)];
}

// Traverse every pixel touched by a segment; narrow walls cannot hide between samples.
export function segmentInside(a, b, mask, width, height) {
  const ts = [0, 1];
  for (let axis = 0; axis < 2; axis++) {
    const delta = b[axis] - a[axis];
    if (!delta) continue;
    for (let grid = Math.floor(Math.min(a[axis], b[axis])) + 1; grid < Math.max(a[axis], b[axis]); grid++) {
      const t = (grid - a[axis]) / delta;
      if (t > 0 && t < 1) ts.push(t);
    }
  }
  ts.sort((x, y) => x - y);
  for (let i = 0; i < ts.length; i++) {
    const t = ts[i];
    if (!inside(mask, width, height, a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t)) return false;
    if (i) {
      const mid = (ts[i - 1] + t) / 2;
      if (!inside(mask, width, height, a[0] + (b[0] - a[0]) * mid, a[1] + (b[1] - a[1]) * mid)) return false;
    }
  }
  return true;
}

function smoothPoints(raw, mask, width, height, preset) {
  if (!preset || raw.length < 3) return raw;
  const distance = [0];
  for (let i = 1; i < raw.length; i++) distance.push(distance[i - 1] + Math.hypot(raw[i][0] - raw[i - 1][0], raw[i][1] - raw[i - 1][1]));
  const total = distance.at(-1), points = raw.slice();
  for (let i = 1; i < raw.length - 1; i++) {
    let x = 0, y = 0, sum = 0;
    let start = i;
    while (start > 0 && distance[i] - distance[start - 1] <= preset.radius) start--;
    for (let j = start; j < raw.length && distance[j] - distance[i] <= preset.radius; j++) {
      const d = distance[j] - distance[i];
      const spacing = ((distance[j + 1] ?? distance[j]) - (distance[j - 1] ?? distance[j])) / 2;
      const weight = Math.exp(-d * d / (2 * preset.sigma * preset.sigma)) * spacing;
      x += raw[j][0] * weight; y += raw[j][1] * weight; sum += weight;
    }
    if (!sum) continue;
    let dx = x / sum - raw[i][0], dy = y / sum - raw[i][1];
    const displacement = Math.hypot(dx, dy);
    // Fade the adjustment near anchored nodes; keep true endpoints and junctions exact.
    const fade = Math.min(1, distance[i] / preset.radius, (total - distance[i]) / preset.radius);
    const scale = Math.min(fade, preset.maxOffset / (displacement || 1));
    dx *= scale; dy *= scale;
    const candidate = [raw[i][0] + dx, raw[i][1] + dy];
    if (inside(mask, width, height, ...candidate)) points[i] = candidate;
  }
  // Reject wall-crossing adjustments. Reverting a neighbor may invalidate another segment.
  for (let pass = 0; pass < 8; pass++) {
    let changed = false;
    for (let i = 1; i < points.length; i++) {
      if (points[i - 1] === raw[i - 1] && points[i] === raw[i]) continue;
      if (!segmentInside(points[i - 1], points[i], mask, width, height)) {
        points[i - 1] = raw[i - 1]; points[i] = raw[i]; changed = true;
      }
    }
    if (!changed) return points;
  }
  return raw;
}

export function smoothGraph(result, width, height, strength = 1) {
  const level = Math.max(0, Math.min(3, Math.round(Number(strength) || 0)));
  const preset = PRESETS[level];
  let maxDisplacementPx = 0;
  const edges = result.graph.edges.map(edge => {
    const rawPoints = edge.points;
    // Provenance belongs to the original segments, not their adjusted display coordinates.
    const inferredSegments = rawPoints.slice(1).map((b, i) => {
      const a = rawPoints[i];
      return !!(result.inferredMask?.[Math.floor(a[1]) * width + Math.floor(a[0])] ||
        result.inferredMask?.[Math.floor(b[1]) * width + Math.floor(b[0])]);
    });
    const points = smoothPoints(rawPoints, result.mask, width, height, preset);
    let lengthPx = 0;
    points.forEach((p, i) => {
      maxDisplacementPx = Math.max(maxDisplacementPx, Math.hypot(p[0] - rawPoints[i][0], p[1] - rawPoints[i][1]));
      if (i) lengthPx += Math.hypot(p[0] - points[i - 1][0], p[1] - points[i - 1][1]);
    });
    return {...edge, points, rawPoints, inferredSegments, lengthPx, rawLengthPx: edge.lengthPx};
  });
  return {...result.graph, edges, smoothing: {method: 'mask-constrained-gaussian', level,
    label: SMOOTHING_LABELS[level], coordinateSpace: 'processing-image', maxAllowedDisplacementPx: preset?.maxOffset ?? 0,
    maxDisplacementPx, preservesNodes: true, preservesTopology: true}};
}

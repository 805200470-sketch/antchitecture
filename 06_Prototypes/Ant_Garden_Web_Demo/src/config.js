// All tunable values for the canyon demo live here.
// World units: 1 unit ≈ 1 ant body length. Image pixels map onto the XZ plane.

export const QUALITY_PRESETS = {
  // pixelRatio is capped by the screen's devicePixelRatio (Retina = 2)
  low:    { density: 0.6, pixelRatio: 1.0, lodSplatScale: 0.45, cullScale: 0.8 },
  medium: { density: 1.0, pixelRatio: 1.5, lodSplatScale: 0.8,  cullScale: 1.0 },
  high:   { density: 1.5, pixelRatio: 2.0, lodSplatScale: 1.3,  cullScale: 1.2 },
};

export const CONFIG = {
  seed: 20261002,

  path: {
    // Modules recognized from /Volumes/T9/path image (see tools/); listed in data/paths/index.json
    modulesIndex: './data/paths/index.json',
    modulesDir: './data/paths/',
    samples: {
      gray: './data/gray-network.json',
      color: './data/color-network.json',
    },
    defaultSample: 'path-013',
    worldSize: 106,         // the source image's longest side spans this many world units
    pxToWorld: 0.11,        // used only when worldSize is unset
    widthMode: 'designed',  // 'designed' | 'measured' | 'auto' — see PathField._initNetwork
    lineDrawingMaxWidth: 1.5, // 'auto' only: narrower recognized widths count as a line drawing
    measuredInfluence: 1.5, // 'designed': how strongly the photo's relative widths carry over (0 = ignore)
    fixedWidth: 12.0,       // 'designed' canyon floor width (before per-path variation)
    edgeWidthRange: [0.8, 1.4],  // each path gets its own base width: narrower passages to broad avenues
    widthVariation: 0.35,   // ± fraction, swelling and narrowing along each path
    widthFrequency: 0.05,   // how often the width swells (per world unit)
    pinch: 0.3,             // occasional squeeze points
    minHalfWidth: 3.2,
    maxHalfWidth: 10,
    chamberNoise: 0.3,      // irregular chamber outlines (rounded lobes)
    chamberMin: 9,          // chamber radius limits (world units)
    chamberMax: 18,
    pondChance: 0.7,        // junction chambers that get a still pond
    widthScale: 1.35,       // corridor width = meanWidthPx * pxToWorld * widthScale
    minWidth: 3.2,
    maxWidth: 12,
    junctionClearing: 2.2,  // junction chambers: radius = this * widest incident half-width
    endpointClearing: 1.7,  // inner dead ends become rounded chambers
    resampleStep: 0.8,      // polyline resampling step (world units)
  },

  field: {
    cell: 0.5,              // distance-field grid cell size
  },

  // Every module is framed by a regular (pointy-top) hexagon inscribed in the source image.
  frame: {
    fill: 0.985,
    borderMargin: 0.035,    // path points this close to the image edge count as exit tails
    innerPad: 11,           // room between inner features (loops, junctions) and the frame
    wallMargin: 0.9,        // the ant stops this far inside the frame
    sideDepth: 7,           // depth of the soil cross-section below the plateau
    exitRadius: 3.2,        // how close to an exit the "next module" prompt appears
  },

  ground: {
    undulation: 0.3,        // gentle floor noise amplitude
    bankHeight: 5.0,        // the canyon walls: a mossy bank rising to the plateau
    bankWidth: 4.5,
    bankLumps: 0.7,
    plateauNoise: 1.1,
    chunk: 12,              // ground splat chunk size (keeps float16 centers precise)
    spacingFloor: 0.08,     // splat spacing at medium quality
    spacingBank: 0.09,
    spacingPlateau: 0.14,
    sproutDensity: 0.08,    // tiny glowing sprouts per square unit near the canyon walls
    mistPerChunk: 5,
    shaftChance: 0.35,
    slotSpacing: 34,        // story slots along the paths (placeholders for user objects)
    // painted terrain textures from Codex (concept/env2_tex_*), projected onto the ground splats
    textures: { floor: './concept/env2_tex_floor_01.png', bank: './concept/env2_tex_bank_01.png' },
    textureTile: 9,         // world units per texture tile
    textureMix: 0.78,       // 0 = procedural colours only, 1 = texture only
    textureGain: 0.9,
  },

  // High-detail assets converted from concept images (tools/image_to_splat.py).
  // Listed in assets/splats/index.json as { id, kind, file, height, footRadius, replace }.
  // `replace: true` swaps out the procedural variants of that kind.
  // `tint` darkens the studio-lit images so they sit in the dim teal canyon light.
  imageAssets: {
    index: './assets/splats/index.json',
    faceJitter: 0.35,
    tint: [0.52, 0.66, 0.8],                 // default: night blue, darker than the studio-lit images
    kindTint: { spores: [0.62, 0.72, 0.84], curl: [0.58, 0.72, 0.86], tuft: [0.45, 0.6, 0.72] },
  },

  // Generic asset library: each kind gets a few seeded variants, then instances
  // are copied, rotated, scaled and tinted along the path.
  assets: {
    trunk:  { variants: 4 },
    mound:  { variants: 4 },
    fan:    { variants: 3 },
    spores: { variants: 4 },
    curl:   { variants: 3 },
  },

  placement: {
    // d = signed distance to the corridor edge (negative inside the walkable canyon floor)
    trunkNear: { dMin: 4.5, dMax: 9,   spacing: 8,   scale: [0.85, 1.25] },
    trunkFar:  { dMin: 10,  dMax: 18,  spacing: 12,  scale: [0.9, 1.35] },
    backdrop:  { dMin: 18,  dMax: 60,  spacing: 15,  scale: [1.0, 1.5], haze: 0.35 },
    mound:     { dMin: 0.3, dMax: 6,   spacing: 3.4, scale: [0.8, 1.5] },
    curl:      { dMin: -0.4, dMax: 4,  spacing: 6.5, scale: [0.7, 1.3] },
    spores:    { dMin: -1.6, dMax: 3,  spacing: 2.8, scale: [0.7, 1.4] },
    fanWall:   { dMin: 3,   dMax: 12,  spacing: 15,  scale: [0.9, 1.4] },
    // small moss clumps on the canyon floor near the walls (reuses the mound assets, scaled down)
    floorTufts: { dMin: -3.6, dMax: -0.6, spacing: 3.2, scale: [0.2, 0.4] },
    fanLandmark: { scale: [1.35, 1.8], minClearing: 2.6 },
  },

  render: {
    lod: true,
    fov: 62,
    near: 0.05,
    far: 900,
    exposure: 1.0,
    bloom: { strength: 0.75, radius: 0.45, threshold: 0.66 },
    maxStdDev: Math.sqrt(7),
    // P toggles a 'particle' look: each Gaussian drawn smaller so the scene reads as glowing points
    particleMode: { focalAdjustment: 0.38, maxStdDev: 2.0 },
    // instance culling distances (scaled by the quality preset)
    cull: { trunk: 110, mound: 55, fan: 100, spores: 45, curl: 55, ground: 80, slot: 60, side: 200 },
    // per-mesh LoD detail multipliers (Spark lodScale): spend the budget near the ant
    lodScale: { ground: 1.5, mound: 1.3, spores: 1.4, curl: 1.4, fan: 1.1, trunk: 1.0, slot: 1, side: 0.8 },
    // Distance haze: a Spark SplatEdit sphere around the camera mixes far splats toward `color`.
    // (Spark's soft edge ramps across roughly radius ± softEdge/2; tuned by eye.)
    fog: { radius: 42, softEdge: 26, color: [0.13, 0.26, 0.36], amount: 0.68, tint: [0.62, 0.78, 0.95] },
  },

  ant: {
    url: './assets/ant_001.glb',
    bodyLength: 1.0,
    glbBodyLength: 2.714,   // mandible tip → gaster tip in the GLB (from the Blender export)
    glbBodyCenterZ: 0.06,
    glowColor: 0x2f9aa0,    // faint bioluminescent tint (follows the shell texture)
    glowIntensity: 0.12,
    rimColor: 0x9ff6ff,     // fresnel rim glow around the silhouette
    rimStrength: 0.85,
    radius: 0.42,           // collision radius
    crawlSpeed: 1.0,        // units / second (≈ body lengths / second)
    fastSpeed: 2.5,         // hold Shift
    turnRate: 9.0,          // max radians / second when slow (quick pivots)
    sprintTurnRate: 4.2,    // max radians / second at full sprint (smooth arcs)
    turnGain: 10,           // how eagerly the heading chases the input direction
    accel: 7.0,             // speed-up rate (1/s)
    sprintAccel: 4.5,       // sprint ramps up a little slower: it feels like effort
    decel: 10,              // stopping is snappy
    idleBelow: 0.06,        // gait thresholds (units / second)
    fastAbove: 1.45,
    gaitFade: 0.16,         // seconds to cross-fade between clips
    leanRoll: 0.028,        // body roll into turns
    leanPitch: 0.035,       // head dips when accelerating
    groundOffset: 0.0,
    forwardAxis: '+Z',      // head direction in the GLB (from the Blender export)
    crawlCycleSpeed: 0.157, // ground speed the Crawl clip was authored for, in body lengths/s
    fastCycleSpeed: 0.441,
    talkKey: 'Space',
  },

  // Other ants wandering the paths (distinct species from the player's Ant_001).
  npcs: {
    count: 16,              // per nest; stitched worlds get count × nests, up to maxCount
    maxCount: 40,
    drawDistance: 60,       // farther NPCs keep walking but are not drawn or animated
    walkSpeed: [0.55, 0.95],
    fastChance: 0.15,
    pauseChance: 0.35,
    pause: [0.8, 3.5],
    greetRadius: 2.6,       // they stop and touch antennae when the player is this close
    talkRadius: 2.4,        // E near an NPC greets it
    species: [
      // NPC Ant_001s share the player's model; a warm glow and no ground ring tell them apart
      { id: 'ant_001', url: './assets/ant_001.glb', glbBodyLength: 2.714, glbBodyCenterZ: 0.06, bodyLength: 1.0,
        radius: 0.42, rimColor: 0xffd9a0, glowColor: 0x8a5a22 },
      { id: 'ant_002', url: './assets/ant_002.glb', glbBodyLength: 3.083, glbBodyCenterZ: -0.217, bodyLength: 1.15,
        crawlCycleSpeed: 0.266, fastCycleSpeed: 0.82, radius: 0.48, rimColor: 0x8fb4ff, glowColor: 0x2c5f9a },
      { id: 'ant_003', url: './assets/ant_003.glb', glbBodyLength: 2.881, glbBodyCenterZ: -0.094, bodyLength: 1.0,
        crawlCycleSpeed: 0.216, fastCycleSpeed: 0.737, radius: 0.42, rimColor: 0xd2f4ff, glowColor: 0x3a8fb0,
        flight: { chance: 0.45, altitude: [5, 9], speed: 2.6, duration: [8, 16], takeoffForward: 0.451, landForward: 0.492 } },
      // Ant_004 盾甲卫蚁: heavy and armoured — bigger, slower, short low steps
      // (cycle speeds = measured body lengths/s × bodyLength, as for ant_002)
      { id: 'ant_004', url: './assets/ant_004.glb', glbBodyLength: 3.144, glbBodyCenterZ: -0.208, bodyLength: 1.2,
        crawlCycleSpeed: 0.144, fastCycleSpeed: 0.469, radius: 0.5, walkSpeed: [0.38, 0.6], fastSpeed: 1.6,
        rimColor: 0xbff5d8, glowColor: 0x2f7a5a },
      // Ant_005 裂隙疾行蚁: slim and quick — long legs, the fastest cadence of all
      { id: 'ant_005', url: './assets/ant_005.glb', glbBodyLength: 3.482, glbBodyCenterZ: -0.465, bodyLength: 0.95,
        crawlCycleSpeed: 0.186, fastCycleSpeed: 0.58, radius: 0.4, walkSpeed: [0.85, 1.25], fastSpeed: 3.0,
        rimColor: 0x9fd8ff, glowColor: 0x1f5f86 },
    ],
  },

  // Teleport tool (M): click the big map to jump. cooldown 0 for now; set seconds later for a time limit.
  teleport: { key: 'KeyM', cooldown: 0 },

  fx: { swayRadius: 28 },

  // Stitched worlds: decoration spacing multipliers indexed by module count [1, 2, 3], so the whole
  // world stays under ~70M instanced splats (Spark draws nothing at ~100M).
  // portInset: how far inside the frame a tail is cut back before the connector canyon takes over;
  // linkBulge: how much wider the connector is at the seam (0.3 = +30%).
  stitch: { spacingMul: [1, 1.4, 1.75], groundSpacingMul: [1, 1.2, 1.3], portInset: 9, linkBulge: 0.3 },

  // Silhouettes of the surrounding forest beyond the hexagon (atmosphere only; nothing to walk on).
  surroundings: { count: 22, minDist: 14, maxDist: 95, scale: [1.8, 2.8], sink: 12, tint: [0.16, 0.26, 0.36] },

  camera: {
    distance: 3.2,
    minDistance: 1.4,
    maxDistance: 9,
    height: 0.45,           // look-at offset above the ant
    pitch: 0.16,            // radians above horizontal
    minPitch: -0.55,        // below 0 the camera dips low and tilts up to look at the canopy
    maxPitch: 1.2,
    followDelay: 1.4,       // seconds of no dragging before the camera swings behind the ant
    sprintFovKick: 9,       // degrees added to the field of view at full sprint
    sprintDistance: 1.15,   // camera pulls back a little when sprinting
    wallMargin: 0.35,
  },

  interact: {
    slotRadius: 2.4,
  },
};

// Palette sampled from the concept sheet and reference painting (sRGB, 0..1).
export const PALETTE = {
  skyTop:     [0.24, 0.38, 0.5],
  skyMid:     [0.07, 0.16, 0.23],
  skyLow:     [0.01, 0.03, 0.05],
  barkDeep:   [0.06, 0.17, 0.18],
  bark:       [0.16, 0.38, 0.38],
  barkLight:  [0.36, 0.62, 0.60],
  lace:       [0.62, 0.84, 0.80],
  laceGold:   [0.90, 0.87, 0.72],
  holeDeep:   [0.025, 0.09, 0.10],
  holeMid:    [0.11, 0.33, 0.35],
  rim:        [0.55, 0.80, 0.77],
  mossDark:   [0.04, 0.11, 0.13],
  moss:       [0.11, 0.26, 0.29],
  mossLight:  [0.26, 0.46, 0.5],
  frost:      [0.82, 0.92, 0.93],
  soil:       [0.045, 0.1, 0.13],
  trail:      [0.035, 0.075, 0.1],
  stalk:      [0.58, 0.77, 0.82],
  stem:       [0.22, 0.45, 0.46],
  pearl:      [1.0, 0.96, 0.86],
  glowWarm:   [1.0, 0.88, 0.62],
  glowCool:   [0.80, 0.94, 1.0],
  filament:   [0.80, 0.91, 0.94],
  haze:       [0.2, 0.36, 0.48],
  mist:       [0.32, 0.5, 0.62],
  shaft:      [0.86, 0.95, 0.94],
};

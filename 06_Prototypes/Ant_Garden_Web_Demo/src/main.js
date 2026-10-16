// Ant Garden canyon demo — entry point.
// Path Recognize network → PathField → splat canyon (generic assets along the
// path) → player-controlled Ant_001 with a follow camera.
import * as THREE from 'three';
import { SparkRenderer } from '@sparkjsdev/spark';
import { EffectComposer } from 'three/addons/postprocessing/EffectComposer.js';
import { RenderPass } from 'three/addons/postprocessing/RenderPass.js';
import { UnrealBloomPass } from 'three/addons/postprocessing/UnrealBloomPass.js';
import { OutputPass } from 'three/addons/postprocessing/OutputPass.js';
import { CONFIG, QUALITY_PRESETS } from './config.js';
import { PathField } from './pathfield.js';
import { buildWorld, makeSky, makeDepthFloor } from './world.js';
import { Ant } from './ant.js';
import { CameraRig } from './camera-rig.js';
import { Hud } from './hud.js';
import { BigMap } from './bigmap.js';
import { Fx } from './fx.js';
import { spawnNpcs } from './npc.js';
import { stitchModules } from './stitch.js';
import { nextFrame } from './yield.js';

const STORE_QUALITY = 'antgarden.quality';
const STORE_NETWORK = 'antgarden.customNetwork';
const params = new URLSearchParams(location.search);

function readStore(key) {
  try {
    return localStorage.getItem(key);
  } catch {
    return null;
  }
}
function writeStore(key, value) {
  try {
    if (value === null) localStorage.removeItem(key);
    else localStorage.setItem(key, value);
  } catch {
    /* storage unavailable: settings just won't persist */
  }
}

const qualityKey = params.get('quality') ?? readStore(STORE_QUALITY) ?? 'medium';
const quality = QUALITY_PRESETS[qualityKey] ?? QUALITY_PRESETS.medium;

async function loadModules() {
  try {
    const res = await fetch(CONFIG.path.modulesIndex);
    return res.ok ? (await res.json()).paths.map((p) => p.id) : [];
  } catch {
    return [];
  }
}

async function loadNetwork(modules) {
  const want = params.get('sample') ?? CONFIG.path.defaultSample;
  // Stitched worlds: "stitch:<row|cluster>:path-a,path-b[,path-c]"
  if (want.startsWith('stitch:')) {
    const [, layout, list] = want.split(':');
    const ids = list.split(',').filter((id) => modules.includes(id));
    const networks = [];
    for (const id of ids) {
      const res = await fetch(`${CONFIG.path.modulesDir}${id}.json`);
      if (!res.ok) throw new Error(`读取模块 ${id} 失败：${res.status}`);
      networks.push(await res.json());
    }
    return { stitched: { ids, layout, networks }, network: networks[0], label: `拼接 ${ids.map((i) => i.replace('path-', '')).join(' + ')}`, key: want };
  }
  const custom = want === 'custom' ? readStore(STORE_NETWORK) : null;
  if (custom) return { network: JSON.parse(custom), label: '自定义路径', key: 'custom' };
  let url = null;
  let key = want;
  if (modules.includes(want)) url = `${CONFIG.path.modulesDir}${want}.json`;
  else if (CONFIG.path.samples[want]) url = CONFIG.path.samples[want];
  else {
    key = modules[0] ?? 'gray';
    url = modules.length ? `${CONFIG.path.modulesDir}${key}.json` : CONFIG.path.samples.gray;
  }
  const res = await fetch(url);
  if (!res.ok) throw new Error(`读取路径失败：${res.status}`);
  return { network: await res.json(), label: key, key };
}

/** Config for stitched worlds: same look, sparser decoration (splat budget). */
/** Thinner decoration for an n-module world, so it stays inside the instanced-splat budget. */
function stitchedConfig(base, n) {
  const c = structuredClone(base);
  const pick = (v) => (Array.isArray(v) ? v[Math.min(n, v.length) - 1] : v);
  const k = pick(c.stitch.spacingMul);
  const kg = pick(c.stitch.groundSpacingMul);
  for (const rule of Object.values(c.placement)) if (rule.spacing) rule.spacing *= k;
  c.ground.spacingFloor *= kg;
  c.ground.spacingBank *= kg;
  c.ground.spacingPlateau *= kg;
  c.surroundings.count = Math.round(c.surroundings.count * 1.4);
  return c;
}

function setupUiControls(modules, currentKey) {
  const sampleSel = document.getElementById('sample');
  const qualitySel = document.getElementById('quality');
  const group = document.getElementById('module-options');
  for (const id of modules) {
    const opt = document.createElement('option');
    opt.value = id;
    opt.textContent = `模块 ${id.replace('path-', '')}`;
    group.appendChild(opt);
  }
  sampleSel.value = currentKey;
  qualitySel.value = qualityKey;
  const reload = (changes) => {
    const p = new URLSearchParams(location.search);
    for (const [k, v] of Object.entries(changes)) p.set(k, v);
    location.search = p.toString();
  };
  sampleSel.addEventListener('change', () => reload({ sample: sampleSel.value }));
  qualitySel.addEventListener('change', () => {
    writeStore(STORE_QUALITY, qualitySel.value);
    reload({ quality: qualitySel.value });
  });

  // Drop a Path Recognize network JSON anywhere to regenerate the canyon.
  const drop = document.getElementById('drop');
  window.addEventListener('dragover', (e) => {
    e.preventDefault();
    drop.classList.add('show');
  });
  window.addEventListener('dragleave', (e) => {
    if (e.relatedTarget === null) drop.classList.remove('show');
  });
  window.addEventListener('drop', async (e) => {
    e.preventDefault();
    drop.classList.remove('show');
    const file = e.dataTransfer.files?.[0];
    if (!file) return;
    try {
      const text = await file.text();
      const net = JSON.parse(text);
      new PathField(net, CONFIG); // validates
      writeStore(STORE_NETWORK, text);
      reload({ sample: 'custom' });
    } catch (err) {
      alert(`这个文件不能用来生成峡谷：${err.message}`);
    }
  });
}

/** Decode the painted terrain textures into ImageData (missing files are simply skipped). */
async function loadTextures(paths) {
  const out = {};
  for (const [key, url] of Object.entries(paths ?? {})) {
    try {
      // fetch + createImageBitmap rather than img.decode(): decode() waits for the tab to be visible
      const res = await fetch(url);
      if (!res.ok) continue;
      const bmp = await createImageBitmap(await res.blob());
      const c = document.createElement('canvas');
      c.width = bmp.width;
      c.height = bmp.height;
      const g = c.getContext('2d');
      g.drawImage(bmp, 0, 0);
      bmp.close();
      out[key] = g.getImageData(0, 0, c.width, c.height);
    } catch {
      /* texture not generated yet: procedural colours only */
    }
  }
  return Object.keys(out).length ? out : null;
}

function makeEnvironment(renderer) {
  // Soft teal sky light for the ant's PBR materials (splats are pre-lit).
  const envScene = new THREE.Scene();
  envScene.add(makeSky());
  const lamp = new THREE.Mesh(
    new THREE.SphereGeometry(40, 16, 8),
    new THREE.MeshBasicMaterial({ color: new THREE.Color(2.2, 2.3, 2.2) }),
  );
  lamp.position.set(60, 300, 80);
  envScene.add(lamp);
  const pmrem = new THREE.PMREMGenerator(renderer);
  const env = pmrem.fromScene(envScene, 0.04).texture;
  pmrem.dispose();
  return env;
}

async function main() {
  const hud = new Hud();
  hud.setProgress('读取路径数据…', 0.02);
  const modules = await loadModules();
  const { network, label, key: currentKey, stitched } = await loadNetwork(modules);
  // Stitched worlds are 2–3× larger: thin out the decoration to stay under the renderer's splat ceiling.
  const cfg = stitched ? stitchedConfig(CONFIG, stitched.ids.length) : CONFIG;
  setupUiControls(modules, currentKey);
  const goToModule = (id) => {
    const p = new URLSearchParams(location.search);
    p.set('sample', id);
    location.search = p.toString();
  };

  const renderer = new THREE.WebGLRenderer({ antialias: false, powerPreference: 'high-performance' });
  renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, quality.pixelRatio));
  renderer.setSize(window.innerWidth, window.innerHeight);
  renderer.toneMapping = THREE.ACESFilmicToneMapping;
  renderer.toneMappingExposure = CONFIG.render.exposure;
  document.getElementById('app').appendChild(renderer.domElement);

  const scene = new THREE.Scene();
  const camera = new THREE.PerspectiveCamera(CONFIG.render.fov, window.innerWidth / window.innerHeight, CONFIG.render.near, CONFIG.render.far);
  const spark = new SparkRenderer({ renderer, maxStdDev: CONFIG.render.maxStdDev, lodSplatScale: quality.lodSplatScale });
  scene.add(spark);
  const sky = makeSky();
  scene.add(sky);
  scene.environment = makeEnvironment(renderer);

  // Lights only affect the ant (splats carry baked lighting).
  scene.add(new THREE.HemisphereLight(0x7aa9c4, 0x06121a, 1.0));
  const key = new THREE.DirectionalLight(0xd6e8ff, 1.7); // cool moonlight
  key.position.set(0.3, 1, 0.35);
  scene.add(key);
  const rim = new THREE.DirectionalLight(0x6fe3ff, 1.6);
  scene.add(rim);

  hud.setProgress('由路径计算峡谷…', 0.05);
  await nextFrame();
  let field;
  let stitchReport = null;
  if (stitched) {
    const r = stitchModules(stitched.networks, stitched.ids, cfg, stitched.layout);
    field = r.field;
    stitchReport = r.report;
  } else {
    field = new PathField(network, cfg);
  }
  scene.add(makeDepthFloor(field));

  const textures = await loadTextures(CONFIG.ground.textures);
  const world = await buildWorld({
    field,
    cfg,
    quality,
    scene,
    textures,
    onProgress: (text, frac) => hud.setProgress(text, 0.05 + frac * 0.85),
  });

  hud.setProgress('载入蚂蚁…', 0.92);
  const ant = new Ant({ field, colliders: world.colliders, cfg: CONFIG });
  await ant.load(CONFIG.ant.url);
  scene.add(ant.root);
  scene.add(ant.shadow);
  const spawn = field.spawnPoint();
  ant.place(spawn.x, spawn.z, spawn.heading);

  // A soft ring under the player's ant tells it apart from the NPC ants.
  const marker = new THREE.Mesh(
    new THREE.RingGeometry(0.58, 0.64, 48),
    new THREE.MeshBasicMaterial({ color: 0x7fd8e6, transparent: true, opacity: 0.3, depthWrite: false, blending: THREE.AdditiveBlending }),
  );
  marker.renderOrder = 21;
  scene.add(marker);

  hud.setProgress('放入其他蚂蚁…', 0.95);
  const npcs = await spawnNpcs({ field, colliders: world.colliders, cfg: CONFIG, scene, player: ant });
  const fx = new Fx(scene, world, CONFIG);

  const rig = new CameraRig(camera, renderer.domElement, field, CONFIG);
  rig.colliders = world.colliders;
  rig.snapBehind(spawn.heading);
  rig.update(0, ant.position, ant.heading, false);

  hud.buildMinimap(field, world);
  const bigmap = new BigMap({
    field,
    world,
    cfg: CONFIG,
    modules,
    currentKey,
    getAnt: () => ant,
    getNpcs: () => npcs,
    onModule: (id) => goToModule(id),
    onTeleport: (x, z) => {
      fx.burst(ant.position.x, ant.position.y, ant.position.z);
      ant.place(x, z, ant.heading);
      rig.snapBehind(ant.heading);
      camera.userData.placed = false;
      fx.burst(x, ant.position.y, z);
    },
  });
  const moduleIndex = stitched ? modules.indexOf(stitched.ids[stitched.ids.length - 1]) : modules.indexOf(currentKey);
  if (stitchReport) {
    console.info('stitch', stitchReport);
  }
  document.getElementById('path-name').textContent = stitchReport
    ? `${label} · ${stitchReport.modules.join('，')} · ${stitchReport.connectors} 条接口通道 · ${world.exits.length} 个出口`
    : `${label} · ${network.image?.name ?? ''} · ${field.edges.length} 条路径 · ${world.slots.length} 个故事物件位 · ${world.exits.length} 个出口`;
  // Each exit leads to a neighbouring module (next ones in the folder order).
  world.exits.forEach((ex, i) => {
    ex.target = modules.length ? modules[(Math.max(0, moduleIndex) + 1 + i) % modules.length] : null;
  });

  // Post-processing: bloom for the glowing spores and pearls.
  const composer = new EffectComposer(renderer);
  composer.addPass(new RenderPass(scene, camera));
  const b = CONFIG.render.bloom;
  const bloom = new UnrealBloomPass(new THREE.Vector2(window.innerWidth, window.innerHeight), b.strength, b.radius, b.threshold);
  composer.addPass(bloom);
  composer.addPass(new OutputPass());

  window.addEventListener('resize', () => {
    camera.aspect = window.innerWidth / window.innerHeight;
    camera.updateProjectionMatrix();
    renderer.setSize(window.innerWidth, window.innerHeight);
    composer.setSize(window.innerWidth, window.innerHeight);
  });

  // Input
  const keys = new Set();
  window.addEventListener('keydown', (e) => {
    if (e.target instanceof HTMLSelectElement) return;
    keys.add(e.code);
    if (e.code === CONFIG.teleport.key) {
      bigmap.toggle();
      return;
    }
    if (e.code === 'Escape') bigmap.close();
    if (e.code === 'KeyP') setParticleMode(!particleMode);
    if (e.code === CONFIG.ant.talkKey) {
      e.preventDefault();
      ant.playTalk();
    }
    if (e.code === 'KeyE' && nearNpc) {
      nearNpc.greet(ant);
      ant.playTalk();
    } else if (e.code === 'KeyE' && nearSlot) {
      hud.togglePanel(!hud.panel.classList.contains('show'), slotHtml(nearSlot));
    } else if (e.code === 'KeyE' && nearExit?.target) {
      hud.setPrompt(`正在前往模块 ${nearExit.target.replace('path-', '')}…`);
      goToModule(nearExit.target);
    }
    if (e.code === 'KeyH') document.getElementById('help').classList.toggle('hidden');
  });
  window.addEventListener('keyup', (e) => keys.delete(e.code));
  window.addEventListener('blur', () => keys.clear());
  const down = (...codes) => (codes.some((c) => keys.has(c)) ? 1 : 0);

  const slotHtml = (slot) =>
    `<h3>故事物件位 · ${slot.id}</h3>
     <p>这是路径旁的一个故事物件位。下一阶段，用户上传的照片与故事会在这里生成成物件。</p>
     <p class="dim">现在是占位标记：靠近 → 出现提示 → 打开内容，这条交互已经可以测试。</p>`;

  // Particle look (P): draw each Gaussian smaller so the scene reads as glowing points.
  let particleMode = readStore('antgarden.particles') === '1';
  const setParticleMode = (on) => {
    particleMode = on;
    const pm = CONFIG.render.particleMode;
    spark.focalAdjustment = on ? pm.focalAdjustment : 1;
    spark.maxStdDev = on ? pm.maxStdDev : CONFIG.render.maxStdDev;
    writeStore('antgarden.particles', on ? '1' : '0');
  };
  setParticleMode(particleMode);

  // Stitched worlds: which nest the ant is in (hexagons tile, so the nearest centre is the one).
  const nests = field.modules ?? [];
  let currentNest = -1;
  const nestAt = (x, z) => {
    let best = 0;
    for (let i = 1; i < nests.length; i++) {
      if (Math.hypot(nests[i].x - x, nests[i].z - z) < Math.hypot(nests[best].x - x, nests[best].z - z)) best = i;
    }
    return best;
  };

  let nearSlot = null;
  let nearExit = null;
  let nearNpc = null;
  let lastPrompt = '';
  let last = performance.now();
  let time = 0;
  hud.hideLoading();
  window.__antGarden = { scene, camera, ant, world, field, spark, renderer, rig, npcs, bigmap, fx };

  renderer.setAnimationLoop((now) => {
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    time += dt;

    const basis = rig.basis();
    const ix = down('KeyD', 'ArrowRight') - down('KeyA', 'ArrowLeft');
    const iz = down('KeyW', 'ArrowUp') - down('KeyS', 'ArrowDown');
    const move = { x: basis.fx * iz + basis.rx * ix, z: basis.fz * iz + basis.rz * ix };
    const mapOpen = bigmap.isOpen;
    ant.update(dt, mapOpen ? { x: 0, z: 0 } : move, !mapOpen && down('ShiftLeft', 'ShiftRight') === 1);
    for (const npc of npcs) npc.update(dt, ant);
    marker.position.set(ant.position.x, ant.position.y + 0.04, ant.position.z);
    marker.quaternion.copy(ant.shadow.quaternion);
    marker.material.opacity = 0.2 + 0.08 * Math.sin(time * 2.2);
    rig.update(dt, ant.position, ant.heading, ant.speed > 0.05, ant.sprinting);
    rim.position.set(-camera.position.x + ant.position.x, 0.6, -camera.position.z + ant.position.z);
    sky.position.copy(camera.position);
    world.update(camera, time);

    let closest = null;
    let best = CONFIG.interact.slotRadius;
    for (const s of world.slots) {
      const d = Math.hypot(s.x - ant.position.x, s.z - ant.position.z);
      if (d < best) {
        best = d;
        closest = s;
      }
    }
    nearNpc = null;
    for (const npc of npcs) if (npc.near(ant, CONFIG.npcs.talkRadius)) nearNpc = npc;
    let exit = null;
    if (!closest) {
      for (const ex of world.exits) {
        if (Math.hypot(ex.x - ant.position.x, ex.z - ant.position.z) < CONFIG.frame.exitRadius) exit = ex;
      }
    }
    if (closest !== nearSlot && !closest) hud.togglePanel(false);
    nearSlot = closest;
    nearExit = exit;
    // one prompt at a time: ants first, then story slots, then exits
    const promptText = nearNpc
      ? '按 E 和它碰碰触角'
      : nearSlot
        ? '按 E 查看这个故事物件位'
        : nearExit?.target
          ? `出口 · 按 E 前往相邻模块 ${nearExit.target.replace('path-', '')}`
          : '';
    if (promptText !== lastPrompt) {
      lastPrompt = promptText;
      hud.setPrompt(promptText);
    }
    if (nests.length > 1) {
      const m = nestAt(ant.position.x, ant.position.z);
      if (m !== currentNest) {
        currentNest = m;
        hud.showRegion(`蚁穴 ${nests[m].id.replace('path-', '')}`, `第 ${m + 1} / ${nests.length} 个蚁穴`);
      }
    }
    fx.update(dt, camera, ant);
    bigmap.draw(dt);
    hud.update(dt, ant, world, camera);
    composer.render(dt);
  });
}

main().catch((err) => {
  console.error(err);
  new Hud().showError(`出错了：${err.message}`);
});


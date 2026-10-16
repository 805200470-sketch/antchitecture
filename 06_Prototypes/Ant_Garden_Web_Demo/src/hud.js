// On-screen UI: loading progress, minimap of the recognized path, stats,
// interaction prompt and the story-slot panel.

const $ = (id) => document.getElementById(id);

export class Hud {
  constructor() {
    this.loading = $('loading');
    this.loadingLabel = $('loading-label');
    this.loadingBar = $('loading-bar');
    this.stats = $('stats');
    this.prompt = $('prompt');
    this.region = $('region');
    this.regionTimer = 0;
    this.panel = $('panel');
    this.minimap = $('minimap');
    this.fps = 60;
    this.statTimer = 0;
  }

  setProgress(label, frac) {
    this.loadingLabel.textContent = label;
    this.loadingBar.style.width = `${Math.round(frac * 100)}%`;
  }

  hideLoading() {
    this.loading.classList.add('done');
  }

  showError(message) {
    this.loading.classList.remove('done');
    this.loading.classList.add('error');
    this.loadingLabel.textContent = message;
  }

  /** Pre-render the walkable canyon, centerlines and story slots. */
  buildMinimap(field, world) {
    const pb = field.pathBounds;
    const margin = 2;
    this.mapBounds = { minX: pb.minX - margin, minZ: pb.minZ - margin, maxX: pb.maxX + margin, maxZ: pb.maxZ + margin };
    const wWorld = this.mapBounds.maxX - this.mapBounds.minX;
    const hWorld = this.mapBounds.maxZ - this.mapBounds.minZ;
    const W = 200;
    const H = Math.round((W * hWorld) / wWorld);
    this.minimap.width = W * 2;
    this.minimap.height = H * 2;
    this.minimap.style.width = `${W}px`;
    this.minimap.style.height = `${H}px`;
    this.mapScale = (W * 2) / wWorld;

    const bg = document.createElement('canvas');
    bg.width = W * 2;
    bg.height = H * 2;
    const g = bg.getContext('2d');
    const img = g.createImageData(bg.width, bg.height);
    for (let py = 0; py < bg.height; py++) {
      for (let px = 0; px < bg.width; px++) {
        const x = this.mapBounds.minX + px / this.mapScale;
        const z = this.mapBounds.minZ + py / this.mapScale;
        const d = field.dist(x, z);
        const i = (py * bg.width + px) * 4;
        if (field.hexDist(x, z) > 0) {
          img.data[i + 3] = 0;
        } else if (d < 0) {
          img.data[i] = 46;
          img.data[i + 1] = 92;
          img.data[i + 2] = 92;
          img.data[i + 3] = 235;
        } else if (d < 6) {
          const t = d / 6;
          img.data[i] = 18 - 10 * t;
          img.data[i + 1] = 40 - 22 * t;
          img.data[i + 2] = 40 - 20 * t;
          img.data[i + 3] = 220;
        } else {
          img.data[i] = 6;
          img.data[i + 1] = 15;
          img.data[i + 2] = 17;
          img.data[i + 3] = 210;
        }
      }
    }
    g.putImageData(img, 0, 0);
    g.lineWidth = 1.5;
    g.strokeStyle = 'rgba(200, 240, 235, 0.55)';
    for (const e of field.edges) {
      g.beginPath();
      e.pts.forEach(([x, z], i) => {
        const [mx, my] = this.toMap(x, z);
        if (i === 0) g.moveTo(mx, my);
        else g.lineTo(mx, my);
      });
      g.setLineDash(e.inferred ? [4, 4] : []);
      g.stroke();
    }
    g.setLineDash([]);
    // frame outline: outer edges bright, shared (stitched) edges faint
    for (const fe of field.frameEdges) {
      const [ax, ay] = this.toMap(fe.x0, fe.z0);
      const [bx, by] = this.toMap(fe.x1, fe.z1);
      g.strokeStyle = fe.shared ? 'rgba(143, 224, 212, 0.25)' : 'rgba(143, 224, 212, 0.9)';
      g.lineWidth = 2.5;
      g.beginPath();
      g.moveTo(ax, ay);
      g.lineTo(bx, by);
      g.stroke();
    }
    for (const ex of world.exits) {
      const [mx, my] = this.toMap(ex.x, ex.z);
      g.fillStyle = 'rgba(143, 224, 212, 0.95)';
      g.beginPath();
      g.arc(mx, my, 5, 0, Math.PI * 2);
      g.fill();
    }
    for (const s of world.slots) {
      const [mx, my] = this.toMap(s.x, s.z);
      g.beginPath();
      g.arc(mx, my, 5, 0, Math.PI * 2);
      g.strokeStyle = 'rgba(255, 235, 190, 0.9)';
      g.lineWidth = 2;
      g.stroke();
    }
    this.mapBg = bg;
  }

  toMap(x, z) {
    return [(x - this.mapBounds.minX) * this.mapScale, (z - this.mapBounds.minZ) * this.mapScale];
  }

  update(dt, ant, world, camera) {
    this.fps += (1 / Math.max(dt, 1e-3) - this.fps) * Math.min(1, dt * 3);
    this.statTimer -= dt;
    if (this.statTimer <= 0) {
      this.statTimer = 0.4;
      const visible = world.instances.filter((i) => i.mesh.visible).length;
      this.stats.innerHTML =
        `<b>${Math.round(this.fps)}</b> FPS · 可见物件 ${visible}/${world.instances.length}` +
        `<br>素材库 ${(world.uniqueSplats / 1e6).toFixed(2)}M splat${world.imageAssets ? `（含 ${world.imageAssets} 个高精度素材）` : ''} · 场景实例合计 ${(world.instancedSplats / 1e6).toFixed(1)}M` +
        `<br>视野范围内 ${(world.visibleSplats / 1e6).toFixed(1)}M（实际每帧由 LoD 预算裁到更少）`;
    }
    if (!this.mapBg) return;
    const g = this.minimap.getContext('2d');
    g.clearRect(0, 0, this.minimap.width, this.minimap.height);
    g.drawImage(this.mapBg, 0, 0);
    // camera view direction
    const [cx, cy] = this.toMap(camera.position.x, camera.position.z);
    const [ax, ay] = this.toMap(ant.position.x, ant.position.z);
    g.strokeStyle = 'rgba(160, 220, 230, 0.35)';
    g.lineWidth = 1.5;
    g.beginPath();
    g.moveTo(cx, cy);
    g.lineTo(ax, ay);
    g.stroke();
    // ant arrow
    g.save();
    g.translate(ax, ay);
    g.rotate(-ant.heading + Math.PI);
    g.fillStyle = '#ffe9b8';
    g.beginPath();
    g.moveTo(0, -9);
    g.lineTo(6, 7);
    g.lineTo(0, 3);
    g.lineTo(-6, 7);
    g.closePath();
    g.fill();
    g.restore();
  }

  setPrompt(text) {
    if (text) {
      this.prompt.textContent = text;
      this.prompt.classList.add('show');
    } else {
      this.prompt.classList.remove('show');
    }
  }

  /** A title that fades in when the ant walks into another nest of a stitched world. */
  showRegion(title, sub, seconds = 2.8) {
    this.region.querySelector('.t').textContent = title;
    this.region.querySelector('.s').textContent = sub;
    this.region.classList.add('show');
    clearTimeout(this.regionTimer);
    this.regionTimer = setTimeout(() => this.region.classList.remove('show'), seconds * 1000);
  }

  togglePanel(show, html) {
    if (show) {
      this.panel.querySelector('.body').innerHTML = html;
      this.panel.classList.add('show');
    } else {
      this.panel.classList.remove('show');
    }
  }
}

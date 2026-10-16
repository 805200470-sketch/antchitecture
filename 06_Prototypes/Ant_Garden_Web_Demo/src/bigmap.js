// Full-screen module map + teleport tool. Press M (or click the tool slot) to
// open; click anywhere walkable to teleport the ant there; module buttons jump
// to another hexagon module. A cooldown is configurable for later time limits.

const TAU = Math.PI * 2;

export class BigMap {
  /**
   * @param {object} o { field, world, cfg, modules, currentKey, onTeleport(x,z), onModule(id), getAnt(), getNpcs() }
   */
  constructor(o) {
    Object.assign(this, o);
    this.el = document.getElementById('bigmap');
    this.canvas = document.getElementById('bigmap-canvas');
    this.hint = document.getElementById('bigmap-hint');
    this.toolEl = document.getElementById('tool');
    this.isOpen = false;
    this.cooldownLeft = 0;
    this._buildModules();
    this._buildWaypoints();
    this.canvas.addEventListener('pointerdown', (e) => this._click(e));
    this.canvas.addEventListener('pointermove', (e) => this._hover(e));
    this.el.querySelector('.close').addEventListener('click', () => this.close());
    this.toolEl.addEventListener('click', () => this.toggle());
  }

  _buildModules() {
    const box = this.el.querySelector('.modules');
    box.innerHTML = '';
    for (const id of this.modules) {
      const b = document.createElement('button');
      b.textContent = id.replace('path-', '');
      b.title = `前往模块 ${id}`;
      if (id === this.currentKey) b.classList.add('current');
      b.addEventListener('click', () => this.onModule(id));
      box.appendChild(b);
    }
  }

  /** One-click destinations: chambers, story slots and exits. */
  _buildWaypoints() {
    const box = this.el.querySelector('.waypoints');
    box.innerHTML = '';
    const add = (label, cls, x, z) => {
      const b = document.createElement('button');
      b.textContent = label;
      b.className = cls;
      b.addEventListener('click', () => {
        const s = this._snap(x, z);
        if (!s || this.cooldownLeft > 0) return;
        this.cooldownLeft = this.cfg.teleport.cooldown;
        this.close();
        this.onTeleport(s.x, s.z);
      });
      box.appendChild(b);
    };
    this.field.nodes
      .filter((n) => n.degree >= 3 && n.inside)
      .forEach((n, i) => add(`洞室 ${i + 1}`, 'k-chamber', n.x, n.z));
    this.world.slots.forEach((s, i) => add(`物件位 ${i + 1}`, 'k-slot', s.x, s.z));
    this.world.exits.forEach((ex, i) => add(`出口 ${i + 1}`, 'k-exit', ex.x - ex.dirX * 2, ex.z - ex.dirZ * 2));
  }

  toggle() {
    if (this.isOpen) this.close();
    else this.open();
  }

  open() {
    this.isOpen = true;
    this.el.classList.add('show');
    this._layout();
    this._renderBase();
  }

  close() {
    this.isOpen = false;
    this.el.classList.remove('show');
  }

  _layout() {
    const f = this.field;
    const R = f.hex.R;
    const fb = f.bounds;
    this.bounds = { minX: fb.minX - 2, maxX: fb.maxX + 2, minZ: fb.minZ - 2, maxZ: fb.maxZ + 2 };
    void R;
    const w = this.bounds.maxX - this.bounds.minX;
    const h = this.bounds.maxZ - this.bounds.minZ;
    const maxH = window.innerHeight * 0.74;
    const maxW = Math.min(window.innerWidth - 48, 900);
    const scale = Math.min(maxW / w, maxH / h);
    this.cssW = Math.round(w * scale);
    this.cssH = Math.round(h * scale);
    const dpr = Math.min(window.devicePixelRatio || 1, 2);
    this.canvas.width = Math.round(this.cssW * dpr);
    this.canvas.height = Math.round(this.cssH * dpr);
    this.canvas.style.width = `${this.cssW}px`;
    this.canvas.style.height = `${this.cssH}px`;
    this.px = this.canvas.width / w; // device pixels per world unit
  }

  toMap(x, z) {
    return [(x - this.bounds.minX) * this.px, (z - this.bounds.minZ) * this.px];
  }

  toWorld(mx, my) {
    return [this.bounds.minX + mx / this.px, this.bounds.minZ + my / this.px];
  }

  /** Hill-shaded terrain: canyon floor, banks and plateau, clipped to the hexagon. */
  _renderBase() {
    const f = this.field;
    const W = this.canvas.width;
    const H = this.canvas.height;
    const base = document.createElement('canvas');
    base.width = W;
    base.height = H;
    const g = base.getContext('2d');
    const img = g.createImageData(W, H);
    const step = 1 / this.px;
    for (let py = 0; py < H; py++) {
      for (let px = 0; px < W; px++) {
        const [x, z] = this.toWorld(px + 0.5, py + 0.5);
        const i = (py * W + px) * 4;
        if (f.hexDist(x, z) > 0) continue;
        const d = f.pathDist(x, z);
        const hgt = f.height(x, z);
        // hillshade from the height field (light from the upper left)
        const hx = f.height(x + step, z) - f.height(x - step, z);
        const hz = f.height(x, z + step) - f.height(x, z - step);
        const shade = Math.max(0.35, Math.min(1.25, 0.85 - (hx * 0.9 + hz * 0.7) / (2 * step) * 0.35));
        let r, gg, b;
        if (f.dist(x, z) < 0) {
          const t = Math.min(1, -d / 4);
          r = 64 + 30 * t;
          gg = 128 + 34 * t;
          b = 126 + 30 * t;
        } else {
          const t = Math.min(1, hgt / 6);
          r = 26 - 12 * t;
          gg = 58 - 20 * t;
          b = 60 - 16 * t;
        }
        img.data[i] = r * shade;
        img.data[i + 1] = gg * shade;
        img.data[i + 2] = b * shade;
        img.data[i + 3] = 255;
      }
    }
    g.putImageData(img, 0, 0);
    // recognized centerlines
    g.lineWidth = Math.max(1, this.px * 0.18);
    g.strokeStyle = 'rgba(220, 250, 245, 0.55)';
    for (const e of f.edges) {
      g.beginPath();
      e.pts.forEach(([x, z], i) => {
        const [mx, my] = this.toMap(x, z);
        if (i === 0) g.moveTo(mx, my);
        else g.lineTo(mx, my);
      });
      g.stroke();
    }
    // landmarks and trunks on the plateau
    for (const inst of this.world.instances) {
      if (inst.kind !== 'trunk' && inst.kind !== 'fan') continue;
      if (this.field.hexDist(inst.x, inst.z) > 0) continue; // forest silhouettes outside the module
      const [mx, my] = this.toMap(inst.x, inst.z);
      g.fillStyle = inst.kind === 'fan' ? 'rgba(190, 245, 240, 0.85)' : 'rgba(8, 20, 24, 0.55)';
      g.beginPath();
      g.arc(mx, my, inst.kind === 'fan' ? this.px * 0.9 : this.px * 1.3, 0, TAU);
      g.fill();
    }
    // frame outline: outer edges bright, shared (stitched) edges faint
    for (const fe of f.frameEdges) {
      const [ax, ay] = this.toMap(fe.x0, fe.z0);
      const [bx, by] = this.toMap(fe.x1, fe.z1);
      g.strokeStyle = fe.shared ? 'rgba(143, 224, 212, 0.25)' : 'rgba(143, 224, 212, 0.9)';
      g.lineWidth = Math.max(2, this.px * 0.35);
      g.beginPath();
      g.moveTo(ax, ay);
      g.lineTo(bx, by);
      g.stroke();
    }
    this.base = base;
  }

  /** Per-frame overlay: slots, exits, NPCs, the ant, hover target, cooldown. */
  draw(dt) {
    this.cooldownLeft = Math.max(0, this.cooldownLeft - dt);
    const cd = this.cfg.teleport.cooldown;
    this.toolEl.classList.toggle('cooling', this.cooldownLeft > 0);
    this.toolEl.style.setProperty('--cd', cd > 0 ? String(this.cooldownLeft / cd) : '0');
    if (!this.isOpen || !this.base) return;
    const g = this.canvas.getContext('2d');
    g.clearRect(0, 0, this.canvas.width, this.canvas.height);
    g.drawImage(this.base, 0, 0);
    const u = this.px;
    for (const s of this.world.slots) {
      const [mx, my] = this.toMap(s.x, s.z);
      g.strokeStyle = 'rgba(255, 228, 170, 0.95)';
      g.lineWidth = Math.max(1.5, u * 0.25);
      g.beginPath();
      g.arc(mx, my, u * 1.1, 0, TAU);
      g.stroke();
    }
    for (const ex of this.world.exits) {
      const [mx, my] = this.toMap(ex.x, ex.z);
      g.save();
      g.translate(mx, my);
      g.rotate(Math.atan2(ex.dirZ, ex.dirX));
      g.fillStyle = 'rgba(143, 224, 212, 0.95)';
      g.beginPath();
      g.moveTo(u * 1.6, 0);
      g.lineTo(-u * 0.8, u * 1.1);
      g.lineTo(-u * 0.8, -u * 1.1);
      g.closePath();
      g.fill();
      g.restore();
    }
    for (const npc of this.getNpcs()) {
      const [mx, my] = this.toMap(npc.position.x, npc.position.z);
      g.fillStyle = 'rgba(255, 190, 150, 0.95)';
      g.beginPath();
      g.arc(mx, my, Math.max(3, u * 0.55), 0, TAU);
      g.fill();
    }
    const ant = this.getAnt();
    const [ax, ay] = this.toMap(ant.position.x, ant.position.z);
    g.save();
    g.translate(ax, ay);
    g.rotate(-ant.heading + Math.PI);
    g.fillStyle = '#ffe9b8';
    const a = Math.max(7, u * 1.4);
    g.beginPath();
    g.moveTo(0, -a);
    g.lineTo(a * 0.7, a * 0.8);
    g.lineTo(0, a * 0.35);
    g.lineTo(-a * 0.7, a * 0.8);
    g.closePath();
    g.fill();
    g.restore();
    if (this.hover) {
      const [hx, hy] = this.toMap(this.hover.x, this.hover.z);
      g.strokeStyle = this.hover.ok ? 'rgba(200, 255, 245, 0.95)' : 'rgba(255, 150, 130, 0.9)';
      g.lineWidth = 2;
      g.beginPath();
      g.arc(hx, hy, u * 1.4, 0, TAU);
      g.stroke();
    }
  }

  /** Nearest walkable point to a clicked spot (null if none nearby). */
  _snap(x, z) {
    const r = this.cfg.ant.radius + 0.25;
    if (this.field.dist(x, z) < -r) return { x, z };
    let best = null;
    let bestD = Infinity;
    for (let rad = 0.5; rad <= 7; rad += 0.5) {
      for (let k = 0; k < 24; k++) {
        const a = (k / 24) * TAU;
        const px = x + Math.cos(a) * rad;
        const pz = z + Math.sin(a) * rad;
        if (this.field.dist(px, pz) < -r) {
          const d = Math.hypot(px - x, pz - z);
          if (d < bestD) {
            bestD = d;
            best = { x: px, z: pz };
          }
        }
      }
      if (best) return best;
    }
    return null;
  }

  _event(e) {
    const rect = this.canvas.getBoundingClientRect();
    const sx = ((e.clientX - rect.left) / rect.width) * this.canvas.width;
    const sy = ((e.clientY - rect.top) / rect.height) * this.canvas.height;
    return this.toWorld(sx, sy);
  }

  _hover(e) {
    const [x, z] = this._event(e);
    const s = this._snap(x, z);
    this.hover = s ? { ...s, ok: true } : { x, z, ok: false };
    this.hint.textContent = s
      ? this.cooldownLeft > 0
        ? `传送冷却中：${this.cooldownLeft.toFixed(1)} 秒`
        : '点击传送到这里'
      : '这里走不到（峡谷外），换个位置';
  }

  _click(e) {
    const [x, z] = this._event(e);
    const s = this._snap(x, z);
    if (!s || this.cooldownLeft > 0) return;
    this.cooldownLeft = this.cfg.teleport.cooldown;
    this.close();
    this.onTeleport(s.x, s.z);
  }
}

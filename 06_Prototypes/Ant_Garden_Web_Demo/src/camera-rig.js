// Third-person orbit camera that hugs the ant, stays inside the canyon floor
// and swings back behind the ant when the player stops dragging.
import * as THREE from 'three';
import { clamp } from './noise.js';

const _target = new THREE.Vector3();
const _desired = new THREE.Vector3();
const _look = new THREE.Vector3();

export class CameraRig {
  constructor(camera, dom, field, cfg) {
    this.camera = camera;
    this.field = field;
    this.cfg = cfg.camera;
    this.yaw = Math.PI;
    this.pitch = this.cfg.pitch;
    this.distance = this.cfg.distance;
    this.lastDrag = -1e9;
    this.time = 0;
    this.dragging = false;
    this.colliders = [];
    this.sprint = 0; // smoothed 0..1
    this.baseFov = camera.fov;
    this._bind(dom);
  }

  _bind(dom) {
    let lastX = 0;
    let lastY = 0;
    dom.addEventListener('pointerdown', (e) => {
      if (e.button !== 0 && e.pointerType === 'mouse') return;
      this.dragging = true;
      lastX = e.clientX;
      lastY = e.clientY;
      dom.setPointerCapture(e.pointerId);
    });
    dom.addEventListener('pointermove', (e) => {
      if (!this.dragging) return;
      this.yaw -= (e.clientX - lastX) * 0.0055;
      this.pitch = clamp(this.pitch + (e.clientY - lastY) * 0.004, this.cfg.minPitch, this.cfg.maxPitch);
      lastX = e.clientX;
      lastY = e.clientY;
      this.lastDrag = this.time;
    });
    const end = (e) => {
      this.dragging = false;
      if (dom.hasPointerCapture?.(e.pointerId)) dom.releasePointerCapture(e.pointerId);
    };
    dom.addEventListener('pointerup', end);
    dom.addEventListener('pointercancel', end);
    dom.addEventListener(
      'wheel',
      (e) => {
        e.preventDefault();
        this.distance = clamp(this.distance * Math.exp(e.deltaY * 0.0012), this.cfg.minDistance, this.cfg.maxDistance);
      },
      { passive: false },
    );
  }

  /** Movement basis for camera-relative controls: forward and right on the XZ plane. */
  basis() {
    return {
      fx: -Math.sin(this.yaw),
      fz: -Math.cos(this.yaw),
      rx: Math.cos(this.yaw),
      rz: -Math.sin(this.yaw),
    };
  }

  snapBehind(heading) {
    this.yaw = heading + Math.PI;
  }

  /** True when a camera sample point sits inside a prop's collision cylinder. */
  _hitsProp(x, y, z) {
    for (const col of this.colliders) {
      // low props (moss tufts, pond rims) never block the view
      if ((col.h ?? 50) < 1.4) continue;
      const dx = x - col.x;
      const dz = z - col.z;
      const r = col.r + this.cfg.wallMargin;
      if (dx * dx + dz * dz < r * r && y < this.field.height(col.x, col.z) + (col.h ?? 50) + 0.3) return true;
    }
    return false;
  }

  update(dt, antPos, antHeading, moving, sprinting = false) {
    this.time += dt;
    const c = this.cfg;
    this.sprint += ((sprinting ? 1 : 0) - this.sprint) * (1 - Math.exp(-(sprinting ? 3 : 2) * dt));
    // Sprinting: wider view, slightly longer boom, quicker swing behind the ant.
    const fov = this.baseFov + c.sprintFovKick * this.sprint;
    if (Math.abs(this.camera.fov - fov) > 0.01) {
      this.camera.fov = fov;
      this.camera.updateProjectionMatrix();
    }
    const delay = c.followDelay * (1 - 0.6 * this.sprint);
    if (moving && !this.dragging && this.time - this.lastDrag > delay) {
      let diff = antHeading + Math.PI - this.yaw;
      diff = Math.atan2(Math.sin(diff), Math.cos(diff));
      this.yaw += diff * (1 - Math.exp(-(1.6 + 2.2 * this.sprint) * dt));
    }
    _target.set(antPos.x, antPos.y + c.height, antPos.z);
    // Negative pitch: keep the camera low and raise the look-at point instead (look up at the canopy).
    const camPitch = Math.max(this.pitch, -0.08);
    const lookUp = Math.max(0, -this.pitch);
    const cp = Math.cos(camPitch);
    const dist = this.distance * (1 + (c.sprintDistance - 1) * this.sprint);
    _desired.set(
      _target.x + Math.sin(this.yaw) * cp * dist,
      _target.y + Math.sin(camPitch) * dist,
      _target.z + Math.cos(this.yaw) * cp * dist,
    );
    // March from the ant toward the desired spot; stop before a wall or the floor.
    const steps = 16;
    let ok = 0;
    for (let i = 1; i <= steps; i++) {
      const t = i / steps;
      const x = _target.x + (_desired.x - _target.x) * t;
      const y = _target.y + (_desired.y - _target.y) * t;
      const z = _target.z + (_desired.z - _target.z) * t;
      if (this.field.dist(x, z) > -c.wallMargin || y < this.field.height(x, z) + 0.15) break;
      if (this._hitsProp(x, y, z)) break;
      ok = t;
    }
    _desired.lerpVectors(_target, _desired, Math.max(ok, Math.min(1, c.minDistance / Math.max(dist, 1e-3))));
    const minY = this.field.height(_desired.x, _desired.z) + 0.15;
    if (_desired.y < minY) _desired.y = minY;
    const k = 1 - Math.exp(-(12 + 8 * this.sprint) * dt);
    this.camera.position.lerp(_desired, this.camera.userData.placed ? k : 1);
    this.camera.userData.placed = true;
    _look.copy(_target);
    _look.y += Math.tan(Math.min(1.25, lookUp * 2.2)) * this.camera.position.distanceTo(_target);
    this.camera.lookAt(_look);
  }
}

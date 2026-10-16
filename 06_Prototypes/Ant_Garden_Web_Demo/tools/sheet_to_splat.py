#!/usr/bin/env python3
"""Carve a true 3D splat asset from orthographic design views (visual hull).

Front view gives width × height, side view gives depth × height; a voxel is
solid only where both silhouettes (and an optional top view) agree. Surface
voxels become oriented Gaussians coloured from the view that faces them, so
the object holds up from every angle — unlike the single-image relief in
image_to_splat.py. The bottom slice also yields an accurate collision footprint.

Usage:
  python3 tools/sheet_to_splat.py --front f.png --side s.png [--top t.png] [--back b.png] \
      --height 6 --out assets/splats/curl_3d.ply [--res 220]
  python3 tools/sheet_to_splat.py --sheet sheet.png ...   # front | side panels side by side
"""
import argparse
import json
from pathlib import Path

import numpy as np
from PIL import Image

from image_to_splat import foreground, quat_from_z, write_ply


def load_rgba(path):
    return np.asarray(Image.open(path).convert('RGBA')).astype(np.float32) / 255.0


def cut(rgba, tol):
    """Object mask + colours, cropped to the object's bounding box."""
    fg, alpha, _ = foreground(rgba, tol, False)
    ys, xs = np.nonzero(fg & (alpha > 0.2))
    y0, y1, x0, x1 = ys.min(), ys.max() + 1, xs.min(), xs.max() + 1
    return (fg & (alpha > 0.2))[y0:y1, x0:x1], rgba[y0:y1, x0:x1, :3]


def resample(mask, rgb, rows, cols):
    m = Image.fromarray((mask * 255).astype(np.uint8)).resize((cols, rows), Image.BILINEAR)
    c = Image.fromarray((rgb * 255).astype(np.uint8)).resize((cols, rows), Image.LANCZOS)
    return np.asarray(m) > 127, np.asarray(c).astype(np.float32) / 255.0


def carve(front, side, top, back, height, res, tol):
    fm, fc = cut(front, tol)
    sm, sc = cut(side, tol)
    ny = res
    nx = max(4, round(res * fm.shape[1] / fm.shape[0]))
    nz = max(4, round(res * sm.shape[1] / sm.shape[0]))
    fm, fc = resample(fm, fc, ny, nx)
    sm, sc = resample(sm, sc, ny, nz)
    bc = None
    if back is not None:
        bm, bc = cut(back, tol)
        bm, bc = resample(bm, bc, ny, nx)
        bc = bc[:, ::-1]  # back view is mirrored left/right
    # occupancy[y, x, z]; y = 0 is the top row of the images
    occ = fm[:, :, None] & sm[:, None, :]
    if top is not None:
        tm, _ = cut(top, tol)
        tm, _ = resample(tm, np.zeros(tm.shape + (3,), np.float32), nz, nx)
        occ &= tm.T[None, :, :]
    # surface = solid voxels with an empty 6-neighbour
    pad = np.pad(occ, 1)
    inner = pad[1:-1, 1:-1, 1:-1]
    nb = (pad[:-2, 1:-1, 1:-1] & pad[2:, 1:-1, 1:-1] & pad[1:-1, :-2, 1:-1] & pad[1:-1, 2:, 1:-1]
          & pad[1:-1, 1:-1, :-2] & pad[1:-1, 1:-1, 2:])
    surf = inner & ~nb
    yi, xi, zi = np.nonzero(surf)
    # normals from the smoothed occupancy gradient
    f = occ.astype(np.float32)
    for ax in range(3):
        f = (np.roll(f, 1, ax) + 2 * f + np.roll(f, -1, ax)) / 4
    gy, gx, gz = np.gradient(f)
    n = np.stack([-gx[yi, xi, zi], gy[yi, xi, zi], -gz[yi, xi, zi]], 1)  # world: +y up (image rows go down)
    n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-6
    # colour from the view facing the normal (front/back for z, side for x)
    wf = np.clip(np.abs(n[:, 2]), 0, 1) ** 2
    ws = np.clip(np.abs(n[:, 0]), 0, 1) ** 2
    cf = fc[yi, xi]
    if bc is not None:
        cf = np.where((n[:, 2] < 0)[:, None], bc[yi, xi], cf)
    else:
        cf = np.where((n[:, 2] < 0)[:, None], cf * 0.62, cf)  # unseen back: darker front
    cs = sc[yi, zi] * np.where(n[:, 0] < 0, 0.85, 1.0)[:, None]
    col = (cf * wf[:, None] + cs * ws[:, None]) / (wf + ws + 1e-6)[:, None]
    vs = height / ny
    xyz = np.stack([(xi - nx / 2) * vs, (ny - 1 - yi) * vs, (zi - nz / 2) * vs], 1)
    xyz += np.random.default_rng(1).uniform(-0.3, 0.3, xyz.shape) * vs
    # disks facing the normal (sign does not matter for a flat Gaussian)
    nd = np.where((n[:, 2] < 0)[:, None], -n, n)
    scale = np.tile(np.array([0.75 * vs, 0.75 * vs, 0.22 * vs], np.float32), (len(xyz), 1))
    # footprint: solid columns in the bottom 1.3 units → circles on a coarse XZ grid
    band = occ[max(0, ny - int(1.3 / vs)):].any(0)
    cell = max(1, int(0.5 / vs))
    foot = []
    for x0 in range(0, nx, cell):
        for z0 in range(0, nz, cell):
            if band[x0:x0 + cell, z0:z0 + cell].any():
                foot.append([round((x0 + cell / 2 - nx / 2) * vs, 3), round((z0 + cell / 2 - nz / 2) * vs, 3),
                             round(cell * vs * 0.75, 3)])
    return xyz, nd, col, scale, foot, (nx * vs, height, nz * vs)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--front')
    ap.add_argument('--side')
    ap.add_argument('--top')
    ap.add_argument('--back')
    ap.add_argument('--sheet', help='front and side panels side by side in one image')
    ap.add_argument('--height', type=float, required=True)
    ap.add_argument('--out', required=True)
    ap.add_argument('--res', type=int, default=220)
    ap.add_argument('--tol', type=float, default=0.06)
    a = ap.parse_args()
    if a.sheet:
        s = load_rgba(a.sheet)
        w = s.shape[1] // 2
        front, side = s[:, :w], s[:, w:]
    else:
        front, side = load_rgba(a.front), load_rgba(a.side)
    top = load_rgba(a.top) if a.top else None
    back = load_rgba(a.back) if a.back else None
    xyz, n, col, scale, foot, size = carve(front, side, top, back, a.height, a.res, a.tol)
    write_ply(a.out, xyz, n, col, np.full(len(xyz), 0.97, np.float32), scale, quat_from_z(n))
    meta = {'splats': int(len(xyz)), 'size': [round(v, 3) for v in size], 'footprint3d': foot, 'height': a.height}
    Path(a.out).with_suffix('.json').write_text(json.dumps(meta, indent=2))
    print(json.dumps({k: meta[k] for k in ('splats', 'size', 'height')}), 'footprint circles:', len(foot))


if __name__ == '__main__':
    main()

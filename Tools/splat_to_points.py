#!/usr/bin/env python3
"""splat_to_points.py <in.splat> <plant name> [--keep 0.35] [--out PATH]

A Tripo Gaussian splat (.splat: 32 bytes a splat - position, scale, RGBA, rotation; y pointing down) as the particle
look's points for a plant (AGP4, as UnityProject.nosync/Assets/AntGarden/Resources/AntGarden/Points/<name>.bytes),
lined up with the points made from the plant's mesh: the same height, foot and middle, turned (and mirrored if that
fits better) to lie over them. Each splat's normal is its flattest axis; its colour is the splat's own; the glowing
band comes from Art/Plants/plant_looks.json as for the mesh. The mesh's points are copied to
03_Models/Plants/<name>.mesh_points.bytes first (never overwritten), so they can be put back.

numpy only. Default --out is the plant's Points file itself.
"""
import argparse
import json
import os
import shutil
import struct

import numpy as np

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
POINTS = os.path.join(ROOT, 'UnityProject.nosync/Assets/AntGarden/Resources/AntGarden/Points')
LOOKS = os.path.join(ROOT, 'UnityProject.nosync/Assets/AntGarden/Art/Plants/plant_looks.json')
AGP4 = 0x34504741
REC = np.dtype([('p', '<u2', 3), ('n', '<u4'), ('c', 'u1', 3), ('f', 'u1'), ('o', 'u1')])


def read_agp4(path):
    b = open(path, 'rb').read()
    head, n = struct.unpack_from('<iI', b, 0)
    if head != AGP4:
        raise SystemExit(f'{path}: not AGP4')
    area = struct.unpack_from('<f', b, 8)[0]
    lo = np.array(struct.unpack_from('<3f', b, 12))
    size = np.array(struct.unpack_from('<3f', b, 24)) / 65535
    a = np.frombuffer(b, dtype=REC, count=n, offset=36)
    return lo + a['p'] * size, area


def encode_normals(n):
    """Octahedral, two 16-bit halves, lowest bit clear (ThingPen.EncodeNormal)."""
    n = n / np.maximum(1e-8, np.abs(n).sum(1, keepdims=True))
    x, y = n[:, 0].copy(), n[:, 2].copy()
    down = n[:, 1] < 0
    ox = (1 - np.abs(y)) * np.where(x >= 0, 1, -1)
    oy = (1 - np.abs(x)) * np.where(y >= 0, 1, -1)
    x[down], y[down] = ox[down], oy[down]
    a = np.clip(np.round((x * 0.5 + 0.5) * 65535), 0, 65535).astype(np.uint32)
    b = np.clip(np.round((y * 0.5 + 0.5) * 65535), 0, 65535).astype(np.uint32)
    return (a & 0xFFFE) | (b << 16)


def quat_axes(r):
    """The rotation's three axes (columns) from the .splat's bytes (w, x, y, z as q * 128 + 128)."""
    q = r.astype(np.float64) / 128 - 1
    q /= np.maximum(1e-8, np.linalg.norm(q, axis=1, keepdims=True))
    w, x, y, z = q.T
    m = np.empty((len(q), 3, 3))
    m[:, 0, 0] = 1 - 2 * (y * y + z * z); m[:, 0, 1] = 2 * (x * y - w * z); m[:, 0, 2] = 2 * (x * z + w * y)
    m[:, 1, 0] = 2 * (x * y + w * z); m[:, 1, 1] = 1 - 2 * (x * x + z * z); m[:, 1, 2] = 2 * (y * z - w * x)
    m[:, 2, 0] = 2 * (x * z - w * y); m[:, 2, 1] = 2 * (y * z + w * x); m[:, 2, 2] = 1 - 2 * (x * x + y * y)
    return m


def chamfer(a, b):
    d = ((a[:, None, :] - b[None, :, :]) ** 2).sum(-1)
    return np.sqrt(d.min(1)).mean() + np.sqrt(d.min(0)).mean()


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('splat')
    ap.add_argument('name')
    ap.add_argument('--keep', type=float, default=0.35, help='least opacity kept')
    ap.add_argument('--out', default=None)
    args = ap.parse_args()
    mesh_path = os.path.join(POINTS, args.name + '.bytes')
    backup = os.path.join(ROOT, '03_Models/Plants', args.name + '.mesh_points.bytes')
    if not os.path.exists(backup):
        shutil.copyfile(mesh_path, backup)
    mesh, area = read_agp4(backup)

    s = np.fromfile(args.splat, dtype=np.dtype([('p', '<f4', 3), ('s', '<f4', 3), ('c', 'u1', 4), ('r', 'u1', 4)]))
    s = s[s['c'][:, 3] / 255 >= args.keep]
    pos = s['p'].astype(np.float64) * np.array([1, -1, 1])            # y up
    axes = quat_axes(s['r'])
    flat = np.argmin(s['s'], axis=1)
    nrm = axes[np.arange(len(s)), :, flat] * np.array([1, -1, 1])      # the flattest axis, y up

    # the mesh's height, foot and middle
    h_m, h_s = np.ptp(mesh[:, 1]), np.ptp(pos[:, 1])
    pos *= h_m / h_s
    pos[:, 1] += mesh[:, 1].min() - pos[:, 1].min()
    pos[:, [0, 2]] += np.median(mesh[:, [0, 2]], 0) - np.median(pos[:, [0, 2]], 0)

    # turned (and mirrored) to lie over the mesh's points
    rng = np.random.default_rng(7)
    sm = mesh[rng.choice(len(mesh), 1500, replace=False)]
    ss_i = rng.choice(len(pos), 1500, replace=False)
    best = (1e9, 0, 1)
    for mirror in (1, -1):
        for deg in range(0, 360, 6):
            t = np.radians(deg)
            c, si = np.cos(t), np.sin(t)
            q = pos[ss_i] - np.r_[np.median(pos[:, 0]), 0, np.median(pos[:, 2])]
            q[:, 0] *= mirror
            x, z = q[:, 0] * c - q[:, 2] * si, q[:, 0] * si + q[:, 2] * c
            q = np.stack([x, q[:, 1], z], 1) + np.r_[np.median(pos[:, 0]), 0, np.median(pos[:, 2])]
            e = chamfer(q, sm)
            if e < best[0]:
                best = (e, deg, mirror)
    e, deg, mirror = best
    mid = np.r_[np.median(pos[:, 0]), 0, np.median(pos[:, 2])]
    t = np.radians(deg)
    c, si = np.cos(t), np.sin(t)

    def place(v, is_pos):
        v = v - mid if is_pos else v.copy()
        v[:, 0] *= mirror
        x, z = v[:, 0] * c - v[:, 2] * si, v[:, 0] * si + v[:, 2] * c
        v = np.stack([x, v[:, 1], z], 1)
        return v + mid if is_pos else v

    pos, nrm = place(pos, True), place(nrm, False)
    print(f'{args.name}: {len(pos)} splats kept of {len(s)}; turned {deg} deg{" mirrored" if mirror < 0 else ""}, '
          f'mean gap to the mesh points {e:.4f} (height {h_m:.3f})')

    # the glowing band, as the mesh's (plant_looks.json)
    looks = json.load(open(LOOKS))
    band = looks.get(args.name, {}).get('glow')
    y01 = (pos[:, 1] - pos[:, 1].min()) / max(1e-6, np.ptp(pos[:, 1]))
    glow = np.zeros(len(pos), bool) if not band else (y01 >= band[0]) & (y01 <= band[1])

    order = rng.permutation(len(pos))
    pos, nrm, col, glow = pos[order], nrm[order], s['c'][order, :3], glow[order]
    lo = pos.min(0)
    size = np.maximum(1e-6, np.ptp(pos, 0))
    a = np.zeros(len(pos), dtype=REC)
    a['p'] = np.clip(np.round((pos - lo) / size * 65535), 0, 65535).astype(np.uint16)
    a['n'] = encode_normals(nrm)
    a['c'] = col
    a['f'] = 64 | np.where(glow, 128, 0)    # an even spread (density 1); bit 7: the glowing part
    a['o'] = 255
    out = args.out or mesh_path
    with open(out, 'wb') as f:
        f.write(struct.pack('<iIf3f3f', AGP4, len(pos), area, *lo, *size))
        f.write(a.tobytes())
    print(f'wrote {out}')


if __name__ == '__main__':
    main()

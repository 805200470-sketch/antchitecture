#!/usr/bin/env python3
"""turntable_points.py <film dir> <subject> <out dir> [--from F] [--to F] [--bg r,g,b]

Process film P02, the middle phase: a plant's (or the ant's) stored points on their own, drawn from the very camera the
game filmed it with (ProcessFilms.cs -turntablefilm: <film dir>/film.json), so they can be faded into the game's frames
and the model's. The points are the game's own (Resources/AntGarden/Points/<plant>.bytes, AGP4; ant_<id>.bytes, AGA3),
placed as the game places them: a plant bent, twisted and stretched as its splats are (ParticleLook.Shape, with this
plant's numbers from film.json) and set where it stood; the ant skinned with the bone matrices the game had (film.json
skin). Each point a small round dot in its texture's colour, lit a little from its normal by a light beside the camera,
nearer dots over farther ones; drawn at twice the size and scaled down, so the dots are soft. Frames f00000.jpg ... at
1920x1080.
"""
import argparse
import json
import os
import struct

import cv2
import numpy as np

POINTS = os.path.expanduser('~/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Resources/AntGarden/Points/')


def load_plant(name):
    b = open(POINTS + name + '.bytes', 'rb').read()
    head, n, area = struct.unpack_from('<iif', b, 0)
    assert head == 0x34504741, hex(head)
    mn = np.array(struct.unpack_from('<3f', b, 12)); size = np.array(struct.unpack_from('<3f', b, 24)) / 65535.0
    rec = np.frombuffer(b, dtype=np.dtype([('p', '<u2', 3), ('n', '<u4'), ('c', 'u1', 3), ('f', 'u1'), ('ao', 'u1')]), count=n, offset=36)
    pos = mn + rec['p'].astype(np.float64) * size
    e = rec['n'].astype(np.int64)
    x = (e & 0xFFFF) / 65535.0 * 2 - 1; z = (e >> 16) / 65535.0 * 2 - 1
    y = 1 - np.abs(x) - np.abs(z)
    neg = y < 0
    ox = (1 - np.abs(z)) * np.where(x >= 0, 1, -1); oz = (1 - np.abs(x)) * np.where(z >= 0, 1, -1)
    x = np.where(neg, ox, x); z = np.where(neg, oz, z)
    nrm = np.stack([x, y, z], 1); nrm /= np.linalg.norm(nrm, axis=1, keepdims=True) + 1e-9
    col = rec['c'].astype(np.float32) / 255.0
    ao = rec['ao'].astype(np.float32) / 255.0
    return pos, nrm, col, ao, area, n


def shape(pos, nrm, s):
    """ParticleLook.Shape and SolidSplat's sunk lump, in the plant's own space."""
    p = pos.copy(); n = nrm.copy()
    top = max(s['top'], 1e-4)
    y01 = np.clip(pos[:, 1] / top, 0, 1)
    if not s.get('rigid') and not (s['bend'] == 0 and s['twist'] == 0 and s['sy'] == 1 and s['sxz'] == 1):
        y = np.maximum(0, p[:, 1])
        p[:, 0] *= s['sxz']; p[:, 2] *= s['sxz']; p[:, 1] *= s['sy']
        n[:, 0] /= s['sxz']; n[:, 2] /= s['sxz']; n[:, 1] /= s['sy']
        a = s['twist'] * y; ca, sa = np.cos(a), np.sin(a)
        px, pz = p[:, 0] * ca - p[:, 2] * sa, p[:, 0] * sa + p[:, 2] * ca
        nx, nz = n[:, 0] * ca - n[:, 2] * sa, n[:, 0] * sa + n[:, 2] * ca
        p[:, 0], p[:, 2], n[:, 0], n[:, 2] = px, pz, nx, nz
        yy = y * s['sy']; b = s['bend']; bx, bz = s['bendDir']
        p[:, 0] += bx * b * yy * yy; p[:, 2] += bz * b * yy * yy
        n[:, 1] -= 2 * b * yy * (bx * n[:, 0] + bz * n[:, 2])
        n /= np.linalg.norm(n, axis=1, keepdims=True) + 1e-9
    if s.get('sunk', 0) > 0:
        p[:, 1] -= np.where(y01 < s['sunk'] + 0.03, 1 + s['sunk'], s['sunk']) * top * s['sy']
    return p, n


def load_ant(name, skin):
    b = open(POINTS + 'ant_' + name + '.bytes', 'rb').read()
    magic, ns = struct.unpack_from('<ii', b, 0)
    assert magic in (0x32414741, 0x33414741), hex(magic)
    o = 8
    for _ in range(ns):
        ln, sh = 0, 0
        while True:   # .NET BinaryWriter string: 7-bit length
            c = b[o]; o += 1
            ln |= (c & 0x7F) << sh; sh += 7
            if c < 0x80:
                break
        o += ln + 64
    n, = struct.unpack_from('<i', b, o); o += 4
    area = 0.0
    if magic == 0x33414741:
        area, = struct.unpack_from('<f', b, o); o += 4
    rec = np.frombuffer(b, dtype=np.dtype([('p', '<f4', 3), ('n', '<f4', 3), ('c', 'u1', 4), ('s0', '<u2'), ('s1', '<u2'), ('w', '<f4')]), count=n, offset=o)
    S = np.array(skin, np.float64)            # bones x 4 x 4 (rows)
    p = np.concatenate([rec['p'].astype(np.float64), np.ones((n, 1))], 1)
    m0, m1 = S[rec['s0']], S[rec['s1']]
    w = rec['w'].astype(np.float64)[:, None]
    pw = np.einsum('nij,nj->ni', m1, p)[:, :3] * (1 - w) + np.einsum('nij,nj->ni', m0, p)[:, :3] * w
    nn = rec['n'].astype(np.float64)
    nw = np.einsum('nij,nj->ni', m1[:, :3, :3], nn) * (1 - w) + np.einsum('nij,nj->ni', m0[:, :3, :3], nn) * w
    nw /= np.linalg.norm(nw, axis=1, keepdims=True) + 1e-9
    col = rec['c'][:, :3].astype(np.float32) / 255.0
    return pw, nw, col, np.ones(n, np.float32), n, area


def quat_rows(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('film')
    ap.add_argument('subject')
    ap.add_argument('out')
    ap.add_argument('--from', dest='f0', type=int, default=0)
    ap.add_argument('--to', dest='f1', type=int, default=-1)
    ap.add_argument('--bg', default='3,3,4')
    ap.add_argument('--dot', type=float, default=1.0)       # dot radius, in 1080p pixels at the subject's middle
    ap.add_argument('--gain', type=float, default=1.0)
    a = ap.parse_args()
    film = json.load(open(os.path.join(a.film, 'film.json')))
    sub = next(s for s in film['subjects'] if s['name'] == a.subject or s['dir'] == a.subject)
    W, H, SS = film['width'], film['height'], 2
    bg = np.array([float(v) for v in a.bg.split(',')], np.float32) / 255.0
    if sub['kind'] == 'plant':
        pos, nrm, col, ao, area, n = load_plant(sub['name'])
        pos, nrm = shape(pos, nrm, sub)
        M = np.array(sub['m'], np.float64)
        P = pos @ M[:3, :3].T + M[:3, 3]
        N = nrm @ M[:3, :3].T
        N /= np.linalg.norm(N, axis=1, keepdims=True) + 1e-9
        spacing = np.sqrt(area * M[1, 1] ** 2 / n)     # (world units between points)
    else:
        P, N, col, ao, n, area = load_ant(sub['name'], sub['skin'])
        spacing = np.sqrt(area / n) * np.linalg.norm(np.array(sub['skin'][0])[:3, 0])   # (bind space, as the skin scales it)
    os.makedirs(a.out, exist_ok=True)
    cams = sub['cams']
    f1 = len(cams) - 1 if a.f1 < 0 else min(a.f1, len(cams) - 1)
    fov = np.radians(sub['fov'])
    fpx = (H * SS / 2) / np.tan(fov / 2)
    centre = np.array(sub['centre'])
    # each point's colour: its texture's, lit a little (the light beside the camera, from above left), its crevices darker
    base = np.clip(col * (0.55 + 0.45 * ao[:, None]) * a.gain, 0, 1)
    print(f"{sub['name']}: {n} points, spacing {spacing:.4f}, frames {a.f0}..{f1}")
    for f in range(a.f0, f1 + 1):
        c = cams[f]
        C = np.array(c[:3]); R = quat_rows(c[3:7])
        v = (P - C) @ R            # camera space: x right, y up, z forward
        z = v[:, 2]
        ok = z > 0.02
        x = W * SS / 2 + v[:, 0] * fpx / np.where(ok, z, 1)
        y = H * SS / 2 - v[:, 1] * fpx / np.where(ok, z, 1)
        light = R @ np.array([-0.45, 0.55, -0.7])
        light /= np.linalg.norm(light)
        lam = np.abs(N @ light)                         # (two-sided: a capture's normals may face either way)
        rim = np.clip(1 - np.abs(N @ R[:, 2]), 0, 1) ** 3
        shade = (0.42 + 0.58 * lam + 0.25 * rim)[:, None]
        rgb = np.clip(base * shade, 0, 1)
        # dots: a fifth of the points' spacing on the screen at the subject's middle (the cloud reads as dots, not a skin)
        zc = np.linalg.norm(centre - C)
        r_px = max(1.0, a.dot * 0.2 * spacing * fpx / zc)
        offs = [(dx, dy) for dx in range(-int(np.ceil(r_px)), int(np.ceil(r_px)) + 1)
                for dy in range(-int(np.ceil(r_px)), int(np.ceil(r_px)) + 1) if dx * dx + dy * dy <= r_px * r_px + 0.25]
        xi = np.round(x).astype(np.int64); yi = np.round(y).astype(np.int64)
        idx_all, dep_all, col_all = [], [], []
        for dx, dy in offs:
            xx, yy = xi + dx, yi + dy
            m = ok & (xx >= 0) & (xx < W * SS) & (yy >= 0) & (yy < H * SS)
            idx_all.append(yy[m] * (W * SS) + xx[m]); dep_all.append(z[m]); col_all.append(rgb[m])
        idx = np.concatenate(idx_all); dep = np.concatenate(dep_all); cc = np.concatenate(col_all)
        order = np.lexsort((dep, idx))
        idx, cc = idx[order], cc[order]
        first = np.ones(len(idx), bool); first[1:] = idx[1:] != idx[:-1]
        img = np.empty((H * SS * W * SS, 3), np.float32); img[:] = bg
        img[idx[first]] = cc[first]
        img = img.reshape(H * SS, W * SS, 3)
        out = cv2.resize(img, (W, H), interpolation=cv2.INTER_AREA)
        cv2.imwrite(os.path.join(a.out, f'f{f:05d}.jpg'), (np.clip(out, 0, 1)[..., ::-1] * 255 + 0.5).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 95])
        if f % 60 == 0:
            print(f'  frame {f}', flush=True)


if __name__ == '__main__':
    main()

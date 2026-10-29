#!/usr/bin/env python3
"""points_preview.py <name | file.bytes> <out.jpg> [--inflate F]

A plant's particle points as the game stores them (Resources/AntGarden/Points/<name>.bytes, AGP4), drawn on their
own from the front and the side: each point a small round dab in its texture's colour, nearer ones over farther
ones, lit a little from its normal (so the form reads as it does in a capture). --inflate F spreads the points
through a layer F of the plant's height deep on either side of its surface, as the game's thickening does
(ParticleLook.thicken: a capture's normals may face in or out, so evenly both ways).
"""
import struct
import sys

import numpy as np
from PIL import Image, ImageDraw, ImageFont

ROOT = '/Users/xujiafeng/Desktop/AntGarden/UnityProject.nosync/Assets/AntGarden/Resources/AntGarden/Points/'


def load(name):
    # (a plant's name, or the path of any AGP4 file: a backup in 03_Models/Plants)
    b = open(name if name.endswith('.bytes') else ROOT + name + '.bytes', 'rb').read()
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
    return pos, nrm, col, ao, area


def render(pos, nrm, col, ao, ax_u, ax_d, sign, W=700, H=1000, dab=2):
    u = pos[:, ax_u] * (1 if ax_u != 2 else -1); v = pos[:, 1]; dep = pos[:, ax_d] * sign
    o = np.argsort(dep)
    # (fitted to the narrower side: a plant wider than it is tall stays inside the picture)
    scale = 0.88 * min(W / max(u.max() - u.min(), 1e-9), H / max(v.max() - v.min(), 1e-9))
    x = ((u - (u.min() + u.max()) / 2) * scale + W / 2).astype(int)
    y = (H - ((v - v.min()) * scale + H * 0.06)).astype(int)
    light = np.clip(0.55 + 0.45 * (nrm @ np.array([0.35, 0.8, 0.45]) * 0.5 + 0.5), 0, 1.2)
    c = col * (light * (0.45 + 0.55 * ao))[:, None]
    img = np.zeros((H, W, 3), np.float32) + np.array([14, 18, 20]) / 255.0
    for i in o:
        xi, yi = x[i], y[i]
        if 0 <= xi < W and 0 <= yi < H:
            img[max(0, yi - dab):yi + dab, max(0, xi - dab):xi + dab] = c[i]
    return Image.fromarray((np.clip(img, 0, 1) * 255).astype(np.uint8))


def main():
    name, out = sys.argv[1], sys.argv[2]
    inflate = float(sys.argv[sys.argv.index('--inflate') + 1]) if '--inflate' in sys.argv else 0.0
    pos, nrm, col, ao, area = load(name)
    if inflate:
        u = np.random.default_rng(433).random(len(pos))
        pos = pos + nrm * (inflate * (pos[:, 1].max() - pos[:, 1].min()) * (2 * u - 1))[:, None]
    f = render(pos, nrm, col, ao, 0, 2, -1); s = render(pos, nrm, col, ao, 2, 0, 1)
    c = Image.new('RGB', (1410, 1000), (14, 18, 20)); c.paste(f, (0, 0)); c.paste(s, (710, 0))
    c.save(out, quality=88)
    print(name.split('/')[-1], len(pos), 'points', f'area {area:.2f}')


if __name__ == '__main__':
    main()

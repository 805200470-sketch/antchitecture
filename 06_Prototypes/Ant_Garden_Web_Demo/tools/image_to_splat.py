#!/usr/bin/env python3
"""Turn a single-object concept image into a 3D Gaussian splat (.ply).

The object is cut out of its plain light background, the silhouette is
inflated into a rounded relief (a front shell and a darker back shell), and
every pixel becomes a small oriented Gaussian carrying that pixel's colour.
Front views work best, which is how canyon props are seen from the path.
Needs only numpy and Pillow; nothing is uploaded anywhere.

Usage:
  python3 tools/image_to_splat.py concept/fan_tree.png assets/splats/fan_tree.ply --height 12
Options:
  --height H        object height in world units (1 unit ≈ one ant body length)
  --max-side N      processing resolution (longest side, px); more = finer & heavier
  --thickness T     inflation strength (0.5 thin relief … 2 round)
  --tol T           background colour tolerance (0..1)
  --crop-edges      object continues past the image's top/bottom edge (trunk sections)
"""
import argparse
import json
import math
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter

SH_C0 = 0.28209479177387814


def load(path, max_side):
    im = Image.open(path).convert('RGBA')
    s = min(1.0, max_side / max(im.size))
    if s < 1:
        im = im.resize((round(im.width * s), round(im.height * s)), Image.LANCZOS)
    return np.asarray(im).astype(np.float32) / 255.0


def foreground(rgba, tol, crop_edges):
    """Background = border-connected pixels close to the border's median colour."""
    rgb = rgba[..., :3]
    h, w = rgb.shape[:2]
    border = np.concatenate([rgb[0], rgb[-1], rgb[:, 0], rgb[:, -1]])
    if crop_edges:  # only the left/right edges are guaranteed background
        border = np.concatenate([rgb[:, 0], rgb[:, -1]])
    bg = np.median(border, axis=0)
    diff = np.sqrt(((rgb - bg) ** 2).sum(-1))
    cand = (diff < tol) | (rgba[..., 3] < 0.1)
    img = Image.fromarray((cand * 255).astype(np.uint8), 'L').copy()  # copy: floodfill needs its own buffer
    seeds = [(x, 0) for x in range(0, w, 6)] + [(x, h - 1) for x in range(0, w, 6)]
    seeds += [(0, y) for y in range(0, h, 6)] + [(w - 1, y) for y in range(0, h, 6)]
    if crop_edges:
        seeds = [(0, y) for y in range(0, h, 6)] + [(w - 1, y) for y in range(0, h, 6)]
    for xy in seeds:
        if img.getpixel(xy) == 255:
            ImageDraw.floodfill(img, xy, 128)
    fg = np.asarray(img) != 128
    # drop isolated specks, keep thin filaments: open with a 3x3 window
    m = Image.fromarray((fg * 255).astype(np.uint8), 'L').filter(ImageFilter.MinFilter(3)).filter(ImageFilter.MaxFilter(3))
    fg = (np.asarray(m) > 0) | (fg & (diff > tol * 2.5))
    # soft alpha: pixels barely different from the background (hairs, glow edges) stay translucent
    alpha = np.clip((diff - tol * 0.6) / (tol * 1.8), 0.0, 1.0) * rgba[..., 3]
    alpha = np.where(fg, np.maximum(alpha, 0.0), 0.0)
    return fg, alpha, bg


def inside_distance(mask, crop_edges, max_iter=600):
    """Octagonal distance to the silhouette edge (alternating 4/8-neighbour erosion)."""
    d = np.zeros(mask.shape, np.float32)
    cur = mask.copy()
    for it in range(max_iter):
        if not cur.any():
            break
        d[cur] += 1.0
        e = cur.copy()
        e[1:, :] &= cur[:-1, :]
        e[:-1, :] &= cur[1:, :]
        e[:, 1:] &= cur[:, :-1]
        e[:, :-1] &= cur[:, 1:]
        if it % 2:
            e[1:, 1:] &= cur[:-1, :-1]
            e[1:, :-1] &= cur[:-1, 1:]
            e[:-1, 1:] &= cur[1:, :-1]
            e[:-1, :-1] &= cur[1:, 1:]
        e[:, 0] = False
        e[:, -1] = False
        if not crop_edges:
            e[0, :] = False
            e[-1, :] = False
        cur = e
    return d



def dilate(mask, steps):
    """Octagonal dilation by `steps` pixels (alternating 4/8-neighbour)."""
    m = mask.copy()
    for i in range(steps):
        g = m.copy()
        g[1:, :] |= m[:-1, :]
        g[:-1, :] |= m[1:, :]
        g[:, 1:] |= m[:, :-1]
        g[:, :-1] |= m[:, 1:]
        if i % 2:
            g[1:, 1:] |= m[:-1, :-1]
            g[1:, :-1] |= m[:-1, 1:]
            g[:-1, 1:] |= m[1:, :-1]
            g[:-1, :-1] |= m[1:, 1:]
        m = g
    return m


def medial_radius(d, fg):
    """Radius of the largest inscribed disc covering each pixel (≈ local half-thickness)."""
    r = d.copy()
    t = 1.0
    while t <= d.max():
        ti = int(round(t))
        grown = dilate(d >= ti, ti) & fg
        r[grown] = np.maximum(r[grown], ti)
        t *= 1.3
    return r


def label_components(mask, min_px):
    """Connected components via Pillow flood fill (no scipy). Returns label map, labels."""
    img = Image.fromarray(np.where(mask, 255, 0).astype(np.uint8), 'L').copy()
    labels = []
    nxt = 1
    while nxt < 250:
        arr = np.asarray(img)
        ys, xs = np.nonzero(arr == 255)
        if not len(ys):
            break
        ImageDraw.floodfill(img, (int(xs[0]), int(ys[0])), nxt)
        if (np.asarray(img) == nxt).sum() >= min_px:
            labels.append(nxt)
        nxt += 1
    return np.asarray(img).copy(), labels

def box_blur(a, r):
    if r < 1:
        return a
    k = 2 * r + 1
    p = np.pad(a, r, mode='edge')
    c = np.cumsum(np.cumsum(p, 0), 1)
    c = np.pad(c, ((1, 0), (1, 0)))
    return (c[k:, k:] - c[:-k, k:] - c[k:, :-k] + c[:-k, :-k]) / (k * k)


def quat_from_z(n):
    """Quaternions (w, x, y, z) rotating +Z onto unit normals n (n.z >= 0 assumed)."""
    w = 1.0 + n[:, 2]
    q = np.stack([w, -n[:, 1], n[:, 0], np.zeros_like(w)], 1)
    return q / np.linalg.norm(q, axis=1, keepdims=True)


def write_ply(path, xyz, normal, rgb, alpha, scale, quat):
    n = len(xyz)
    fields = ['x', 'y', 'z', 'nx', 'ny', 'nz', 'f_dc_0', 'f_dc_1', 'f_dc_2', 'opacity',
              'scale_0', 'scale_1', 'scale_2', 'rot_0', 'rot_1', 'rot_2', 'rot_3']
    data = np.zeros((n, len(fields)), np.float32)
    data[:, 0:3] = xyz
    data[:, 3:6] = normal
    data[:, 6:9] = (rgb - 0.5) / SH_C0
    a = np.clip(alpha, 0.004, 0.996)
    data[:, 9] = np.log(a / (1 - a))
    data[:, 10:13] = np.log(scale)
    data[:, 13:17] = quat
    header = 'ply\nformat binary_little_endian 1.0\nelement vertex %d\n' % n
    header += ''.join('property float %s\n' % f for f in fields) + 'end_header\n'
    with open(path, 'wb') as fh:
        fh.write(header.encode('ascii'))
        fh.write(data.astype('<f4').tobytes())


def convert(src, dst, height, max_side, thickness, tol, crop_edges, back_shade=0.5,
            mode='round', spread=0.18, base_frac=0.2, flatten=1.0):
    rgba = load(src, max_side)
    fg, alpha, bg = foreground(rgba, tol, crop_edges)
    ys, xs = np.nonzero(fg)
    if len(xs) < 50:
        raise SystemExit(f'{src}: no object found against the background')
    h_px = ys.max() - ys.min() + 1
    ps = height / h_px  # world units per pixel
    base_v = ys.max()
    cx = (xs.min() + xs.max()) / 2

    d = inside_distance(fg, crop_edges)
    lum = rgba[..., :3] @ np.array([0.299, 0.587, 0.114], np.float32)
    offset = np.zeros_like(d)
    if mode == 'round':
        # Volumetric: every part swells to a round cross-section (stems → tubes,
        # bulbs → spheres, trunks → cylinders, mounds → domes).
        r = medial_radius(d, fg)
        z = np.sqrt(np.maximum(0.0, r * r - (r - d) ** 2)) * thickness
        z = np.minimum(z, r * flatten)
        # Clusters: separate parts above the base step forward/back so the group is 3D.
        base_rows = int((ys.max() - ys.min()) * base_frac)
        upper = fg.copy()
        upper[ys.max() - base_rows:, :] = False
        lab, labels = label_components(upper, max(60, int(0.002 * fg.sum())))
        if len(labels) >= 2:
            rng = np.random.default_rng(7)
            width_px = xs.max() - xs.min() + 1
            ramp_rows = max(1, int((ys.max() - ys.min()) * 0.12))
            rows = np.arange(fg.shape[0])[:, None]
            ramp = np.clip((ys.max() - base_rows - rows) / ramp_rows, 0, 1)
            for i, lb in enumerate(labels):
                off = (rng.uniform(-1, 1) if i else 0.0) * spread * width_px
                offset = np.where(lab == lb, off * ramp, offset)
    else:
        z = thickness * 1.9 * np.power(d, 0.65)  # single-image relief
    z = z + 2.0 * (lum - box_blur(lum, 4)) * np.minimum(d, 4.0)  # painted texture relief
    z = box_blur(z, 1)
    gz_v, gz_u = np.gradient(z)
    sel = fg & (alpha > 0.04)
    v, u = np.nonzero(sel)
    zf = z[sel]
    zo = offset[sel]
    nrm = np.stack([-gz_u[sel], gz_v[sel], np.ones_like(zf)], 1)
    nrm /= np.linalg.norm(nrm, axis=1, keepdims=True)
    col = rgba[..., :3][sel]
    a = alpha[sel]

    front = np.stack([(u - cx) * ps, (base_v - v) * ps, (zo + zf) * ps], 1)
    back_z = (zo - zf * (1.0 if mode == 'round' else 0.75)) * ps
    # thin, translucent edge pixels (hairs, glow) get only a front splat
    solid = a > 0.6
    back = np.stack([(u[solid] - cx) * ps, (base_v - v[solid]) * ps, back_z[solid]], 1)
    nb = nrm[solid] * np.array([-1, -1, 1], np.float32)  # same disk orientation, mirrored shell
    shade = back_shade * (0.6 + 0.4 * col[solid].mean(1, keepdims=True))

    xyz = np.concatenate([front, back])
    normal = np.concatenate([nrm, nb])
    rgb = np.concatenate([col, col[solid] * shade])
    al = np.concatenate([a, a[solid]])
    sig_t = 0.62 * ps
    sig_n = 0.14 * ps
    scale = np.tile(np.array([sig_t, sig_t, sig_n], np.float32), (len(xyz), 1))
    quat = quat_from_z(normal)
    write_ply(dst, xyz, normal, rgb, al, scale, quat)

    # footprint radius near the base, for collision
    base_rows = ys > ys.max() - max(3, int(0.15 * h_px))
    foot = (xs[base_rows].max() - xs[base_rows].min()) / 2 * ps if base_rows.any() else 0.3
    # Collision footprint at ant height: a row of circles along the object's width,
    # each as thick as the inflated relief is deep at that spot.
    band_px = int(min(h_px, 1.3 / ps))
    band = fg[max(0, base_v - band_px):base_v + 1]
    zband = z[max(0, base_v - band_px):base_v + 1]
    cols = np.nonzero(band.any(0))[0]
    footprint = []
    if len(cols):
        step_px = max(1, int(0.45 / ps))
        for c0 in range(cols.min(), cols.max() + 1, step_px):
            c1 = min(cols.max() + 1, c0 + step_px)
            occ = band[:, c0:c1]
            if not occ.any():
                continue
            depth = float(zband[:, c0:c1][occ].max()) * ps * (2.0 if mode == 'round' else 1.75)
            footprint.append([round(float(((c0 + c1) / 2 - cx) * ps), 3), round(max(0.3, depth / 2), 3)])
    meta = {
        'source': str(src),
        'splats': int(len(xyz)),
        'height': float(height),
        'width': float((xs.max() - xs.min() + 1) * ps),
        'depth': float((zf.max() * 2 + np.ptp(zo)) * ps),
        'mode': mode,
        'footRadius': float(foot),
        'footprint': footprint,
        'processing': {'maxSide': max_side, 'thickness': thickness, 'tol': tol, 'cropEdges': crop_edges,
                       'background': [round(float(c), 3) for c in bg]},
    }
    Path(dst).with_suffix('.json').write_text(json.dumps(meta, indent=2))
    return meta


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('src')
    ap.add_argument('dst')
    ap.add_argument('--height', type=float, required=True)
    ap.add_argument('--max-side', type=int, default=900)
    ap.add_argument('--thickness', type=float, default=1.0)
    ap.add_argument('--tol', type=float, default=0.09)
    ap.add_argument('--crop-edges', action='store_true')
    ap.add_argument('--mode', choices=['round', 'relief'], default='round')
    ap.add_argument('--spread', type=float, default=0.18, help='depth spread of separate parts (× width)')
    ap.add_argument('--flatten', type=float, default=1.0, help='cap thickness at this × local radius')
    args = ap.parse_args()
    m = convert(args.src, args.dst, args.height, args.max_side, args.thickness, args.tol, args.crop_edges,
                mode=args.mode, spread=args.spread, flatten=args.flatten)
    print(json.dumps(m))

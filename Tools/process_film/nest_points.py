#!/usr/bin/env python3
"""nest_points.py <film dir> <out dir> [--workers 6] [--seconds 18]

Process film P03: a particle nest from its raw points to its finished look. <film dir> is a ProcessFilms.cs
-nestpointsfilm run's out folder: film.json (every frame's camera), solid.bin (SolidThings' splats: the floor's things,
the plants' coarse splats, the giants, the vines; 24 bytes each), ground.bin (the particle look's ground and dust points;
16 bytes each) and nest/f00000.jpg ... (the game's own frames, the camera slowly circling the nest).

  0-3 s     the points come, one by one, white, without colour or light (each a dot, added up: a scan)
  3-6 s     raw points
  6-9 s     their colours come in (each the colour the game gives it, before its light)
  9-11.5 s  the game's frames come in over them: the glows and the light
  to the end the game's own frames
Frames f00000.jpg ... at 1920x1080 into <out dir>; Tools/frames_to_video.sh makes the MP4.
"""
import argparse
import json
import os
from multiprocessing import Pool

import cv2
import numpy as np

W, H, FPS = 1920, 1080, 30
DATA = {}


def smooth(a, b, x):
    t = np.clip((x - a) / max(b - a, 1e-9), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def quat_rows(q):
    x, y, z, w = q
    return np.array([[1 - 2 * (y * y + z * z), 2 * (x * y - z * w), 2 * (x * z + y * w)],
                     [2 * (x * y + z * w), 1 - 2 * (x * x + z * z), 2 * (y * z - x * w)],
                     [2 * (x * z - y * w), 2 * (y * z + x * w), 1 - 2 * (x * x + y * y)]])


def load(film_dir):
    s = np.fromfile(os.path.join(film_dir, 'solid.bin'), dtype=np.dtype([('p', '<f4', 3), ('n', '<u4'), ('c', '<u4'), ('r', '<f4')]))
    g = np.fromfile(os.path.join(film_dir, 'ground.bin'), dtype=np.dtype([('p', '<f4', 3), ('c', '<u4')]))
    ok = np.isfinite(s['p']).all(1)
    s = s[ok]
    P = np.concatenate([s['p'], g['p']]).astype(np.float32)
    c = np.concatenate([s['c'], g['c']])
    rgb = np.stack([c & 255, (c >> 8) & 255, (c >> 16) & 255], 1).astype(np.float32) / 255.0
    # the ground's points are pale already (the particle look's scan colour); the splats are albedos, darker: even them up
    gain = np.where(np.arange(len(P)) < len(s), 1.0, 0.55).astype(np.float32)
    rgb = np.clip(rgb * gain[:, None], 0, 1)
    dust = np.zeros(len(P), bool); dust[len(s):] = ((g['c'] >> 30) & 3) == 1
    rng = np.random.default_rng(20261104)
    order = rng.random(len(P)).astype(np.float32)     # when each point comes (0..1 of the coming)
    return P, rgb, dust, order


def init(film_dir):
    DATA['P'], DATA['rgb'], DATA['dust'], DATA['order'] = load(film_dir)
    DATA['film'] = json.load(open(os.path.join(film_dir, 'film.json')))
    DATA['dir'] = film_dir


def splat(x, y, w, n_ch_vals):
    """Bilinear splats of weights (and weighted values) into the frame: returns the summed planes."""
    x0 = np.floor(x).astype(np.int64); y0 = np.floor(y).astype(np.int64)
    fx = (x - x0).astype(np.float32); fy = (y - y0).astype(np.float32)
    planes = [np.zeros(W * H, np.float64) for _ in range(1 + len(n_ch_vals))]
    for dx, dy, k in ((0, 0, (1 - fx) * (1 - fy)), (1, 0, fx * (1 - fy)), (0, 1, (1 - fx) * fy), (1, 1, fx * fy)):
        xx, yy = x0 + dx, y0 + dy
        m = (xx >= 0) & (xx < W) & (yy >= 0) & (yy < H)
        idx = yy[m] * W + xx[m]
        ww = (w * k)[m]
        planes[0] += np.bincount(idx, weights=ww, minlength=W * H)
        for i, v in enumerate(n_ch_vals):
            planes[1 + i] += np.bincount(idx, weights=ww * v[m], minlength=W * H)
    return [p.reshape(H, W).astype(np.float32) for p in planes]


def frame(args):
    f, out, T = args
    film, P, rgb, dust, order = DATA['film'], DATA['P'], DATA['rgb'], DATA['dust'], DATA['order']
    t = f / FPS
    cam = film['cams'][min(f, len(film['cams']) - 1)]
    C = np.array(cam[:3], np.float32); R = quat_rows(cam[3:7]).astype(np.float32)
    fpx = (H / 2) / np.tan(np.radians(film['fov']) / 2)
    game = cv2.imread(os.path.join(DATA['dir'], 'nest', f'f{min(f, film["frames"] - 1):05d}.jpg')).astype(np.float32)[..., ::-1] / 255.0
    to_game = smooth(9.0, 11.5, t)
    if to_game < 1.0:
        v = (P - C) @ R
        z = v[:, 2]
        ok = z > 0.5
        come = np.clip((t - 0.2 - order * 2.6) / 0.35, 0, 1)        # each point fades in over a third of a second
        ok &= come > 0
        x = W / 2 + v[ok, 0] * fpx / z[ok]
        y = H / 2 - v[ok, 1] * fpx / z[ok]
        zc = np.float32(film['distance'])
        w = come[ok] * np.exp(-(z[ok] - zc) / (zc * 1.6)).astype(np.float32)   # further ones fainter (depth)
        w *= np.where(dust[ok], 0.35, 1.0).astype(np.float32)
        acc, r, g, b = splat(x, y, w, [rgb[ok, 0], rgb[ok, 1], rgb[ok, 2]])
        dens = 1.0 - np.exp(-acc * 0.3)                               # the summed dots, as light
        mean = np.stack([r, g, b], -1) / np.maximum(acc, 1e-6)[..., None]
        colour = smooth(6.0, 9.0, t)
        white = np.array([0.86, 0.9, 0.93], np.float32)
        # its colour as the game gives it, at the brightness the points add up to
        lum = mean @ np.array([0.3, 0.55, 0.15], np.float32)
        tinted = mean / np.maximum(lum, 0.05)[..., None] * 0.62
        pts = dens[..., None] * (white * (1 - colour) + np.clip(tinted, 0, 1.4) * colour)
        # as the colour comes, toward the game frame's own brightness, so the light comes in without a jump
        match = float(np.clip(game.mean() / max(float(pts.mean()), 1e-4), 0.45, 1.2))
        pts = pts * (1 + (match - 1) * smooth(6.0, 10.0, t))
        img = pts * (1 - to_game) + game * to_game
    else:
        img = game
    fade = smooth(0, 0.5, t) * (1 - smooth(T - 0.5, T, t))
    out_img = np.clip(img * fade, 0, 1)
    cv2.imwrite(os.path.join(out, f'f{f:05d}.jpg'), (out_img[..., ::-1] * 255 + 0.5).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 95])
    return f


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('film')
    ap.add_argument('out')
    ap.add_argument('--workers', type=int, default=6)
    ap.add_argument('--seconds', type=float, default=18.0)
    ap.add_argument('--only', default='')     # frames to draw (comma list), for trials
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    film = json.load(open(os.path.join(a.film, 'film.json')))
    n = min(int(round(a.seconds * FPS)), film['frames'])
    frames = [int(v) for v in a.only.split(',')] if a.only else list(range(n))
    with Pool(a.workers, initializer=init, initargs=(a.film,)) as pool:
        for k, f in enumerate(pool.imap_unordered(frame, [(f, a.out, n / FPS) for f in frames])):
            if k % 30 == 0:
                print(f'  {k}/{len(frames)}', flush=True)
    print('done', len(frames), 'frames')


if __name__ == '__main__':
    main()

#!/usr/bin/env python3
"""turntable_composite.py <mesh dir> <points dir> <game dir> <out dir> [--frames 420]

Process film P02: one subject turning, its three stages cross-faded, every frame from the same camera:
  0 - 4.2 s    its model (turntable_mesh.py, or the game's own mesh pass for the ant)
  4.2 - 5.4 s  model -> points
  5.4 - 8.4 s  its stored points (turntable_points.py)
  8.4 - 9.6 s  points -> the game
  9.6 s - end  as the game draws it (ProcessFilms.cs -turntablefilm)
fading in from black and out to it. Frames f00000.jpg ... into <out dir>.
"""
import argparse
import os

import cv2
import numpy as np

FPS = 30
STAGES = [(4.2, 5.4), (8.4, 9.6)]


def smooth(a, b, x):
    t = np.clip((x - a) / max(b - a, 1e-9), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def read(d, f):
    p = os.path.join(d, f'f{f:05d}.jpg')
    return cv2.imread(p).astype(np.float32) / 255.0 if os.path.exists(p) else None


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('mesh'); ap.add_argument('points'); ap.add_argument('game'); ap.add_argument('out')
    ap.add_argument('--frames', type=int, default=420)
    ap.add_argument('--gain', default='1,1,1')     # each stage's brightness (model, points, game)
    a = ap.parse_args()
    gain = [float(v) for v in a.gain.split(',')]
    os.makedirs(a.out, exist_ok=True)
    T = a.frames / FPS
    for f in range(a.frames):
        t = f / FPS
        w2 = smooth(*STAGES[0], t)            # toward the points
        w3 = smooth(*STAGES[1], t)            # toward the game
        weights = [(1 - w2), w2 * (1 - w3), w3]
        img = None
        for (d, g), w in zip(((a.mesh, gain[0]), (a.points, gain[1]), (a.game, gain[2])), weights):
            if w <= 1e-4:
                continue
            im = read(d, f)
            if im is None:
                raise SystemExit(f'missing frame {f} in {d}')
            img = im * g * w if img is None else img + im * g * w
        fade = smooth(0, 0.5, t) * (1 - smooth(T - 0.5, T, t))
        cv2.imwrite(os.path.join(a.out, f'f{f:05d}.jpg'), (np.clip(img * fade, 0, 1) * 255 + 0.5).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 95])
    print('done', a.frames, 'frames ->', a.out)


if __name__ == '__main__':
    main()

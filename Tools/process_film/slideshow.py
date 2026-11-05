#!/usr/bin/env python3
"""slideshow.py <out dir> <image>... [--seconds 3.2] [--fade 0.6]

A plain 1920x1080 slideshow of stills for the edit: each image whole (fitted inside the frame, on the films' dark ground),
pushed in slowly, cross-faded into the next; from black and back to it. Frames f00000.jpg ... into <out dir>;
Tools/frames_to_video.sh makes the MP4.
"""
import argparse
import os

import cv2
import numpy as np

W, H, FPS = 1920, 1080, 30
BG = np.array([13, 11, 9], np.float32) / 255.0     # (BGR) the films' dark ground


def smooth(a, b, x):
    t = np.clip((x - a) / max(b - a, 1e-9), 0.0, 1.0)
    return t * t * (3 - 2 * t)


SOURCES = {}


def source(path):
    if path not in SOURCES:
        if len(SOURCES) > 3:
            SOURCES.pop(next(iter(SOURCES)))
        SOURCES[path] = cv2.imread(path, cv2.IMREAD_UNCHANGED)
    return SOURCES[path]


def fitted(path, z):
    im = source(path)
    if im.ndim == 2:
        im = cv2.cvtColor(im, cv2.COLOR_GRAY2BGR)
    if im.shape[2] == 4:                                  # transparent parts on the dark ground
        a = im[..., 3:4].astype(np.float32) / 255.0
        im = (im[..., :3].astype(np.float32) / 255.0) * a + BG * (1 - a)
    else:
        im = im.astype(np.float32) / 255.0
    h, w = im.shape[:2]
    s = min((W - 120) / w, (H - 90) / h) * z
    im = cv2.resize(im, (max(1, int(w * s)), max(1, int(h * s))), interpolation=cv2.INTER_AREA)
    out = np.empty((H, W, 3), np.float32); out[:] = BG
    h2, w2 = im.shape[:2]
    y0, x0 = (H - h2) // 2, (W - w2) // 2
    ys, xs = max(0, -y0), max(0, -x0)
    out[max(0, y0):max(0, y0) + min(H, h2 - ys), max(0, x0):max(0, x0) + min(W, w2 - xs)] = im[ys:ys + min(H, h2 - ys), xs:xs + min(W, w2 - xs)]
    return out


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('out'); ap.add_argument('images', nargs='+')
    ap.add_argument('--seconds', type=float, default=3.2)
    ap.add_argument('--fade', type=float, default=0.6)
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    per = a.seconds
    T = per * len(a.images) + a.fade
    n = int(round(T * FPS))
    for f in range(n):
        t = f / FPS
        img = np.zeros((H, W, 3), np.float32)
        for i, p in enumerate(a.images):
            t0 = i * per
            w = smooth(t0, t0 + a.fade, t) * (1 - smooth(t0 + per, t0 + per + a.fade, t))
            if i == len(a.images) - 1:
                w = smooth(t0, t0 + a.fade, t)
            if w <= 1e-4:
                continue
            z = 1.0 + 0.035 * np.clip((t - t0) / (per + a.fade), 0, 1)
            img += fitted(p, z) * w
        fade = smooth(0, 0.4, t) * (1 - smooth(T - 0.5, T, t))
        cv2.imwrite(os.path.join(a.out, f'f{f:05d}.jpg'), (np.clip(img * fade, 0, 1) * 255 + 0.5).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 94])
    print('done', n, 'frames,', len(a.images), 'images')


if __name__ == '__main__':
    main()

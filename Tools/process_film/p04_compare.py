#!/usr/bin/env python3
"""p04_compare.py <old aerial> <new aerial> <old walk dir> <new walk dir> <out dir> [--walk-seconds 9]

Process film P04: the game before the particles (the 10-16 build) and after (the particle nest), the same nest (path-012,
the blue one) from the same eye (SceneShots.Aerial: 55° up, 200° round, 0.95 of the nest's size away, a 50° lens),
then a walk along the glowing way in each, side by side.

  0 - 8 s    the old aerial; a line slides across to the middle (both halves), then on to the new one
  8 s - end  the two walks side by side: left the 10-16 build's (the blue ruin's tunnel, -trailfilm), right the
             particle nest's (-trailfilm in lab:path-012@blue); each its first stretch
Small labels name each side (English; --no-labels: none). Frames the screen cut to 1920x999 are scaled to 1080 high and cropped to 1920 wide (the
same 50° vertical lens). Frames f00000.jpg ... into <out dir>.
"""
import argparse
import glob
import os

import cv2
import numpy as np
from PIL import Image, ImageDraw, ImageFont

W, H, FPS = 1920, 1080, 30
FONT = '/System/Library/Fonts/Hiragino Sans GB.ttc'
FONT_EN = '/System/Library/Fonts/HelveticaNeue.ttc'


def smooth(a, b, x):
    t = np.clip((x - a) / max(b - a, 1e-9), 0.0, 1.0)
    return t * t * (3 - 2 * t)


def to1080(im):
    """BGR float image of any size -> 1920x1080: scaled to 1080 high, centre-cropped (or padded) to 1920 wide."""
    h, w = im.shape[:2]
    s = H / h
    im = cv2.resize(im, (int(round(w * s)), H), interpolation=cv2.INTER_AREA if s < 1 else cv2.INTER_CUBIC)
    w2 = im.shape[1]
    if w2 >= W:
        x0 = (w2 - W) // 2
        return im[:, x0:x0 + W]
    out = np.zeros((H, W, 3), np.float32)
    out[:, (W - w2) // 2:(W - w2) // 2 + w2] = im
    return out


def load(p):
    return to1080(cv2.imread(p).astype(np.float32) / 255.0)


def zoom(im, z):
    """Push in by z about the centre."""
    if z <= 1.0001:
        return im
    M = np.array([[z, 0, W / 2 * (1 - z)], [0, z, H / 2 * (1 - z)]], np.float32)
    return cv2.warpAffine(im, M, (W, H), flags=cv2.INTER_LINEAR)


def label_layer(texts):
    """RGBA float layer with the labels (text, x, y, anchor); Latin text in Helvetica Neue."""
    img = Image.new('RGBA', (W, H), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    latin = all(ord(ch) < 0x2E80 for text, *_ in texts for ch in text)
    f = ImageFont.truetype(FONT_EN if latin and os.path.exists(FONT_EN) else FONT, 30)
    for text, x, y, anchor in texts:
        for dx, dy in ((2, 2), (1, 1)):
            d.text((x + dx, y + dy), text, font=f, fill=(0, 0, 0, 150), anchor=anchor)
        d.text((x, y), text, font=f, fill=(236, 234, 228, 235), anchor=anchor)
    a = np.asarray(img).astype(np.float32) / 255.0
    return a[..., [2, 1, 0]], a[..., 3:4]


def over(base, layer, alpha):
    if layer is None:
        return base
    rgb, a = layer
    return base * (1 - a * alpha) + rgb * a * alpha


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('old_aerial'); ap.add_argument('new_aerial'); ap.add_argument('old_walk'); ap.add_argument('new_walk'); ap.add_argument('out')
    ap.add_argument('--walk-seconds', type=float, default=9.0)
    ap.add_argument('--old-label', default='16 Oct · before particles')
    ap.add_argument('--new-label', default='4 Nov · particle nest')
    ap.add_argument('--no-labels', action='store_true')
    a = ap.parse_args()
    os.makedirs(a.out, exist_ok=True)
    old_a, new_a = load(a.old_aerial), load(a.new_aerial)
    old_w = sorted(glob.glob(os.path.join(a.old_walk, 'f*.jpg')))
    new_w = sorted(glob.glob(os.path.join(a.new_walk, 'f*.jpg')))
    left = None if a.no_labels else label_layer([(a.old_label, 40, 40, 'la')])
    right = None if a.no_labels else label_layer([(a.new_label, W - 40, 40, 'ra')])
    split_l = None if a.no_labels else label_layer([(a.old_label, 40, 40, 'la')])
    split_r = None if a.no_labels else label_layer([(a.new_label, W // 2 + 40, 40, 'la')])
    A = 8.0
    T = A + a.walk_seconds
    n = int(round(T * FPS))
    xs = np.arange(W, dtype=np.float32)[None, :, None]
    for f in range(n):
        t = f / FPS
        if t < A:
            z = 1.0 + 0.05 * smooth(0, A, t)
            o, nw = zoom(old_a, z), zoom(new_a, z)
            # the line: from the right edge to the middle (1.4-3.4 s), held, then on to the left edge (5.0-6.6 s)
            line = W * (1 - 0.5 * smooth(1.4, 3.4, t) - 0.5 * smooth(5.0, 6.6, t))
            m = np.clip((xs - line) / 3.0 + 0.5, 0, 1)            # 1 right of the line: the new one
            img = o * (1 - m) + nw * m
            if 0.05 < line / W < 0.995:
                img[:, max(0, int(line) - 1):int(line) + 1] = 0.92   # the line itself
            img = over(img, left, smooth(0.4, 1.0, t) * float(np.clip((line - 560) / 300, 0, 1)))
            img = over(img, right, float(np.clip((W - 560 - line) / 300, 0, 1)) * (1 - smooth(7.4, 8.0, t)))
            fade = smooth(0, 0.5, t) * (1 - smooth(A - 0.35, A, t))
        else:
            k = f - int(A * FPS)
            o = to1080(cv2.imread(old_w[min(k, len(old_w) - 1)]).astype(np.float32) / 255.0)
            nw = to1080(cv2.imread(new_w[min(k, len(new_w) - 1)]).astype(np.float32) / 255.0)
            img = np.zeros((H, W, 3), np.float32)
            img[:, :W // 2 - 2] = o[:, W // 4:W // 4 + W // 2 - 2]       # each its middle half
            img[:, W // 2 + 2:] = nw[:, W // 4 + 2:W // 4 + W // 2]
            img[:, W // 2 - 2:W // 2 + 2] = 0.06
            lab = smooth(A + 0.3, A + 0.9, t)
            img = over(over(img, split_l, lab), split_r, lab)
            fade = smooth(A, A + 0.35, t) * (1 - smooth(T - 0.5, T, t))
        cv2.imwrite(os.path.join(a.out, f'f{f:05d}.jpg'), (np.clip(img * fade, 0, 1) * 255 + 0.5).astype(np.uint8), [cv2.IMWRITE_JPEG_QUALITY, 95])
    print('done', n, 'frames')


if __name__ == '__main__':
    main()
